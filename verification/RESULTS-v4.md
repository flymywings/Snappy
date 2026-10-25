# Snappy 0.4.0 verification

Approved changes: fixed 90% charcoal backdrop, opaque foreground elements,
2 DIP even-count midpoint guides with 1 DIP ordinary guides, and a solid two-tile
icon with near-white/blue fills. Compact sizing, interaction, and schema unchanged.

## Evidence

- Core suite: 38 passing tests.
- Release build: zero warnings and errors.
- Extracted self-contained ZIP ran as version 0.4.0: exit code 0, 70 successful
  native checks, one skipped check. Executable manifest remains asInvoker, and
  associated-icon extraction confirms the tile mark is embedded in the EXE.
- Native Windows 10 22H2 smoke: 70 successful checks, one skipped foreground check.
- Exact rendered alpha confirms one 90% backdrop through panel and grid; caption
  glyphs, midpoint guide, and popup background retain opaque pixels.
- Main HWND has the native layered-composition flag. Caption hit-testing, settings
  button, nonactivation, popup opening, selection, and existing placement checks pass.
- Rendered guide checks cover 1×1, 1×2, 2×1, 6×6, 3×3, 3×4, and 4×3; midpoint strokes
  occupy two pixels at 96 DPI and ordinary guide strokes occupy one.
- Inspected light/dark/busy synthetic composites, 6×6 grid, and icon size proofs.
- SVG/XAML sources match; deterministic PNG/ICO exports cover 16–256 px. Fine neutral
  keylines preserve the near-white silhouette against a light shell background.

Independent review compared 0.4 with 0.3 and found no actionable regressions in
input hooks, geometry, alpha layering, guide parity, or icon export/resource mapping.

## Limitations

Tool-launched windows still cannot obtain foreground permission; that restoration
assertion is explicitly skipped. Native hit-test and focus-state checks pass, but
physical pointer use on arbitrary external apps, portrait/mixed-DPI hardware,
Windows 11, and hotplug remain manual checks. The background comparison PNGs are
controlled render composites, not desktop screenshots. No public release, signing,
system-wide SDK installation, or change to administrator requirements was made.
