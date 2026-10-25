using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Snappy.Windows;

public partial class App : Application
{
    private Mutex? singleInstance;
    private Settings settings = null!;
    private SettingsStore store = null!;
    private HotkeyHost hotkeys = null!;
    private DragController drag = null!;
    private ForegroundTracker tracker = null!;
    private Forms.NotifyIcon tray = null!;
    private System.Drawing.Icon? trayIcon;
    private PlacementService placement = null!;
    private EditorWindow? editor;
    private PanelWindow panel = null!;
    private bool paused;
    private readonly HashSet<nint> placing = [];
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && e.Args[0] == "--self-test")
        { SmokeTest.Run(this, e.Args.Length > 1 ? e.Args[1] : "snappy-smoke.json"); return; }
        singleInstance = new Mutex(true, @"Local\Snappy.WindowManager", out var created);
        if (!created)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (HotkeyHost.RevealExisting()) { Shutdown(); return; }
                await Task.Delay(50);
            }
            MessageBox.Show("Exit the older Snappy from its tray icon, then open this version again.", "Snappy"); Shutdown(); return;
        }
        try
        {
            store = new SettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snappy", "settings.json"));
            settings = store.Load(); placement = new(new WindowSystem()); hotkeys = new();
            hotkeys.Invoked += Command; hotkeys.RevealRequested += Reveal;
            drag = new(() => settings, Place, Notify) { Paused = !settings.EnableDragSnapping };
            panel = new PanelWindow(d => LayoutEngine.Resolve(settings, d.Identity, d.Orientation), ConfigureGrid, OpenEditor, RememberPosition, settings.PanelPosition);
            panel.SnapRequested += Place;
            tracker = new ForegroundTracker(); tracker.Changed += panel.UpdateTarget;
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Show grid", null, (_, _) => Dispatcher.Invoke(Reveal));
            menu.Items.Add("Advanced settings", null, (_, _) => Dispatcher.Invoke(OpenEditor));
            menu.Items.Add("How to use Snappy", null, (_, _) => Dispatcher.Invoke(ShowHelp));
            menu.Items.Add("Pause snapping", null, (_, _) => Dispatcher.Invoke(() =>
            {
                paused = !paused; drag.Paused = paused || !settings.EnableDragSnapping; panel.SetPaused(paused);
                panel.SetEditingAdvanced(editor is not null);
                tray.Text = paused ? "Snappy · Paused" : "Snappy · Window manager";
                ((Forms.ToolStripMenuItem)menu.Items[3]).Checked = paused;
            }));
            menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(Shutdown));
            using (var resource = GetResourceStream(new Uri("pack://application:,,,/Snappy;component/Assets/Snappy.ico"))!.Stream)
            using (var icon = new System.Drawing.Icon(resource, new System.Drawing.Size(16, 16)))
                trayIcon = (System.Drawing.Icon)icon.Clone();
            tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "Snappy · Window manager", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (_, _) => Dispatcher.Invoke(Reveal);
            if (!hotkeys.Registry.TryApply(Bindings(settings), out var error)) Notify(error!);
            tracker.Refresh(true);
            if (!e.Args.Contains("--background")) Reveal();
            if (store.RecoveryMessage is { } recovery) Notify(recovery);
        }
        catch (Exception ex)
        { MessageBox.Show(ex.Message, "Snappy could not start", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    private void Reveal()
    {
        if (panel is null) return;
        tracker.Refresh(true); panel.Show();
        // Do not Activate: the selected app keeps foreground focus.
    }
    private static Dictionary<string, Hotkey> Bindings(Settings value)
    {
        var bindings = new Dictionary<string, Hotkey> { ["picker"] = value.PickerShortcut };
        foreach (var layout in value.Layouts)
        foreach (var zone in layout.Zones)
            if (zone.Shortcut is { } key) bindings.Add($"zone:{layout.Id}:{zone.Id}", key);
        return bindings;
    }
    private void Command(string command)
    {
        if (command == "picker") { Reveal(); return; }
        if (paused) return;
        tracker.Refresh(); var target = tracker.Target;
        if (!WindowSystem.Eligible(target)) { Notify("Select a resizable app window first."); return; }
        var parts = command.Split(':');
        var display = Monitors.ForWindow(target); var layout = LayoutEngine.Resolve(settings, display.Identity, display.Orientation);
        if (parts.Length != 3 || layout.Id.ToString() != parts[1]) { Notify("That shortcut belongs to a different layout. Select it in Advanced settings."); return; }
        var zone = layout.Zones.FirstOrDefault(z => z.Id.ToString() == parts[2]);
        if (zone is not null) Place(target, layout, zone.Region, display);
    }
    private async void Place(nint target, Layout layout, GridRegion region, Display display)
    {
        if (paused || !placing.Add(target)) return;
        try
        {
            var bounds = Monitors.PlacementBounds(layout, region, display);
            var result = await placement.PlaceAsync(target, bounds);
            if (!result.Success) Notify(result.Error!);
            else panel.ShowStatus("");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            if (ex is InvalidOperationException) { panel.RefreshDisplays(); tracker.Refresh(true); }
            Notify(ex.Message);
        }
        finally { placing.Remove(target); tracker.Refresh(); }
    }
    private void OpenEditor()
    {
        drag.Cancel();
        if (editor is not null) { editor.Activate(); return; }
        panel.SetEditingAdvanced(true);
        editor = new(settings, candidate =>
        {
            candidate.PanelPosition = settings.PanelPosition;
            return Save(candidate);
        });
        editor.Closed += (_, _) => { editor = null; panel.SetEditingAdvanced(false); panel.RefreshLayout(); tracker.Refresh(true); };
        editor.Show();
    }
    private static Settings Clone(Settings value) => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(value))!;
    private string? ConfigureGrid(Display display, int columns, int rows)
    {
        var candidate = Clone(settings);
        SimpleGrids.Configure(candidate, display.Identity, display.Orientation, columns, rows);
        return Save(candidate);
    }
    private void RememberPosition(PanelPosition position)
    {
        var candidate = Clone(settings); candidate.PanelPosition = position;
        if (Save(candidate) is { } error) Notify("Could not remember panel position: " + error);
    }
    private string? Save(Settings candidate)
    {
        var previous = settings;
        try
        {
            SettingsStore.Validate(candidate);
            if (!hotkeys.Registry.TryStage(Bindings(candidate), out var change, out var error)) return error;
            using var pendingKeys = change;
            if (candidate.LaunchAtLogin != previous.LaunchAtLogin) LoginStartup.Set(candidate.LaunchAtLogin);
            store.Save(candidate); pendingKeys!.Commit(); settings = candidate;
            drag.Paused = paused || !settings.EnableDragSnapping; panel.RefreshLayout(); return null;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            if (candidate.LaunchAtLogin != previous.LaunchAtLogin)
                try { LoginStartup.Set(previous.LaunchAtLogin); } catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException or System.Security.SecurityException) { Notify("Startup preference could not be restored: " + rollback.Message); }
            return ex.Message;
        }
    }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    { drag.Cancel(); panel.RefreshDisplays(); tracker.Refresh(true); editor?.RefreshDisplays(); });
    private void ShowHelp() => MessageBox.Show(
        "Choose rows and columns, click an app window, then click a grid cell.\nDrag across cells for a larger area. The grid stays open.\n\nHold Shift while dragging a window to use saved zones.\nRelease Shift or press Esc to cancel drag snapping.\n\nThe gear opens Advanced settings. Grid changes save automatically.\n" +
        Ui.KeyName(settings.PickerShortcut) + " shows the grid. Close hides it; tray menu exits.\n\nSnappy 0.5.1 · MIT · Offline · No telemetry", "Snappy · Quick guide");
    private void Notify(string text)
    {
        panel?.ShowStatus(text);
        if (tray is null || panel?.IsVisible == true) return;
        tray.BalloonTipTitle = "Snappy"; tray.BalloonTipText = text; tray.ShowBalloonTip(5000);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        tracker?.Dispose(); drag?.Dispose(); panel?.Exit(); hotkeys?.Dispose(); tray?.Dispose(); trayIcon?.Dispose(); singleInstance?.Dispose(); base.OnExit(e);
    }
}
