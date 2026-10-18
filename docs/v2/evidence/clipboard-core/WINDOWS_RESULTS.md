# Persistent clipboard covers and image search — Windows delivery

Date: 2026-10-19. User priority: make the existing V2 usable, enable the two missing core capabilities, and switch the local desktop entry rather than leave a functionally incomplete preview as the only new UI.

## Changes

- Native STA clipboard image access, button and Ctrl+V cover save. Text inputs retain normal paste behavior.
- Dedicated image-search button uses the stable client's 9x8 dHash algorithm and Hamming-distance ranking. This is basic cover similarity, not semantic AI identification.
- Atomic cover map and saved catalog outside version folders. Custom covers survive restart and rescan; no source model or texture writes.
- Read/copy the legacy index and cover mapping into independent V2 data. Keep a local legacy-ID-to-V2-ID map and metadata backup; never write into old data. Existing path-derived V2 IDs are retained in this incremental Alpha (no SQLite/schema redesign).
- Strict ID-only/empty-payload native actions; image URLs accept only a fixed-length hex revision token to refresh changed covers. No remote authentication or update URL changes.
- Local install script creates a new version directory, checks every copied file's SHA256, backs up the desktop shortcut and keeps the original EXE available. This is local desktop replacement, **not** proof of GitHub OTA migration.

## Actual verification

- .NET tests: 45 passed. UI tests: 18 passed using installed Edge.
- Self-contained Windows x64 Release publish completed; no SDK is required to launch the result.
- Actual published EXE/WebView2: cover button saved an image, Ctrl+V handler saved a distinct second image, same-image search ranked the corresponding cover first, text clipboard returned an explicit error without changing covers. Source fixture hashes unchanged.
- A separate second EXE process restored the two fixture records, covers, decoded images and enabled controls.
- Legacy real-data import: two records and both covers displayed/decoded in real WebView2; six legacy data files and 36 authorized source files were hash-identical before and after.
- Screenshots captured by the actual WebView2 controller and validated from PNG dimensions: 1016×689, 1920×1080 and dark 1016×689. Public evidence uses synthetic fixture files/images only. Private cover images and local catalogs are not committed.
- One repeated smoke run timed out on the second cover shortcut. Subsequent fresh runs passed; the failure was not hidden. Clipboard timing/contention was not conclusively established as its cause. Added bounded STA clipboard retry and diagnostic state to make transient contention tolerable and future failures actionable; further user interaction remains useful.

## Boundaries / remaining work

- These two buttons are implemented, not merely visually enabled. Local V2 is still not full v0.1.7 parity: C4D FileDrop/Shift (#15), editing real virtual classifications and the production OTA migration remain incomplete/unverified.
- Existing scanner still scans C4D/FBX/OBJ. Legacy imported records preserve other extensions, but broad-format rescan parity is not claimed.
- No public stable manifest changed, no public stable merge/release, no automatic rollback claim. The unfinished isolated updater is preserved on `codex/v2-beta-update` (checkpoint `f70d9bd`). Its A→B install succeeded but B→C failed acceptance and restored B.
- Original stable EXE/data and all other worktrees are preserved. New local data is under `D:\Codex\3D-Asset-Library-V2-Data`; private originals remain at their existing locations.

## Local handoff

- Desktop shortcut switched and launched through the shortcut; actual window process ID at delivery: 75792. Installation copied/verified 282 files into a fresh build directory. Original shortcut backed up; `3D素材管理器-旧版备用` retained on the desktop.
- Installed EXE independently launched and captured the restored real library at all three required dimensions (exit 0). Those private screenshots remain outside Git.
- Re-ran final published EXE cover workflow and a separate restart process after adding clipboard retry: both exit 0. Retry mitigates contention, but the earlier timeout's exact cause remains unproven.
- Local runnable ZIP: `.artifacts/clipboard-delivery/3D-Asset-Library-V2-Covers-Search-win-x64.zip`.
- ZIP SHA256: `2eadc649265c725c80050ef3cf0783057a2011c50ae1c6969f58adb9e96b796c`.
- Public screenshots in this directory show only synthetic gradient covers, never private commercial work.
