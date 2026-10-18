# Multi-folder import delivery — 2026-10-19

## Delivered behavior

- Windows folder picker supports multiple selections through .NET's built-in `FolderBrowserDialog.Multiselect` / `SelectedPaths`. Repeated imports append instead of replacing the library.
- The trusted native scanner resolves selected paths; the page never supplies scan paths. The new `rescanLibrary` action accepts an empty payload and scans only stored, available roots.
- Records are deduplicated by the existing normalized full-path SHA256 ID. Reimporting a directory or overlapping parent/child roots retains one record per physical path. Identical names at different paths remain separate records; this is not content-based duplicate removal.
- Source roots persist across restart. Each root can filter all its contained assets, including overlapping roots, without duplicating cards in the complete library. Disconnected roots and their indexed records are retained.
- Cover matching priority: exact model basename; recognized preview/cover/thumb/render suffixes; then folder-name / generic preview names only in single-model folders. PNG/JPG/JPEG/WebP/BMP matching is case-insensitive. Arbitrary texture images are not guessed as covers.
- Existing available cover associations are preserved on scans. The custom-cover map remains authoritative; no original models, textures, source images or existing cache covers are overwritten. Missing covers can acquire a newly discovered match.
- Existing single-root `library.json` is read compatibly, and saved with additive root/membership metadata on the next explicit scan. A local data backup was made before that real-data scan.

## Validation

- .NET Release tests: **50 passed**. New tests cover append/repeat/overlap, same-name different-path records, persisted multiple roots, offline retention, deterministic image matching and manual/automatic cover preservation.
- Chromium/Edge UI tests: **24 passed**, including root navigation and rescan dispatch, existing layouts/themes, zoom, paste/search and drag-ID contracts.
- Actual Windows WebView2 multi-folder smoke: **exit 0**. Clicked imports with native test selections A, A+B, A again, then overlapping parent; retained 2 records/3 roots. Rescanned, checked root/search filtering, decoded matched previews, preserved custom cover and source hashes. The native test selection hook replaces only the folder dialog; it uses production import/merge/storage and page controls.
- Real existing user library: **exit 0** for read-only scan/merge, search and native locator-bridge verification; the two existing records/covers were retained. Captures verified actual dimensions 1016×689 and 1920×1080 (plus dark 1016×689); private images remain local.
- Existing original assets, V2 cache covers and legacy data: **44 files hash-compared unchanged** after real-data scan.
- Actual Windows UI/zoom smoke: **exit 0**. Background-window paint timing is checked by waiting for measured card width rather than an arbitrary 100 ms delay.
- Actual Windows clipboard/image-search smoke: full sequential run **exit 0**, including button/Ctrl+V saves, Top-1 match and invalid text clipboard rejection without cover changes. An earlier concurrent run hit a Windows clipboard-operation failure; the sequential run passed. This is not a claim that OS-wide clipboard contention cannot recur.
- Existing C4D drag implementation is unchanged; prior user-confirmed ordinary-open and Shift-merge acceptance remains applicable, not newly repeated during this task.

## Installed local delivery

- Desktop entry: existing `3D素材管理器.lnk`, launched successfully.
- EXE: `D:\Codex\3D-Asset-Library-V2-App\builds\20261019-031329-148\AssetLibrary.V2.exe`.
- Data backup: `D:\Codex\3D-Asset-Library-V2-App\data-backups\multi-import-20261019-031136-221` (excluding regenerable WebView2 browser profile).
- Previous build and shortcut backups retained. The old client may not understand multiple-root metadata; use the data backup if reverting the client.
- Self-contained ZIP: `.artifacts/multi-import-delivery/3D-Asset-Library-V2-Multi-Folder-win-x64.zip`.
- ZIP SHA256: `fe3933305426c064e1599d639c2ca31d6bdef6bb257277ee159237ccc0c40159`.
- Logs/screenshots/fixtures are isolated under `.artifacts/multi-import-delivery/` and are not tracked in Git.
- Formal v0.1.7 scripts/update manifests and the unrelated asset-organizer project were not changed.

Current scanning formats remain C4D, FBX and OBJ. Automatic preview rendering and content-based duplicate detection are not part of this change.
