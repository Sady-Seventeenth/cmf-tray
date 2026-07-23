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

    private const int TrackWidth = 30;   // battery bar width (design units)
    private const int SliderSpan = 200;  // usable slider width for the bass fill
    private const int MaxBass = 5;

    public Action<Protocol.AncMode>? SetAnc;
    public Action<bool>? SetBassEnabled;
    public Action<int>? SetBassLevel;
    public Action? OnShown;

    private Protocol.AncMode? _anc;
    private bool _bassOn;
    private int _bassLevel;
    private DateTime _lastHide = DateTime.MinValue;

    public FlyoutWindow()
    {
        InitializeComponent();
        Dark.Freeze();
        RefreshAnc();
        RefreshBass();
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

        // Rounded corners + dark mode (Win11).
        int round = 2, dark = 1;
        DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        // Acrylic blur behind. GradientColor is 0xAABBGGRR — low alpha so the blur shows through.
        var accent = new AccentPolicy
        {
            AccentState = (int)AccentState.EnableAcrylicBlurBehind,
            AccentFlags = 2,
            GradientColor = 0x33171412,
            AnimationId = 0,
        };
        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttributeData { Attribute = 19, Data = ptr, SizeOfData = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    // ---------- click handlers ----------
    private void SegNoise_Click(object sender, MouseButtonEventArgs e) => SetAnc?.Invoke(Protocol.AncMode.AncHigh);
    private void SegTrans_Click(object sender, MouseButtonEventArgs e) => SetAnc?.Invoke(Protocol.AncMode.Transparency);
    private void SegOff_Click(object sender, MouseButtonEventArgs e) => SetAnc?.Invoke(Protocol.AncMode.Off);

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

    // ---------- state from device ----------
    public void UpdateBattery(Protocol.Cell b)
    {
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
        TxtName.Text = name;
        TxtStatus.Text = connected ? "Connected" : "Disconnected";
    }

    public void SetStatus(string text) => TxtStatus.Text = text;

    public void UpdateAnc(Protocol.AncMode? mode)
    {
        _anc = mode;
        RefreshAnc();
    }

    public void UpdateBass(bool enabled, int level)
    {
        _bassOn = enabled;
        _bassLevel = Math.Clamp(level, 0, MaxBass);
        RefreshBass();
    }

    // ---------- visuals ----------
    private void RefreshAnc()
    {
        bool nc = _anc is Protocol.AncMode.AncHigh or Protocol.AncMode.AncMid or Protocol.AncMode.AncLow;
        bool tr = _anc == Protocol.AncMode.Transparency;
        bool off = _anc == Protocol.AncMode.Off;

        SegNoise.Background = nc ? White : Clear;
        NoiseRing.Stroke = nc ? Dark : White;
        NoiseDot.Fill = nc ? Dark : White;

        SegTrans.Background = tr ? White : Clear;
        TransRing.Stroke = tr ? Dark : White;
        TransDot.Fill = tr ? Dark : White;

        SegOff.Background = off ? White : Clear;
        OffRing.Stroke = off ? Dark : White;
        OffLine.Stroke = off ? Dark : White;
    }

    private void RefreshBass()
    {
        BassToggleBg.Background = _bassOn
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF))
            : (Brush)FindResource("InsetBg");
        BassKnob.Fill = _bassOn ? Dark : White;
        BassKnob.HorizontalAlignment = _bassOn ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        BassFill.Width = _bassLevel <= 0 ? 0 : _bassLevel / (double)MaxBass * SliderSpan;
    }

    // ---------- show / hide ----------
    public void ShowAtTray()
    {
        Show();
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth - 12;
        Top = wa.Bottom - ActualHeight - 12;
        Activate();
        OnShown?.Invoke();
    }

    public void ToggleAtTray()
    {
        if (IsVisible) { Hide(); _lastHide = DateTime.UtcNow; return; }
        if ((DateTime.UtcNow - _lastHide).TotalMilliseconds < 300) return;
        ShowAtTray();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Hide();
        _lastHide = DateTime.UtcNow;
    }
}
