using Snappy.Core;
using Xunit;

namespace Snappy.Tests;

public class SimpleGridTests
{
    [Theory] [InlineData("Portrait", 1, 2)] [InlineData("Landscape", 2, 1)]
    public void FreshSettingsStartWithClickableHalves(string orientation, int columns, int rows)
    {
        var settings = Layouts.Defaults();
        var layout = LayoutEngine.Resolve(settings, "new-monitor", orientation);
        Assert.Equal(columns, layout.Columns); Assert.Equal(rows, layout.Rows);
        Assert.All(layout.Zones, z => Assert.Equal((1, 1), (z.Region.ColumnSpan, z.Region.RowSpan)));
    }
    [Fact] public void ResizingCustomGridPreservesItsZonesAndShortcuts()
    {
        var custom = Layouts.Presets()[0];
        custom.Zones[0] = custom.Zones[0] with { Shortcut = new(3, 65) };
        var settings = new Settings { Layouts = [custom], Monitors = [new("screen", "Portrait", custom.Id)] };
        var changed = SimpleGrids.Configure(settings, "screen", "Portrait", 1, 3);
        Assert.Equal(3, changed.Zones.Count); Assert.NotEqual(custom.Id, changed.Id);
        Assert.Equal(new Hotkey(3, 65), settings.Layouts.Single(l => l.Id == custom.Id).Zones[0].Shortcut);
        Assert.Equal(changed.Id, LayoutEngine.Resolve(settings, "screen", "Portrait").Id);
        SettingsStore.Validate(settings);
    }
    [Fact] public void RepeatedGridChangesDoNotAccumulateLayoutsOrChangeOtherMonitor()
    {
        var settings = Layouts.Defaults();
        SimpleGrids.Configure(settings, "a", "Portrait", 1, 3);
        var before = settings.Layouts.Count;
        SimpleGrids.Configure(settings, "a", "Portrait", 2, 3);
        Assert.Equal(before, settings.Layouts.Count);
        Assert.Equal(6, LayoutEngine.Resolve(settings, "a", "Portrait").Zones.Count);
        Assert.Equal(1, LayoutEngine.Resolve(settings, "b", "Portrait").Columns);
        Assert.Equal(2, LayoutEngine.Resolve(settings, "b", "Portrait").Rows);
    }
    [Fact] public void PanelPositionAndGridSurviveRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), "SnappyTests", Guid.NewGuid() + ".json");
        try
        {
            var settings = Layouts.Defaults(); settings.PanelPosition = new(-320, 55);
            SimpleGrids.Configure(settings, "monitor", "Portrait", 1, 3);
            var store = new SettingsStore(path); store.Save(settings); var loaded = store.Load();
            Assert.Equal(new PanelPosition(-320, 55), loaded.PanelPosition);
            Assert.Equal(3, LayoutEngine.Resolve(loaded, "monitor", "Portrait").Rows);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    [Fact] public void SnappyControlsNeverReplaceExternalTargetAndInvalidWindowClearsIt()
    {
        var selection = new TargetSelection();
        selection.Observe(1, false, true); selection.Observe(10, true, true);
        Assert.Equal((nint)1, selection.Window);
        selection.Observe(2, false, true); Assert.Equal((nint)2, selection.Window);
        selection.Observe(3, false, false); Assert.Equal((nint)0, selection.Window);
    }
    [Fact] public void EditingAssignedDefaultDoesNotChangeUnassignedMonitors()
    {
        var settings = Layouts.Defaults();
        var original = LayoutEngine.Resolve(settings, "a", "Portrait");
        settings.Monitors.Add(new("a", "Portrait", original.Id)); // Advanced save makes an explicit assignment.
        SimpleGrids.Configure(settings, "a", "Portrait", 1, 3);
        Assert.Equal(2, LayoutEngine.Resolve(settings, "unassigned", "Portrait").Rows);
        Assert.Equal(3, LayoutEngine.Resolve(settings, "a", "Portrait").Rows);
    }
    [Fact] public void AdvancedCustomizationPreservesNamedCellsOnNextSimpleGridEdit()
    {
        var settings = Layouts.Defaults();
        var grid = SimpleGrids.Configure(settings, "a", "Portrait", 1, 3);
        grid.Zones[0] = grid.Zones[0] with { Name = "Inbox" };
        SimpleGrids.MarkCustom(settings, grid.Id);
        var changed = SimpleGrids.Configure(settings, "a", "Portrait", 2, 3);
        Assert.NotEqual(grid.Id, changed.Id);
        Assert.Equal("Inbox", settings.Layouts.Single(l => l.Id == grid.Id).Zones[0].Name);
    }
}
