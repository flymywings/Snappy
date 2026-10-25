using System.Text.Json;

namespace Snappy.Windows;

internal sealed class EditorWindow : Window
{
    private readonly Settings draft;
    private readonly Func<Settings, string?> save;
    private readonly ComboBox monitor = new(), layouts = new();
    private readonly TextBox name = Ui.Input(""), columns = Ui.Input("6", 55), rows = Ui.Input("6", 55), gap = Ui.Input("0", 55), zoneName = Ui.Input("");
    private readonly ListBox zoneList = new() { Height = 160, Margin = new Thickness(0, 4, 0, 10) };
    private readonly ShortcutBox zoneKey = new(null), pickerKey;
    private readonly CheckBox startup = new() { Content = "Launch Snappy when I sign in" };
    private readonly CheckBox dragEnabled = new() { Content = "Enable Shift-drag snapping" };
    private readonly TextBlock status = Ui.Text("", 13, true);
    private readonly GridSurface surface;
    private bool refreshing;
    private Layout Current => (Layout)layouts.SelectedItem;

    internal EditorWindow(Settings settings, Func<Settings, string?> save)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        draft = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
        this.save = save; pickerKey = new(draft.PickerShortcut);
        Title = "Snappy · Layouts & settings"; Width = 1000; Height = 800; MinWidth = 840; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        surface = new(draft.Layouts[0]) { Height = 390, Margin = new Thickness(0, 8, 24, 12) };
        var outer = new DockPanel { Margin = new Thickness(26) };
        var header = new StackPanel();
        Icon = Ui.BrandIcon;
        header.Children.Add(Ui.Text("Snappy / layouts", 21));
        header.Children.Add(Ui.Text("One layout. Grid, shortcuts, and drag zones.", 14, true));
        header.Children.Add(Ui.Text("Monitor", 12, true)); header.Children.Add(monitor);
        header.Children.Add(Ui.Text("Active layout", 12, true));
        layouts.DisplayMemberPath = "Name"; layouts.MinWidth = 300;
        header.Children.Add(Ui.Row(layouts, Ui.Button("New", NewLayout), Ui.Button("Duplicate", Duplicate), Ui.Button("Delete", DeleteLayout)));
        DockPanel.SetDock(header, Dock.Top); outer.Children.Add(header);
        var footer = new StackPanel();
        footer.Children.Add(status);
        footer.Children.Add(Ui.Row(Ui.Button("Save changes", Save, true), Ui.Button("Cancel", Close), Ui.Text("Changes apply after saving.", 12, true)));
        DockPanel.SetDock(footer, Dock.Bottom); outer.Children.Add(footer);
        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(360) });
        var left = new StackPanel();
        left.Children.Add(Ui.Text("Select a rectangle", 18)); left.Children.Add(surface);
        left.Children.Add(Ui.Text("Select cells, name the region, then Add zone.\nEmpty areas keep ordinary window movement.", 13, true));
        left.Children.Add(Ui.Text("Layouts are shared: editing a layout updates every monitor using it.", 12, true));
        body.Children.Add(left);
        var right = new StackPanel(); Grid.SetColumn(right, 1);
        right.Children.Add(Ui.Text("Layout name", 12, true)); right.Children.Add(name);
        right.Children.Add(Ui.Row(Labelled("Columns", columns), Labelled("Rows", rows), Labelled("Gap (DIP)", gap), Ui.Button("Update grid", UpdateGrid)));
        right.Children.Add(Ui.Text("Saved zones", 15)); right.Children.Add(zoneList);
        right.Children.Add(Ui.Text("Zone name", 12, true)); right.Children.Add(zoneName);
        right.Children.Add(Ui.Text("Zone shortcut · press keys, Backspace to clear", 12, true)); right.Children.Add(zoneKey);
        right.Children.Add(Ui.Row(Ui.Button("Add zone", AddZone), Ui.Button("Update", UpdateZone), Ui.Button("Remove", RemoveZone)));
        right.Children.Add(Ui.Text("Picker shortcut", 12, true)); right.Children.Add(pickerKey);
        startup.IsChecked = draft.LaunchAtLogin; right.Children.Add(startup);
        dragEnabled.IsChecked = draft.EnableDragSnapping; right.Children.Add(dragEnabled);
        body.Children.Add(new ScrollViewer { Content = right, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }.InColumn(1));
        outer.Children.Add(body); Content = outer;
        monitor.SelectionChanged += (_, _) => ChooseMonitor();
        layouts.SelectionChanged += (_, _) => { if (!refreshing && layouts.SelectedItem is Layout) { Assign(); RefreshFields(); } };
        zoneList.SelectionChanged += (_, _) =>
        {
            if (zoneList.SelectedItem is not ZoneItem item) return;
            zoneName.Text = item.Zone.Name; zoneKey.Set(item.Zone.Shortcut); surface.Select(item.Zone.Region);
        };
        foreach (var display in Monitors.All()) monitor.Items.Add(display);
        refreshing = true;
        layouts.ItemsSource = draft.Layouts; layouts.SelectedIndex = 0;
        refreshing = false;
        if (monitor.Items.Count > 0) monitor.SelectedIndex = 0; else RefreshFields();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && e.OriginalSource is not ShortcutBox) Close(); };
        Loaded += (_, _) =>
        {
            var display = Monitors.ForWindow(new WindowInteropHelper(this).Handle);
            Height = Math.Min(Height, display.WorkArea.Height / display.Scale - 24); Ui.Center(this, display);
        };
    }
    private static StackPanel Labelled(string label, UIElement input)
    {
        var panel = new StackPanel(); panel.Children.Add(Ui.Text(label, 12, true)); panel.Children.Add(input); return panel;
    }
    private void ChooseMonitor()
    {
        if (refreshing) return;
        if (monitor.SelectedItem is not Display display) return;
        refreshing = true; layouts.SelectedItem = LayoutEngine.Resolve(draft, display.Identity, display.Orientation); refreshing = false;
        Assign(); RefreshFields();
        surface.Height = display.Orientation == "Portrait" ? 390 : 280;
    }
    internal void RefreshDisplays()
    {
        var identity = (monitor.SelectedItem as Display)?.Identity;
        refreshing = true;
        try
        {
            monitor.Items.Clear();
            foreach (var display in Monitors.All()) monitor.Items.Add(display);
            monitor.SelectedItem = monitor.Items.Cast<Display>().FirstOrDefault(d => d.Identity == identity);
        }
        finally { refreshing = false; }
        status.Text = monitor.SelectedItem is null
            ? "Display disconnected. Draft kept; select a current monitor before saving."
            : "Display configuration changed. Your draft is kept; save to apply it.";
    }
    private void Assign()
    {
        if (monitor.SelectedItem is not Display display || layouts.SelectedItem is not Layout layout) return;
        draft.Monitors.RemoveAll(m => m.Identity == display.Identity && m.Orientation == display.Orientation);
        draft.Monitors.Add(new(display.Identity, display.Orientation, layout.Id));
    }
    private void RefreshFields()
    {
        if (layouts.SelectedItem is not Layout layout) return;
        name.Text = layout.Name; columns.Text = layout.Columns.ToString(); rows.Text = layout.Rows.ToString(); gap.Text = layout.Gap.ToString();
        surface.Layout = layout; RefreshZones(); status.Text = "";
    }
    private void RefreshZones(Guid? selected = null)
    {
        zoneList.Items.Clear();
        foreach (var zone in Current.Zones) { var item = new ZoneItem(zone); zoneList.Items.Add(item); if (zone.Id == selected) zoneList.SelectedItem = item; }
        if (selected is null) { zoneName.Text = ""; zoneKey.Set(null); }
    }
    private void Replace(Layout replacement)
    {
        SimpleGrids.MarkCustom(draft, replacement.Id);
        var index = draft.Layouts.FindIndex(l => l.Id == replacement.Id);
        draft.Layouts[index] = replacement;
        refreshing = true; layouts.ItemsSource = null; layouts.ItemsSource = draft.Layouts; layouts.SelectedItem = replacement; refreshing = false;
        surface.Layout = replacement;
    }
    private Layout FromFields()
    {
        if (!int.TryParse(columns.Text, out var c) || !int.TryParse(rows.Text, out var r) || !int.TryParse(gap.Text, out var g))
            throw new ArgumentException("Grid dimensions and gap must be whole numbers.");
        var layout = Current with { Name = name.Text.Trim(), Columns = c, Rows = r, Gap = g, Zones = [.. Current.Zones] };
        LayoutEngine.Validate(layout); return layout;
    }
    private void UpdateGrid() => Attempt(() => { Replace(FromFields()); RefreshZones(); });
    private void AddZone() => Attempt(() =>
    {
        var region = surface.Selection ?? throw new ArgumentException("Select a rectangle in the grid first.");
        var layout = FromFields();
        var zone = new Zone(Guid.NewGuid(), zoneName.Text.Trim(), region, zoneKey.Shortcut);
        layout.Zones.Add(zone); LayoutEngine.Validate(layout); Replace(layout); RefreshZones(zone.Id);
    });
    private void UpdateZone() => Attempt(CommitZone);
    private void CommitZone()
    {
        if (zoneList.SelectedItem is not ZoneItem item) throw new ArgumentException("Select a saved zone to update.");
        var layout = FromFields();
        var region = surface.Selection ?? item.Zone.Region;
        var updated = item.Zone with { Name = zoneName.Text.Trim(), Region = region, Shortcut = zoneKey.Shortcut };
        layout.Zones[layout.Zones.FindIndex(z => z.Id == item.Zone.Id)] = updated;
        LayoutEngine.Validate(layout); Replace(layout); RefreshZones(updated.Id);
    }
    private void RemoveZone() => Attempt(() =>
    {
        if (zoneList.SelectedItem is not ZoneItem item) return;
        var layout = Current with { Zones = Current.Zones.Where(z => z.Id != item.Zone.Id).ToList() };
        Replace(layout); RefreshZones();
    });
    private void NewLayout()
    {
        var layout = new Layout(Guid.NewGuid(), "My layout", 6, 6, 0, []); draft.Layouts.Add(layout); SelectNew(layout);
    }
    private void Duplicate()
    {
        var layout = Current with { Id = Guid.NewGuid(), Name = Current.Name + " copy", Zones = Current.Zones.Select(z => z with { Id = Guid.NewGuid(), Shortcut = null }).ToList() };
        draft.Layouts.Add(layout); SelectNew(layout);
    }
    private void SelectNew(Layout layout)
    {
        refreshing = true; layouts.ItemsSource = null; layouts.ItemsSource = draft.Layouts; layouts.SelectedItem = layout; refreshing = false;
        Assign(); RefreshFields();
    }
    private void DeleteLayout() => Attempt(() =>
    {
        if (draft.Layouts.Count == 1) throw new ArgumentException("Keep at least one layout.");
        var id = Current.Id; draft.Layouts.Remove(Current);
        SimpleGrids.MarkCustom(draft, id);
        draft.Monitors.RemoveAll(m => m.LayoutId == id); SelectNew(draft.Layouts[0]);
    });
    private void Save() => Attempt(() =>
    {
        if (monitor.SelectedItem is not Display) throw new ArgumentException("Select a connected monitor before saving.");
        if (zoneList.SelectedItem is ZoneItem) CommitZone(); else Replace(FromFields());
        draft.PickerShortcut = pickerKey.Shortcut ?? throw new ArgumentException("Picker shortcut cannot be empty.");
        draft.LaunchAtLogin = startup.IsChecked == true; draft.EnableDragSnapping = dragEnabled.IsChecked == true; Assign(); SettingsStore.Validate(draft);
        var error = save(draft); if (error is not null) throw new ArgumentException(error); Close();
    });
    private void Attempt(Action action)
    {
        try { action(); status.Text = ""; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException)
        { status.Text = e.Message; }
    }
    private sealed record ZoneItem(Zone Zone)
    {
        public override string ToString() => $"{Zone.Name}  ·  {Zone.Region.ColumnSpan}×{Zone.Region.RowSpan}  ·  {Ui.KeyName(Zone.Shortcut)}";
    }
}
internal static class GridExtensions
{
    internal static T InColumn<T>(this T element, int column) where T : UIElement { Grid.SetColumn(element, column); return element; }
}
