using System.IO;
using System.Runtime.InteropServices;

namespace CmfTray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--probe"))
            return Probe(args);

        if (args.Contains("--anc-map"))
            return AncMap(args);

        if (args.Contains("--shot"))
            return Shot();

        // Single instance: if one is already running, just exit quietly.
        using var mutex = new System.Threading.Mutex(true, @"Global\CmfTray_SingleInstance", out bool isNew);
        if (!isNew) return 0;

        // Log any startup/runtime crash so a failed login-launch isn't silent.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);

        // Default: system tray app. WPF hosts the flyout window; a WinForms NotifyIcon
        // provides the tray icon. Run the WPF message loop with no main window.
        System.Windows.Forms.Application.EnableVisualStyles();
        var wpf = new System.Windows.Application
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
        };
        wpf.DispatcherUnhandledException += (_, e) => { LogCrash(e.Exception); e.Handled = true; };

        try
        {
            using var app = new TrayApp(wpf);
            wpf.Run();
            return 0;
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            return 1;
        }
    }

    private static void LogCrash(Exception? ex)
    {
        try
        {
            string dir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            File.AppendAllText(Path.Combine(dir, "startup-error.log"), $"{DateTime.Now:s}  {ex}\n\n");
        }
        catch { /* logging must never throw */ }
    }

    /// <summary>Render the flyout with sample data to a PNG so the design can be verified headlessly.</summary>
    private static int Shot()
    {
        var wpf = new System.Windows.Application();
        var win = new FlyoutWindow();
        win.UpdateConnected("CMF Buds", true);
        win.UpdateBattery(new Protocol.Cell(100, 100, 50, false, false, false));
        win.UpdateAnc(Protocol.AncMode.Transparency);
        win.UpdateBass(true, 3);
        win.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
        win.Show();
        win.UpdateLayout();

        int w = (int)Math.Ceiling(win.ActualWidth);
        int h = (int)Math.Ceiling(win.ActualHeight);
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bmp.Render(win);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
        string path = Path.Combine(AppContext.BaseDirectory, "flyout-shot.png");
        using (var fs = File.Create(path)) enc.Save(fs);
        win.Close();
        return 0;
    }

    [DllImport("kernel32.dll")] private static extern bool AllocConsole();

    /// <summary>
    /// Console diagnostic: list paired devices, connect to the chosen one, and dump battery /
    /// firmware / ANC responses. Proves the SPP channel works before touching the UI.
    /// Usage:  CmfTray --probe           (auto-picks the first likely earbuds)
    ///         CmfTray --probe 2         (pick candidate index 2 from the list)
    /// </summary>
    private static StreamWriter? _log;
    private static void Out(string s)
    {
        Console.WriteLine(s);
        try { _log?.WriteLine(s); _log?.Flush(); } catch { }
    }

    private static int Probe(string[] args)
    {
        AllocConsole();
        try { _log = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "probe-log.txt"), append: false); }
        catch { }
        Out("=== CMF Tray — SPP probe ===\n");

        var candidates = RfcommClient.ListPairedEarbuds();
        if (candidates.Count == 0)
        {
            Console.WriteLine("No paired Bluetooth devices found. Pair your earbuds in Windows Settings first.");
            Pause();
            return 1;
        }

        Out("Paired devices:");
        for (int i = 0; i < candidates.Count; i++)
            Out($"  [{i}] {candidates[i].Name}  ({candidates[i].Address})" +
                (candidates[i].Connected ? "  [connected]" : "") +
                (candidates[i].Authenticated ? "" : "  (not authenticated)"));

        int idx = 0;
        var idxArg = args.FirstOrDefault(a => int.TryParse(a, out _));
        if (idxArg is not null) idx = int.Parse(idxArg);
        idx = Math.Clamp(idx, 0, candidates.Count - 1);
        var chosen = candidates[idx];
        Out($"\nUsing [{idx}] {chosen.Name}\n");

        using var client = new RfcommClient();
        client.Log += m => Out($"  · {m}");
        client.FrameReceived += frame =>
        {
            ushort cmd = Protocol.GetCommand(frame);
            Out($"  <- {Protocol.Hex(frame)}  (cmd 0x{cmd:X4})");
            HandleProbeFrame(cmd, frame);
        };

        try
        {
            client.Connect(chosen.Address, chosen.Name);
        }
        catch (Exception ex)
        {
            Out($"\nCONNECT FAILED: {ex.Message}");
            Out("If this says the service is unreachable, the buds may need to be in the case-open/" +
                "connected state, or another app already holds the SPP channel.");
            Pause();
            return 2;
        }

        Out("\nSending queries…");
        Send(client, "battery",  Protocol.ReadBattery());
        Thread.Sleep(200);
        Send(client, "firmware", Protocol.ReadFirmware());
        Thread.Sleep(200);
        Send(client, "anc read", Protocol.ReadAnc());
        Thread.Sleep(200);
        Send(client, "eq read",  Protocol.Build(Protocol.CmdReadEq));
        Thread.Sleep(200);
        Send(client, "listen md", Protocol.Build(Protocol.CmdReadListenMode));

        Out("\nListening for responses for 5s…");
        Thread.Sleep(5000);
        Out("\nProbe complete. See probe-log.txt for this transcript.");
        Pause();
        return 0;
    }

    private static volatile byte[]? _lastAncRead;

    /// <summary>
    /// Empirically maps every ANC mode: set it, read it back, log the raw response so we can
    /// confirm the set-payload and the read-parser against real hardware. Restores original mode.
    /// </summary>
    private static int AncMap(string[] args)
    {
        AllocConsole();
        try { _log = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "anc-map-log.txt"), append: false); }
        catch { }
        Out("=== CMF Tray — ANC mapping ===\n");

        var candidates = RfcommClient.ListPairedEarbuds();
        int idx = 0;
        var idxArg = args.FirstOrDefault(a => int.TryParse(a, out _));
        if (idxArg is not null) idx = Math.Clamp(int.Parse(idxArg), 0, candidates.Count - 1);
        if (candidates.Count == 0) { Out("No paired devices."); Pause(); return 1; }
        var chosen = candidates[idx];
        Out($"Using [{idx}] {chosen.Name}\n");

        using var client = new RfcommClient();
        client.Log += m => Out($"  · {m}");
        client.FrameReceived += frame =>
        {
            ushort cmd = Protocol.GetCommand(frame);
            if (cmd is 0x401E or 0xE003 or 0x401F) _lastAncRead = frame;
        };

        try { client.Connect(chosen.Address, chosen.Name); }
        catch (Exception ex) { Out($"CONNECT FAILED: {ex.Message}"); Pause(); return 2; }

        // Capture original.
        client.Send(Protocol.ReadAnc());
        Thread.Sleep(500);
        var original = _lastAncRead;
        Out($"Original ANC read: {(original is null ? "(none)" : Protocol.Hex(original))}\n");

        var modes = new (string Name, Protocol.AncMode Mode)[]
        {
            ("Off",          Protocol.AncMode.Off),
            ("Transparency", Protocol.AncMode.Transparency),
            ("ANC High",     Protocol.AncMode.AncHigh),
            ("ANC Mid",      Protocol.AncMode.AncMid),
            ("ANC Low",      Protocol.AncMode.AncLow),
            ("ANC Adaptive", Protocol.AncMode.AncAdaptive),
        };

        foreach (var (name, mode) in modes)
        {
            _lastAncRead = null;
            var setFrame = Protocol.SetAnc(mode);
            client.Send(setFrame);
            Thread.Sleep(350);
            client.Send(Protocol.ReadAnc());
            Thread.Sleep(400);
            string read = _lastAncRead is null ? "(no read)" : Protocol.Hex(_lastAncRead);
            Out($"  SET {name,-13} (0x{(byte)mode:X2})  set={Protocol.Hex(setFrame)}");
            Out($"      read-back  {read}");
        }

        // Restore original mode if we can identify it (trailing 01 <mode> 00 group).
        if (original is not null && original.Length >= 14)
        {
            byte origMode = original[12];
            Out($"\nRestoring original mode 0x{origMode:X2}…");
            client.Send(Protocol.Build(Protocol.CmdSetAnc, 0x01, origMode, 0x00));
            Thread.Sleep(300);
        }

        Out("\nDone. See anc-map-log.txt.");
        Pause();
        return 0;
    }

    private static void Send(RfcommClient c, string label, byte[] frame)
    {
        Out($"  -> {label,-9} {Protocol.Hex(frame)}");
        c.Send(frame);
    }

    private static void HandleProbeFrame(ushort cmd, byte[] frame)
    {
        // Battery push/response commands seen in the wild.
        if (cmd is 0x4007 or 0xE001)
        {
            var b = Protocol.ParseBattery(frame);
            Out($"     BATTERY  L={Fmt(b.LeftPct, b.LeftCharging)} " +
                $"R={Fmt(b.RightPct, b.RightCharging)} Case={Fmt(b.CasePct, b.CaseCharging)}");
        }
        else if (cmd == 0x4042)
        {
            Out($"     FIRMWARE {Protocol.ParseFirmware(frame)}");
        }
    }

    private static string Fmt(int? pct, bool charging) =>
        pct is null ? "—" : $"{pct}%{(charging ? "⚡" : "")}";

    private static void Pause()
    {
        Console.WriteLine("\nPress any key to close…");
        try { Console.ReadKey(); } catch { Thread.Sleep(4000); }
    }
}
