namespace Snappy.Windows;

internal sealed class ShortcutBox : TextBox
{
    internal Hotkey? Shortcut { get; private set; }
    internal ShortcutBox(Hotkey? shortcut)
    {
        IsReadOnly = true; MinWidth = 170; ToolTip = "Press a key with Ctrl, Alt, Shift, or Win. Backspace clears."; Set(shortcut);
        PreviewKeyDown += Capture;
    }
    internal void Set(Hotkey? value) { Shortcut = value; Text = Ui.KeyName(value); }
    private void Capture(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Back or Key.Delete) { Set(null); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var modifiers = Keyboard.Modifiers;
        uint bits = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) bits |= 1;
        if (modifiers.HasFlag(ModifierKeys.Control)) bits |= 2;
        if (modifiers.HasFlag(ModifierKeys.Shift)) bits |= 4;
        if (modifiers.HasFlag(ModifierKeys.Windows)) bits |= 8;
        if (bits == 0) return;
        Set(new(bits, (uint)KeyInterop.VirtualKeyFromKey(key)));
    }
}
