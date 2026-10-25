using Snappy.Core;
using Xunit;

namespace Snappy.Tests;

public class DragTests
{
    [Fact] public void ShiftDragIntoZoneSnaps()
    {
        var drag = new DragSession(); drag.Begin(true); drag.Update(true, false);
        Assert.True(drag.End(true, false, true)); Assert.False(drag.Active);
    }
    [Theory] [InlineData(false, false)] [InlineData(true, true)]
    public void ReleasingShiftOrEscapeCancelsForEntireDrag(bool shift, bool escape)
    {
        var drag = new DragSession(); drag.Begin(true); drag.Update(true, false);
        drag.Update(shift, escape); drag.Update(true, false);
        Assert.False(drag.End(true, false, true));
    }
    [Fact] public void ResizeNeverSnapsAndUncoveredAreaDoesNotSnap()
    {
        var drag = new DragSession(); drag.Begin(false); drag.Update(true, false);
        Assert.False(drag.End(true, false, true));
        drag.Begin(true); drag.Update(true, false); Assert.False(drag.End(true, false, false));
    }
    [Fact] public void DisconnectOrPauseCancelsDrag()
    {
        var drag = new DragSession(); drag.Begin(true); drag.Update(true, false); drag.Cancel();
        Assert.False(drag.End(true, false, true));
    }
    [Fact] public void ConfirmedTitlebarMoveMayResizeDuringDpiTransition()
    {
        var drag = new DragSession(); drag.Begin(true); drag.Update(true, false);
        drag.ObserveSizeChange(true, true);
        Assert.True(drag.End(true, false, true));
    }
    [Fact] public void UnconfirmedSizeChangeIsResizeAndNeverSnaps()
    {
        var drag = new DragSession(); drag.Begin(true); drag.Update(true, false);
        drag.ObserveSizeChange(true, false);
        Assert.False(drag.End(true, false, true));
    }
    [Fact] public void ShiftReleaseAndRepressBetweenPositionSamplesStillCancels()
    {
        var drag = new DragSession(); drag.Begin(true); drag.Update(true, false);
        drag.KeyTransition(0xA0, false); drag.KeyTransition(0xA0, true);
        Assert.False(drag.End(true, false, true));
    }
    [Fact] public void EscapeTapBetweenPositionSamplesStillCancels()
    {
        var drag = new DragSession(); drag.Begin(true); drag.Update(true, false);
        drag.KeyTransition(0x1B, true); drag.KeyTransition(0x1B, false);
        Assert.False(drag.End(true, false, true));
    }
}
