using System.Globalization;

namespace Snappy.Windows;

internal sealed class ZoneOverlay : Window
{
    private readonly OverlaySurface surface;
    internal ZoneOverlay()
    {
        Title = "Snappy zones"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        IsHitTestVisible = false;
        surface = new(); Content = surface;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = Native.GetWindowLongPtr(handle, Native.GwlExStyle).ToInt64();
            Native.SetWindowLongPtr(handle, Native.GwlExStyle, new nint(style | 0x08000000 | 0x20 | 0x80));
        };
    }
    internal void Display(Display display, Layout layout, Guid? selected)
    {
        surface.Display = display; surface.Layout = layout; surface.Selected = selected;
        if (!IsVisible) Show();
        var rect = display.WorkArea;
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, new nint(-1), rect.X, rect.Y, rect.Width, rect.Height, 0x0010);
        surface.InvalidateVisual();
    }
    private sealed class OverlaySurface : FrameworkElement
    {
        internal Display? Display;
        internal Layout? Layout;
        internal Guid? Selected;
        protected override void OnRender(DrawingContext dc)
        {
            if (Display is not { } display || Layout is not { } layout || ActualWidth <= 0 || ActualHeight <= 0) return;
            var sx = ActualWidth / display.WorkArea.Width; var sy = ActualHeight / display.WorkArea.Height;
            foreach (var zone in layout.Zones)
            {
                var bounds = LayoutEngine.Bounds(layout, zone.Region, display.WorkArea, display.Scale);
                var rect = new Rect((bounds.X - display.WorkArea.X) * sx + 2, (bounds.Y - display.WorkArea.Y) * sy + 2, Math.Max(1, bounds.Width * sx - 4), Math.Max(1, bounds.Height * sy - 4));
                var selected = zone.Id == Selected;
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(selected ? (byte)60 : (byte)12, 128, 179, 250)),
                    new Pen(selected ? Ui.Accent : new SolidColorBrush(Color.FromArgb(180, 164, 169, 178)), selected ? 2 : 1), rect, 6, 6);
                var label = new FormattedText(zone.Name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface(Ui.Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 18, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                { MaxTextWidth = Math.Max(1, rect.Width - 32), Trimming = TextTrimming.CharacterEllipsis };
                dc.DrawText(label, new Point(rect.X + 16, rect.Y + 14));
            }
        }
    }
}
