# Approved visual refinement, Snappy 0.4

Keep compact panel dimensions and arrangement. Use 90% charcoal background with
opaque text/icons/controls/guides. Enable per-pixel transparency on the main panel
and keep popup menus opaque. Emphasize only halfway guides when the corresponding
row/column count is even: 2 DIP midpoints, 1 DIP ordinary lines. No hit-test or
snapping geometry changes and no settings-schema change.

Replace the outline icon with two solid interlocking tiles: near-white upper-left,
soft blue lower-right, stepped gap. Export matching SVG/XAML and 16–256 px PNG/ICO.
Verify renders against light/dark/busy backgrounds, native caption and input
behavior, icon sizes, existing tests, and extracted portable package. Archive
source and portable 0.4 builds separately from earlier releases.

Legibility choice: a 0.75 DIP neutral keyline around each filled tile keeps the
near-white shape visible against light shell backgrounds. The fills, stepped gap,
and simple solid silhouette remain unchanged; no gradient, lettering, or ornament.
