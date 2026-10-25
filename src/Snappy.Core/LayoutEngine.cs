namespace Snappy.Core;

public static class LayoutEngine
{
    public static PixelRect Bounds(Layout layout, GridRegion region, PixelRect workArea, double scale = 1)
    {
        ValidateRegion(layout, region);
        if (workArea.Width <= 0 || workArea.Height <= 0 || !double.IsFinite(scale) || scale <= 0)
            throw new ArgumentException("Invalid monitor work area or scale.");
        int Boundary(int origin, int length, int cell, int count) => origin + (int)Math.Round((double)length * cell / count, MidpointRounding.AwayFromZero);
        var gap = (int)Math.Round(layout.Gap * scale, MidpointRounding.AwayFromZero);
        var left = Boundary(workArea.X, workArea.Width, region.Column, layout.Columns) + (region.Column == 0 ? gap : gap / 2);
        var top = Boundary(workArea.Y, workArea.Height, region.Row, layout.Rows) + (region.Row == 0 ? gap : gap / 2);
        var right = Boundary(workArea.X, workArea.Width, region.Column + region.ColumnSpan, layout.Columns)
            - (region.Column + region.ColumnSpan == layout.Columns ? gap : gap - gap / 2);
        var bottom = Boundary(workArea.Y, workArea.Height, region.Row + region.RowSpan, layout.Rows)
            - (region.Row + region.RowSpan == layout.Rows ? gap : gap - gap / 2);
        if (right <= left || bottom <= top) throw new ArgumentException("Gap leaves no room for this zone.");
        return new(left, top, right - left, bottom - top);
    }
    public static void Validate(Layout layout)
    {
        if (layout.Id == Guid.Empty || string.IsNullOrWhiteSpace(layout.Name) || layout.Zones is null)
            throw new ArgumentException("Layout needs a name and valid identity.");
        ValidateRegion(layout, new(0, 0, 1, 1));
        var ids = new HashSet<Guid>();
        foreach (var zone in layout.Zones)
        {
            if (zone is null || zone.Region is null || zone.Id == Guid.Empty || !ids.Add(zone.Id) || string.IsNullOrWhiteSpace(zone.Name))
                throw new ArgumentException("Zones need unique identities and names.");
            ValidateRegion(layout, zone.Region);
        }
        for (var i = 0; i < layout.Zones.Count; i++)
        for (var j = i + 1; j < layout.Zones.Count; j++)
        {
            var a = layout.Zones[i].Region; var b = layout.Zones[j].Region;
            if (a.Column < b.Column + b.ColumnSpan && a.Column + a.ColumnSpan > b.Column &&
                a.Row < b.Row + b.RowSpan && a.Row + a.RowSpan > b.Row)
                throw new ArgumentException("Saved zones cannot overlap.");
        }
    }
    private static void ValidateRegion(Layout layout, GridRegion region)
    {
        if (layout.Columns is < 1 or > 24 || layout.Rows is < 1 or > 24 || layout.Gap is < 0 or > 64 ||
            region.Column < 0 || region.Row < 0 || region.ColumnSpan < 1 || region.RowSpan < 1 ||
            region.ColumnSpan > layout.Columns - region.Column || region.RowSpan > layout.Rows - region.Row)
            throw new ArgumentException("Use a 1–24 cell grid, 0–64 gap, and regions inside the grid.");
    }
    public static Layout Resolve(Settings settings, string identity, string orientation)
    {
        var assignment = settings.Monitors.FirstOrDefault(m => m.Identity == identity && m.Orientation == orientation);
        return settings.Layouts.FirstOrDefault(l => l.Id == assignment?.LayoutId)
            ?? settings.Layouts.FirstOrDefault(l => l.Name == (orientation == "Portrait" ? "Top / bottom" : "Left / right"))
            ?? settings.Layouts.First();
    }
    public static Zone? HitTest(Layout layout, PixelRect workArea, int x, int y, double scale = 1) =>
        layout.Zones.FirstOrDefault(zone => Bounds(layout, zone.Region, workArea, scale).Contains(x, y));
}
