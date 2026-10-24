using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace ChatMixOverlay;
public sealed class SettingsWindow : Window
{
    public SettingsWindow(Settings settings, Action preview, Func<string> status)
    {
        Title = "ChatMix Overlay"; Width = 480; Height = 630; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"); FontSize = 13;
        using (var icon = Appearance.LoadIcon()) Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        void Palette()
        {
            var light = Appearance.IsLight(settings);
            ThemeMode = settings.ColorMode == "Follow Windows" ? System.Windows.ThemeMode.System : light ? System.Windows.ThemeMode.Light : System.Windows.ThemeMode.Dark;
            Background = light ? Appearance.Brush(243, 243, 241) : Appearance.Brush(32, 32, 32);
            Resources["WindowFill"] = Background;
            Foreground = light ? Appearance.Brush(31, 33, 36) : Appearance.Brush(239, 239, 239);
            Resources["CardFill"] = light ? Appearance.Brush(250, 250, 248) : Appearance.Brush(43, 43, 43);
            Resources["CardEdge"] = light ? Appearance.Brush(222, 223, 220) : Appearance.Brush(59, 59, 59);
        }
        Palette();
        Microsoft.Win32.UserPreferenceChangedEventHandler themeChanged = (_, _) => Dispatcher.BeginInvoke(new Action(Palette));
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += themeChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= themeChanged;
        var panel = new StackPanel { Margin = new Thickness(24) };
        var scroller = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var backdrop = new Border { Child = scroller };
        backdrop.SetResourceReference(Border.BackgroundProperty, "WindowFill"); Content = backdrop;
        var heading = new Grid { Margin = new Thickness(0, 0, 0, 20) }; heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); heading.ColumnDefinitions.Add(new ColumnDefinition());
        var logo = new Image { Source = Icon, Width = 36, Height = 36, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center }; heading.Children.Add(logo);
        var title = new StackPanel(); Grid.SetColumn(title, 1); heading.Children.Add(title);
        title.Children.Add(new TextBlock { Text = "ChatMix Overlay", FontSize = 23, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock { Text = "A quieter way to keep your balance in view.", Opacity = .66, Margin = new Thickness(0, 4, 0, 0), FontSize = 12 }); panel.Children.Add(heading);
        Border Card(UIElement content) { var card = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 0, 0, 10), Child = content }; card.SetResourceReference(Border.BackgroundProperty, "CardFill"); card.SetResourceReference(Border.BorderBrushProperty, "CardEdge"); panel.Children.Add(card); return card; }
        Grid Row(string label, UIElement control)
        {
            var row = new Grid { MinHeight = 40 }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Medium });
            Grid.SetColumn(control, 1); row.Children.Add(control); return row;
        }
        ComboBox Choice(string[] values, string selected) { var box = new ComboBox { ItemsSource = values, SelectedItem = selected, MinHeight = 32, VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left }; if (box.SelectedIndex < 0) box.SelectedIndex = 0; return box; }
        var mode = Choice(["Light", "Dark", "Follow Windows"], settings.ColorMode); Card(Row("Color mode", mode));
        mode.SelectionChanged += (_, _) => { settings.ColorMode = (string)mode.SelectedItem; Palette(); };
        var placement = Choice(["Bottom center", "Top center", "Bottom right"], settings.Position);
        var monitor = Choice(new[] { "Primary", "Active monitor" }.Concat(Forms.Screen.AllScreens.Select(s => s.DeviceName)).ToArray(), settings.Monitor);
        var layout = new StackPanel(); layout.Children.Add(Row("Position", placement)); layout.Children.Add(Row("Monitor", monitor)); Card(layout);
        var timing = new StackPanel(); var durationLabel = new TextBlock { FontWeight = FontWeights.Medium };
        var duration = new Slider { Minimum = .5, Maximum = 5, Value = settings.Duration, TickFrequency = .5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 0) };
        durationLabel.Text = $"Display duration   {duration.Value:0.0} s"; duration.ValueChanged += (_, _) => durationLabel.Text = $"Display duration   {duration.Value:0.0} s";
        timing.Children.Add(durationLabel); timing.Children.Add(duration); Card(timing);
        var reverse = new CheckBox { Content = "Reverse Game and Chat direction", IsChecked = settings.Reverse, MinHeight = 36, VerticalContentAlignment = VerticalAlignment.Center };
        var startup = new CheckBox { Content = "Start with Windows", IsChecked = settings.Startup, MinHeight = 36, VerticalContentAlignment = VerticalAlignment.Center };
        var options = new StackPanel(); options.Children.Add(reverse); options.Children.Add(startup); Card(options);
        panel.Children.Add(new TextBlock { Text = status(), TextWrapping = TextWrapping.Wrap, Opacity = .65, FontSize = 12, Margin = new Thickness(0, 6, 0, 4), MaxHeight = 32 });
        panel.Children.Add(new TextBlock { Text = "Sonar handles your audio. This app only displays the balance.", Opacity = .55, FontSize = 11, Margin = new Thickness(0, 0, 0, 20) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right }; panel.Children.Add(buttons);
        void Apply() { settings.Position = (string)placement.SelectedItem; settings.Monitor = (string)monitor.SelectedItem; settings.Duration = duration.Value; settings.Reverse = reverse.IsChecked == true; settings.Startup = startup.IsChecked == true; }
        var test = new Button { Content = "Preview overlay", MinWidth = 128, Height = 34, Margin = new Thickness(0, 0, 8, 0) }; test.Click += (_, _) => { Apply(); preview(); }; buttons.Children.Add(test);
        var save = new Button { Content = "Save", MinWidth = 88, Height = 34 }; save.SetResourceReference(StyleProperty, "AccentButtonStyle"); save.Click += (_, _) => { Apply(); try { settings.Save(); Close(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save settings"); } }; buttons.Children.Add(save);
    }
}
