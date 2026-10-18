# V2 Windows WebView2 client (Issues #14, #30, #32, and #33)

This folder contains the Windows 11 x64 .NET 10 + WebView2 V2 Alpha client.
It remains isolated from the stable v0.1.7 client and update manifest.

## What this slice proves

- .NET 10 WinForms can load the repository's trusted local HTML in WebView2.
- The page can call a strict C# message bridge with `{id, action, payload}`.
- The allowed page actions are `getHostInfo`, native folder selection/scan,
  and locate-by-asset-ID; unknown actions, page-supplied paths, commands,
  malformed JSON, oversized messages, untrusted origins, and remote resources
  are rejected.
- A real Windows process can perform a WebView2 handshake and run an
  interaction smoke test covering search, category, theme, layout, and card
  selection, plus the main, navigation, and details scrolling regions.
- Screenshot capture verifies the PNG IHDR dimensions are exactly 1016x689
  and 1920x1080 before reporting success, and includes a 1016x689 dark-mode
  evidence image.
- Browser-prototype controls without a native implementation are visibly
  disabled and the interface states that it is using synthetic data.
- The user can choose one real library root with the native Windows folder
  picker. C# recursively scans `.c4d`, `.fbx`, and `.obj` files without
  accepting a path from page JavaScript or modifying source files.
- Same-directory, same-name PNG/JPG/JPEG/WebP/BMP covers are served through a
  native ID whitelist. Missing covers use an explicit placeholder.
  Covers use a separate, unmapped HTTPS origin so WebView2 can intercept each
  request instead of bypassing the handler through static folder mapping.
- Scan progress reports folders, models, and read errors. Folder-scan requests
  remain pending until native completion instead of expiring after five minutes.
- Real filenames, folders, extension filters, search, details, and Explorer
  locate are connected. Locate accepts only a native-issued 64-character
  asset ID; the page cannot submit an arbitrary path.

This slice does **not** implement SQLite persistence, custom cover paste,
image similarity search, C4D FileDrop/Shift, updating, or migration. In
particular, Issue #15 is still open and C4D drag-and-drop must not be reported
as available.

## Requirements

- Windows 11 x64
- .NET 10 SDK to build
- Microsoft Edge WebView2 Evergreen Runtime to run

The published artifact is self-contained for .NET. It intentionally uses the
shared Evergreen WebView2 Runtime because Microsoft recommends Evergreen for
normal desktop apps and the Fixed Version runtime adds more than 250 MB.

## Build and test

Run from this directory:

```powershell
dotnet restore AssetLibrary.V2.sln
dotnet test AssetLibrary.V2.sln --configuration Release --no-restore
dotnet publish AssetLibrary.Desktop\AssetLibrary.Desktop.csproj `
  --configuration Release --runtime win-x64 --self-contained true `
  --output .artifacts\publish\win-x64
```

Smoke-test the published process without touching stable data:

```powershell
Start-Process .artifacts\publish\win-x64\AssetLibrary.V2.exe `
  -ArgumentList @(
    '--smoke-test',
    '--user-data-dir',
    (Resolve-Path .artifacts).Path + '\webview2-user-data'
  ) -Wait -PassThru
```

Run the real read-only library integration check against a user-authorized
test directory. The test verifies that native and rendered item counts agree,
search works, matched cover images actually decode, and clicking the locate
button reaches the trusted native asset resolver. In this automated mode the
final Explorer launch is replaced by a recorder to avoid opening shell windows
in CI; actual Explorer file selection remains a separate manual acceptance
check. Callers must independently
compare source hashes before and after when using real files:

```powershell
Start-Process .artifacts\publish\win-x64\AssetLibrary.V2.exe `
  -ArgumentList @(
    '--library-test-root', 'D:\Codex\3D-Asset-Test',
    '--user-data-dir',
    (Resolve-Path .artifacts).Path + '\webview2-user-data'
  ) -Wait -PassThru
```

Run the real WebView2 interaction smoke test:

```powershell
Start-Process .artifacts\publish\win-x64\AssetLibrary.V2.exe `
  -ArgumentList @(
    '--ui-interaction-test',
    '--user-data-dir',
    (Resolve-Path .artifacts).Path + '\webview2-user-data'
  ) -Wait -PassThru
```

Create the two required light screenshots and one compact dark screenshot:

```powershell
Start-Process .artifacts\publish\win-x64\AssetLibrary.V2.exe `
  -ArgumentList @(
    '--capture-directory',
    (Resolve-Path .artifacts).Path + '\screenshots',
    '--user-data-dir',
    (Resolve-Path .artifacts).Path + '\webview2-user-data'
  ) -Wait -PassThru
```

Normal launches use `%LOCALAPPDATA%\3DAssetLibrary\V2Preview\WebView2` for
WebView2 browser state. Automated local tests pass `--user-data-dir` so all
generated state remains inside the selected test output directory. These test
paths do not read or write the stable client's user assets, indexes, covers,
or configuration.
