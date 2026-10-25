using System.Globalization;

namespace Snappy.Windows;

internal sealed class GridSurface : FrameworkElement
{
    private Layout layout;
    private (int X, int Y)? start;
    private (int X, int Y)? hover;
    internal GridRegion? Selection { get; private set; }
    internal event Action<GridRegion>? Selected;
    internal bool ActivateOnSelection { get; set; } = true;
    internal bool ShowZoneLabels { get; set; } = true;
    internal bool PreserveSelectionPreview { get; set; } = true;
    internal bool TransparentBackground { get; set; }
    internal Layout Layout
    {
        get => layout;
        set { layout = value; Selection = null; hover = null; start = null; if (IsMouseCaptured) ReleaseMouseCapture(); InvalidateVisual(); }
    }
    internal GridSurface(Layout layout)
    {
        this.layout = layout; Focusable = true; Cursor = Cursors.Hand; MinWidth = 100; MinHeight = 100; SnapsToDevicePixels = true;
        ToolTip = "Click and drag across cells to select a rectangular region.";
        IsEnabledChanged += (_, _) => { if (!IsEnabled) { hover = null; start = null; Selection = null; } InvalidateVisual(); };
    }
    internal void Select(GridRegion? region) { Selection = region; InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        dc.PushOpacity(IsEnabled ? 1 : 0.5);
        dc.DrawRoundedRectangle(TransparentBackground ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(24, 26, 30)), new Pen(new SolidColorBrush(Color.FromRgb(93, 99, 109)), 1),
            new Rect(0.5, 0.5, Math.Max(1, ActualWidth - 1), Math.Max(1, ActualHeight - 1)), 4, 4);
        foreach (var zone in layout.Zones)
        {
            if (!ShowZoneLabels) continue;
            var rect = RegionRect(zone.Region); rect.Inflate(-3, -3);
            if (rect.Width <= 0 || rect.Height <= 0) continue;
            dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(112, 119, 131)), 1), rect, 3, 3);
            var text = new FormattedText(zone.Name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(Ui.Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 12, Ui.Muted, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = Math.Max(1, rect.Width - 12), MaxTextHeight = Math.Max(1, rect.Height - 8), Trimming = TextTrimming.CharacterEllipsis };
            dc.DrawText(text, new Point(rect.X + 6, rect.Y + 6));
        }
        var ordinary = new Pen(new SolidColorBrush(Color.FromRgb(93, 99, 109)), 1);
        var midpoint = new Pen(new SolidColorBrush(Color.FromRgb(164, 169, 178)), 2);
        for (var x = 1; x < layout.Columns; x++)
        {
            var isMidpoint = layout.Columns % 2 == 0 && x == layout.Columns / 2;
            var line = Math.Round(x * ActualWidth / layout.Columns) + (isMidpoint ? 0 : 0.5);
            dc.DrawLine(isMidpoint ? midpoint : ordinary, new Point(line, 1), new Point(line, ActualHeight - 1));
        }
        for (var y = 1; y < layout.Rows; y++)
        {
            var isMidpoint = layout.Rows % 2 == 0 && y == layout.Rows / 2;
            var line = Math.Round(y * ActualHeight / layout.Rows) + (isMidpoint ? 0 : 0.5);
            dc.DrawLine(isMidpoint ? midpoint : ordinary, new Point(1, line), new Point(ActualWidth - 1, line));
        }
        var hoverRegion = hover is { } cell ? new GridRegion(cell.X, cell.Y, 1, 1) : null;
        var highlight = start is not null || PreserveSelectionPreview ? Selection ?? hoverRegion : hoverRegion ?? Selection;
        if (highlight is not null && IsEnabled)
        {
            var rect = RegionRect(highlight); rect.Inflate(-1.5, -1.5);
            if (rect.Width > 0 && rect.Height > 0)
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(48, 128, 179, 250)), new Pen(Ui.Accent, 1), rect, 2, 2);
        }
        dc.Pop();
    }
    private Rect RegionRect(GridRegion region) => new(region.Column * ActualWidth / layout.Columns, region.Row * ActualHeight / layout.Rows,
        region.ColumnSpan * ActualWidth / layout.Columns, region.RowSpan * ActualHeight / layout.Rows);
    private (int X, int Y) Cell(Point point) => (Math.Clamp((int)(point.X / ActualWidth * layout.Columns), 0, layout.Columns - 1), Math.Clamp((int)(point.Y / ActualHeight * layout.Rows), 0, layout.Rows - 1));
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e); BeginSelection(e.GetPosition(this)); CaptureMouse(); e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (start is not null) UpdateSelection(e.GetPosition(this));
        else { var cell = Cell(e.GetPosition(this)); if (hover != cell) { hover = cell; InvalidateVisual(); } }
    }
    protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); hover = null; if (start is null) InvalidateVisual(); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        CompleteSelection(e.GetPosition(this)); e.Handled = true;
    }
    internal void BeginSelection(Point point)
    { if (ActivateOnSelection) Focus(); start = Cell(point); UpdateSelection(point); }
    internal void CompleteSelection(Point point)
    {
        if (start is null) return;
        UpdateSelection(point); start = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (Selection is { } region) Selected?.Invoke(region);
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); start = null; }
    private void UpdateSelection(Point point)
    {
        if (start is not { } first) return;
        var last = Cell(point);
        Selection = new(Math.Min(first.X, last.X), Math.Min(first.Y, last.Y), Math.Abs(first.X - last.X) + 1, Math.Abs(first.Y - last.Y) + 1); InvalidateVisual();
    }
}
