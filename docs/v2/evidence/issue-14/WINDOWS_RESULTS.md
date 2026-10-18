# Issue #14 Windows build and run evidence

Date: 2026-10-18 (Asia/Shanghai)

This evidence is for the V2 WebView2 host preview only. It does not change or
replace the stable v0.1.7 client.

## Test environment

- OS: Windows 11 24H2 x64, build 26100.1742
- SDK: .NET SDK 10.0.402
- Target: `net10.0-windows`, `win-x64`
- WebView2 SDK: `Microsoft.Web.WebView2` 1.0.4258.31
- WebView2 Evergreen Runtime: 155.0.4283.62
- Browser state for these runs: isolated under the Issue #14 worktree
- Page data: 18 synthetic entries generated at startup; no user asset,
  thumbnail, cover, catalog, or `.local-data` file was read

## Commands and actual results

### Unit and build test

```powershell
dotnet test v2\windows\AssetLibrary.V2.sln `
  --configuration Release --no-restore --verbosity minimal
```

Result: **21 passed, 0 failed, 0 skipped**, with no compiler warning in the
final run.

The tests cover the public page-to-host seam: valid host information, strict
action allowlist, malformed/oversized requests, illegal IDs and payloads,
native handler failure responses, trusted message origin, and local-only
resource loading.

### Self-contained publish

```powershell
dotnet publish `
  v2\windows\AssetLibrary.Desktop\AssetLibrary.Desktop.csproj `
  --configuration Release --runtime win-x64 --self-contained true `
  --output .artifacts\publish\win-x64
```

Result: success. Local artifact:

`.artifacts\publish\win-x64`

- 280 files, 124,826,255 bytes
- `AssetLibrary.V2.exe` SHA256:
  `62311EC31ABE903CB5CEFDC4ECCBBCBD10839222DD70A1AF1BEA157E8781FF11`
- Local ZIP test artifact:
  `.artifacts\3D-Asset-Library-V2-Issue14-win-x64.zip`
  (50,379,378 bytes), SHA256
  `D636009289C5AA359FBB023417D3AE29BAE075F4489553374320D1C6300033B7`
- .NET runtime is self-contained. WebView2 uses the machine's shared Evergreen
  Runtime and shows an explicit startup error if it is unavailable.

### Real Windows process smoke test

The published `AssetLibrary.V2.exe` was started with `--smoke-test` and an
isolated `--user-data-dir`. It created a real WinForms window, initialized the
installed WebView2 Runtime, loaded the mapped local HTML, completed the
JavaScript-to-C# `getHostInfo` handshake, and exited with code **0**.

The same published executable then captured both required viewports and exited
with code **0**. The PNG files remain in the local test output and are uploaded
by the Windows Actions job rather than committed, in accordance with the V2
public PR filename safety policy:

- 1016x689 — SHA256
  `17BF13085FFD0E4DC9D1345A5380B462136B3C501FB13E26ACF5F86431865D70`
- 1920x1080 — SHA256
  `908FC1176526DFDD37AD4E2ABA695D69D2B5562AAB475E7DD21643011A830790`

## Security boundary verified in this slice

- The page is served only from the virtual HTTPS origin
  `https://assetlibrary.local/index.html`.
- Remote HTTP/HTTPS and `file://` resources are blocked.
- Browser popups, downloads, default script dialogs, host objects, and web
  permissions are disabled.
- Prototype export, import, and rescan controls are hidden until a later issue
  supplies real native implementations.
- The only native action is `getHostInfo`; the page cannot send a path or OS
  command through this bridge.

## Explicitly not tested or implemented

- C4D FileDrop, ordinary drop, or Shift merge (Issue #15)
- Real folder scanning, SQLite, thumbnails, categories, or user data migration
- Clipboard cover writes or dHash search through native services
- Velopack updates, installation, rollback, or stable release migration
- Windows 10 compatibility

The screenshots prove local HTML rendering and the native handshake only; they
do not prove real asset operations.
