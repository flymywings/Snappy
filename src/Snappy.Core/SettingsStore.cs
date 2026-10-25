using System.Text.Json;

namespace Snappy.Core;

public sealed class SettingsStore(string path)
{
    public string Path { get; } = path;
    public string? RecoveryMessage { get; private set; }
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public Settings Load()
    {
        RecoveryMessage = null;
        if (!File.Exists(Path) && !File.Exists(Path + ".bak")) return Layouts.Defaults();
        try { return Read(Path); }
        catch (Exception e) when (Recoverable(e))
        {
            try
            {
                var recovered = Read(Path + ".bak");
                RecoveryMessage = "Settings recovered from backup. The damaged file will be preserved.";
                return recovered;
            }
            catch (Exception backupError) when (Recoverable(backupError))
            {
                RecoveryMessage = "Settings could not be read. Using presets; damaged files are preserved on save.";
                return Layouts.Defaults();
            }
        }
    }
    public void Save(Settings settings)
    {
        Validate(settings);
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!;
        Directory.CreateDirectory(directory);
        var temp = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { JsonSerializer.Serialize(stream, settings, Options); stream.Flush(true); }
            if (File.Exists(Path))
            {
                try { _ = Read(Path); File.Replace(temp, Path, Path + ".bak", true); }
                catch (Exception e) when (e is JsonException or ArgumentException)
                {
                    File.Copy(Path, Path + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), true);
                    File.Move(temp, Path, true);
                }
            }
            else File.Move(temp, Path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static bool Recoverable(Exception e) => e is IOException or JsonException or ArgumentException;
    private static Settings Read(string path)
    {
        using var stream = File.OpenRead(path);
        var settings = JsonSerializer.Deserialize<Settings>(stream) ?? throw new JsonException("Empty settings.");
        Validate(settings); return settings;
    }
    public static void Validate(Settings settings)
    {
        if (settings.Version != 1) throw new InvalidDataException($"Unsupported settings version {settings.Version}. File left unchanged.");
        if (settings.Layouts is null || settings.Layouts.Count == 0 || settings.Monitors is null || settings.PickerShortcut is null)
            throw new ArgumentException("Settings need at least one layout and a picker shortcut.");
        if (settings.SimpleGridIds is null) throw new ArgumentException("Invalid simple-grid preferences.");
        var ids = new HashSet<Guid>();
        var keys = new HashSet<Hotkey> { settings.PickerShortcut };
        CheckShortcut(settings.PickerShortcut);
        foreach (var layout in settings.Layouts)
        {
            if (layout is null) throw new ArgumentException("Invalid layout.");
            LayoutEngine.Validate(layout);
            if (!ids.Add(layout.Id)) throw new ArgumentException("Duplicate layout identity.");
            foreach (var zone in layout.Zones.Where(z => z.Shortcut is not null))
            {
                CheckShortcut(zone.Shortcut!);
                if (!keys.Add(zone.Shortcut!)) throw new ArgumentException("Shortcuts must be unique across layouts.");
            }
        }
        if (settings.Monitors.Any(m => m is null || string.IsNullOrWhiteSpace(m.Identity) || m.Orientation is not ("Portrait" or "Landscape")))
            throw new ArgumentException("Invalid monitor assignment.");
    }
    private static void CheckShortcut(Hotkey key)
    {
        if (key.Modifiers is 0 or > 15 || key.Key is < 1 or > 254)
            throw new ArgumentException("A shortcut needs a modifier and a valid key.");
    }
}
