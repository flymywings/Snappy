# Approved icon integration — 0.5.0

User approved the soft window-and-blue-pane design with app-like content and gray
window controls, then requested a narrower blue pane from the right and removal
of the cursor's glow. Apply that resulting artwork to the app. GitHub publishing
was explicitly deferred to later work.

The authoritative image is copied without modification to
`src/Snappy.Windows/Assets/Snappy-source.png` (SHA-256
`33E67569D91167FAF551CDB4C738D42EB051427E8213B9CE98C336E1ACC0F7A4`).
Do not recreate it as a different vector drawing. IconTool supports PNG input,
finds the main connected artwork for margin normalization, and renders nine square
outputs with uniform scaling and transparency. The source file remains untouched.

The executable and tray use the generated ICO; header/window brand images use the
embedded 256 px PNG. All derive from the same master. Historical SVG/XAML assets
remain archived outside the Windows project. Source/settings/placement behavior
is unchanged apart from cached bitmap loading for the brand image.
