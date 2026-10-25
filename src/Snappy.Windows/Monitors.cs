using System.Runtime.InteropServices;

namespace Snappy.Windows;

internal sealed record Display(nint Handle, string Identity, string Name, string Orientation, PixelRect WorkArea, double Scale)
{
    public string Label => Name.Replace("DISPLAY", "Display ") + " · " + Orientation;
    public override string ToString() => $"{Name} · {Orientation} · {WorkArea.Width}×{WorkArea.Height}";
}

internal static class Monitors
{
    internal static PixelRect PlacementBounds(Layout layout, GridRegion region, Display selected,
        IReadOnlyList<Display>? connected = null)
    {
        var current = (connected ?? All()).FirstOrDefault(m => m.Identity == selected.Identity &&
            m.Orientation == selected.Orientation);
        if (current is null)
            throw new InvalidOperationException("The selected monitor is no longer available in that orientation. Select a monitor and try again.");
        return LayoutEngine.Bounds(layout, region, current.WorkArea, current.Scale);
    }
    internal static List<Display> All()
    {
        var displays = new List<Display>();
        Native.EnumDisplayMonitors(0, 0, (nint monitor, nint hdc, ref Native.Rect rect, nint data) =>
        { displays.Add(Read(monitor)); return true; }, 0);
        return displays;
    }
    internal static Display ForWindow(nint window) => Read(Native.MonitorFromWindow(window, 2));
    internal static Display AtCursor()
    {
        Native.GetCursorPos(out var point); return Read(Native.MonitorFromPoint(point, 2));
    }
    private static Display Read(nint monitor)
    {
        var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>(), Device = "" };
        if (!Native.GetMonitorInfo(monitor, ref info)) throw new InvalidOperationException("Monitor disconnected.");
        var device = new Native.DisplayDevice { Size = Marshal.SizeOf<Native.DisplayDevice>(), Name = "", Description = "", Id = "", Key = "" };
        var found = Native.EnumDisplayDevices(info.Device, 0, ref device, 1);
        var identity = found && !string.IsNullOrEmpty(device.Id) ? device.Id : info.Device;
        var scale = Native.GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi / 96.0 : 1;
        return new(monitor, identity, info.Device.Replace("\\\\.\\", ""),
            info.Monitor.Bottom - info.Monitor.Top > info.Monitor.Right - info.Monitor.Left ? "Portrait" : "Landscape", info.Work.Pixels, scale);
    }
}
