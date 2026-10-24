using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ChatMixOverlay;
public sealed class Overlay : Window
{
    private readonly Settings settings;
    private readonly Border card;
    private readonly Ellipse thumb;
    private readonly TranslateTransform shift = new();
    private readonly DispatcherTimer timer = new();
    private readonly SolidColorBrush accent = new();
    private readonly SolidColorBrush centerBrush = new();
    private readonly SolidColorBrush gameBrush = new();
    private readonly SolidColorBrush chatBrush = new();
    private Border track = null!;
    private bool light;
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private double target, lastFrame;
    private bool visible, rendering;
    private static readonly Color GameColor = Color.FromRgb(99, 206, 190);
    private static readonly Color ChatColor = Color.FromRgb(100, 183, 239);
    public Overlay(Settings settings)
    {
        this.settings = settings;
        Width = 272; Height = 76; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false;
        ShowActivated = false; Topmost = true; Opacity = 0; IsHitTestVisible = false;
        UseLayoutRounding = true;
        card = new Border
        {
            Width = 252,
            Height = 54,
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            Padding = new Thickness(18, 0, 18, 0),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Opacity = .22 }
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        // Fixed vector paths keep both symbols crisp and independent of installed icon fonts.
        grid.Children.Add(MakeIcon("M7,5 L17,5 C19,5 20,7 20.5,9 L22,16 Q22.5,19 20,19 Q19,19 18,18 L15,15 L9,15 L6,18 Q5,19 4,19 Q1.5,19 2,16 L3.5,9 C4,7 5,5 7,5 Z M7.5,8.5 L7.5,12.5 M5.5,10.5 L9.5,10.5 M18,10.5 A1,1 0 1 1 16,10.5 A1,1 0 1 1 18,10.5", GameColor));
        var chat = MakeIcon("M5,4 L19,4 Q22,4 22,7 L22,15 Q22,18 19,18 L11,18 L6,22 L6,18 L5,18 Q2,18 2,15 L2,7 Q2,4 5,4 Z M7,11 L7.1,11 M12,11 L12.1,11 M17,11 L17.1,11", ChatColor);
        Grid.SetColumn(chat, 2); grid.Children.Add(chat);
        var bar = new Canvas { Width = 144, Height = 20, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        Grid.SetColumn(bar, 1); grid.Children.Add(bar);
        track = new Border { Width = 144, Height = 4, CornerRadius = new CornerRadius(2), Background = new LinearGradientBrush(Color.FromRgb(58, 104, 98), Color.FromRgb(58, 92, 115), 0) };
        Canvas.SetTop(track, 8); bar.Children.Add(track);
        var center = new Rectangle { Width = 1, Height = 10, Fill = centerBrush };
        Canvas.SetLeft(center, 71.5); Canvas.SetTop(center, 5); bar.Children.Add(center);
        thumb = new Ellipse { Width = 14, Height = 14, Fill = accent, StrokeThickness = 2.5, RenderTransform = shift };
        Canvas.SetTop(thumb, 3); Canvas.SetLeft(thumb, 65); bar.Children.Add(thumb);
        card.Child = grid; Content = card;
        timer.Tick += (_, _) => FadeOut();
        Closed += (_, _) => StopRendering();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(hwnd, -20, new IntPtr(GetWindowLongPtr(hwnd, -20).ToInt64() | 0x08000000L | 0x20L | 0x80L));
            HwndSource.FromHwnd(hwnd)?.AddHook(Hook);
        };
    }
    private Viewbox MakeIcon(string geometry, Color color)
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(geometry),
            Stroke = color == GameColor ? gameBrush : chatBrush,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        var canvas = new Canvas { Width = 24, Height = 24 }; canvas.Children.Add(path);
        return new Viewbox { Width = 22, Height = 22, Stretch = Stretch.Uniform, Child = canvas, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
    }
    public void Present(double balance)
    {
        target = Math.Clamp(balance * (settings.Reverse ? -1 : 1), -1, 1) * 67;
        light = Appearance.IsLight(settings);
        card.Background = light ? Appearance.Brush(244, 244, 242) : Appearance.Brush(42, 42, 42);
        card.BorderBrush = new SolidColorBrush(light ? Color.FromArgb(32, 0, 0, 0) : Color.FromArgb(36, 255, 255, 255));
        gameBrush.Color = light ? Color.FromRgb(19, 122, 108) : GameColor;
        chatBrush.Color = light ? Color.FromRgb(25, 109, 170) : ChatColor;
        centerBrush.Color = light ? Color.FromRgb(112, 122, 127) : Color.FromRgb(169, 177, 183);
        track.Background = new LinearGradientBrush(light ? Color.FromRgb(168, 204, 196) : Color.FromRgb(58, 104, 98), light ? Color.FromRgb(169, 195, 216) : Color.FromRgb(58, 92, 115), 0);
        thumb.Stroke = card.Background;
        UpdateColor();
        if (!visible)
        {
            if (!IsVisible) { shift.X = target; UpdateColor(); Show(); }
            Position();
            // Only animate visibility once per appearance, never on every wheel event.
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(65)));
        }
        visible = true;
        if (!rendering) { rendering = true; lastFrame = clock.Elapsed.TotalSeconds; CompositionTarget.Rendering += SmoothFrame; }
        timer.Stop(); timer.Interval = TimeSpan.FromSeconds(settings.Duration); timer.Start();
    }
    private void SmoothFrame(object? sender, EventArgs e)
    {
        var now = clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - lastFrame, 0, .05); lastFrame = now;
        // Frame-rate independent low-pass smoothing: no animation clocks restart on new samples.
        shift.X += (target - shift.X) * (1 - Math.Exp(-dt / .065));
        if (Math.Abs(target - shift.X) < .025) { shift.X = target; StopRendering(); }
        UpdateColor();
    }
    private void UpdateColor()
    {
        var value = shift.X / 67;
        var end = value < 0 ? gameBrush.Color : chatBrush.Color;
        var amount = Math.Abs(value);
        byte Mix(byte neutral, byte channel) => (byte)Math.Round(neutral + (channel - neutral) * amount);
        accent.Color = Color.FromRgb(Mix(light ? (byte)66 : (byte)222, end.R), Mix(light ? (byte)94 : (byte)227, end.G), Mix(light ? (byte)116 : (byte)233, end.B));
    }
    private void StopRendering() { if (rendering) CompositionTarget.Rendering -= SmoothFrame; rendering = false; }
    private void FadeOut()
    {
        timer.Stop(); visible = false;
        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
        animation.Completed += (_, _) => { if (!visible) { StopRendering(); Hide(); } };
        BeginAnimation(OpacityProperty, animation);
    }
    private void Position()
    {
        var screens = Forms.Screen.AllScreens;
        var screen = screens.FirstOrDefault(s => s.DeviceName == settings.Monitor) ?? Forms.Screen.PrimaryScreen ?? screens[0];
        if (settings.Monitor == "Active monitor") screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var area = screen.WorkingArea;
        var hwnd = new WindowInteropHelper(this).Handle;
        var dpi = GetDpiForWindow(hwnd) / 96.0;
        // Native pixel positioning avoids WPF's virtual-desktop DPI conversion ambiguity.
        var width = (int)Math.Round(Width * dpi); var height = (int)Math.Round(Height * dpi); var margin = (int)(24 * dpi);
        var x = area.Left + (area.Width - width) / 2;
        var y = settings.Position == "Top center" ? area.Top + margin : area.Bottom - height - margin;
        if (settings.Position == "Bottom right") x = area.Right - width - margin;
        SetWindowPos(hwnd, new IntPtr(-1), x, y, width, height, 0x0010);
        // A second move after a DPI transition uses the destination monitor's DPI.
        var newDpi = GetDpiForWindow(hwnd) / 96.0;
        if (Math.Abs(newDpi - dpi) > .01) Dispatcher.BeginInvoke(new Action(Position), DispatcherPriority.Loaded);
    }
    private IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) { if (msg == 0x0084) { handled = true; return new IntPtr(-1); } if (msg == 0x0021) { handled = true; return new IntPtr(3); } return IntPtr.Zero; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int n);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h, int n, IntPtr v);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
