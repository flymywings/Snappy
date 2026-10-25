using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Snappy.Windows;

internal sealed class WindowSystem : IWindowSystem
{
    internal static bool Eligible(nint window, bool allowOwn = false)
    {
        if (window == 0 || !Native.IsWindow(window) || !Native.IsWindowVisible(window) || Native.IsIconic(window) || Native.GetAncestor(window, 2) != window) return false;
        var style = Native.GetWindowLongPtr(window, Native.GwlStyle).ToInt64();
        if ((style & Native.WsThickFrame) == 0 || (style & Native.WsChild) != 0) return false;
        if (Native.ClassName(window) is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        Native.GetWindowThreadProcessId(window, out var process);
        if (!allowOwn && process == Environment.ProcessId) return false;
        return Native.DwmGetWindowAttributeInt(window, 14, out var cloaked, 4) != 0 || cloaked == 0;
    }
    internal bool AllowOwnWindows { get; init; }
    public async Task<PlacementResult> PrepareAsync(nint window)
    {
        if (!Eligible(window, AllowOwnWindows)) return new(false, "Choose a visible, resizable application window.");
        if (Native.IsHungAppWindow(window)) return new(false, "The window is not responding. Try again when it recovers.");
        Native.GetWindowThreadProcessId(window, out var processId);
        var process = Native.OpenProcess(0x1000, false, processId);
        if (process == 0) return new(false, "Cannot access this window. Elevated windows are not supported.");
        try
        {
            if (!Native.OpenProcessToken(process, 8, out var token)) return new(false, "Cannot inspect this window's permissions.");
            try
            {
                if (!Native.GetTokenInformation(token, 20, out var elevated, 4, out _)) return new(false, "Cannot inspect this window's permissions.");
                if (elevated != 0) return new(false, "The selected window runs as administrator. Snappy cannot move it.");
            }
            finally { Native.CloseHandle(token); }
        }
        finally { Native.CloseHandle(process); }
        if (Native.IsZoomed(window))
        {
            if (!Native.ShowWindowAsync(window, 9)) return new(false, "The window could not be restored.");
            var timeout = Stopwatch.StartNew();
            while (Native.IsZoomed(window) && timeout.ElapsedMilliseconds < 500) await Task.Delay(10);
            if (Native.IsZoomed(window) || !Native.IsWindow(window)) return new(false, "The window did not respond to restore.");
            await Task.Delay(20);
        }
        return new(true);
    }
    public WindowGeometry Geometry(nint window)
    {
        if (!Native.GetWindowRect(window, out var outer)) throw new InvalidOperationException("The window closed.");
        var visible = Native.DwmGetWindowAttribute(window, 9, out var frame, Marshal.SizeOf<Native.Rect>()) == 0 ? frame.Pixels : outer.Pixels;
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Native.MinMaxInfo>());
        try
        {
            Marshal.StructureToPtr(new Native.MinMaxInfo(), pointer, false);
            var ok = Native.SendMessageTimeout(window, 0x24, 0, pointer, 0x2, 150, out _) != 0;
            if (!ok) throw new InvalidOperationException("The window did not respond to a size query. No movement was attempted.");
            var limits = Marshal.PtrToStructure<Native.MinMaxInfo>(pointer);
            return new(outer.Pixels, visible, limits.MinTrack.X, limits.MinTrack.Y);
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }
    public async Task<bool> MoveAsync(nint window, PixelRect outer)
    {
        // ASYNCWINDOWPOS prevents a hung foreign UI thread from blocking Snappy's dispatcher.
        if (!Native.SetWindowPos(window, 0, outer.X, outer.Y, outer.Width, outer.Height, 0x4014)) return false;
        var timeout = Stopwatch.StartNew();
        do
        {
            await Task.Delay(20);
            if (!Native.GetWindowRect(window, out var actual)) return false;
            if (actual.Pixels == outer) { await Task.Delay(20); return true; }
        } while (timeout.ElapsedMilliseconds < 500);
        return Native.IsWindow(window) && !Native.IsHungAppWindow(window);
    }
    public void Focus(nint window) => Native.SetForegroundWindow(window);
}
