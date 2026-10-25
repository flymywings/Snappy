namespace Snappy.Core;

public sealed class DragSession
{
    public bool Active { get; private set; }
    public bool Cancelled { get; private set; }
    private bool activated;
    public void ObserveSizeChange(bool changed, bool confirmedTitlebarMove)
    {
        // A confirmed title-bar move can change pixel size during WM_DPICHANGED.
        if (changed && !confirmedTitlebarMove) Cancel();
    }
    public void KeyTransition(int virtualKey, bool down)
    {
        if (virtualKey == 0x1B && down) Update(true, true);
        else if (virtualKey is 0x10 or 0xA0 or 0xA1) Update(down, false);
    }
    public void Begin(bool isMove) { Active = isMove; Cancelled = !isMove; activated = false; }
    public void Update(bool shift, bool escape)
    {
        if (!Active || Cancelled) return;
        if (escape || activated && !shift) { Cancelled = true; return; }
        if (shift) activated = true;
    }
    public bool End(bool shift, bool escape, bool hasZone)
    {
        Update(shift, escape);
        var snap = Active && !Cancelled && activated && shift && !escape && hasZone;
        Active = false; return snap;
    }
    public void Cancel() { Cancelled = true; Active = false; }
}
