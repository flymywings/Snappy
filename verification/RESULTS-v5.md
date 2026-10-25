# Snappy 0.5.0 verification

Approved icon integration only. The final generated artwork with cursor glow
removed is the source master; the app does not replace it with a new interpretation.

## Evidence

- Master file hash matches the approved source exactly:
  `33E67569D91167FAF551CDB4C738D42EB051427E8213B9CE98C336E1ACC0F7A4`.
- Export viewport: 209,89,996,932 from a 1466×1073 master; uniform aspect-preserving
  fit with transparent canvas padding. No master pixels or source file rewritten.
- ICO resolutions: 16,20,24,32,40,48,64,128,256; PNG payloads match exported files.
- Core tests: 38 passed. Release build: zero warnings/errors.
- Windows 10 native smoke: 70 successful checks; one foreground-permission check
  explicitly skipped. New header icon and 16 px tray icon render/decode successfully.
- Inspected approved master, 256 px export, 16 px export, and in-app panel render.
- Independent source/asset review found no actionable defects in resource paths,
  image/stream lifetimes, crop/aspect handling, ICO encoding, or bitmap caching.
- Extracted portable ZIP: Snappy.exe exited 0; 70 checks passed and one focus
  check was skipped. Product version is 0.5.0. The executable's extracted icon
  matches the replacement artwork; its manifest retains `asInvoker`.

## Limits and delivery

The existing hardware/focus limitations remain: physical external-app pointer and
foreground behavior, portrait/mixed-DPI hardware, Windows 11, and hotplug require
manual checks. No schema, snapping behavior, administrator requirement, startup
preference, or user configuration was changed. No GitHub repository or push was
created; the user requested GitHub work later. Previous archives remain available.
