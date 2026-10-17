# Native launcher

This small Windows launcher fixes the original self-update deadlock without
changing the asset-library UI or user data.

- Only one launcher/application instance can run at a time.
- A versioned launcher stages `3D素材管理器.exe.new`, exits, and lets a separate
  helper replace the stable executable after the launching process releases it.
- Normal online updates continue to download only the verified PowerShell UI
  script (`.runtime/override.ps1`), not the whole application.
- The launcher never reads, writes, moves, or deletes model/texture source files.
- Existing `.local-data`, covers, thumbnails, catalog and settings are untouched.

Build on Windows with the installed .NET 8 SDK:

```powershell
dotnet publish .\AssetLibraryLauncher.csproj -c Release -r win-x64 --self-contained false
```

For an existing v0.1.2 installation, run `Migrate-V012Runtime.ps1` once. It
creates `.runtime/app-v0.1.3.ps1` and leaves the v0.1.2 script in place as a
rollback copy. The launcher automatically chooses the highest valid versioned
runtime script.
