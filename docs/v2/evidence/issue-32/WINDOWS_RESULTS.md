# Issue #32 Windows verification

Verified on a real Windows host on 2026-10-18.

## Environment

- Windows kernel: 10.0.26100.0 (Windows 11)
- Architecture: x64
- .NET SDK: 10.0.402
- Microsoft Edge WebView2 Runtime: 155.0.4283.62
- Build: Release, `win-x64`, self-contained

## Automated results

- Browser UI contract: 15 passed, 0 failed.
- .NET tests: 23 passed, 0 failed.
- `dotnet format --verify-no-changes`: passed.
- Published EXE WebView2 handshake: passed.
- Published EXE interactions: search, category, light/dark theme, grid/list/board,
  card selection, keyboard selection/navigation, and three independent scrolling
  regions passed.
- ZIP was extracted into a clean test directory; handshake, interactions, and a
  normal visible launch all passed from the extracted copy.

## Screenshot evidence

The application validates PNG IHDR dimensions before reporting capture success.
CI also checks both baseline and result images and calculates a pixel-change
signal instead of trusting filenames.

| File | Actual pixels | SHA256 |
| --- | ---: | --- |
| `webview2-1016x689.png` | 1016×689 | `1CC23DCDCCB237BCA25AB110ABEC96B69393569536670F800AABEAB223553D29` |
| `webview2-1920x1080.png` | 1920×1080 | `17358029548B0B09609C8B32D52B27BD23298674B407C105A03EF2B2947DB2DA` |
| `webview2-dark-1016x689.png` | 1016×689 | `9C9B68D87BD35D991F0E9E64C2C8CF043F92EA94822B4A6AE0651E97015F26CD` |

Compared with the `v2-dev` baseline, 43.95% of pixels changed at 1016×689 and
59.35% changed at 1920×1080 using the checked threshold.

## Local delivery artifact

- ZIP: `.artifacts/issue-32-final/3D-Asset-Library-V2-Issue32-win-x64.zip`
- ZIP size: 50,389,453 bytes
- ZIP SHA256: `E7F833DBCD3B52E59BBBF4D02181C3771343D4866D51F0C7FCD8E1EF00E8766A`
- EXE SHA256: `0B947DDB61A18CEE82C55116905F1F1FA88FEB8A65DD0E4AE10E920F7C4CDA66`
- Application DLL SHA256: `5D471434956CFF19666C8D333481048C6517439E395DA80F8761B5F0C974B423`

The pull-request workflow produces the corresponding downloadable GitHub
Actions artifact. Generated ZIPs and screenshots are intentionally not
committed to the repository.

## Scope and limitations

This build uses clearly labelled synthetic assets. It does not read or mutate
the stable client's indexes, covers, settings, or real model/texture files.
Unsupported native actions remain disabled and do not show fake success.

Scanning, SQLite, cover paste, image search, Explorer location, updater
integration, and real asset data are not connected in this V2 slice. C4D
FileDrop/Shift behavior remains Issue #15 work and was not claimed or tested as
complete here. Stable v0.1.7 and its update manifest are outside this change.
