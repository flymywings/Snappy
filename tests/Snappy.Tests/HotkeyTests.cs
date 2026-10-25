using Snappy.Core;
using Xunit;

namespace Snappy.Tests;

public class HotkeyTests
{
    [Fact] public void ConflictKeepsPreviousBindingsAndRemovesPartiallyRegisteredKeys()
    {
        var platform = new TestKeys(); using var registry = new HotkeyRegistry(platform);
        Assert.True(registry.TryApply(new Dictionary<string, Hotkey> { ["picker"] = new(3, 32) }, out _));
        platform.Blocked = new(3, 65);
        Assert.False(registry.TryApply(new Dictionary<string, Hotkey> { ["picker"] = new(3, 32), ["zone1"] = new(3, 66), ["zone2"] = new(3, 65) }, out var error));
        Assert.NotNull(error);
        var binding = Assert.Single(platform.Keys);
        Assert.Equal(new Hotkey(3, 32), binding.Value); Assert.Equal("picker", registry.Command(binding.Key));
    }
    [Fact] public void SwappingCommandsReusesAlreadyRegisteredKeys()
    {
        var platform = new TestKeys(); using var registry = new HotkeyRegistry(platform);
        Assert.True(registry.TryApply(new Dictionary<string, Hotkey> { ["a"] = new(3, 65), ["b"] = new(3, 66) }, out _));
        Assert.True(registry.TryApply(new Dictionary<string, Hotkey> { ["b"] = new(3, 65), ["a"] = new(3, 66) }, out _));
        Assert.Equal("b", registry.Command(platform.Keys.Single(k => k.Value.Key == 65).Key));
    }
    [Fact] public void DuplicateRequestedKeysAreRejectedWithoutChangingExistingBindings()
    {
        var platform = new TestKeys(); using var registry = new HotkeyRegistry(platform);
        Assert.False(registry.TryApply(new Dictionary<string, Hotkey> { ["a"] = new(3, 65), ["b"] = new(3, 65) }, out _));
        Assert.Empty(platform.Keys);
    }
    private sealed class TestKeys : IHotkeyPlatform
    {
        public readonly Dictionary<int, Hotkey> Keys = [];
        public Hotkey? Blocked;
        public bool Register(int id, Hotkey shortcut)
        {
            if (shortcut == Blocked || Keys.ContainsValue(shortcut)) return false;
            Keys.Add(id, shortcut); return true;
        }
        public void Unregister(int id) => Keys.Remove(id);
    }
    [Fact] public void FailedPersistenceKeepsOldKeysReservedUntilRollbackCompletes()
    {
        var platform = new TestKeys(); using var registry = new HotkeyRegistry(platform);
        Assert.True(registry.TryApply(new Dictionary<string, Hotkey> { ["picker"] = new(3, 32) }, out _));
        Assert.True(registry.TryStage(new Dictionary<string, Hotkey> { ["picker"] = new(3, 65) }, out var change, out _));
        Assert.False(platform.Register(999, new(3, 32))); // Another app cannot claim the old key during the write.
        change!.Dispose(); // Disk/startup failure: abandon candidate registrations.
        var old = Assert.Single(platform.Keys); Assert.Equal(new Hotkey(3, 32), old.Value);
        Assert.Equal("picker", registry.Command(old.Key));
    }
    [Fact] public void SuccessfulPersistenceReleasesOldKeysOnlyOnCommit()
    {
        var platform = new TestKeys(); using var registry = new HotkeyRegistry(platform);
        Assert.True(registry.TryApply(new Dictionary<string, Hotkey> { ["picker"] = new(3, 32) }, out _));
        Assert.True(registry.TryStage(new Dictionary<string, Hotkey> { ["picker"] = new(3, 65) }, out var change, out _));
        Assert.Equal(2, platform.Keys.Count);
        change!.Commit(); change.Dispose();
        Assert.Equal(new Hotkey(3, 65), Assert.Single(platform.Keys).Value);
    }
}
