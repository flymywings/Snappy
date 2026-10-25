# Verification results — Snappy 0.1.0

Date: 2026-10-23 (Asia/Manila).

- .NET SDK: 10.0.402, installed only under the task's workspace tooling directory.
- Release build: successful, 0 warnings, 0 errors.
- Core suite: 30 passed, 0 failed, 0 skipped.
- Self-contained Windows x64 build/package: successful; includes runtime licenses.
- Portable executable native smoke: exit code 0 on Windows 10 22H2 (19045),
  with 14 successful checks and 1 explicitly skipped focus check.
- The portable app was launched directly, without using the workspace SDK host.
  The system installation has only .NET 6/8 runtimes, demonstrating that the
  bundled runtime is sufficient.
- Editor, picker, and overlay renders were inspected. Their PNGs and the native
  JSON reports are retained in this folder.

## Coverage

Core tests cover portrait halves with odd sizes, negative coordinates, scaled
gaps, nonoverlapping zones, uncovered drop areas, monitor/orientation resolution,
JSON round trips, backup recovery, unsupported schema protection, drag cancellation,
fast key transitions, DPI size changes, invisible frame compensation, destination
border correction, minimum sizes, failed placement, and staged hotkey transactions.

Native smoke checks cover real monitor discovery, top-half placement and visible
frame accuracy, minimum-size rejection, own-window filtering, maximize/restore,
hotkey registration and conflict, keyboard-hook availability, click-through and
non-activating overlay styles, preservation of editor fields on display refresh,
UI rendering, non-resizable windows, and a bounded unresponsive-window query.
The portable build rejected the unresponsive query in 164 ms.

The focus check is skipped because a process launched through the execution tool
did not receive foreground permission. No focus-lock bypass was added.

## Review

An independent read-only source review identified seven material issues. All were
addressed: title-bar movement classification across DPI changes; bounded native
queries and asynchronous movement; destination-frame remeasurement; latched key
transitions; editor draft preservation on display changes; hotkey staging through
persistence; and preservation of newer settings schemas during Save.

No minor findings were deferred. Explicit product exclusions remain unchanged.

## Implementation choices

- New source project under outputs/Snappy; no existing Git repository or branch
  was modified. Git initialization and public hosting were not performed.
- Implementation ran inline, followed by an independent source review.
- SDK and package downloads stayed in the workspace; no system-wide SDK install.
- Targeting Windows first uses WPF; future OS ports will need their own UI and
  window-control adapters while reusing the core.

## Remaining manual verification

The available host exposes one landscape display. Windows 11, physical portrait
displays, mixed-DPI transitions, actual drag interaction, user-triggered focus,
elevated foreign windows, monitor hotplug, and sign-in startup have not been
fully exercised. See CHECKLIST.md. These are release-validation limits, not claims
that the corresponding checks passed.

No public release, trademark clearance, deployment, or code signing was performed.
