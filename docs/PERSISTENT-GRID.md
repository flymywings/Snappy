# Persistent grid implementation

Approved plan: Open Snappy, configure rows/columns, select an external window,
click a cell to snap. Drag across cells for larger rectangles. Panel remains open
and normal clicks must not activate it. Advanced tools remain behind gear.

Implementation choices:

- Windows foreground events plus bounded periodic validity refresh retain the
  last eligible external HWND and ignore Snappy's own windows.
- Main panel uses nonactivating native styles and MA_NOACTIVATE, with mouse-only
  steppers; keyboard configuration remains available through Advanced.
- Grid generation is per-monitor/orientation and preserves shared fallback and
  custom layouts. Advanced edits explicitly relinquish generated-grid ownership.
- Settings schema stays version 1 with additive optional preferences; existing
  JSON loads with defaults for panel position, generated-grid IDs, and drag toggle.
- Main steppers are disabled during Advanced editing. Advanced saves merge the
  current panel position to avoid overwriting independently saved position changes.
- Relaunch uses existing mutex plus a versioned native message host. Older running
  binaries are not terminated; the user is told to exit their tray instance first.
- No Git repository, shared branch, deployment, system SDK installation, or public
  release was created. SDK/packages remain in workspace tooling directories.
