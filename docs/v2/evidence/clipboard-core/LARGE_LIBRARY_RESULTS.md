# Large-library browsing and project reference previews — 2026-10-19

## Observed problem

The user's real catalog contained 1,034 records (971 C4D, 57 FBX, 6 OBJ), with only 11 records having existing readable model covers. Missing-cover records spanned 471 folders; 411 of those folders had no sibling image. Random PNG names and texture thumbnails could not safely be treated as matching renders. Long folder text wrapped on every card, while all records were rendered into the same material wall.

## Delivered changes

- Real catalogs render at most 80 cards per page. Search, category/root filters, image-search ranking and preview filters operate over the full in-memory catalog before pagination.
- Added available-preview / missing-preview filters and counts. Titles are limited to two lines; the folder label uses one shortened line with the complete relative path as a tooltip and in the existing inspector.
- Missing-preview cards use a compact placeholder; portrait project reference images fit inside a compact 4:3 frame without cropping. Dedicated model covers retain their aspect ratio. Existing Ctrl-wheel sizing and native model FileDrop remain.
- An explicit `00_封面Cover`, Cover/Preview or equivalent folder can supply a **project reference image**. The closest qualifying ancestor within the asset's selected source root is used. Named cover images or a single image in such a folder qualify; arbitrary sibling textures/random images are not selected.
- A reference is stored separately from `CoverPath` / `HasCover`, is labeled **项目参考图**, and is described in the inspector as a reference rather than that model's render. References never replace an existing readable model cover. Selection is derived on load/scan without rewriting original files or existing cover associations.
- Project reference images also participate in the existing dHash comparison, with reference labeling retained in search results. Multiple models sharing one project reference can consequently receive equal scores; this does not establish that their geometry matches.

## Verified results

- Red-capable UI regressions first failed on unbounded cards (120 vs expected 80) and tall reference images (height/width 2.5 vs compact limit), then passed after implementation.
- .NET Release tests: **51 passed**. Includes separation/persistence/root boundaries for reference previews, model-cover precedence, reference-image similarity and unchanged source-image hashes.
- Complete Edge UI suite: **26 passed**. Targeted pagination/reference/image-search tests were rerun after the final search integration: **3 passed**.
- Actual Windows WebView2 tested the user's complete 1,034-record catalog: **exit 0**. Verified 80-card pages, next-page navigation, a global search target beyond the first page, and correct preview filter counts.
- Real catalog outcome: **11 existing model-cover records, 929 project-reference records, 94 without previews**. Reference records can share project images; these are not 929 unique rendered model thumbnails.
- Actual EXE captures validated at **1016×689 / 1920×1080 / dark 1016×689**. Real images remain only in local private artifacts.
- Actual Windows UI/zoom smoke: **exit 0**. Its helper now waits for initial layout and respects the saved zoom limit before measuring changes; a fixed early paint measurement had failed while the WebView2 window was occluded.
- Actual Windows cover-paste and image-search regression smoke: **exit 0**, including Ctrl+V/button cover saving, Top-1 comparison, invalid clipboard rejection and unchanged fixture originals.
- All **1,034 original model size/modification metadata records unchanged**; existing referenced/cache cover-file SHA256 values unchanged (10 distinct files in the snapshot); the user's index SHA256 also unchanged. Whole large model contents were not all rehashed.

## Local delivery

- Installed and started through the existing desktop shortcut: `D:\Codex\3D-Asset-Library-V2-App\builds\20261019-032543-550\AssetLibrary.V2.exe`.
- Data backup: `D:\Codex\3D-Asset-Library-V2-App\data-backups\large-library-20261019-032354-627` (browser profile excluded).
- ZIP: `.artifacts/large-library-delivery/3D-Asset-Library-V2-Large-Library-win-x64.zip`.
- ZIP SHA256: `b574df42cd687bf2eaac3d5d74257afd14905d2a91c26f5c2f55919d53ec9d39`.
- Logs and private captures under `.artifacts/large-library-delivery/`; no original images/models uploaded or committed.
- Previous builds retained. Formal v0.1.7/update manifests and the unrelated organizer project unchanged.

These previews do not extract/render every C4D scene. The remaining 94 records still need an explicitly associated image or future preview-generation support. The prior user-confirmed C4D ordinary-open/Shift-merge implementation was not changed or re-certified during this task.
