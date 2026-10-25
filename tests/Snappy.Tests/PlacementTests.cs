using Snappy.Core;
using Xunit;

namespace Snappy.Tests;

public class PlacementTests
{
    [Fact] public async Task InvisibleFrameBordersAreCompensatedAndWindowGetsFocus()
    {
        var windows = new TestWindows();
        var result = await new PlacementService(windows).PlaceAsync(1, new(0, 0, 1080, 960));
        Assert.True(result.Success);
        Assert.Equal(new PixelRect(-8, 0, 1096, 968), windows.Moved);
        Assert.True(windows.Focused);
    }
    [Fact] public async Task MinimumSizeFailureDoesNotMoveWindow()
    {
        var windows = new TestWindows { MinimumWidth = 1200 };
        Assert.False((await new PlacementService(windows).PlaceAsync(1, new(0, 0, 1080, 960))).Success);
        Assert.Null(windows.Moved);
    }
    [Fact] public async Task InaccessibleWindowIsNeverMoved()
    {
        var windows = new TestWindows { Allowed = false };
        var result = await new PlacementService(windows).PlaceAsync(1, new(0, 0, 1080, 960));
        Assert.False(result.Success); Assert.NotNull(result.Error); Assert.Null(windows.Moved);
    }
    [Fact] public async Task RefusedSizeIsReportedAndOriginalPositionRestored()
    {
        var windows = new TestWindows { IgnoreMove = true };
        Assert.False((await new PlacementService(windows).PlaceAsync(1, new(0, 0, 1080, 960))).Success);
        Assert.Equal(new PixelRect(100, 100, 816, 608), windows.Moved);
    }
    [Fact] public async Task DestinationDpiBordersAreRemeasuredAndVisibleBoundsCorrected()
    {
        var windows = new TestWindows { DpiChange = true };
        var result = await new PlacementService(windows).PlaceAsync(1, new(0, 0, 1080, 960));
        Assert.True(result.Success); Assert.Equal(2, windows.MoveCount);
        Assert.Equal(new PixelRect(-12, 0, 1104, 972), windows.Moved);
        Assert.Equal(new PixelRect(0, 0, 1080, 960), windows.Geometry(1).Visible);
    }
    [Fact] public async Task UnresponsiveGeometryProducesFailureWithoutTryingSynchronousMovement()
    {
        var windows = new TestWindows { Unresponsive = true };
        var result = await new PlacementService(windows).PlaceAsync(1, new(0, 0, 1080, 960));
        Assert.False(result.Success); Assert.Contains("respond", result.Error); Assert.Null(windows.Moved);
    }
    private sealed class TestWindows : IWindowSystem
    {
        public bool Allowed = true, Focused, IgnoreMove, DpiChange, Unresponsive;
        public int MinimumWidth = 100, MoveCount;
        public PixelRect? Moved;
        public Task<PlacementResult> PrepareAsync(nint window) => Task.FromResult(new PlacementResult(Allowed, Allowed ? null : "Permission denied."));
        public WindowGeometry Geometry(nint window)
        {
            if (Unresponsive) throw new InvalidOperationException("Window did not respond.");
            var outer = IgnoreMove || Moved is null ? new PixelRect(100, 100, 816, 608) : Moved.Value;
            var border = DpiChange && MoveCount > 0 ? 12 : 8;
            return new(outer, new(outer.X + border, outer.Y, outer.Width - border * 2, outer.Height - border), MinimumWidth, 50);
        }
        public Task<bool> MoveAsync(nint window, PixelRect outer) { Moved = outer; MoveCount++; return Task.FromResult(true); }
        public void Focus(nint window) { Focused = true; }
    }
}
