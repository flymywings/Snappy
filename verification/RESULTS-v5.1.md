# Snappy 0.5.1 verification

## Reproduction and fix

The placement guard compared a fresh monitor enumeration against cached work-area
bounds. A taskbar/work-area change could leave the selected monitor in the panel
with stale bounds, repeatedly rejecting grid clicks. The earlier native panel
tests bypassed this guard, so they did not catch the failure.

Factored that guard into the shared Windows placement-bounds operation. Before
the fix, a regression with stale cached bounds threw the exact exception:
`Display configuration changed. Try again.` Native self-test exited 1.

Placement now resolves the selected connected monitor by identity and orientation,
then computes bounds from its current work area and DPI. No replacement monitor
is silently chosen when the selected monitor disconnects or rotates. The panel
refreshes on the unavailable-monitor failure.

Microsoft documents the work area separately from full monitor bounds and its
system-parameter notifications:
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-monitorinfo
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa

## Verified

- Core tests: 38 passed.
- Release build: zero warnings and errors.
- Extracted self-contained 0.5.1 ZIP: exit 0; 76 native checks passed, one
  foreground-permission check explicitly skipped.
- Stale work area refreshed before placement; current DPI used for gaps.
- Negative monitor coordinates retained; disconnected and rotated monitors rejected.
- Grid click using stale cached monitor bounds placed a native test window into
  the current expected rectangle. The grid test now uses the production bounds path.
- Independent code review: no actionable findings.

These tests reproduce the stale-cache failure mechanism. The user's physical
monitor setup has not been independently reproduced. Physical portrait/mixed-DPI,
Windows 11, hotplug and external-app pointer/focus checks remain manual.
