# Native drag payload and responsive masonry — 2026-10-19

## Scope and cause

- Desktop multicolumn CSS stacked two results in the first column; it did not place cards in shortest available columns or adapt the column count to the result count.
- The page had no native model drag handler. Cover images remained browser-draggable, so dragging a cover could deliver an image/URL rather than the indexed model file. The user's C4D error itself has not yet been independently reproduced with a physical cross-window drag.
- Reused the stable client's Windows `FileDrop` approach. No new framework, updater, version number, original model changes, or release-manifest changes.

## Implemented

- Responsive shortest-column masonry, recalculated on width/image changes. Sparse results share available columns; real cover aspect ratios are retained and the inspector uses contain instead of crop.
- Browser image drag disabled. A physical left-button gesture beyond 8 px requests `beginAssetDrag` with only the asset ID.
- Native host resolves the session's real model path, verifies existence, builds a Windows FileDrop payload and enters OLE drag after returning from the WebView2 message callback. A physical left-button check prevents non-gesture requests.
- Drop cancellation is not reported as successful C4D import. Shift state remains supplied by Windows/the target application; it is not simulated by the page.

## Actual local results

- Regression tests first reproduced both first-column stacking and browser-draggable covers, then passed with the implementation.
- .NET Release tests: **47 passed**. Covers ID-only bridge validation, missing/unknown model rejection, actual FileDrop contents and cancellation results.
- Chromium/Edge UI tests: **22 passed**. Includes 820×600, 1016×689, 1920×1080, two-card horizontal placement, image proportions, resize/non-overlap, mouse threshold, clipboard/search and 1,000 synthetic records.
- Windows x64 self-contained publish: passed.
- Real WebView2 UI interaction smoke: exit 0.
- Real WebView2 cover feature smoke: exit 0. Clipboard button and Ctrl+V save, image Top-1 ranking, text-clipboard rejection, source hashes preserved.
- Restart smoke: exit 0, two records/covers restored and images decoded.
- Actual read-only real-directory scan/search/native locator-bridge test: exit 0. This is a locator bridge test, not a claim of manual Explorer UI inspection.
- EXE captures checked by PNG dimensions: 1016×689, 1920×1080 and dark 1016×689. Synthetic captures may be distributed as artifacts; real-library captures stay local/private.
- Existing originals, legacy data and V2 covers were hash-compared before/after, unchanged.
- Installed in a new build directory and started via the existing desktop shortcut. Previous shortcut/build and stable fallback retained.

## Explicit outstanding acceptance

**C4D ordinary drag and Shift-merge are NOT signed off.** C4D 2026.4 was launched to a blank scene. The available desktop automation rejected an out-of-window drag endpoint. An in-window synthesized drag reached the native physical-button guard after button release and correctly showed the hold-button error; it does not prove an OLE transfer. The user has been asked to perform the actual cross-window gesture. Do not close Issue #15 or claim three ordinary/three Shift imports passed on this evidence.

## Local delivery

- Installed EXE: `D:\Codex\3D-Asset-Library-V2-App\builds\20261019-025843-113\AssetLibrary.V2.exe`.
- Shortcut backup: `D:\Codex\3D-Asset-Library-V2-App\backups\20261019-025843-113`.
- ZIP under this worktree: `.artifacts/drag-masonry-delivery/3D-Asset-Library-V2-Drag-Masonry-win-x64.zip`.
- ZIP SHA256: `733cbbe227b3f5d496c7cbbcb18962446974c25b12e51147b122108035859d53`.
- Test logs and captures: `.artifacts/drag-masonry-delivery/` (ignored, not source-controlled).
- Kept v0.1.7 and its public update manifest unchanged. No automatic production rollout.
