namespace Snappy.Core;

public sealed record PlacementResult(bool Success, string? Error = null);
public sealed record WindowGeometry(PixelRect Outer, PixelRect Visible, int MinimumWidth, int MinimumHeight);
public interface IWindowSystem
{
    Task<PlacementResult> PrepareAsync(nint window);
    WindowGeometry Geometry(nint window);
    Task<bool> MoveAsync(nint window, PixelRect outer);
    void Focus(nint window);
}
public sealed class PlacementService(IWindowSystem windows)
{
    public async Task<PlacementResult> PlaceAsync(nint window, PixelRect target)
    {
        try
        {
            if (target.Width <= 0 || target.Height <= 0) return new(false, "The selected area is empty.");
            var ready = await windows.PrepareAsync(window);
            if (!ready.Success) return ready;
            var before = windows.Geometry(window);
            var geometry = before;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var left = geometry.Visible.X - geometry.Outer.X;
                var top = geometry.Visible.Y - geometry.Outer.Y;
                var right = geometry.Outer.Right - geometry.Visible.Right;
                var bottom = geometry.Outer.Bottom - geometry.Visible.Bottom;
                var outer = new PixelRect(target.X - left, target.Y - top, target.Width + left + right, target.Height + top + bottom);
                if (outer.Width < geometry.MinimumWidth || outer.Height < geometry.MinimumHeight)
                    return new(false, "This window's minimum size is larger than the selected zone.");
                if (!await windows.MoveAsync(window, outer))
                    return new(false, "Windows refused to move this window, or it stopped responding.");
                geometry = windows.Geometry(window);
                // Moving across monitors can change DPI and frame thickness. Correct using destination borders.
                if (Near(geometry.Visible, target)) { windows.Focus(window); return new(true); }
            }
            var restored = await windows.MoveAsync(window, before.Outer);
            return new(false, restored ? "The window refused the selected size. Its previous position was restored."
                : "The window refused the selected size and could not be restored.");
        }
        catch (InvalidOperationException ex) { return new(false, ex.Message); }
    }
    private static bool Near(PixelRect a, PixelRect b) => Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1 && Math.Abs(a.Width - b.Width) <= 1 && Math.Abs(a.Height - b.Height) <= 1;
}
