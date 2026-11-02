using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CmfTray;

/// <summary>
/// System-tray front end. Owns the Bluetooth client and the WPF flyout window; a WinForms
/// NotifyIcon provides the tray icon. State reacts to pushed frames in real time, with a slow
/// poll as a safety net.
/// </summary>
public sealed class TrayApp : IDisposable
{
    private readonly System.Windows.Application _wpf;
    private readonly NotifyIcon _tray;
    private readonly FlyoutWindow _flyout = new();
    private readonly RfcommClient _client = new();

    // Battery poll interval: 5 minutes as requested
    private readonly DispatcherTimer _battTimer = new() { Interval = TimeSpan.FromMinutes(5) };
    private readonly DispatcherTimer _stateTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer _reconnectTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private readonly DispatcherTimer _trimTimer = new() { Interval = TimeSpan.FromMinutes(2) };

    private Protocol.Cell _battery;
    private Protocol.AncMode? _ancMode;
    private bool _bassEnabled;
    private int _bassLevel;
    private bool? _lowLag;
    private RfcommClient.Candidate? _device;
    private volatile bool _connecting;

    private bool _phoneAudioActive;
    private bool _warnedLowBattery;

    public TrayApp(System.Windows.Application wpf)
    {
        _wpf = wpf;

        _tray = new NotifyIcon
        {
            Icon = MakeIcon(false),
            Text = "CMF Tray — отключено",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };
        _tray.ContextMenuStrip.Opening += (_, _) => RebuildMenu();
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) _flyout.ToggleAtTray(); };

        _flyout.SetAnc = mode => { TrySend(Protocol.SetAnc(mode)); DelayedRead(); };
        _flyout.SetBassEnabled = on => { _bassEnabled = on; TrySend(Protocol.SetEnhancedBass(on, _bassLevel)); };
        _flyout.SetBassLevel = lvl => { _bassLevel = lvl; TrySend(Protocol.SetEnhancedBass(_bassEnabled, lvl)); };
        _flyout.ToggleAudioSource = ToggleAudioChannel;
        _flyout.OnShown = RefreshState;

        _client.FrameReceived += OnFrame;
        _client.Disconnected += () => Post(OnDisconnected);
        _client.Log += _ => { };

        // Battery refresh (5 min) + a liveness poll (a failed send flips us to disconnected).
        _battTimer.Tick += (_, _) => { if (_client.IsConnected) TrySend(Protocol.ReadBattery()); };
        _stateTimer.Tick += (_, _) => { if (_client.IsConnected) TrySend(Protocol.ReadAnc()); };
        // Cheap watcher: only opens the control channel when the buds are on THIS PC.
        _reconnectTimer.Tick += (_, _) => { if (!_client.IsConnected) BeginAutoConnect(); };
        // Keep the idle footprint small.
        _trimTimer.Tick += (_, _) => TrimMemory();
        _battTimer.Start();
        _stateTimer.Start();
        _reconnectTimer.Start();
        _trimTimer.Start();

        _flyout.UpdateConnected("CMF Buds", false);
        _flyout.UpdateAudioSource(AudioController.IsMuted());

        var warmup = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        warmup.Tick += (_, _) => { warmup.Stop(); TrimMemory(); };
        warmup.Start();
    }

    private void RefreshState()
    {
        if (!_client.IsConnected) return;
        TrySend(Protocol.ReadBattery());
        TrySend(Protocol.ReadAnc());
        TrySend(Protocol.ReadEnhancedBass());
        TrySend(Protocol.ReadLatency());
        _phoneAudioActive = AudioController.IsMuted();
        Post(() => _flyout.UpdateAudioSource(_phoneAudioActive));
    }

    private void ToggleAudioChannel()
    {
        _phoneAudioActive = !_phoneAudioActive;
        AudioController.SetMute(_phoneAudioActive);
        _flyout.UpdateAudioSource(_phoneAudioActive);

        if (_phoneAudioActive)
            Notify("Звук ноутбука заглушен. Наушники передают звук с телефона.");
        else
            Notify("Звук возвращен на ноутбук.");
    }

    private void SetLowLag(bool on)
    {
        _lowLag = on;
        TrySend(Protocol.SetLatency(on));
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        t.Tick += (_, _) => { t.Stop(); if (_client.IsConnected) TrySend(Protocol.ReadLatency()); };
        t.Start();
    }

    // ---------------------------------------------------------------- tray menu (right-click)

    private void RebuildMenu()
    {
        var m = _tray.ContextMenuStrip!;
        m.Items.Clear();

        m.Items.Add(new ToolStripMenuItem(_client.IsConnected ? $"● {_device?.Name ?? "Подключено"}" : "○ Отключено") { Enabled = false });
        m.Items.Add(new ToolStripSeparator());

        // Audio switch (Dual connection helper)
        _phoneAudioActive = AudioController.IsMuted();
        string switchText = _phoneAudioActive ? "🔊 Вернуть звук на ноутбук" : "📱 Отдать звук телефону";
        var switchItem = new ToolStripMenuItem(switchText, null, (_, _) => ToggleAudioChannel())
        {
            Font = new System.Drawing.Font(m.Font, System.Drawing.FontStyle.Bold)
        };
        m.Items.Add(switchItem);
        m.Items.Add(new ToolStripSeparator());

        bool ancOn = _ancMode is Protocol.AncMode.AncHigh or Protocol.AncMode.AncMid or Protocol.AncMode.AncLow;
        var anc = new ToolStripMenuItem("Управление шумом") { Enabled = _client.IsConnected };
        anc.DropDownItems.Add(AncItem("Шумоподавление", Protocol.AncMode.AncHigh, ancOn));
        anc.DropDownItems.Add(AncItem("Прозрачность", Protocol.AncMode.Transparency, _ancMode == Protocol.AncMode.Transparency));
        anc.DropDownItems.Add(AncItem("Выключено", Protocol.AncMode.Off, _ancMode == Protocol.AncMode.Off));
        m.Items.Add(anc);

        string latStatus = _lowLag.HasValue ? (_lowLag.Value ? " (Вкл)" : " (Выкл)") : "";
        var lat = new ToolStripMenuItem($"Режим низкой задержки{latStatus}") { Enabled = _client.IsConnected };
        lat.DropDownItems.Add(new ToolStripMenuItem("Вкл", null, (_, _) => SetLowLag(true)) { Checked = _lowLag == true });
        lat.DropDownItems.Add(new ToolStripMenuItem("Выкл", null, (_, _) => SetLowLag(false)) { Checked = _lowLag == false });
        m.Items.Add(lat);

        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(new ToolStripMenuItem("Открыть панель", null, (_, _) => _flyout.ShowAtTray()));
        m.Items.Add(new ToolStripMenuItem(_client.IsConnected ? "Переподключить" : "Подключить", null, (_, _) => { _client.Disconnect(); BeginAutoConnect(); }));
        m.Items.Add(new ToolStripMenuItem("Выбрать устройство…", null, (_, _) => ChooseDevice()));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(new ToolStripMenuItem("Автозапуск с Windows", null, (_, _) => SetAutoStart(!IsAutoStart())) { Checked = IsAutoStart() });
        m.Items.Add(new ToolStripMenuItem("Выход", null, (_, _) => Quit()));
    }

    // ---------------------------------------------------------------- run at login

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "CmfTray";

    private static bool IsAutoStart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(RunValue) is not null;
    }

    private static void SetAutoStart(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
        else if (k.GetValue(RunValue) is not null) k.DeleteValue(RunValue);
    }

    private ToolStripMenuItem AncItem(string label, Protocol.AncMode mode, bool isChecked) =>
        new(label, null, (_, _) => { TrySend(Protocol.SetAnc(mode)); DelayedRead(); }) { Checked = isChecked };

    private void DelayedRead()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        t.Tick += (_, _) => { t.Stop(); if (_client.IsConnected) TrySend(Protocol.ReadAnc()); };
        t.Start();
    }

    // ---------------------------------------------------------------- connection

    private void BeginAutoConnect()
    {
        if (_connecting || _client.IsConnected) return;
        _connecting = true;
        Task.Run(() =>
        {
            try
            {
                var list = RfcommClient.ListPairedEarbuds();
                var target = list.Count > 0 ? list[0] : null;

                if (target is null || !target.Connected)
                {
                    Post(() => _flyout.UpdateConnected(target?.Name ?? _device?.Name ?? "CMF Buds", false));
                    return;
                }

                Post(() => _flyout.SetStatus("Подключение…"));
                ConnectCore(target);
            }
            catch
            {
                Post(() => _flyout.UpdateConnected(_device?.Name ?? "CMF Buds", false));
            }
            finally { _connecting = false; }
        });
    }

    private void ChooseDevice()
    {
        var list = RfcommClient.ListPairedEarbuds();
        if (list.Count == 0) { Notify("Сопряженные устройства не найдены. Подключите наушники в настройках Windows."); return; }
        using var dlg = new DevicePicker(list);
        if (dlg.ShowDialog() == DialogResult.OK && dlg.Selected is { } sel && !_connecting)
        {
            _connecting = true;
            Task.Run(() => { try { ConnectCore(sel); } finally { _connecting = false; } });
        }
    }

    private void ConnectCore(RfcommClient.Candidate c)
    {
        try
        {
            _client.Connect(c.Address, c.Name);
            _device = c;
            Post(() =>
            {
                _tray.Icon = MakeIcon(true);
                _tray.Text = $"CMF Tray — {c.Name}";
                _flyout.UpdateConnected(c.Name, true);
                RefreshState();
            });
        }
        catch
        {
            _client.Disconnect();
            Post(() => _flyout.UpdateConnected(_device?.Name ?? "CMF Buds", false));
        }
    }

    private void OnDisconnected()
    {
        _tray.Icon = MakeIcon(false);
        _tray.Text = "CMF Tray — отключено";
        _battery = default;
        _ancMode = null;
        _lowLag = null;
        _flyout.UpdateConnected(_device?.Name ?? "CMF Buds", false);
        _flyout.UpdateBattery(default);
        _flyout.UpdateAnc(null);
        TrimMemory();
    }

    private void TrySend(byte[] frame)
    {
        try { _client.Send(frame); }
        catch { OnDrop(); }
    }

    private void OnDrop()
    {
        _client.Disconnect();
        Post(OnDisconnected);
    }

    private void OnFrame(byte[] frame)
    {
        ushort cmd = Protocol.GetCommand(frame);
        switch (cmd)
        {
            case 0x4007 or 0xE001:
            {
                var b = Protocol.ParseBattery(frame);
                Post(() =>
                {
                    _battery = b;
                    _tray.Text = $"CMF Tray — L{b.LeftPct}% R{b.RightPct}% Кейс{b.CasePct}%";
                    _flyout.UpdateBattery(b);
                    CheckLowBattery(b);
                });
                break;
            }
            case 0x401E or 0xE003:
            {
                var mode = Protocol.ParseAnc(frame);
                if (mode is not null) Post(() => { _ancMode = mode; _flyout.UpdateAnc(mode); });
                break;
            }
            case 0x404E:
            {
                var (enabled, level) = Protocol.ParseEnhancedBass(frame);
                Post(() => { _bassEnabled = enabled; _bassLevel = level; _flyout.UpdateBass(enabled, level); });
                break;
            }
            case 0x4040:
            {
                var l = Protocol.ParseLatency(frame);
                if (l.HasValue) _lowLag = l.Value;
                break;
            }
        }
    }

    private void CheckLowBattery(Protocol.Cell b)
    {
        bool leftLow = b.LeftPct.HasValue && b.LeftPct.Value <= 20 && !b.LeftCharging;
        bool rightLow = b.RightPct.HasValue && b.RightPct.Value <= 20 && !b.RightCharging;

        if (leftLow || rightLow)
        {
            if (!_warnedLowBattery)
            {
                _warnedLowBattery = true;
                string msg = $"Низкий заряд батареи: L: {b.LeftPct ?? 0}%, R: {b.RightPct ?? 0}%. Пора убрать наушники в кейс.";
                Notify(msg, ToolTipIcon.Warning);
            }
        }
        else if ((b.LeftPct ?? 100) > 25 && (b.RightPct ?? 100) > 25)
        {
            _warnedLowBattery = false;
        }
    }

    // ---------------------------------------------------------------- helpers

    private void Post(Action a) => _flyout.Dispatcher.BeginInvoke(a);

    private void Notify(string msg, ToolTipIcon icon = ToolTipIcon.Info) => _tray.ShowBalloonTip(4000, "CMF Buds", msg, icon);

    private static void TrimMemory() { }

    private static Icon MakeIcon(bool connected) => IconFactory.MakeFromSvg(connected);

    private void Quit()
    {
        _tray.Visible = false;
        Dispose();
        _wpf.Shutdown();
    }

    public void Dispose()
    {
        _battTimer.Stop();
        _stateTimer.Stop();
        _reconnectTimer.Stop();
        _trimTimer.Stop();
        _client.Dispose();
        try { _flyout.Close(); } catch { }
        _tray.Dispose();
    }
}
