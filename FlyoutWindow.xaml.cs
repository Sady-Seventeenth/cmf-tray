using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Rectangle = System.Windows.Shapes.Rectangle;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace CmfTray;

public partial class FlyoutWindow : Window
{
    private static readonly Brush White = Brushes.White;
    private static readonly Brush Dark = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
    private static readonly Brush Clear = Brushes.Transparent;

    private Brush _accentBrush = new SolidColorBrush(Color.FromRgb(0xD7, 0x19, 0x20));
    private Brush _contrastBrush = Brushes.White;
    private Brush _subtleBrush = new SolidColorBrush(Color.FromArgb(0x35, 0xD7, 0x19, 0x20));
    private WallpaperTheme _theme = WallpaperTheme.Default();

    private const int TrackWidth = 30;   // battery bar width (design units)
    private const int SliderSpan = 200;  // usable slider width for the bass fill
    private const int MaxBass = 5;

    public Action<Protocol.AncMode>? SetAnc;
    public Action<bool>? SetBassEnabled;
    public Action<int>? SetBassLevel;
    public Action? ToggleAudioSource;
    public Action? OnShown;

    private Protocol.AncMode? _anc;
    private bool _bassOn;
    private int _bassLevel;
    private bool _phoneAudioActive;
    private DateTime _lastHide = DateTime.MinValue;

    public FlyoutWindow()
    {
        InitializeComponent();
        Dark.Freeze();
        ApplyTheme();
        RefreshAnc();
        RefreshBass();
        RefreshAudioSource();
    }

    public void ApplyTheme()
    {
        try
        {
            _theme = WallpaperTheme.Extract();
            _accentBrush = new SolidColorBrush(_theme.Accent); _accentBrush.Freeze();
            _contrastBrush = new SolidColorBrush(_theme.ContrastOnAccent); _contrastBrush.Freeze();
            _subtleBrush = new SolidColorBrush(_theme.AccentSubtle); _subtleBrush.Freeze();

            var cardBrush = new SolidColorBrush(_theme.CardBg); cardBrush.Freeze();
            MainCard.Background = cardBrush;
            BatLFill.Fill = _accentBrush;
            BatRFill.Fill = _accentBrush;

            ApplyAcrylicBlur(_theme.AcrylicGradient);
            RefreshAnc();
            RefreshBass();
            RefreshAudioSource();
        }
        catch { }
    }

    // ---------- Windows acrylic blur-behind (SetWindowCompositionAttribute) ----------
    private enum AccentState { Disabled = 0, EnableBlurBehind = 3, EnableAcrylicBlurBehind = 4 }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy { public int AccentState; public int AccentFlags; public uint GradientColor; public int AnimationId; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }

