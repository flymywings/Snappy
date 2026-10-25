using System.Windows.Threading;

namespace Snappy.Windows;

internal sealed class DragController : IDisposable
{
    private readonly Func<Settings> settings;
    private readonly Action<nint, Layout, GridRegion, Display> place;
    private readonly Action<string> report;
    private readonly Native.WinEventProc callback;
    private readonly nint hook;
    private readonly DispatcherTimer timer;
    private readonly DragSession session = new();
    private readonly ZoneOverlay overlay = new();
    private nint target;
    private PixelRect initial;
    private bool wasMaximized, confirmedMove, paused, disposed;
    private readonly Native.KeyboardProc keyboardCallback;
    private nint keyboardHook;
    private Display? display;
    private Layout? layout;
    private Zone? zone;
    internal bool Paused { get => paused; set { paused = value; Cancel(); } }
    internal DragController(Func<Settings> settings, Action<nint, Layout, GridRegion, Display> place, Action<string> report)
    {
        this.settings = settings; this.place = place; this.report = report;
        callback = OnEvent;
        keyboardCallback = KeyboardEvent;
        hook = Native.SetWinEventHook(Native.EventMoveStart, Native.EventMoveEnd, 0, callback, 0, 0, 2);
        if (hook == 0) throw new InvalidOperationException("Windows drag tracking could not be started.");
        timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => Safe(Update);
    }
    private void OnEvent(nint eventHook, uint type, nint window, int objectId, int childId, uint thread, uint time)
    {
        if (disposed || paused || objectId != 0 || childId != 0) return;
        Safe(() =>
        {
            if (type == Native.EventMoveStart)
            {
                Cancel();
                if (!WindowSystem.Eligible(window)) return;
                Native.GetCursorPos(out var point);
                var packed = new nint(unchecked((int)((uint)(ushort)point.X | (uint)(ushort)point.Y << 16)));
                Native.SendMessageTimeout(window, 0x84, 0, packed, 2, 75, out var hit);
                // HTLEFT through HTBOTTOMRIGHT are sizing handles, not title-bar movement.
                if (hit.ToInt64() is >= 10 and <= 17) return;
                if (!Native.GetWindowRect(window, out var rect)) return;
                target = window; initial = rect.Pixels; wasMaximized = Native.IsZoomed(window);
                confirmedMove = hit == 2;
                keyboardHook = Native.SetWindowsHookEx(13, keyboardCallback, Native.GetModuleHandle(null), 0);
                if (keyboardHook == 0) { Cancel(); report("Drag cancellation tracking is unavailable. Use the picker or shortcuts."); return; }
                session.Begin(true); timer.Start(); Update();
            }
            else if (type == Native.EventMoveEnd && target == window)
            {
                Update();
                var snap = session.End(Native.Down(0x10), Native.Down(0x1B), zone is not null);
                var chosen = zone; var chosenLayout = layout; var chosenDisplay = display;
                var chosenTarget = target; Cancel();
                if (snap && chosen is not null && chosenLayout is not null && chosenDisplay is not null)
                    Application.Current.Dispatcher.BeginInvoke(() => place(chosenTarget, chosenLayout, chosen.Region, chosenDisplay));
            }
        });
    }
    private void Update()
    {
        if (!session.Active || !Native.IsWindow(target)) { Cancel(); return; }
        session.Update(Native.Down(0x10), Native.Down(0x1B));
        if (session.Cancelled) { overlay.Hide(); zone = null; return; }
        if (!Native.GetWindowRect(target, out var rect)) { Cancel(); return; }
        if (wasMaximized && !Native.IsZoomed(target)) { initial = rect.Pixels; wasMaximized = false; }
        else if (!wasMaximized)
        {
            session.ObserveSizeChange(rect.Pixels.Width != initial.Width || rect.Pixels.Height != initial.Height, confirmedMove);
            if (!session.Active) { Cancel(); return; }
        }
        if (!Native.Down(0x10)) { overlay.Hide(); zone = null; return; }
        display = Monitors.AtCursor();
        layout = LayoutEngine.Resolve(settings(), display.Identity, display.Orientation);
        Native.GetCursorPos(out var point);
        zone = LayoutEngine.HitTest(layout, display.WorkArea, point.X, point.Y, display.Scale);
        overlay.Display(display, layout, zone?.Id);
    }
    private void Safe(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        { Cancel(); report(e.Message); }
    }
    private nint KeyboardEvent(int code, nint message, nint data)
    {
        if (code >= 0 && session.Active)
        {
            var key = System.Runtime.InteropServices.Marshal.ReadInt32(data);
            var down = message is 0x100 or 0x104;
            var up = message is 0x101 or 0x105;
            var otherShiftHeld = key == 0xA0 && Native.Down(0xA1) || key == 0xA1 && Native.Down(0xA0);
            if (down || up && !otherShiftHeld) session.KeyTransition(key, down);
        }
        return Native.CallNextHookEx(keyboardHook, code, message, data);
    }
    internal void Cancel()
    {
        timer?.Stop(); session.Cancel(); overlay.Hide(); target = 0; zone = null; display = null; layout = null;
        if (keyboardHook != 0) { Native.UnhookWindowsHookEx(keyboardHook); keyboardHook = 0; }
    }
    public void Dispose() { disposed = true; Cancel(); Native.UnhookWinEvent(hook); overlay.Close(); }
}
