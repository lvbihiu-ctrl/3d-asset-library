param([string]$ExampleFolder='')
$ErrorActionPreference='Stop'
try {
 Add-Type -AssemblyName System.Windows.Forms
 Add-Type -AssemblyName System.Drawing
 $sw=[Diagnostics.Stopwatch]::StartNew()
 $projectRoot=Split-Path $PSScriptRoot -Parent
 $cache=Join-Path $projectRoot '.local-data'
 $thumbDir=Join-Path $cache 'thumbs'
 New-Item -ItemType Directory -Path $thumbDir -Force | Out-Null
 $settings=Join-Path $cache 'settings.json'
 $previous=''
 if(Test-Path -LiteralPath $settings){try{$previous=(Get-Content -LiteralPath $settings -Raw -Encoding UTF8|ConvertFrom-Json).root}catch{}}
 $picker=New-Object System.Windows.Forms.FolderBrowserDialog
 $picker.Description='选择要测试的素材文件夹：不会修改或搬动任何原始素材'
 $picker.ShowNewFolderButton=$false
 if($previous -and (Test-Path -LiteralPath $previous)){$picker.SelectedPath=$previous}
 elseif($ExampleFolder -and (Test-Path -LiteralPath $ExampleFolder)){$picker.SelectedPath=$ExampleFolder}
 if($picker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK){Write-Host '已取消选择。';exit 0}
 $root=[IO.Path]::GetFullPath($picker.SelectedPath).TrimEnd([char]'\')
 ('{"root":'+(ConvertTo-Json -Compress $root)+'}') | Set-Content -LiteralPath $settings -Encoding UTF8
 Write-Host ('正在扫描：'+$root)
 $modelExt=@('.c4d','.fbx','.obj','.blend','.max','.3ds','.glb','.gltf','.stl','.abc','.usd','.usdz','.skp')
 $imgExt=@('.jpg','.jpeg','.png','.bmp','.gif','.webp')
 $all=@(Get-ChildItem -LiteralPath $root -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Length -ge 0 })
 $models=New-Object 'System.Collections.Generic.List[object]'
 $images=New-Object 'System.Collections.Generic.List[object]'
 $extra=New-Object 'System.Collections.Generic.List[object]'
 $textures=0
 function Is-TexturePath([string]$path){
  if($path -match '(?i)(^|[\\/])(tex|textures|texture|贴图|纹理|maps|pbr|mapsource)([\\/]|$)'){return $true}
  $name=[IO.Path]::GetFileNameWithoutExtension($path)
  return ($name -match '(?i)(^|[._\- ])(albedo|basecolor|base_color|normal|roughness|metallic|metalness|bump|displacement|opacity|specular|ao|ambientocclusion|height|glossiness|反射|法线|粗糙度|金属度|置换)([._\- ]|$)')
 }
 foreach($f in $all){
  $ext=$f.Extension.ToLowerInvariant()
  if($modelExt -contains $ext){$models.Add($f)}
  elseif($imgExt -contains $ext){if(Is-TexturePath $f.FullName){$textures++}else{$images.Add($f)}}
  elseif($ext -in @('.hdr','.exr','.sbsar')){$extra.Add($f)}
 }
 $picsByDir=@{}
 foreach($f in $images){$dir=$f.DirectoryName.ToLowerInvariant();if(-not $picsByDir.ContainsKey($dir)){$picsByDir[$dir]=New-Object 'System.Collections.Generic.List[object]'};$picsByDir[$dir].Add($f)}
 $modelByDir=@{}
 foreach($f in $models){$dir=$f.DirectoryName.ToLowerInvariant();if(-not $modelByDir.ContainsKey($dir)){$modelByDir[$dir]=0};$modelByDir[$dir]++}
 $used=[System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 $assets=New-Object 'System.Collections.Generic.List[object]'
 function Relative([string]$path){return $path.Substring($root.Length).TrimStart([char]'\').Replace('\','/')}
 function Classify([string]$rel,[string]$ext){
  $s=$rel.ToLowerInvariant()
  if($s -match '(^|/)(kit|场景|场景组件|组件|套件)(/|_)'){return '场景KIT'}
  if($ext -in @('.hdr','.exr') -or $s -match '(hdri|环境贴图)'){return 'HDRI'}
  if($ext -eq '.sbsar' -or $s -match '(^|/)(材质|material)(/|_)'){return '材质'}
  if($modelExt -contains $ext){return '模型'}
  return '预览图'
 }
 function Category([string]$rel){
  $s=$rel.ToLowerInvariant()
  if($s -match '摄像头|webcam|camera|相机'){return '摄像设备'}
  if($s -match '手机|phone|充电|charger|数码'){return '数码产品'}
  if($s -match '桌|desk|table|办公'){return '桌面办公'}
  if($s -match '灯|light|lamp'){return '灯具'}
  if($s -match '植物|plant|树|花'){return '植物'}
  if($s -match '椅|chair|沙发|sofa|家具'){return '家具'}
  if($s -match '岩|stone|rock|户外|outdoor'){return '户外'}
  if($s -match '汽车|车辆|car'){return '车辆'}
  return '未分类'
 }
 function Digest([string]$value){
  $md5=[Security.Cryptography.MD5]::Create()
  try{return ([BitConverter]::ToString($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes($value)))).Replace('-','').ToLowerInvariant()}
  finally{$md5.Dispose()}
 }
 $created=0;$reused=0;$unreadable=0
 $jpegCodec=[Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object {$_.MimeType -eq 'image/jpeg'} | Select-Object -First 1
 function MakePreview($imgFile){
  if(-not $imgFile){return $null}
  $rel=Relative $imgFile.FullName
  $key=Digest ($rel+'|'+$imgFile.Length+'|'+$imgFile.LastWriteTimeUtc.Ticks)
  $thumb=Join-Path $thumbDir ($key+'.jpg')
  if(Test-Path -LiteralPath $thumb){$script:reused++}
  else{
   $image=$null;$bmp=$null;$graphics=$null;$params=$null
   try{
    $image=[Drawing.Image]::FromFile($imgFile.FullName)
    $bw=280;$bh=190
    $bmp=[Drawing.Bitmap]::new($bw,$bh)
    $graphics=[Drawing.Graphics]::FromImage($bmp)
    $graphics.Clear([Drawing.Color]::FromArgb(237,241,247))
    $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $scale=[Math]::Min($bw/$image.Width,$bh/$image.Height)
    $w=[Math]::Max(1,[int]($image.Width*$scale));$h=[Math]::Max(1,[int]($image.Height*$scale))
    $graphics.DrawImage($image,[int](($bw-$w)/2),[int](($bh-$h)/2),$w,$h)
    $params=[Drawing.Imaging.EncoderParameters]::new(1)
    $params.Param[0]=[Drawing.Imaging.EncoderParameter]::new([Drawing.Imaging.Encoder]::Quality,[long]67)
    $bmp.Save($thumb,$jpegCodec,$params)
    $script:created++
   }catch{$script:unreadable++;return $null}
   finally{if($params){$params.Dispose()};if($graphics){$graphics.Dispose()};if($bmp){$bmp.Dispose()};if($image){$image.Dispose()}}
  }
  try{return 'data:image/jpeg;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($thumb))}catch{return $null}
 }
 function NewRecord($file,$image){
  $rel=Relative $file.FullName
  $ext=$file.Extension.ToLowerInvariant()
  $isModel=$modelExt -contains $ext
  $kind=Classify $rel $ext
  $preview=MakePreview $image
  $name=[IO.Path]::GetFileNameWithoutExtension($file.Name)
  $row=[ordered]@{
   id=($rel.ToLowerInvariant());name=$name;kind=$kind;category=(Category $rel)
   ext=($ext.TrimStart('.'));rel=$rel;full=$file.FullName
   preview=$preview;hasPreview=([bool]$preview);model=$isModel
   bytes=[long]$file.Length;changed=$file.LastWriteTime.ToString('yyyy-MM-dd HH:mm')
   previewSource=$(if($image){Relative $image.FullName}else{''})
  }
  $script:assets.Add([pscustomobject]$row)
 }
 foreach($m in $models){
  $pic=$null;$dir=$m.DirectoryName.ToLowerInvariant();$stem=[IO.Path]::GetFileNameWithoutExtension($m.Name)
  $siblings=@();if($picsByDir.ContainsKey($dir)){$siblings=$picsByDir[$dir].ToArray()}
  foreach($p in $siblings){
   $ps=[IO.Path]::GetFileNameWithoutExtension($p.Name)
   if($ps -ieq $stem -or $ps -imatch ('^'+[Regex]::Escape($stem)+'([_\- ]?(preview|render|thumb|cover|预览|渲染|封面))$')){$pic=$p;break}
  }
  if(-not $pic -and $modelByDir[$dir] -eq 1){
   foreach($p in $siblings){if([IO.Path]::GetFileNameWithoutExtension($p.Name) -imatch '^(preview|thumbnail|thumb|cover|render|预览|封面|效果图|主图)(\d+)?$'){$pic=$p;break}}
  }
  if($pic){$null=$used.Add($pic.FullName)}
  NewRecord $m $pic
 }
 foreach($e in $extra){NewRecord $e $null}
 foreach($p in $images){if(-not $used.Contains($p.FullName)){NewRecord $p $p}}
 $sw.Stop()
 $summary=[ordered]@{
  root=$root; scannedAt=(Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
  filesVisited=$all.Count; assetCount=$assets.Count; models=$models.Count
  pictureFiles=$images.Count; suppressedTextures=$textures
  missingPreview=@($assets|Where-Object {$_.model -and -not $_.hasPreview}).Count
  newPreviews=$created; reusedPreviews=$reused; unreadablePreviews=$unreadable
  durationSeconds=[Math]::Round($sw.Elapsed.TotalSeconds,2)
 }
 $obj=[ordered]@{version='14.0';summary=$summary;assets=@($assets.ToArray())}
 $data='window.__ASSET_CATALOG__='+($obj|ConvertTo-Json -Depth 8 -Compress)+';'
 [IO.File]::WriteAllText((Join-Path $cache 'catalog.js'),$data,([Text.UTF8Encoding]::new($false)))
 $sourceUI=Join-Path $projectRoot 'app\素材工作台.html'
 Copy-Item -LiteralPath $sourceUI -Destination (Join-Path $cache 'index.html') -Force
 Write-Host ('扫描完成：素材 '+$assets.Count+' 条；新建预览 '+$created+' 张；复用缓存 '+$reused+' 张；耗时 '+[Math]::Round($sw.Elapsed.TotalSeconds,2)+' 秒。')
 Start-Process -FilePath (Join-Path $cache 'index.html')
}catch{
 Write-Host ('扫描失败：'+$_.Exception.Message) -ForegroundColor Red
 [Windows.Forms.MessageBox]::Show(('无法完成扫描：'+$_.Exception.Message),'素材库测试')|Out-Null
 exit 1
}
