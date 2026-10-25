using Snappy.Core;
using Xunit;

namespace Snappy.Tests;

public class SettingsTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SnappyTests", Guid.NewGuid().ToString());
    [Fact] public void SaveAndReloadPreservesZoneIdsHotkeysAndDisconnectedMonitors()
    {
        var store = new SettingsStore(System.IO.Path.Combine(directory, "settings.json"));
        var settings = Layouts.Defaults();
        settings.Layouts[0].Zones[0] = settings.Layouts[0].Zones[0] with { Shortcut = new(3, 0x31) };
        settings.Monitors.Add(new("absent", "Portrait", settings.Layouts[0].Id));
        store.Save(settings);
        var loaded = store.Load();
        Assert.Equal(settings.Layouts[0].Zones[0], loaded.Layouts[0].Zones[0]);
        Assert.Equal("absent", Assert.Single(loaded.Monitors).Identity);
    }
    [Fact] public void CorruptionRecoversBackupAndDoesNotOverwriteItOnNextSave()
    {
        var store = new SettingsStore(System.IO.Path.Combine(directory, "settings.json"));
        var settings = Layouts.Defaults();
        store.Save(settings);
        store.Save(settings);
        File.WriteAllText(store.Path, "bad json");
        var recovered = store.Load();
        Assert.Equal(settings.Layouts[0].Id, recovered.Layouts[0].Id);
        Assert.NotNull(store.RecoveryMessage);
        store.Save(recovered);
        File.WriteAllText(store.Path, "bad again");
        Assert.Equal(settings.Layouts[0].Id, store.Load().Layouts[0].Id);
    }
    [Fact] public void UnsupportedVersionIsReportedWithoutReplacingUserData()
    {
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, "settings.json");
        File.WriteAllText(path, "{\"Version\":999}");
        Assert.Throws<InvalidDataException>(() => new SettingsStore(path).Load());
        Assert.Contains("999", File.ReadAllText(path));
    }
    [Fact] public void SaveNeverOverwritesNewerSettingsWrittenWhileAppIsRunning()
    {
        var store = new SettingsStore(System.IO.Path.Combine(directory, "settings.json"));
        var settings = Layouts.Defaults(); store.Save(settings);
        File.WriteAllText(store.Path, "{\"Version\":999}");
        Assert.Throws<InvalidDataException>(() => store.Save(settings));
        Assert.Equal("{\"Version\":999}", File.ReadAllText(store.Path));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
