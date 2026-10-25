# Snappy 0.3 appearance

User-approved direction: solid charcoal, clear contrast, compact Divvy-like utility.
Use capital Snappy, Segoe UI Variable with Segoe UI fallback, quiet gray grid lines,
soft blue only for interaction, and a restrained snapping-corner icon. Keep existing
window selection, cell snapping, monitor selection, and advanced tools intact.

Implementation: compact custom caption with native WindowChrome dragging; selected
app icon/name; themed monitor selector; main grid; one slim row/column strip;
inline status only when useful. No tagline or permanent instructional footer.
Snapping-corner source is a native vector DrawingImage plus matching SVG; raster
icons are exported deterministically at 16–256 px for tray, app, and executable.

Verify existing core suite and native interaction harness, including nonactivation,
caption movement, popup controls, hover/selection renders, and embedded icon sizes.
Inspect actual WPF renderings and extracted portable package before delivery.
