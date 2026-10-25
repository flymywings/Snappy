namespace Snappy.Core;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
}

public sealed record GridRegion(int Column, int Row, int ColumnSpan, int RowSpan);
public sealed record Hotkey(uint Modifiers, uint Key);
public sealed record Zone(Guid Id, string Name, GridRegion Region, Hotkey? Shortcut = null);
public sealed record Layout(Guid Id, string Name, int Columns, int Rows, int Gap, List<Zone> Zones);
public sealed record MonitorAssignment(string Identity, string Orientation, Guid LayoutId);
public sealed class Settings
{
    public int Version { get; set; } = 1;
    public Hotkey PickerShortcut { get; set; } = new(3, 0x20);
    public bool LaunchAtLogin { get; set; }
    public bool EnableDragSnapping { get; set; } = true;
    public PanelPosition? PanelPosition { get; set; }
    public List<Guid> SimpleGridIds { get; set; } = [];
    public List<Layout> Layouts { get; set; } = [];
    public List<MonitorAssignment> Monitors { get; set; } = [];
}

public static class Layouts
{
    public static List<Layout> Presets() =>
    [
        new(Guid.Parse("17b48200-54b4-4000-8000-000000000001"), "Top / bottom", 6, 6, 0,
            [Make("Top", 0, 0, 6, 3), Make("Bottom", 0, 3, 6, 3)]),
        new(Guid.Parse("17b48200-54b4-4000-8000-000000000002"), "Left / right", 6, 6, 0,
            [Make("Left", 0, 0, 3, 6), Make("Right", 3, 0, 3, 6)]),
        new(Guid.Parse("17b48200-54b4-4000-8000-000000000003"), "Three rows", 6, 6, 0,
            [Make("Top", 0, 0, 6, 2), Make("Middle", 0, 2, 6, 2), Make("Bottom", 0, 4, 6, 2)])
    ];
    private static Zone Make(string name, int x, int y, int w, int h) => new(Guid.NewGuid(), name, new(x, y, w, h));
    public static Settings Defaults()
    {
        var presets = Presets();
        var layouts = new List<Layout>
        {
            SimpleGrids.Uniform(presets[0].Id, "Top / bottom", 1, 2),
            SimpleGrids.Uniform(presets[1].Id, "Left / right", 2, 1),
            SimpleGrids.Uniform(presets[2].Id, "Three rows", 1, 3)
        };
        return new() { Layouts = layouts, SimpleGridIds = layouts.Select(l => l.Id).ToList() };
    }
}
