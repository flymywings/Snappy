# Snappy 0.2.0 verification — 2026-10-24

Implemented the approved persistent-grid plan. Startup opens a compact panel,
grid changes autosave, selected external windows are tracked, and cells snap
without closing the panel. Advanced functionality remains available through gear.

## Evidence

- Core tests: 38 passed, zero failed/skipped.
- Release build: zero warnings and errors.
- Native smoke: Windows 10 22H2 (19045), one landscape display.
- Native report: 26 successful checks and one explicitly skipped focus check.
- Panel, Advanced editor, and overlay renderings were inspected.
- Portable package includes .NET runtime, licenses, usage guide, and checksum.
- Extracted 0.2.0 portable executable ran native smoke successfully (exit code 0),
  with 26 successful checks and one explicitly skipped foreground-permission check.
- Embedded executable manifest remains `asInvoker` with `uiAccess=false`.

New native checks exercise the production grid-selection handlers with logical
coordinates: Window A single-cell placement, Window B multi-cell placement,
persistent visibility, paused/missing-target rejection, row stepper updates,
failed-save error with unchanged grid, hide/reveal, mouse-activation rejection,
same-window monitor-geometry refresh using a synthetic monitor snapshot, and
cross-process-compatible reveal messages through a named native host.

Existing checks cover real placement/frame accuracy, minimum sizes, maximized
restore, native hotkeys/conflicts, keyboard-hook installation, click-through
overlays, Advanced draft retention, and bounded unresponsive-window queries.

## Review and regressions

Independent review found three issues, reproduced and fixed before packaging:

- Moving the same target between monitors must refresh panel grid geometry.
- An explicitly assigned fallback layout can still serve unassigned monitors;
  simple-grid changes must preserve it rather than replacing it globally.
- Advanced-customized grids must lose generated-layout ownership so later simple
  grid changes preserve their names, zones, and identities.

Atomic save/hotkey transactions, custom-layout preservation, panel-position
persistence, selection exclusions, and old settings compatibility are covered by
the core tests. No prior source or package archive was overwritten; this is a
separate 0.2.0 release.

## Limits

Native smoke cannot obtain foreground permission for its tool-launched test
window; that focus-restoration assertion is explicitly skipped. The panel's
nonactivation style/message behavior is checked, but the tests do not simulate
physical mouse clicks on user applications. Full external foreground interaction,
physical portrait/mixed-DPI displays, Windows 11, hotplug, and sign-in startup
still need manual verification. No public release or signing was performed.
