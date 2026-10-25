# Release verification

Automated evidence: `dotnet test`, Release build, native `--self-test` report,
and render previews. Native checks use only windows created by the test process.

The available host is Windows 10 22H2, one 1920×1080 display. Do not treat
synthetic portrait geometry tests as physical portrait-monitor verification.

Before calling the release fully validated, check on Windows 10 and Windows 11:

- [ ] Open Snappy, configure columns/rows, select an ordinary browser/editor, click a cell; target retains focus and panel stays open.
- [ ] Select another app and snap it without reopening Snappy or pressing a hotkey.
- [ ] Close panel, then reopen app/tray/hotkey to reveal existing instance.
- [ ] Grid steppers save immediately; a failed save keeps the last working grid and shows an inline error.
- [ ] Same saved zone through picker, hotkey, and Shift-drag produces equal visible bounds (±1 physical pixel).
- [ ] Actual portrait display: top/bottom and three-row presets fill taskbar-free work area.
- [ ] Different scales (100%, 125%, 150%) and monitor positions left/above primary.
- [ ] Cross-monitor drag, including maximized-window drag and different monitor DPI.
- [ ] Esc and Shift release cancel snapping, including fast taps.
- [ ] Resizing through every border/corner never snaps.
- [ ] Uncovered regions and configured gaps never become drop targets.
- [ ] Monitor disconnect/rotation during drag clears overlay; reconnect restores assignments.
- [ ] Elevated windows show an explanatory failure and stay in place.
- [ ] Shortcut conflict preserves old bindings; changing the conflicting key succeeds.
- [ ] Save, restart, and recover damaged settings; future schema file remains untouched.
- [ ] Pause disables picker, shortcuts, and drag snapping; resume restores them.
- [ ] Sign-in startup enabled/disabled works from the permanent portable folder.

No public release, hosting deployment, trademark clearance, or code signing was performed.
