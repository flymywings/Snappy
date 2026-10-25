namespace Snappy.Core;

public interface IHotkeyPlatform
{
    bool Register(int id, Hotkey shortcut);
    void Unregister(int id);
}
public sealed class HotkeyRegistry(IHotkeyPlatform platform) : IDisposable
{
    private Dictionary<int, (string Command, Hotkey Key)> active = [];
    private int nextId = 1;
    private bool staging;
    public bool TryApply(IReadOnlyDictionary<string, Hotkey> requested, out string? error)
    {
        if (!TryStage(requested, out var change, out error)) return false;
        using (change) change!.Commit();
        return true;
    }
    public bool TryStage(IReadOnlyDictionary<string, Hotkey> requested, out HotkeyChange? change, out string? error)
    {
        error = null; change = null;
        if (staging) { error = "Another shortcut change is in progress."; return false; }
        if (requested.Values.Distinct().Count() != requested.Count)
        { error = "Each command needs a different shortcut."; return false; }
        var replacement = new Dictionary<int, (string Command, Hotkey Key)>();
        var added = new List<int>();
        foreach (var (command, key) in requested)
        {
            var existing = active.FirstOrDefault(pair => pair.Value.Key == key);
            if (existing.Key != 0) { replacement.Add(existing.Key, (command, key)); continue; }
            // Register new keys first: a failed edit never releases the old bindings.
            var id = nextId++;
            if (id > 0xBFFF || !platform.Register(id, key))
            {
                foreach (var registered in added) platform.Unregister(registered);
                error = $"Shortcut for '{command}' is unavailable. Previous shortcuts kept.";
                return false;
            }
            replacement.Add(id, (command, key)); added.Add(id);
        }
        staging = true;
        change = new(() =>
        {
            foreach (var id in active.Keys.Except(replacement.Keys)) platform.Unregister(id);
            active = replacement; staging = false;
        }, () =>
        {
            foreach (var id in added) platform.Unregister(id);
            staging = false;
        });
        return true;
    }
    public string? Command(int id) => active.TryGetValue(id, out var binding) ? binding.Command : null;
    public void Dispose()
    {
        foreach (var id in active.Keys) platform.Unregister(id);
        active.Clear();
    }
}
public sealed class HotkeyChange(Action commit, Action rollback) : IDisposable
{
    private bool finished;
    public void Commit() { if (finished) throw new InvalidOperationException("Change already completed."); commit(); finished = true; }
    public void Dispose() { if (!finished) { rollback(); finished = true; } }
}
