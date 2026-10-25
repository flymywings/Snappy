namespace Snappy.Windows;

internal sealed class PanelWindow : Window
{
    private readonly Func<Display, Layout> resolve;
    private readonly Func<Display, int, int, string?> configure;
    private readonly Action<PanelPosition> rememberPosition;
    private readonly ComboBox monitors = new() { Focusable = false, IsTabStop = false, FontSize = 12, DisplayMemberPath = "Label", Margin = new Thickness(0, 0, 0, 12) };
    private readonly TextBlock targetText = Ui.Text("Select a window", 13);
    private readonly Image targetIcon = new() { Width = 18, Height = 18, Margin = new Thickness(0, 0, 8, 0), Source = Ui.BrandIcon };
    private readonly TextBlock status = Ui.Text("", 12, true);
    private readonly TextBlock columnCount = Ui.Text("2", 13), rowCount = Ui.Text("1", 13);
    private readonly GridSurface surface;
    private readonly StackPanel steppers;
    private bool updating, paused, exiting;
    private string? targetMonitor;
    internal nint Target { get; private set; }
    internal Display? SelectedDisplay => monitors.SelectedItem as Display;
    internal Layout CurrentLayout => surface.Layout;
    internal event Action<nint, Layout, GridRegion, Display>? SnapRequested;
    internal PanelWindow(Func<Display, Layout> resolve, Func<Display, int, int, string?> configure,
        Action advanced, Action<PanelPosition> rememberPosition, PanelPosition? position)
    {
        this.resolve = resolve; this.configure = configure; this.rememberPosition = rememberPosition;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Snappy"; Width = 324; SizeToContent = SizeToContent.Height; Icon = Ui.BrandIcon;
        ResizeMode = ResizeMode.NoResize; WindowStyle = WindowStyle.None;
        AllowsTransparency = true; Background = Brushes.Transparent;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
        { CaptionHeight = 44, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(7), UseAeroCaptionButtons = false });
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        var root = new StackPanel { Margin = new Thickness(14, 10, 14, 12) };
        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 12), Height = 30 };
        var hide = Button("×", Hide); hide.ToolTip = "Hide to tray"; hide.Width = 28;
        var gear = Button("", advanced); gear.ToolTip = "Advanced settings"; gear.Width = 28;
        gear.Content = new System.Windows.Shapes.Path { Data = Geometry.Parse("M8,1 V3 M8,13 V15 M1,8 H3 M13,8 H15 M3,3 L4.5,4.5 M11.5,11.5 L13,13 M3,13 L4.5,11.5 M11.5,4.5 L13,3 M11,8 A3,3 0 1 1 5,8 A3,3 0 1 1 11,8"), Stroke = Ui.Muted, StrokeThickness = 1.4, Width = 16, Height = 16, Stretch = Stretch.Uniform };
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(gear, true);
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(hide, true);
        DockPanel.SetDock(hide, Dock.Right); DockPanel.SetDock(gear, Dock.Right); header.Children.Add(hide); header.Children.Add(gear);
        var brand = Ui.Row(new Image { Source = Ui.BrandIcon, Width = 23, Height = 23, Margin = new Thickness(0, 0, 8, 0) }, Ui.Text("Snappy", 15));
        ((TextBlock)brand.Children[1]).FontWeight = FontWeights.SemiBold; ((TextBlock)brand.Children[1]).Margin = new Thickness(0, 3, 0, 0);
        header.Children.Add(brand); root.Children.Add(header);
        targetText.TextTrimming = TextTrimming.CharacterEllipsis; targetText.TextWrapping = TextWrapping.NoWrap;
        targetText.Margin = new Thickness(0); targetText.VerticalAlignment = VerticalAlignment.Center;
        var targetRow = new DockPanel { Height = 26, Margin = new Thickness(0, 0, 0, 9) }; DockPanel.SetDock(targetIcon, Dock.Left); targetRow.Children.Add(targetIcon); targetRow.Children.Add(targetText);
        root.Children.Add(targetRow); root.Children.Add(monitors);
        surface = new(Layouts.Defaults().Layouts[1]) { Height = 180, Margin = new Thickness(0, 0, 0, 12), ActivateOnSelection = false, ShowZoneLabels = false, PreserveSelectionPreview = false, TransparentBackground = true, Focusable = false, IsEnabled = false };
        surface.Selected += region =>
        {
            if (!paused && Target != 0 && SelectedDisplay is { } display) SnapRequested?.Invoke(Target, CurrentLayout, region, display);
        };
        root.Children.Add(surface);
        steppers = Ui.Row(Stepper("Cols", columnCount, -1, 0, 1, 0), Stepper("Rows", rowCount, 0, -1, 0, 1)); root.Children.Add(steppers);
        status.Margin = new Thickness(0, 10, 0, 0); status.Visibility = Visibility.Collapsed; root.Children.Add(status);
        Content = new Border { Background = new SolidColorBrush(Color.FromRgb(28, 31, 35)) { Opacity = 0.9 }, BorderBrush = new SolidColorBrush(Color.FromRgb(80, 86, 96)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Child = root };
        monitors.SelectionChanged += (_, _) => { if (!updating) RefreshLayout(); };
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = Native.GetWindowLongPtr(handle, Native.GwlExStyle).ToInt64();
            Native.SetWindowLongPtr(handle, Native.GwlExStyle, new nint(style | 0x08000000 | 0x80));
            HwndSource.FromHwnd(handle)?.AddHook(Message);
        };
        Loaded += (_, _) =>
        {
            var display = Monitors.All().FirstOrDefault(d => position is not null && d.WorkArea.Contains(position.X + 30, position.Y + 30)) ?? Monitors.AtCursor();
            if (position is null) Ui.Center(this, display);
            else if (Native.GetWindowRect(new WindowInteropHelper(this).Handle, out var rect))
                Native.SetWindowPos(new WindowInteropHelper(this).Handle, 0,
                    Math.Clamp(position.X, display.WorkArea.X, Math.Max(display.WorkArea.X, display.WorkArea.Right - rect.Pixels.Width)),
                    Math.Clamp(position.Y, display.WorkArea.Y, Math.Max(display.WorkArea.Y, display.WorkArea.Bottom - rect.Pixels.Height)), 0, 0, 0x0015);
        };
        Closing += (_, e) => { if (!exiting) { e.Cancel = true; Hide(); } };
        RefreshDisplays();
    }
    private static Button Button(string label, Action action)
    { var button = Ui.Button(label, action); button.Focusable = false; button.IsTabStop = false; button.Padding = new Thickness(4, 1, 4, 1); button.Height = 26; button.Margin = new Thickness(0, 0, 4, 0); button.FontSize = 14; button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); return button; }
    private StackPanel Stepper(string label, TextBlock value, int minusC, int minusR, int plusC, int plusR)
    {
        var panel = new StackPanel { Width = 146, Orientation = Orientation.Horizontal };
        var caption = Ui.Text(label, 11, true); caption.Width = 39; caption.Margin = new Thickness(0); caption.VerticalAlignment = VerticalAlignment.Center; panel.Children.Add(caption);
        value.Width = 20; value.TextAlignment = TextAlignment.Center; value.Margin = new Thickness(0); value.VerticalAlignment = VerticalAlignment.Center;
        var minus = Button("−", () => Change(minusC, minusR)); minus.Width = 26; minus.BorderThickness = new Thickness(1); minus.ToolTip = "Fewer " + label.ToLowerInvariant();
        var plus = Button("+", () => Change(plusC, plusR)); plus.Width = 26; plus.BorderThickness = new Thickness(1); plus.ToolTip = "More " + label.ToLowerInvariant();
        panel.Children.Add(minus); panel.Children.Add(value); panel.Children.Add(plus); return panel;
    }
    private void Change(int columns, int rows)
    {
        if (paused || SelectedDisplay is not { } display) return;
        var error = configure(display, Math.Clamp(CurrentLayout.Columns + columns, 1, 24), Math.Clamp(CurrentLayout.Rows + rows, 1, 24));
        if (error is not null) { ShowStatus(error); return; }
        RefreshLayout(); ShowStatus("");
    }
    internal void UpdateTarget(nint target, string title, Display? display)
    {
        var changed = target != Target;
        Target = target; targetText.Text = title; targetText.ToolTip = title;
        if (changed) targetIcon.Source = Ui.AppIcon(target);
        var monitorKey = display is null ? null : display.Identity + ":" + display.Orientation;
        var monitorChanged = monitorKey != targetMonitor;
        if (display is not null && (changed || monitorKey != targetMonitor || SelectedDisplay is null))
        {
            updating = true; monitors.SelectedItem = monitors.Items.Cast<Display>().FirstOrDefault(d => d.Identity == display.Identity && d.Orientation == display.Orientation); updating = false;
        }
        targetMonitor = monitorKey;
        surface.IsEnabled = !paused && Target != 0 && SelectedDisplay is not null;
        if (changed || monitorChanged) { RefreshLayout(); ShowStatus(paused ? "Snapping paused" : ""); }
    }
    internal void RefreshDisplays(IReadOnlyList<Display>? displays = null)
    {
        var previous = SelectedDisplay;
        updating = true; monitors.Items.Clear();
        foreach (var display in displays ?? Monitors.All()) monitors.Items.Add(display);
        monitors.SelectedItem = monitors.Items.Cast<Display>().FirstOrDefault(d => d.Identity == previous?.Identity && d.Orientation == previous?.Orientation) ?? monitors.Items.Cast<Display>().FirstOrDefault();
        updating = false; RefreshLayout();
    }
    internal void RefreshLayout()
    {
        if (SelectedDisplay is not { } display) { surface.IsEnabled = false; return; }
        surface.Layout = resolve(display); columnCount.Text = CurrentLayout.Columns.ToString(); rowCount.Text = CurrentLayout.Rows.ToString();
        surface.Height = display.Orientation == "Portrait" ? 264 : 176;
        surface.IsEnabled = !paused && Target != 0;
    }
    internal void SetPaused(bool value)
    {
        paused = value; monitors.IsEnabled = steppers.IsEnabled = !value;
        surface.IsEnabled = !value && Target != 0 && SelectedDisplay is not null;
        ShowStatus(value ? "Snapping paused" : "");
    }
    internal void SetEditingAdvanced(bool value) => steppers.IsEnabled = !value && !paused;
    internal void ShowStatus(string text) { status.Text = text; status.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible; }
    private nint Message(nint window, int message, nint wp, nint lp, ref bool handled)
    {
        if (message == 0x21) { handled = true; return 3; } // MA_NOACTIVATE: mouse input without stealing target focus.
        if (message == 0x232 && Native.GetWindowRect(window, out var rect)) rememberPosition(new(rect.Left, rect.Top));
        return 0;
    }
    internal void Exit() { exiting = true; Close(); }
}
