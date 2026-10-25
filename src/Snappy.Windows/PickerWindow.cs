namespace Snappy.Windows;

internal sealed class PickerWindow : Window
{
    internal PickerWindow(Layout layout, Display display, Action<GridRegion> place)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Snappy · Place window"; Width = 430; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        var root = new StackPanel { Margin = new Thickness(24) };
        Icon = Ui.BrandIcon;
        root.Children.Add(Ui.Text("Snappy", 20));
        root.Children.Add(Ui.Text($"{display.Name}  /  {layout.Name}", 13, true));
        root.Children.Add(Ui.Text("Drag across the grid", 17));
        var surface = new GridSurface(layout) { Height = display.Orientation == "Portrait" ? 330 : 215, Margin = new Thickness(0, 4, 0, 12) };
        surface.Selected += region => { Close(); place(region); };
        root.Children.Add(surface);
        root.Children.Add(Ui.Text("Or choose a saved zone", 13, true));
        var zones = new WrapPanel();
        foreach (var zone in layout.Zones)
            zones.Children.Add(Ui.Button(zone.Name + (zone.Shortcut is null ? "" : " · " + Ui.KeyName(zone.Shortcut)), () => { Close(); place(zone.Region); }));
        var scroll = new ScrollViewer { Content = zones, MaxHeight = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(scroll);
        root.Children.Add(Ui.Text("Esc to close · Shift-drag windows to use saved zones", 12, true));
        Content = root;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Loaded += (_, _) => { Ui.Center(this, display); Activate(); surface.Focus(); };
    }
}
