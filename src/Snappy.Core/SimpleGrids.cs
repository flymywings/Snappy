namespace Snappy.Core;

public sealed record PanelPosition(int X, int Y);
public static class SimpleGrids
{
    public static void MarkCustom(Settings settings, Guid id) => settings.SimpleGridIds.RemoveAll(value => value == id);
    public static Layout Configure(Settings settings, string identity, string orientation, int columns, int rows)
    {
        if (columns is < 1 or > 24 || rows is < 1 or > 24) throw new ArgumentException("Choose 1–24 rows and columns.");
        var current = LayoutEngine.Resolve(settings, identity, orientation);
        // Unassigned monitors also consume named fallbacks; explicit assignments do not make them private.
        var portraitFallback = settings.Layouts.FirstOrDefault(l => l.Name == "Top / bottom") ?? settings.Layouts.First();
        var landscapeFallback = settings.Layouts.FirstOrDefault(l => l.Name == "Left / right") ?? settings.Layouts.First();
        var ownAssignment = settings.Monitors.Any(m => m.Identity == identity && m.Orientation == orientation && m.LayoutId == current.Id);
        var reusable = ownAssignment && current.Id != portraitFallback.Id && current.Id != landscapeFallback.Id && settings.SimpleGridIds.Contains(current.Id) &&
            settings.Monitors.Count(m => m.LayoutId == current.Id) == 1 &&
            current.Zones.Count == current.Columns * current.Rows &&
            current.Zones.All(z => z.Region.ColumnSpan == 1 && z.Region.RowSpan == 1 && z.Shortcut is null);
        var result = Uniform(reusable ? current.Id : Guid.NewGuid(), "Simple grid", columns, rows, current.Gap);
        if (reusable) settings.Layouts[settings.Layouts.FindIndex(l => l.Id == current.Id)] = result;
        else { settings.Layouts.Add(result); settings.SimpleGridIds.Add(result.Id); }
        settings.Monitors.RemoveAll(m => m.Identity == identity && m.Orientation == orientation);
        settings.Monitors.Add(new(identity, orientation, result.Id));
        return result;
    }
    public static Layout Uniform(Guid id, string name, int columns, int rows, int gap = 0)
    {
        if (columns is < 1 or > 24 || rows is < 1 or > 24) throw new ArgumentException("Choose 1–24 rows and columns.");
        var zones = new List<Zone>();
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
            zones.Add(new(Guid.NewGuid(), $"{row * columns + column + 1}", new(column, row, 1, 1)));
        return new(id, name, columns, rows, gap, zones);
    }
}
public sealed class TargetSelection
{
    public nint Window { get; private set; }
    public void Observe(nint window, bool isOwnWindow, bool isEligible)
    { if (!isOwnWindow) Window = isEligible ? window : 0; }
}
