namespace Snappy.Windows;

internal sealed class HotkeyHost : IHotkeyPlatform, IDisposable
{
    private readonly HwndSource source;
    internal event Action<string>? Invoked;
    internal event Action? RevealRequested;
    internal event Action? DisplayChanged;
    internal HotkeyRegistry Registry { get; }
    internal HotkeyHost(string name = "Snappy message host v2")
    {
        source = new HwndSource(new HwndSourceParameters(name) { ParentWindow = new nint(-3), Width = 0, Height = 0 });
        source.AddHook(Message);
        Registry = new(this);
    }
    private nint Message(nint hwnd, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == 0x312 && Registry.Command((int)wparam) is { } command) { Invoked?.Invoke(command); handled = true; }
        if (message == 0x8001) { RevealRequested?.Invoke(); handled = true; }
        // Message-only windows do not receive broadcasts. SystemEvents below handles display changes.
        return 0;
    }
    internal void NotifyDisplayChanged() => DisplayChanged?.Invoke();
    internal static bool RevealExisting(string name = "Snappy message host v2")
    {
        var handle = Native.FindWindowEx(new nint(-3), 0, null, name);
        return handle != 0 && Native.PostMessage(handle, 0x8001, 0, 0);
    }
    public bool Register(int id, Hotkey shortcut) => Native.RegisterHotKey(source.Handle, id, shortcut.Modifiers | 0x4000, shortcut.Key);
    public void Unregister(int id) => Native.UnregisterHotKey(source.Handle, id);
    public void Dispose() { Registry.Dispose(); source.Dispose(); }
}
