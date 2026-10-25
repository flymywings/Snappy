using Snappy.Core;
using Xunit;

namespace Snappy.Tests;

public class LayoutTests
{
    [Fact] public void OddPortraitHeightSharesExactlyOneBoundary()
    {
        var layout = Layouts.Presets()[0];
        var top = LayoutEngine.Bounds(layout, layout.Zones[0].Region, new(-1080, -30, 1080, 1919));
        var bottom = LayoutEngine.Bounds(layout, layout.Zones[1].Region, new(-1080, -30, 1080, 1919));
        Assert.Equal(new PixelRect(-1080, -30, 1080, 960), top);
        Assert.Equal(new PixelRect(-1080, 930, 1080, 959), bottom);
        Assert.Equal(top.Bottom, bottom.Y);
    }
    [Fact] public void GapScalesWithDpiAndCreatesOneGapBetweenZones()
    {
        var layout = Layouts.Presets()[0] with { Gap = 8 };
        Assert.Equal(new PixelRect(12, 12, 1056, 942), LayoutEngine.Bounds(layout, layout.Zones[0].Region, new(0, 0, 1080, 1920), 1.5));
        Assert.Equal(new PixelRect(12, 966, 1056, 942), LayoutEngine.Bounds(layout, layout.Zones[1].Region, new(0, 0, 1080, 1920), 1.5));
    }
    [Fact] public void OverlappingZonesAreRejectedButTouchingEdgesAreAllowed()
    {
        var layout = Layouts.Presets()[0];
        LayoutEngine.Validate(layout);
        layout.Zones.Add(new(Guid.NewGuid(), "Overlap", new(0, 2, 2, 2)));
        Assert.Throws<ArgumentException>(() => LayoutEngine.Validate(layout));
    }
    [Fact] public void OutOfBoundsAndEmptyRegionsAreRejected()
    {
        var layout = Layouts.Presets()[0];
        Assert.Throws<ArgumentException>(() => LayoutEngine.Bounds(layout, new(5, 0, 2, 1), new(0, 0, 100, 100)));
        Assert.Throws<ArgumentException>(() => LayoutEngine.Bounds(layout, new(0, 0, 0, 1), new(0, 0, 100, 100)));
    }
    [Fact] public void UncoveredAreaAndGapsHaveNoDragTarget()
    {
        var preset = Layouts.Presets()[0];
        var layout = preset with { Zones = [preset.Zones[0]], Gap = 10 };
        Assert.Null(LayoutEngine.HitTest(layout, new(0, 0, 1000, 2000), 100, 1500));
        Assert.Null(LayoutEngine.HitTest(layout, new(0, 0, 1000, 2000), 0, 0));
        Assert.Equal(layout.Zones[0].Id, LayoutEngine.HitTest(layout, new(0, 0, 1000, 2000), 500, 500)?.Id);
    }
    [Fact] public void AssignmentUsesMonitorIdentityAndOrientationAndKeepsDisconnectedSettings()
    {
        var settings = Layouts.Defaults();
        settings.Monitors.Add(new("portrait", "Portrait", settings.Layouts[2].Id));
        settings.Monitors.Add(new("unplugged", "Landscape", settings.Layouts[0].Id));
        Assert.Equal("Three rows", LayoutEngine.Resolve(settings, "portrait", "Portrait").Name);
        Assert.Equal("Left / right", LayoutEngine.Resolve(settings, "portrait", "Landscape").Name);
        Assert.Equal(2, settings.Monitors.Count);
    }
}
