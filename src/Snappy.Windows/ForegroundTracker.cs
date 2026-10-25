using System.Text;
using System.Windows.Threading;

namespace Snappy.Windows;

internal sealed class ForegroundTracker : IDisposable
{
    private readonly TargetSelection selection = new();
    private readonly Native.WinEventProc callback;
    private readonly nint hook;
    private readonly DispatcherTimer timer;
    private string signature = "";
    internal nint Target => selection.Window;
    internal event Action<nint, string, Display?>? Changed;
    internal ForegroundTracker()
    {
        callback = (_, _, window, _, _, _, _) => Refresh();
        hook = Native.SetWinEventHook(3, 3, 0, callback, 0, 0, 2);
        if (hook == 0) throw new InvalidOperationException("Cannot track the selected window.");
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) => Refresh(); timer.Start();
    }
    internal void Refresh(bool force = false)
    {
        var foreground = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(foreground, out var process);
        selection.Observe(foreground, process == Environment.ProcessId, WindowSystem.Eligible(foreground));
        if (Target != 0 && !WindowSystem.Eligible(Target)) selection.Observe(0, false, false);
        var title = "Select a window"; Display? display = null;
        try
        {
            if (Target != 0)
            {
                var text = new StringBuilder(512); Native.GetWindowText(Target, text, text.Capacity);
                title = string.IsNullOrWhiteSpace(text.ToString()) ? "Selected window" : text.ToString();
                display = Monitors.ForWindow(Target);
            }
        }
        catch (InvalidOperationException) { selection.Observe(0, false, false); }
        var next = $"{Target}:{title}:{display}";
        if (force || next != signature) { signature = next; Changed?.Invoke(Target, title, display); }
    }
    public void Dispose() { timer.Stop(); Native.UnhookWinEvent(hook); }
}
