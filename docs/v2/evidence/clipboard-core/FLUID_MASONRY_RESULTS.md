# Fluid, natural-ratio masonry — 2026-10-19

## User-visible correction

The user rejected the fixed 4:3 project-reference frames and unused right-hand space in the previous large-library delivery. This presentation-only correction restores the approved natural-image waterfall direction, not a new visual design.

- In board mode, both model covers and project references use their decoded image aspect ratio. Wide images become shorter and portrait images become taller; images are not cropped into a fixed frame.
- The existing shortest-column placement remains. Complete columns share available width with 14 px gaps. Ctrl+wheel controls a saved target density, so exact column widths adapt to the window and column count. Adjacent wheel ticks can retain the same column count.
- Sparse results keep compact cards instead of stretching one or two records across the whole view. Missing-preview placeholders remain compact. Grid/list presentation and the inspector's fixed preview frame are unchanged.
- Reference badges, the 80-record page limit, global search/filtering, existing cover precedence and native file dragging are preserved.

## Evidence

Before the fix, the new real-catalog UI regressions failed: a 400×1000 reference image was displayed in a 4:3 frame instead of 0.4 aspect ratio, and the dense layout left 182 px unused at its right edge. After the fix:

- Edge UI suite: **27 passed**. Includes portrait/banner reference proportions, dense fill at 820×600, 1016×689 and 1920×1080, even gaps, no card overlap, sparse results, Ctrl+wheel limits/persistence, clipboard/image-search bridge contracts and drag requests.
- .NET Release tests: **51 passed**.
- Windows x64 Release self-contained publish: successful.
- Actual published EXE / WebView2 UI smoke: **exit 0**. Search, categories, theme, layout, card selection, scrolling and saved Ctrl+wheel target verified; page and sidebar scale unchanged.
- Actual published EXE with the existing **1,034-record** library: **exit 0**. Pagination/global search/preview filters still pass: 11 model-cover records, 929 project-reference records, 94 without previews. Shared references are not unique model renders.
- Real WebView2 captures: actual PNG dimensions checked at **1016×689**, **1920×1080**, and dark **1016×689**. The two light captures were visually inspected. Private captures are not committed or uploaded.
- SHA256 of all **7 non-browser-profile files** in the existing data directory stayed unchanged, including library index, cover associations and cached covers. No source-file writes were added or performed. The earlier model-metadata baseline was not re-run; original model contents were not exhaustively rehashed in this layout-only task.

## Local delivery

- Installed and opened through the existing desktop shortcut: `D:\Codex\3D-Asset-Library-V2-App\builds\20261019-033203-253\AssetLibrary.V2.exe`.
- Installation verified all 282 published files and backed up the previous shortcut. Prior builds remain available.
- Build/test output and private captures: local `.artifacts/fluid-masonry-delivery/`.
- Real C4D drag/open/Shift-merge was user-confirmed in earlier work, not repeated or newly certified here. The native drag implementation was not changed. This task does not alter scanning, SQLite, update manifests or the formal v0.1.7 client.

The remaining acceptance item is the user's visual preference in their normal working window. Automated geometry checks and real captures are not a substitute for that confirmation.
