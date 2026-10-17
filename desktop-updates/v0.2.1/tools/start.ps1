# 3D Asset Library - Windows PowerShell 5.1 - localhost only
# Original source files are always read-only. All generated data stays under D:\Codex.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$script:sourceRoot = Split-Path $PSScriptRoot -Parent
$script:dataRoot = 'D:\Codex\3D-Asset-Library-Ready\.local-data'
$script:thumbRoot = Join-Path $script:dataRoot 'thumbs'
$script:coverRoot = Join-Path $script:dataRoot 'covers'
$script:catalogPath = Join-Path $script:dataRoot 'catalog.json'
$script:coverPath = Join-Path $script:dataRoot 'covers.json'
$script:uiPath = Join-Path $script:sourceRoot 'app\index.html'
$script:versionPath = Join-Path $script:sourceRoot 'version.json'
$script:updaterPath = Join-Path $PSScriptRoot 'update.ps1'
$script:updateManifestUrl = 'https://raw.githubusercontent.com/lvbihiu-ctrl/3d-asset-library/main/desktop-updates/latest.json'
$script:appVersion = '0.2.0'
if (Test-Path -LiteralPath $script:versionPath) {
    try { $script:appVersion = [string]((Get-Content -LiteralPath $script:versionPath -Raw -Encoding UTF8 | ConvertFrom-Json).version) } catch {}
}
$script:enc = New-Object System.Text.UTF8Encoding($false)
foreach ($folder in @($script:dataRoot, $script:thumbRoot, $script:coverRoot)) {
    if (-not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
}

$script:modelsExt = @('.c4d','.fbx','.obj','.blend','.max','.3ds','.glb','.gltf','.stl','.abc','.usd','.usdz','.skp')
$script:imagesExt = @('.jpg','.jpeg','.png','.bmp','.gif','.webp')
$script:extraExt = @('.hdr','.exr','.sbsar')
$script:assets = @()
$script:summary = @{ root = ''; scannedAt = ''; filesVisited = 0; assetCount = 0; suppressedTextures = 0; missingPreview = 0; durationSeconds = 0 }
$script:covers = @{}

function Read-CoverMap {
    $script:covers = @{}
    if (Test-Path -LiteralPath $script:coverPath) {
        try {
            $obj = Get-Content -LiteralPath $script:coverPath -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($prop in $obj.PSObject.Properties) { $script:covers[$prop.Name] = [string]$prop.Value }
        } catch { Write-Host ('Cover metadata could not be loaded: ' + $_.Exception.Message) -ForegroundColor Yellow }
    }
}
function Save-CoverMap {
    $content = ConvertTo-Json -InputObject $script:covers -Depth 4 -Compress
    [IO.File]::WriteAllText($script:coverPath, $content, $script:enc)
}
function Get-Key([string]$text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))
        return ([BitConverter]::ToString($bytes)).Replace('-','').ToLowerInvariant()
    } finally { $sha.Dispose() }
}
function Is-TexturePath([string]$path) {
    if ($path -match '(?i)(^|[\\/])(tex|textures|texture|贴图|纹理|maps|pbr|mapsource)([\\/]|$)') { return $true }
    $name = [IO.Path]::GetFileNameWithoutExtension($path)
    return ($name -match '(?i)(^|[._\- ])(albedo|basecolor|base_color|normal|roughness|metallic|metalness|bump|displacement|opacity|specular|ao|ambientocclusion|height|glossiness|反射|法线|粗糙度|金属度|置换)([._\- ]|$)')
}
function Get-JpegPreview([string]$full, [string]$outFile, [int]$w=560, [int]$h=380) {
    if (Test-Path -LiteralPath $outFile) { return $true }
    $image = $null; $bmp = $null; $graphics = $null; $temp = $outFile + '.tmp'
    try {
        $image = [Drawing.Image]::FromFile($full)
        if ($image.Width -lt 1 -or $image.Height -lt 1) { return $false }
        $bmp = [Drawing.Bitmap]::new($w, $h)
        $graphics = [Drawing.Graphics]::FromImage($bmp)
        $graphics.Clear([Drawing.Color]::FromArgb(237,241,247))
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $scale = [Math]::Min($w / [double]$image.Width, $h / [double]$image.Height)
        $nw = [Math]::Max(1,[int]($image.Width*$scale)); $nh=[Math]::Max(1,[int]($image.Height*$scale))
        $graphics.DrawImage($image,[int](($w-$nw)/2),[int](($h-$nh)/2),$nw,$nh)
        $bmp.Save($temp,[Drawing.Imaging.ImageFormat]::Jpeg)
        Move-Item -LiteralPath $temp -Destination $outFile -Force
        return $true
    } catch {
        if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue }
        return $false
    } finally {
        if ($graphics) { $graphics.Dispose() }
        if ($bmp) { $bmp.Dispose() }
        if ($image) { $image.Dispose() }
    }
}
function Build-Thumbnail($file) {
    if (-not $file) { return '' }
    $key=Get-Key ($file.FullName.ToLowerInvariant() + '|' + $file.Length + '|' + $file.LastWriteTimeUtc.Ticks)
    $name=$key+'.jpg'; $target=Join-Path $script:thumbRoot $name
    if (Get-JpegPreview $file.FullName $target) { return '/thumb/'+$name }
    return ''
}
function Read-Catalog {
    if (-not (Test-Path -LiteralPath $script:catalogPath)) { return }
    try {
        $data=Get-Content -LiteralPath $script:catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $script:assets=@($data.assets)
        $script:summary=$data.summary
    } catch {
        Write-Host ('Saved index could not be loaded: ' + $_.Exception.Message) -ForegroundColor Yellow
    }
}
function Get-Asset([string]$id) {
    foreach($asset in $script:assets) { if ($asset.id -eq $id) { return $asset } }
    return $null
}
function Get-CatalogJson {
    $items=@()
    foreach ($asset in $script:assets) {
        $cover=''; $coverSet=$false
        if ($script:covers.ContainsKey($asset.id)) {
            $filename=$script:covers[$asset.id]
            if (Test-Path -LiteralPath (Join-Path $script:coverRoot $filename)) { $cover='/cover/'+$filename; $coverSet=$true }
        }
        if (-not $cover) { $cover=$asset.preview }
        $items+=@{id=$asset.id;name=$asset.name;kind=$asset.kind;rel=$asset.rel;full=$asset.full;ext=$asset.ext;preview=$cover;manualCover=$coverSet;hasPreview=[bool]$cover;bytes=$asset.bytes;changed=$asset.changed;resources=$asset.resources}
    }
    return (ConvertTo-Json -InputObject @{summary=$script:summary;assets=@($items)} -Compress -Depth 7)
}
function Scan-Folder([string]$root) {
    $sw=[Diagnostics.Stopwatch]::StartNew()
    $root=[IO.Path]::GetFullPath($root)
    if($root.Length -gt 3){$root=$root.TrimEnd([char]'\')}
    $all=@(Get-ChildItem -LiteralPath $root -File -Recurse -ErrorAction SilentlyContinue)
    $models=[System.Collections.Generic.List[object]]::new()
    $images=[System.Collections.Generic.List[object]]::new()
    $extra=[System.Collections.Generic.List[object]]::new()
    $textures=0
    foreach ($f in $all) {
        $ext=$f.Extension.ToLowerInvariant()
        if ($script:modelsExt -contains $ext) { $models.Add($f) }
        elseif ($script:imagesExt -contains $ext) {
            if (Is-TexturePath $f.FullName) { $textures++ } else { $images.Add($f) }
        }
        elseif ($script:extraExt -contains $ext) {
            if (Is-TexturePath $f.FullName) { $textures++ } else { $extra.Add($f) }
        }
    }
    $byDir=@{}
    foreach($f in $images) {
        $dir=$f.DirectoryName.ToLowerInvariant()
        if (-not $byDir.ContainsKey($dir)) { $byDir[$dir]=[System.Collections.Generic.List[object]]::new() }
        $byDir[$dir].Add($f)
    }
    $modelDirs=@{}
    foreach($f in $models) {
        $dir=$f.DirectoryName.ToLowerInvariant()
        if (-not $modelDirs.ContainsKey($dir)) { $modelDirs[$dir]=0 }
        $modelDirs[$dir]++
    }
    $used=[System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $items=[System.Collections.Generic.List[object]]::new()
    foreach($f in $models) {
        $dir=$f.DirectoryName.ToLowerInvariant()
        $stem=[IO.Path]::GetFileNameWithoutExtension($f.Name)
        $siblings=@(); if($byDir.ContainsKey($dir)) {$siblings=@($byDir[$dir].ToArray())}
        $candidate=$null
        foreach($p in $siblings) {
            $s=[IO.Path]::GetFileNameWithoutExtension($p.Name)
            if ($s -ieq $stem -or $s -imatch ('^'+[Regex]::Escape($stem)+'([_\- ]?(preview|render|final|thumb|cover|预览|渲染|封面|成品|效果图))$')) { $candidate=$p;break }
        }
        if (-not $candidate -and $modelDirs[$dir] -eq 1) {
            foreach($p in $siblings) {
                if ([IO.Path]::GetFileNameWithoutExtension($p.Name) -imatch '^(preview|thumbnail|thumb|cover|render|final|预览|封面|效果图|成品图)(\d+)?$') { $candidate=$p;break }
            }
        }
        $thumb=Build-Thumbnail $candidate
        if($candidate -and $thumb){ $null=$used.Add($candidate.FullName) }
        $rel=$f.FullName.Substring($root.Length).TrimStart([char]'\').Replace('\','/')
        $items.Add([pscustomobject]@{
            id=(Get-Key ($f.FullName.ToLowerInvariant()));name=$stem;kind='模型';ext=$f.Extension.TrimStart('.').ToUpperInvariant();
            rel=$rel;full=$f.FullName;preview=$thumb;bytes=[long]$f.Length;changed=$f.LastWriteTime.ToString('yyyy-MM-dd HH:mm');resources='同目录资源（未解析依赖）'
        })
    }
    foreach($f in $extra) {
        $rel=$f.FullName.Substring($root.Length).TrimStart([char]'\').Replace('\','/')
        $kind=if($f.Extension -ieq '.sbsar'){'材质'}else{'HDRI'}
        $items.Add([pscustomobject]@{id=(Get-Key ($f.FullName.ToLowerInvariant()));name=[IO.Path]::GetFileNameWithoutExtension($f.Name);kind=$kind;ext=$f.Extension.TrimStart('.').ToUpperInvariant();rel=$rel;full=$f.FullName;preview='';bytes=[long]$f.Length;changed=$f.LastWriteTime.ToString('yyyy-MM-dd HH:mm');resources=''})
    }
    # Images outside texture folders are independent items unless paired with models.
    foreach($f in $images) {
        if($used.Contains($f.FullName)) {continue}
        $rel=$f.FullName.Substring($root.Length).TrimStart([char]'\').Replace('\','/')
        $thumb=Build-Thumbnail $f
        $items.Add([pscustomobject]@{id=(Get-Key ($f.FullName.ToLowerInvariant()));name=[IO.Path]::GetFileNameWithoutExtension($f.Name);kind='图片';ext=$f.Extension.TrimStart('.').ToUpperInvariant();rel=$rel;full=$f.FullName;preview=$thumb;bytes=[long]$f.Length;changed=$f.LastWriteTime.ToString('yyyy-MM-dd HH:mm');resources=''})
    }
    $sw.Stop()
    $script:assets=@($items.ToArray())
    $script:summary=@{root=$root;scannedAt=(Get-Date).ToString('yyyy-MM-dd HH:mm:ss');filesVisited=$all.Count;assetCount=$script:assets.Count;suppressedTextures=$textures;missingPreview=@($script:assets | Where-Object {$_.kind -eq '模型' -and -not $_.preview}).Count;durationSeconds=[Math]::Round($sw.Elapsed.TotalSeconds,2)}
    $json=ConvertTo-Json -InputObject @{summary=$script:summary;assets=@($script:assets)} -Depth 7
    [IO.File]::WriteAllText($script:catalogPath,$json,$script:enc)
    Write-Host ('Scan complete: '+$script:assets.Count+' assets; '+$textures+' suppressed texture files; '+$sw.Elapsed.TotalSeconds.ToString('F1')+' seconds')
}
function Pick-Folder {
    $dialog=New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description='选择要收录的素材文件夹（原始文件只读）'
    $dialog.ShowNewFolderButton=$false
    if($script:summary.root -and (Test-Path -LiteralPath $script:summary.root)){ $dialog.SelectedPath=$script:summary.root }
    $owner = New-Object System.Windows.Forms.Form
    $owner.TopMost = $true
    $owner.ShowInTaskbar = $false
    $owner.StartPosition = 'CenterScreen'
    $owner.FormBorderStyle = 'FixedToolWindow'
    $owner.Size = [Drawing.Size]::new(1,1)
    $owner.Opacity = 0
    $owner.Show()
    try {
        if($dialog.ShowDialog($owner) -eq [Windows.Forms.DialogResult]::OK){return $dialog.SelectedPath}
        return ''
    } finally { $owner.Close();$owner.Dispose();$dialog.Dispose() }
}
function Response-Bytes($stream,[int]$code,[string]$mime,[byte[]]$body) {
    $status=if($code -eq 200){'OK'}elseif($code -eq 400){'Bad Request'}elseif($code -eq 404){'Not Found'}elseif($code -eq 413){'Payload Too Large'}else{'Error'}
    $head="HTTP/1.1 $code $status`r`nContent-Type: $mime`r`nContent-Length: $($body.Length)`r`nCache-Control: no-store`r`nX-Content-Type-Options: nosniff`r`nConnection: close`r`n`r`n"
    $headBytes=[Text.Encoding]::ASCII.GetBytes($head)
    $stream.Write($headBytes,0,$headBytes.Length)
    if($body.Length -gt 0){ $stream.Write($body,0,$body.Length) }
    $stream.Flush()
}
function Response-Json($stream,[int]$code,[string]$json){Response-Bytes $stream $code 'application/json; charset=utf-8' ([Text.Encoding]::UTF8.GetBytes($json))}
function FailureJson([string]$message){return (ConvertTo-Json -InputObject @{ok=$false;message=$message} -Compress)}
function Read-LatestVersion {
    # HTTPS version check only; no model paths or user data are uploaded.
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $remote = Invoke-WebRequest -Uri $script:updateManifestUrl -UseBasicParsing -TimeoutSec 15
    $doc = $remote.Content | ConvertFrom-Json
    if ([int]$doc.schema -ne 1 -or [string]$doc.version -notmatch '^\d+\.\d+\.\d+$') { throw '线上版本文件格式不正确' }
    $latest = [version]$doc.version
    $current = [version]$script:appVersion
    return @{ ok=$true; current=$script:appVersion; latest=[string]$doc.version; updateAvailable=($latest -gt $current); notes=[string]$doc.notes }
}

function Serve-Client($client){
    $stream=$client.GetStream();$stream.ReadTimeout=60000;$stream.WriteTimeout=60000
    $header=[System.Collections.Generic.List[byte]]::new()
    while($header.Count -lt 16384){
        $b=$stream.ReadByte();if($b -lt 0){return}
        $header.Add([byte]$b)
        $n=$header.Count
        if($n -ge 4 -and $header[$n-4] -eq 13 -and $header[$n-3] -eq 10 -and $header[$n-2] -eq 13 -and $header[$n-1] -eq 10){break}
    }
    if($header.Count -ge 16384){Response-Json $stream 400 (FailureJson '请求头过长');return}
    $raw=[Text.Encoding]::ASCII.GetString($header.ToArray())
    $lines=$raw -split "`r`n"
    $start=$lines[0] -split ' '
    if($start.Length -lt 2){Response-Json $stream 400 (FailureJson '非法请求');return}
    $method=$start[0];$target=$start[1];$path=($target -split '\?')[0]
    $contentLength=0
    foreach($line in $lines){if($line -match '^Content-Length:\s*(\d+)'){ $contentLength=[int]$Matches[1];break }}
    if($contentLength -gt 12582912){ Response-Json $stream 413 (FailureJson '图片超过12MB，请先缩小截图');return }
    $body=[byte[]]::new($contentLength)
    $read=0
    while($read -lt $contentLength){$n=$stream.Read($body,$read,$contentLength-$read);if($n -le 0){break};$read+=$n}
    if($read -ne $contentLength){Response-Json $stream 400 (FailureJson '请求体未读完整');return}
    if($method -eq 'GET' -and $path -eq '/') {
        Response-Bytes $stream 200 'text/html; charset=utf-8' ([IO.File]::ReadAllBytes($script:uiPath));return
    }
    if($method -eq 'GET' -and $path -eq '/api/catalog') {Response-Json $stream 200 (Get-CatalogJson);return}
    if($method -eq 'GET' -and $path -eq '/api/version') {
        Response-Json $stream 200 (ConvertTo-Json -Compress -InputObject @{ok=$true;version=$script:appVersion});return
    }
    if($method -eq 'GET' -and $path -eq '/api/update-check') {
        try { $info=Read-LatestVersion;Response-Json $stream 200 (ConvertTo-Json -Compress -InputObject $info) }
        catch { Response-Json $stream 400 (FailureJson ('检查更新失败（当前版本不受影响）：'+$_.Exception.Message)) };return
    }
    if($method -eq 'POST' -and $path -eq '/api/update') {
        try {
            $info=Read-LatestVersion
            if(-not $info.updateAvailable){Response-Json $stream 200 '{"ok":true,"started":false}';return}
            if(-not(Test-Path -LiteralPath $script:updaterPath)){throw '更新程序不存在'}
            $cmd=@('-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('"'+$script:updaterPath+'"'),'-ServerPID',[string]$PID)
            $process=Start-Process -FilePath 'powershell.exe' -ArgumentList $cmd -WorkingDirectory $script:sourceRoot -PassThru
            if(-not $process){throw '未能启动更新程序'}
            Response-Json $stream 200 '{"ok":true,"started":true}'
        } catch {Response-Json $stream 400 (FailureJson ('无法启动更新：'+$_.Exception.Message))};return
    }

    if($method -eq 'GET' -and $path -match '^/(thumb|cover)/([a-f0-9]{64}\.jpg)$'){
        $parent=if($Matches[1] -eq 'thumb'){$script:thumbRoot}else{$script:coverRoot}
        $file=Join-Path $parent $Matches[2]
        if(Test-Path -LiteralPath $file){Response-Bytes $stream 200 'image/jpeg' ([IO.File]::ReadAllBytes($file))}else{Response-Json $stream 404 (FailureJson '预览文件不存在')};return
    }
    if($method -eq 'POST' -and $path -eq '/api/scan'){
        $folder=Pick-Folder
        if(-not $folder){Response-Json $stream 200 '{"ok":false,"cancelled":true}';return}
        Scan-Folder $folder
        Response-Json $stream 200 '{"ok":true}';return
    }
    if($method -eq 'POST' -and $path -eq '/api/cover'){
        $id='';if($target -match '\?id=([a-f0-9]{64})'){ $id=$Matches[1] }
        if(-not $id -or -not (Get-Asset $id)){Response-Json $stream 404 (FailureJson '素材不存在');return}
        if($body.Length -eq 0){Response-Json $stream 400 (FailureJson '没有图片内容');return}
        $mem=[IO.MemoryStream]::new($body);$img=$null;$bmp=$null;$graphics=$null
        try{
            $img=[Drawing.Image]::FromStream($mem)
            if($img.Width*$img.Height -gt 50000000){throw '图片尺寸过大'}
            $bw=900;$bh=650
            $bmp=[Drawing.Bitmap]::new($bw,$bh)
            $graphics=[Drawing.Graphics]::FromImage($bmp)
            $graphics.Clear([Drawing.Color]::FromArgb(237,241,247))
            $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $scale=[Math]::Min($bw/[double]$img.Width,$bh/[double]$img.Height)
            $ww=[Math]::Max(1,[int]($img.Width*$scale));$hh=[Math]::Max(1,[int]($img.Height*$scale))
            $graphics.DrawImage($img,[int](($bw-$ww)/2),[int](($bh-$hh)/2),$ww,$hh)
            $name=$id+'.jpg';$full=Join-Path $script:coverRoot $name
            $temp=$full+'.tmp';$bmp.Save($temp,[Drawing.Imaging.ImageFormat]::Jpeg)
            Move-Item -LiteralPath $temp -Destination $full -Force
            $script:covers[$id]=$name;Save-CoverMap
            Response-Json $stream 200 '{"ok":true}'
        } catch {Response-Json $stream 400 (FailureJson ('图片处理失败：'+$_.Exception.Message))}
        finally {if($graphics){$graphics.Dispose()};if($bmp){$bmp.Dispose()};if($img){$img.Dispose()};$mem.Dispose()}
        return
    }
    if($method -eq 'POST' -and $path -eq '/api/clear-cover'){
        $id='';if($target -match '\?id=([a-f0-9]{64})'){$id=$Matches[1]}
        if(-not (Get-Asset $id)){Response-Json $stream 404 (FailureJson '素材不存在');return}
        if($script:covers.ContainsKey($id)){$script:covers.Remove($id);Save-CoverMap}
        Response-Json $stream 200 '{"ok":true}';return
    }
    if($method -eq 'POST' -and $path -eq '/api/reveal'){
        $id='';if($target -match '\?id=([a-f0-9]{64})'){$id=$Matches[1]}
        $asset=Get-Asset $id
        if(-not $asset){Response-Json $stream 404 (FailureJson '素材不存在');return}
        if(-not(Test-Path -LiteralPath $asset.full)){Response-Json $stream 400 (FailureJson '原文件不存在，可能移动硬盘已断开或盘符发生变化');return}
        $argument='/select,"'+$asset.full+'"'
        Start-Process -FilePath 'explorer.exe' -ArgumentList $argument | Out-Null
        Response-Json $stream 200 '{"ok":true}';return
    }
    Response-Json $stream 404 (FailureJson '接口不存在')
}

Read-CoverMap
Read-Catalog
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start()
$port=([Net.IPEndPoint]$listener.LocalEndpoint).Port
$url="http://127.0.0.1:$port/"
Write-Host ('Library ready: '+$url) -ForegroundColor Green
Write-Host ('Local data: '+$script:dataRoot)
Write-Host 'Close this console to exit.'
# Launch as a dedicated Edge app window (without tabs/address bar), not a regular browser tab.
# The existing localhost server, cover storage and search logic remain unchanged.
$edgeExe = @(
    (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft\Edge\Application\msedge.exe'),
    (Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft\Edge\Application\msedge.exe'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\Edge\Application\msedge.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
try {
    if ($edgeExe) {
        Start-Process -FilePath $edgeExe -ArgumentList @('--app='+$url,'--window-size=1480,920','--no-first-run') | Out-Null
        Write-Host 'Opened in an Edge standalone app window.' -ForegroundColor Green
    } else {
        Write-Host 'Microsoft Edge not found: opening the default browser instead.' -ForegroundColor Yellow
        Start-Process $url | Out-Null
    }
} catch {
    Write-Host ('App window could not open: '+$_.Exception.Message) -ForegroundColor Yellow
    Write-Host ('Local app address: '+$url)
    try { Start-Process $url | Out-Null } catch {}
}
try {
    while($true){
        $client=$listener.AcceptTcpClient()
        try{Serve-Client $client}
        catch{
            Write-Host ('Request failed: '+$_.Exception.Message) -ForegroundColor Yellow
            try{Response-Json ($client.GetStream()) 500 (FailureJson $_.Exception.Message)}catch{}
        } finally {$client.Close()}
    }
} finally { $listener.Stop() }
