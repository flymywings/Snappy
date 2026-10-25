using System.Windows.Media.Imaging;

namespace Snappy.Windows;

internal static class Ui
{
    internal static readonly SolidColorBrush Accent = new(Color.FromRgb(128, 179, 250));
    internal static readonly SolidColorBrush Muted = new(Color.FromRgb(164, 169, 178));
    internal static readonly FontFamily Font = new("Segoe UI Variable, Segoe UI");
    private static readonly Lazy<ImageSource> Brand = new(() =>
    {
        var bitmap = new BitmapImage(); bitmap.BeginInit();
        bitmap.UriSource = new Uri("pack://application:,,,/Snappy;component/Assets/Snappy-256.png");
        bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit(); bitmap.Freeze();
        return bitmap;
    });
    internal static ImageSource BrandIcon => Brand.Value;
    internal static TextBlock Text(string value, double size = 14, bool muted = false) => new()
    {
        Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap,
        Foreground = muted ? Muted : new SolidColorBrush(Color.FromRgb(240, 241, 243)), FontFamily = Font,
        Margin = new Thickness(0, 4, 0, 8)
    };
    internal static Button Button(string value, Action click, bool primary = false)
    {
        var button = new Button { Content = value };
        if (primary) { button.Background = Accent; button.Foreground = new SolidColorBrush(Color.FromRgb(24, 27, 32)); }
        button.Click += (_, _) => click(); return button;
    }
    internal static ImageSource AppIcon(nint window)
    {
        if (window == 0) return BrandIcon;
        Native.SendMessageTimeout(window, 0x7F, 2, 0, 2, 35, out var icon);
        if (icon == 0) icon = Native.GetClassLongPtr(window, -34);
        if (icon == 0) icon = Native.GetClassLongPtr(window, -14);
        if (icon == 0) return BrandIcon;
        try { return Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromWidthAndHeight(20, 20)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.ExternalException) { return BrandIcon; }
    }
    internal static TextBox Input(string value, double width = double.NaN) => new() { Text = value, Width = width };
    internal static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }
    internal static string KeyName(Hotkey? key)
    {
        if (key is null) return "No shortcut";
        return ((key.Modifiers & 2) != 0 ? "Ctrl+" : "") + ((key.Modifiers & 1) != 0 ? "Alt+" : "") +
            ((key.Modifiers & 4) != 0 ? "Shift+" : "") + ((key.Modifiers & 8) != 0 ? "Win+" : "") + KeyInterop.KeyFromVirtualKey((int)key.Key);
    }
    internal static void Center(Window window, Display display)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0 || !Native.GetWindowRect(handle, out var rect)) return;
        Native.SetWindowPos(handle, 0, display.WorkArea.X + Math.Max(0, (display.WorkArea.Width - rect.Pixels.Width) / 2),
            display.WorkArea.Y + Math.Max(0, (display.WorkArea.Height - rect.Pixels.Height) / 2), 0, 0, 0x0015);
    }
}
