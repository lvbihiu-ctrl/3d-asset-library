param(
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [string]$InstallRoot = 'D:\Codex\3D-Asset-Library-V2-App',
    [string]$DataDirectory = 'D:\Codex\3D-Asset-Library-V2-Data'
)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($PublishDirectory)
$destinationRoot = [IO.Path]::GetFullPath($InstallRoot)
if (!$destinationRoot.StartsWith('D:\Codex\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Local install destination must be beneath D:\Codex.'
}
foreach ($name in @('AssetLibrary.V2.exe','AssetLibrary.V2.dll','Web\index.html','Web\app.js')) {
    if (!(Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) { throw "Missing published file: $name" }
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$versionDirectory = Join-Path $destinationRoot ('builds\'+$stamp)
if (Test-Path -LiteralPath $versionDirectory) { throw 'Destination already exists; refusing to overwrite a running build.' }
New-Item -Path $versionDirectory -ItemType Directory | Out-Null
$sourceFiles = @(Get-ChildItem -LiteralPath $source -File -Recurse)
Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $versionDirectory -Recurse
$hashes = foreach ($file in $sourceFiles) {
    $relative = [IO.Path]::GetRelativePath($source,$file.FullName)
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath (Join-Path $versionDirectory $relative) -Algorithm SHA256).Hash -ne $hash) {
        throw "Installed file checksum mismatch: $relative"
    }
    [pscustomobject]@{Path=$relative;SHA256=$hash}
}
$hashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $versionDirectory 'local-build-sha256.json') -Encoding utf8
$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop '3D素材管理器.lnk'
$backupDirectory = Join-Path $destinationRoot ('backups\'+$stamp)
New-Item -Path $backupDirectory -ItemType Directory | Out-Null
$shell = New-Object -ComObject WScript.Shell
if (Test-Path -LiteralPath $shortcutPath) {
    Copy-Item -LiteralPath $shortcutPath -Destination $backupDirectory
    $old = $shell.CreateShortcut($shortcutPath)
    if ($old.TargetPath -eq 'D:\Codex\3D-Asset-Library-EXE\3D素材管理器.exe') {
        $fallback = Join-Path $desktop '3D素材管理器-旧版备用.lnk'
        if (!(Test-Path -LiteralPath $fallback)) { Copy-Item -LiteralPath $shortcutPath -Destination $fallback }
    }
}
$link = $shell.CreateShortcut($shortcutPath)
$link.TargetPath = Join-Path $versionDirectory 'AssetLibrary.V2.exe'
$link.Arguments = '--data-dir "'+$DataDirectory+'" --user-data-dir "'+(Join-Path $DataDirectory 'WebView2')+'"'
$link.WorkingDirectory = $versionDirectory
$link.Description = '3D asset library V2 - persistent covers and image similarity search'
$link.IconLocation = $link.TargetPath+',0'
$link.Save()
# A user-facing launch: intentionally visible, through the same shortcut used on the desktop.
Start-Process -FilePath $shortcutPath
$deadline = [DateTime]::UtcNow.AddSeconds(20)
do {
    Start-Sleep -Milliseconds 200
    $launched = Get-Process -Name AssetLibrary.V2 -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $link.TargetPath -and $_.MainWindowHandle -ne 0 }
} while (!$launched -and [DateTime]::UtcNow -lt $deadline)
if (!$launched) {
    $savedShortcut = Join-Path $backupDirectory '3D素材管理器.lnk'
    if (Test-Path -LiteralPath $savedShortcut) { Copy-Item -LiteralPath $savedShortcut -Destination $shortcutPath -Force }
    throw 'New window did not appear. Original desktop shortcut restored; all software and data retained.'
}
$launched | Select-Object Id,Path,MainWindowTitle
Write-Output ('LOCAL_INSTALL_PASS files='+$sourceFiles.Count+' shortcut='+$shortcutPath+' backup='+$backupDirectory)
