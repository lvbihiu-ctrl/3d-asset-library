# V2 Windows WebView2 host (Issue #14)

This folder contains the minimal Windows 11 x64 host for GitHub Issue #14.
It is a preview skeleton, not a replacement for the stable v0.1.7 client.

## What this slice proves

- .NET 10 WinForms can load the repository's trusted local HTML in WebView2.
- The page can call a strict C# message bridge with `{id, action, payload}`.
- The only allowed page action is currently `getHostInfo`; unknown actions,
  paths, commands, malformed JSON, oversized messages, untrusted origins, and
  remote resources are rejected.
- A real Windows process can perform a WebView2 handshake, exit as a smoke
  test, and capture synthetic-data screenshots at 1016x689 and 1920x1080.
- Browser-prototype controls without an Issue #14 native implementation are
  hidden in the Windows host instead of presenting actions that cannot work.

This slice does **not** implement scanning, SQLite, C4D FileDrop/Shift,
updating, migration, or access to real user assets.

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

Create the two required synthetic screenshots:

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
generated state remains inside the selected test output directory.