    [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int corner = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));
        ApplyAcrylicBlur(_theme.AcrylicGradient);
    }

    private void ApplyAcrylicBlur(uint gradientColor)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var policy = new AccentPolicy
        {
            AccentState = (int)AccentState.EnableAcrylicBlurBehind,
            GradientColor = gradientColor,
        };
        int policySize = Marshal.SizeOf(policy);
        IntPtr policyPtr = Marshal.AllocHGlobal(policySize);
        try
        {
            Marshal.StructureToPtr(policy, policyPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = 19, // WCA_ACCENT_POLICY
                Data = policyPtr,
                SizeOfData = policySize,
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        catch { }
        finally
        {
            Marshal.FreeHGlobal(policyPtr);
        }
    }

    // ---------- open / close ----------
    public void ToggleAtTray()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        if ((DateTime.UtcNow - _lastHide).TotalMilliseconds < 250)
            return;

        ShowAtTray();
    }

    public void ShowAtTray()
    {
        ApplyTheme();
        PositionAboveTray();
        Show();
        Activate();
        OnShown?.Invoke();
    }

    private void PositionAboveTray()
    {
        var wa = SystemParameters.WorkArea;
        double w = 360;
        double h = 260;
        Left = wa.Right - w - 12;
        Top  = wa.Bottom - h - 12;
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        _lastHide = DateTime.UtcNow;
        Hide();
    }

    // ---------- interactions ----------
    private void SegNoise_Click(object sender, MouseButtonEventArgs e) => SetAnc?.Invoke(Protocol.AncMode.AncHigh);
    private void SegTrans_Click(object sender, MouseButtonEventArgs e) => SetAnc?.Invoke(Protocol.AncMode.Transparency);
    private void SegOff_Click(object sender, MouseButtonEventArgs e)   => SetAnc?.Invoke(Protocol.AncMode.Off);

    private void BassToggle_Click(object sender, MouseButtonEventArgs e)
    {
        _bassOn = !_bassOn;
        RefreshBass();
        SetBassEnabled?.Invoke(_bassOn);
    }

    private void BassSlider_Click(object sender, MouseButtonEventArgs e)
    {
        double x = e.GetPosition(BassSlider).X;
        double frac = Math.Clamp(x / Math.Max(1, BassSlider.ActualWidth), 0, 1);
        _bassLevel = Math.Clamp((int)Math.Round(frac * MaxBass), 0, MaxBass);
        RefreshBass();
        SetBassLevel?.Invoke(_bassLevel);
    }

    private void AudioSwitch_Click(object sender, MouseButtonEventArgs e)
    {
        ToggleAudioSource?.Invoke();
    }

    // ---------- thread-safe state setters ----------
    public void UpdateBattery(Protocol.Cell b)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateBattery(b));
            return;
        }

        Set(BatLPct, BatLFill, b.LeftPct, b.LeftCharging);
        Set(BatRPct, BatRFill, b.RightPct, b.RightCharging);

        static void Set(System.Windows.Controls.TextBlock pct, Rectangle fill, int? v, bool charging)
        {
            pct.Text = v is null ? "—" : $"{v}%";
            fill.Width = v is null ? 0 : Math.Clamp(v.Value / 100.0 * TrackWidth, 0, TrackWidth);
        }
    }

    public void UpdateConnected(string name, bool connected)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateConnected(name, connected));
            return;
        }

        TxtName.Text = name;
        TxtStatus.Text = connected ? "Подключено" : "Отключено";
    }

    public void SetStatus(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetStatus(text));
            return;
        }

        TxtStatus.Text = text;
    }

    public void UpdateAnc(Protocol.AncMode? mode)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateAnc(mode));
            return;
        }

        _anc = mode;
        RefreshAnc();
    }

    public void UpdateBass(bool enabled, int level)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateBass(enabled, level));
            return;
        }

        _bassOn = enabled;
        _bassLevel = Math.Clamp(level, 0, MaxBass);
        RefreshBass();
    }

    public void UpdateAudioSource(bool phoneActive)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateAudioSource(phoneActive));
            return;
        }

        _phoneAudioActive = phoneActive;
        RefreshAudioSource();
    }

    // ---------- visuals ----------
    private void RefreshAudioSource()
    {
        if (_phoneAudioActive)
        {
            AudioSwitchBorder.BorderBrush = _accentBrush;
            AudioSwitchBorder.BorderThickness = new Thickness(1);
            AudioSwitchBorder.Background = _subtleBrush;
            AudioSwitchBtnBorder.Background = _accentBrush;
            TxtAudioBtn.Foreground = _contrastBrush;
            TxtAudioIcon.Text = "📱";
            TxtAudioBtn.Text = "Вернуть на ПК";
        }
        else
        {
            AudioSwitchBorder.BorderBrush = Clear;
            AudioSwitchBorder.BorderThickness = new Thickness(0);
            AudioSwitchBorder.Background = (Brush)FindResource("InsetBg");
            AudioSwitchBtnBorder.Background = _subtleBrush;
            TxtAudioBtn.Foreground = White;
            TxtAudioIcon.Text = "💻";
            TxtAudioBtn.Text = "Отдать телефону";
        }
    }

    private void RefreshAnc()
    {
        bool nc = _anc is Protocol.AncMode.AncHigh or Protocol.AncMode.AncMid or Protocol.AncMode.AncLow;
        bool tr = _anc == Protocol.AncMode.Transparency;
        bool off = _anc == Protocol.AncMode.Off;

        SegNoise.Background = nc ? _accentBrush : Clear;
        NoiseRing.Stroke = nc ? _contrastBrush : White;
        NoiseDot.Fill = nc ? _contrastBrush : White;

        SegTrans.Background = tr ? _accentBrush : Clear;
        TransRing.Stroke = tr ? _contrastBrush : White;
        TransDot.Fill = tr ? _contrastBrush : White;

        SegOff.Background = off ? _accentBrush : Clear;
        OffRing.Stroke = off ? _contrastBrush : White;
        OffLine.Stroke = off ? _contrastBrush : White;
    }

    private void RefreshBass()
    {
        BassToggleBg.Background = _bassOn ? _accentBrush : (Brush)FindResource("InsetBg");
        BassKnob.Fill = _bassOn ? _contrastBrush : White;
        BassKnob.HorizontalAlignment = _bassOn ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        BassSlider.Opacity = _bassOn ? 1.0 : 0.35;
        BassFill.Background = _accentBrush;
        BassFill.Width = Math.Clamp((double)_bassLevel / MaxBass * SliderSpan, 0, SliderSpan);
    }
}
