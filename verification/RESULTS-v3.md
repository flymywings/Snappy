# Snappy 0.3.0 verification — 2026-10-25

Appearance-only refresh: charcoal theme, Segoe UI Variable/Segoe UI fallback,
capital Snappy, compact native-draggable caption, selected-app icon, themed monitor
selector, quiet gray cells, blue hover/selection, slim controls, and snapping-corner
branding for executable/window/tray. Core placement and settings schema unchanged.

## Evidence

- Core tests: 38 passed, zero failed or skipped.
- Release build: zero warnings or errors.
- Extracted self-contained 0.3.0 ZIP: exit code 0, 36 successful native checks,
  one skipped foreground-permission check; executable manifest remains asInvoker.
- Windows associated-icon extraction from the built EXE returns the new snapping
  corner; ICO directory verified at all nine listed resolutions.
- Windows 10 native harness: 36 successful checks, one explicitly skipped focus
  assertion because the execution tool cannot obtain foreground permission.
- Native checks cover caption dragging hit-test, interactive caption settings,
  nonactivation, popup behavior, human-readable dropdown labels, horizontal scroll
  direction and paging, editor region retention, stale-layout selection cancellation,
  16 px embedded tray icon decoding, and existing placement/monitor/persistence flows.
- Inspected actual WPF panel idle/selected and Advanced renders; icon inspected at
  16 and 256 px. Assets contain 16/20/24/32/40/48/64/128/256 px ICO frames and PNGs,
  with editable XAML/SVG and reproducible export tool.

## Review

Independent review identified three regressions, all fixed before packaging:
hover rendering must preserve Advanced selection data; horizontal scrollbars need
horizontal direction/paging; replacing a layout must clear old hover/drag state.
The native harness reproduces these conditions and passes on the corrected build.
The themed dropdown's selection label regression was also reproduced and fixed.

## Limits

Physical mouse/foreground interaction on arbitrary external apps, portrait/mixed-DPI
hardware, Windows 11, light-taskbar icon contrast, and hotplug remain manual checks.
The new caption/popup behavior was verified through Windows messages and WPF events;
this does not substitute for those hardware checks. No public release or signing
was performed. Existing source and portable archives are retained separately.
