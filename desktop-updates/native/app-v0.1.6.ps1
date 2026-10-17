# Windows desktop UI for 3D Asset Library. Source models are read-only.
# The EXE embeds this WinForms implementation; this script is extracted automatically under D:\Codex.
$ErrorActionPreference = 'Stop'
try {
  Add-Type -AssemblyName System.Windows.Forms
  Add-Type -AssemblyName System.Drawing
  [System.Windows.Forms.Application]::EnableVisualStyles()
} catch { exit 11 }
$script:installRoot = 'D:\Codex\3D-Asset-Library-EXE'
$script:dataRoot = 'D:\Codex\3D-Asset-Library-Ready\.local-data'
$script:coverRoot = Join-Path $script:dataRoot 'covers'
$script:thumbRoot = Join-Path $script:dataRoot 'thumbs'
$script:catalogPath = Join-Path $script:dataRoot 'catalog.json'
$script:coverMapPath = Join-Path $script:dataRoot 'covers.json'
$script:settingsPath = Join-Path $script:installRoot 'settings.json'
$script:manifestUrl = 'https://raw.githubusercontent.com/lvbihiu-ctrl/3d-asset-library/refs/heads/main/desktop-updates/native/latest.json'
$script:version = '0.1.6'
$script:smoke = ($env:ASSET_LIBRARY_SMOKE -eq '1')
if($script:smoke){
  $safe = Join-Path $env:TEMP 'AssetLibraryUI-SafeSmoke'
  $script:installRoot = Join-Path $safe 'install'
  $script:dataRoot = Join-Path $safe 'data'
  $script:coverRoot = Join-Path $script:dataRoot 'covers'
  $script:thumbRoot = Join-Path $script:dataRoot 'thumbs'
  $script:catalogPath = Join-Path $script:dataRoot 'catalog.json'
  $script:coverMapPath = Join-Path $script:dataRoot 'covers.json'
  $script:settingsPath = Join-Path $script:installRoot 'settings.json'
}
$script:categoryPath=Join-Path $script:dataRoot 'ui-categories.v1.json'
$script:categories=@('手机数码','场景搭建','灯具道具','材质','其他')
$script:categoryMap=@{}
$script:navFilter='all'
$script:utf8 = New-Object Text.UTF8Encoding($false)
$script:assets = @()
$script:summary = @{ root=''; assetCount=0; suppressedTextures=0; filesVisited=0 }
$script:covers = @{}
$script:selectedId = ''
$script:mode = 'cover'
$script:searchRank = @()
$script:cardOffset = 0
$script:dragPath = ''
$script:dragMouseStart = [Drawing.Point]::new(0,0)
$script:dragging = $false
$script:cardControls = @()
# User interface preference only; never written into source assets or catalog.
$script:themePrefPath = Join-Path $script:installRoot '.runtime\theme-mode.txt'
$script:darkMode = $false
try {
  if(Test-Path -LiteralPath $script:themePrefPath -PathType Leaf){
    $script:darkMode=([IO.File]::ReadAllText($script:themePrefPath).Trim() -eq 'dark')
  }
} catch {}
foreach($p in @($script:installRoot,$script:dataRoot,$script:coverRoot,$script:thumbRoot)) {if(-not (Test-Path -LiteralPath $p)){[void](New-Item -ItemType Directory -Force -Path $p)}}

function Get-Key([string]$value) {
  $sha=[Security.Cryptography.SHA256]::Create()
  try {return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($value)))).Replace('-','').ToLowerInvariant()}
  finally {$sha.Dispose()}
}
function Is-TexturePath([string]$path) {
  if($path -match '(?i)(^|[\\/])(tex|textures|texture|贴图|纹理|maps|pbr|mapsource)([\\/]|$)'){return $true}
  $name=[IO.Path]::GetFileNameWithoutExtension($path)
  return ($name -match '(?i)(^|[._\- ])(albedo|basecolor|base_color|normal|roughness|metallic|metalness|bump|displacement|opacity|specular|ao|ambientocclusion|height|glossiness|反射|法线|粗糙度|金属度|置换)([._\- ]|$)')
}
function Show-Problem([string]$message){
  if($script:smoke){throw $message}
  [void][Windows.Forms.MessageBox]::Show($message,'3D 素材管理器',[Windows.Forms.MessageBoxButtons]::OK,[Windows.Forms.MessageBoxIcon]::Warning)
}
function Set-Status([string]$value){$script:status.Text=$value;$script:status.Refresh()}
function Load-Covers {
  $script:covers=@{}
  if(Test-Path -LiteralPath $script:coverMapPath) {
    try {$data=Get-Content -LiteralPath $script:coverMapPath -Raw -Encoding UTF8 | ConvertFrom-Json;foreach($p in $data.PSObject.Properties){$script:covers[$p.Name]=[string]$p.Value}}catch{}
  }
}
function Save-Covers {
  [IO.File]::WriteAllText($script:coverMapPath,(ConvertTo-Json -InputObject $script:covers -Depth 4 -Compress),$script:utf8)
}
function Load-Catalog {
  if(!(Test-Path -LiteralPath $script:catalogPath)){return}
  try {
    $obj=Get-Content -LiteralPath $script:catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $script:assets=@($obj.assets | Where-Object {$null -ne $_})
    if($obj.summary){$script:summary=$obj.summary}
  }catch{Show-Problem ('读取旧索引失败：'+$_.Exception.Message)}
}
function Save-Catalog {
  $obj=@{summary=$script:summary;assets=@($script:assets)}
  [IO.File]::WriteAllText($script:catalogPath,(ConvertTo-Json -InputObject $obj -Depth 8),$script:utf8)
}
function Category-For($a){
  $id=[string]$a.id
  if($script:categoryMap.ContainsKey($id)){return [string]$script:categoryMap[$id]}
  return '未分类'
}
function Load-Categories {
  $script:categoryMap=@{}
  if(!(Test-Path -LiteralPath $script:categoryPath)){return}
  try {
    $doc=Get-Content -LiteralPath $script:categoryPath -Encoding UTF8 -Raw | ConvertFrom-Json
    if([int]$doc.schema -ne 1){throw '分类文件格式不支持'}
    if($doc.categories){$script:categories=@($doc.categories|ForEach-Object{[string]$_})}
    if($doc.assignments){foreach($p in $doc.assignments.PSObject.Properties){$script:categoryMap[$p.Name]=[string]$p.Value}}
  }catch{Show-Problem ('分类读取失败，已跳过，不影响原模型：'+$_.Exception.Message)}
}
function Save-Categories{
  $obj=@{schema=1;categories=@($script:categories);assignments=$script:categoryMap}
  $tmp=$script:categoryPath+'.new'
  [IO.File]::WriteAllText($tmp,(ConvertTo-Json -InputObject $obj -Depth 5),$script:utf8)
  $null=Get-Content -LiteralPath $tmp -Encoding UTF8 -Raw | ConvertFrom-Json
  if(Test-Path -LiteralPath $script:categoryPath){Copy-Item -LiteralPath $script:categoryPath -Destination ($script:categoryPath+'.bak') -Force}
  Move-Item -LiteralPath $tmp -Destination $script:categoryPath -Force
}
function Layout-Masonry {
  if(!$script:gallery -or !$script:cardControls){return}
  $width=[Math]::Max(218,$script:gallery.ClientSize.Width-30)
  $columns=[Math]::Max(1,[int][Math]::Floor(($width+12)/234))
  $levels=New-Object 'int[]' $columns
  $script:gallery.SuspendLayout()
  try{
    foreach($card in @($script:cardControls)){
      $ix=0
      for($j=1;$j -lt $columns;$j++){if($levels[$j] -lt $levels[$ix]){$ix=$j}}
      $card.Location=[Drawing.Point]::new((12+$ix*234),(12+$levels[$ix]))
      $levels[$ix]+=$card.Height+12
    }
    $max=0;foreach($v in $levels){if($v -gt $max){$max=$v}}
    $script:gallery.AutoScrollMinSize=[Drawing.Size]::new(0,($max+20))
  }finally{$script:gallery.ResumeLayout()}
}
function Load-BitmapCopy([string]$path) {
  if(-not (Test-Path -LiteralPath $path -PathType Leaf)){return $null}
  $image=$null
  try {
    $image=[Drawing.Image]::FromFile($path)
    return [Drawing.Bitmap]::new($image)
  } catch {return $null}
  finally{if($image){$image.Dispose()}}
}
function Make-Thumbnail([string]$path) {
  if(-not $path){return ''}
  try {
    $f=Get-Item -LiteralPath $path -ErrorAction Stop
    $key=Get-Key ($f.FullName.ToLowerInvariant()+'|'+$f.Length+'|'+$f.LastWriteTimeUtc.Ticks)
    $dest=Join-Path $script:thumbRoot ($key+'.jpg')
    if(!(Test-Path -LiteralPath $dest)) {
      $src=$null;$bmp=$null;$g=$null
      try {
        $src=[Drawing.Image]::FromFile($path)
        $bmp=[Drawing.Bitmap]::new(560,380)
        $g=[Drawing.Graphics]::FromImage($bmp)
        $g.Clear([Drawing.Color]::FromArgb(241,244,249))
        $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $s=[Math]::Min(560.0/$src.Width,380.0/$src.Height)
        $w=[Math]::Max(1,[int]($src.Width*$s));$h=[Math]::Max(1,[int]($src.Height*$s))
        $g.DrawImage($src,[int]((560-$w)/2),[int]((380-$h)/2),$w,$h)
        $bmp.Save($dest,[Drawing.Imaging.ImageFormat]::Jpeg)
      } finally {if($g){$g.Dispose()};if($bmp){$bmp.Dispose()};if($src){$src.Dispose()}}
    }
    return ('/thumb/'+$key+'.jpg')
  } catch {return ''}
}
function Image-ForAsset($asset) {
  if($script:covers.ContainsKey([string]$asset.id)) {
    $file=Join-Path $script:coverRoot ([string]$script:covers[[string]$asset.id])
    if(Test-Path -LiteralPath $file){return $file}
  }
  $preview=[string]$asset.preview
  if($preview.StartsWith('/thumb/')){return (Join-Path $script:thumbRoot ([IO.Path]::GetFileName($preview)))}
  if($preview.StartsWith('/cover/')){return (Join-Path $script:coverRoot ([IO.Path]::GetFileName($preview)))}
  return ''
}
function Scan-Folder([string]$root) {
  $sw=[Diagnostics.Stopwatch]::StartNew()
  $all=@(Get-ChildItem -LiteralPath $root -File -Recurse -ErrorAction SilentlyContinue)
  $models=[Collections.Generic.List[object]]::new()
  $images=[Collections.Generic.List[object]]::new()
  $extra=[Collections.Generic.List[object]]::new()
  $suppressed=0
  $modelTypes=@('.c4d','.fbx','.obj','.blend','.max','.3ds','.glb','.gltf','.stl','.abc','.usd','.usdz','.skp')
  $imageTypes=@('.jpg','.jpeg','.png','.bmp','.gif','.webp')
  foreach($f in $all){
    $ext=$f.Extension.ToLowerInvariant()
    if($modelTypes -contains $ext){$models.Add($f)}
    elseif($imageTypes -contains $ext){if(Is-TexturePath $f.FullName){$suppressed++}else{$images.Add($f)}}
    elseif($ext -in @('.hdr','.exr','.sbsar')){if(Is-TexturePath $f.FullName){$suppressed++}else{$extra.Add($f)}}
  }
  $byDir=@{};$modelDirs=@{}
  foreach($f in $images){$dir=$f.DirectoryName.ToLowerInvariant();if(!$byDir.ContainsKey($dir)){$byDir[$dir]=[Collections.Generic.List[object]]::new()};$byDir[$dir].Add($f)}
  foreach($f in $models){$dir=$f.DirectoryName.ToLowerInvariant();if(!$modelDirs.ContainsKey($dir)){$modelDirs[$dir]=0};$modelDirs[$dir]++}
  $used=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  $out=[Collections.Generic.List[object]]::new()
  $root=$root.TrimEnd([char]'\')
  foreach($f in $models){
    $stem=[IO.Path]::GetFileNameWithoutExtension($f.Name);$dir=$f.DirectoryName.ToLowerInvariant();$candidate=$null
    $siblings=@();if($byDir.ContainsKey($dir)){$siblings=@($byDir[$dir].ToArray())}
    foreach($p in $siblings){$s=[IO.Path]::GetFileNameWithoutExtension($p.Name);if($s -ieq $stem -or $s -imatch ('^'+[Regex]::Escape($stem)+'([_\- ]?(preview|render|final|thumb|cover|预览|渲染|封面|成品|效果图))$')){$candidate=$p;break}}
    if(!$candidate -and $modelDirs[$dir] -eq 1){foreach($p in $siblings){if([IO.Path]::GetFileNameWithoutExtension($p.Name) -imatch '^(preview|thumbnail|thumb|cover|render|final|预览|封面|效果图|成品图)(\d+)?$'){$candidate=$p;break}}}
    $preview='';if($candidate){$preview=Make-Thumbnail $candidate.FullName;if($preview){[void]$used.Add($candidate.FullName)}}
    $rel=$f.FullName.Substring($root.Length).TrimStart([char]'\').Replace('\','/')
    $out.Add([pscustomobject]@{id=(Get-Key $f.FullName.ToLowerInvariant());name=$stem;kind='模型';ext=$f.Extension.TrimStart('.').ToUpperInvariant();rel=$rel;full=$f.FullName;preview=$preview;bytes=[long]$f.Length;changed=$f.LastWriteTime.ToString('yyyy-MM-dd HH:mm');resources='同目录资源（未解析依赖）'})
  }
  foreach($f in $extra){$rel=$f.FullName.Substring($root.Length).TrimStart([char]'\').Replace('\','/');$kind=if($f.Extension -ieq '.sbsar'){'材质'}else{'HDRI'};$out.Add([pscustomobject]@{id=(Get-Key $f.FullName.ToLowerInvariant());name=[IO.Path]::GetFileNameWithoutExtension($f.Name);kind=$kind;ext=$f.Extension.TrimStart('.').ToUpperInvariant();rel=$rel;full=$f.FullName;preview='';bytes=[long]$f.Length;changed=$f.LastWriteTime.ToString('yyyy-MM-dd HH:mm');resources=''})}
  foreach($f in $images){if($used.Contains($f.FullName)){continue};$rel=$f.FullName.Substring($root.Length).TrimStart([char]'\').Replace('\','/');$preview=Make-Thumbnail $f.FullName;$out.Add([pscustomobject]@{id=(Get-Key $f.FullName.ToLowerInvariant());name=[IO.Path]::GetFileNameWithoutExtension($f.Name);kind='图片';ext=$f.Extension.TrimStart('.').ToUpperInvariant();rel=$rel;full=$f.FullName;preview=$preview;bytes=[long]$f.Length;changed=$f.LastWriteTime.ToString('yyyy-MM-dd HH:mm');resources=''})}
  $sw.Stop()
  $script:assets=@($out.ToArray())
  $script:summary=@{root=$root;assetCount=$script:assets.Count;filesVisited=$all.Count;suppressedTextures=$suppressed;scannedAt=(Get-Date).ToString('yyyy-MM-dd HH:mm:ss');durationSeconds=[Math]::Round($sw.Elapsed.TotalSeconds,2)}
  Save-Catalog
}
function Hash-Image($im) {
  $mini=$null;$g=$null
  try {
    $mini=[Drawing.Bitmap]::new(9,8)
    $g=[Drawing.Graphics]::FromImage($mini)
    $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBilinear
    $g.DrawImage($im,0,0,9,8)
    $s=New-Object System.Text.StringBuilder
    for($y=0;$y -lt 8;$y++){for($x=0;$x -lt 8;$x++){
      $a=$mini.GetPixel($x,$y);$b=$mini.GetPixel(($x+1),$y)
      $va=($a.R*299+$a.G*587+$a.B*114);$vb=($b.R*299+$b.G*587+$b.B*114)
      [void]$s.Append($(if($va -gt $vb){'1'}else{'0'}))
    }}
    return $s.ToString()
  }finally{if($g){$g.Dispose()};if($mini){$mini.Dispose()}}
}
function Sort-ByImage($pasted){
  $script:searchRank=@()
  $h=Hash-Image $pasted
  $scores=[Collections.Generic.List[object]]::new()
  foreach($a in $script:assets){
    $path=Image-ForAsset $a
    if(!$path -or !(Test-Path -LiteralPath $path)){continue}
    $img=Load-BitmapCopy $path
    if(!$img){continue}
    try{
      $v=Hash-Image $img;$dist=0
      for($i=0;$i -lt 64;$i++){if($h[$i] -ne $v[$i]){$dist++}}
      $scores.Add([pscustomobject]@{id=[string]$a.id;score=$dist})
    }finally{$img.Dispose()}
  }
  $script:searchRank=@($scores | Sort-Object score)
  $script:mode='image'
  $script:keyword.Text=''
  $script:cardOffset=0
  Refresh-Cards
  Set-Status ('截图查找：已比较 '+$scores.Count+' 张封面/预览。相近排序仅为测试算法，不保证识别准确率。')
}
function Find-AssetById([string]$id){foreach($a in $script:assets){if([string]$a.id -eq $id){return $a}};return $null}
function Select-Asset($asset){
  $script:selectedId=[string]$asset.id
  $script:selection.Text=('已选：'+$asset.name+'  |  '+$asset.ext+'  |  '+$asset.full)
  if($script:detail){$script:detail.Text=('素材详情'+[Environment]::NewLine+[Environment]::NewLine+$asset.name+[Environment]::NewLine+$asset.ext+' · '+$asset.kind+[Environment]::NewLine+[Environment]::NewLine+$asset.rel+[Environment]::NewLine+[Environment]::NewLine+'拖入 C4D / Shift 合并')}
  if($script:catPick){
    $script:catPick.Enabled=$true;$script:catPick.Items.Clear()
    [void]$script:catPick.Items.Add('未分类')
    foreach($c in $script:categories){[void]$script:catPick.Items.Add($c)}
    $script:catPick.SelectedItem=(Category-For $asset)
  }
  $script:mode='cover'
  $script:modeLabel.Text='粘贴模式：设置选中素材的封面'
  Set-Status ('已选中 '+$asset.name+'。拖动卡片到 C4D；按住 Shift 可合并到当前场景。')
}
function Cover-FromClipboard {
  if(!$script:selectedId){Set-Status '请先单击一个素材卡片，再按 Ctrl+V 设置封面。';return}
  if(-not [Windows.Forms.Clipboard]::ContainsImage()){Set-Status '剪贴板里不是图片。先复制最终渲染图，再按 Ctrl+V。';return}
  $im=[Windows.Forms.Clipboard]::GetImage()
  if(!$im){return}
  try {
    $name=$script:selectedId+'.jpg';$target=Join-Path $script:coverRoot $name
    $bmp=[Drawing.Bitmap]::new($im)
    try{$bmp.Save($target,[Drawing.Imaging.ImageFormat]::Jpeg)}finally{$bmp.Dispose()}
    $script:covers[$script:selectedId]=$name;Save-Covers
    Refresh-Cards
    Set-Status '封面保存成功。重新扫描或重启程序后仍然保留。'
  }finally{$im.Dispose()}
}
function Begin-Drag($sender,$e,[string]$path) {
  if($e.Button -eq [Windows.Forms.MouseButtons]::Left){$script:dragPath=$path;$script:dragMouseStart=[Windows.Forms.Control]::MousePosition}
}
function Continue-Drag($sender,$e){
  if($e.Button -ne [Windows.Forms.MouseButtons]::Left -or $script:dragging -or !$script:dragPath){return}
  $p=[Windows.Forms.Control]::MousePosition
  if(([Math]::Abs($p.X-$script:dragMouseStart.X)+[Math]::Abs($p.Y-$script:dragMouseStart.Y)) -lt 10){return}
  $path=$script:dragPath;$script:dragPath=''
  if(!(Test-Path -LiteralPath $path -PathType Leaf)){Set-Status '原文件不存在或移动硬盘已断开。';return}
  $script:dragging=$true
  try {
    $list=New-Object Collections.Specialized.StringCollection
    [void]$list.Add($path)
    $payload=New-Object Windows.Forms.DataObject
    $payload.SetFileDropList($list)
    $null=$sender.DoDragDrop($payload,[Windows.Forms.DragDropEffects]::Copy)
    Set-Status '拖拽结束。请检查 C4D：按住 Shift 拖入可合并到当前场景。'
  }catch{Set-Status ('拖拽失败：'+$_.Exception.Message)}finally{$script:dragging=$false}
}
function Wire-CardDrag($control,$asset){
  $control.Tag=[pscustomobject]@{id=[string]$asset.id;path=[string]$asset.full;kind=[string]$asset.kind}
  $control.Cursor=[Windows.Forms.Cursors]::Hand
  $control.Add_Click({param($sender,$e) $a=Find-AssetById ([string]$sender.Tag.id);if($a){Select-Asset $a}})
  if($asset.kind -eq '模型') {
    $control.Add_MouseDown({param($sender,$e) Begin-Drag $sender $e ([string]$sender.Tag.path)})
    $control.Add_MouseMove({param($sender,$e) Continue-Drag $sender $e})
    $control.Add_MouseUp({$script:dragPath=''})
  }
}
function Refresh-Cards {
  $script:gallery.SuspendLayout()
  try {
    foreach($old in @($script:gallery.Controls)){foreach($child in @($old.Controls)){if($child -is [Windows.Forms.PictureBox] -and $child.Image){$child.Image.Dispose();$child.Image=$null}};$script:gallery.Controls.Remove($old);$old.Dispose()}
    $script:cardControls=@()
    $term=$script:keyword.Text.Trim()
    $found=@()
    if($script:mode -eq 'image' -and $script:searchRank.Count -gt 0){
      foreach($rank in $script:searchRank){$a=Find-AssetById $rank.id;if($a){$found+=,$a}}
    }else{
      foreach($a in $script:assets){
        if($script:navFilter -eq 'model' -and $a.kind -ne '模型'){continue}
        if($script:navFilter -eq 'image' -and $a.kind -ne '图片'){continue}
        if($script:navFilter.StartsWith('cat:') -and (Category-For $a) -ne $script:navFilter.Substring(4)){continue}
        if($script:navFilter.StartsWith('folder:')){
          $first=([string]$a.rel).Replace('\','/').Split('/')[0]
          if($first -ne $script:navFilter.Substring(7)){continue}
        }
        if(!$term -or ([string]$a.name).IndexOf($term,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or ([string]$a.rel).IndexOf($term,[StringComparison]::OrdinalIgnoreCase) -ge 0){$found+=,$a}}
    }
    $start=[Math]::Min($script:cardOffset,$found.Count)
    $end=[Math]::Min($start+100,$found.Count)
    for($i=$start;$i -lt $end;$i++){
      $a=$found[$i]
      $card=New-Object Windows.Forms.Panel
      $card.Size=[Drawing.Size]::new(220,244)
      $card.Margin=[Windows.Forms.Padding]::new(0)
      $card.BackColor=[Drawing.Color]::White
      $card.BorderStyle=[Windows.Forms.BorderStyle]::FixedSingle
      $image=New-Object Windows.Forms.PictureBox
      $image.Location=[Drawing.Point]::new(10,10);$image.Size=[Drawing.Size]::new(200,158)
      $image.SizeMode=[Windows.Forms.PictureBoxSizeMode]::Zoom
      $image.BackColor=[Drawing.Color]::FromArgb(242,245,250)
      $path=Image-ForAsset $a
      $h=158
      if($path){$bmp=Load-BitmapCopy $path;if($bmp){
        $h=[int][Math]::Max(120,[Math]::Min(270,(200.0*$bmp.Height/[Math]::Max(1,$bmp.Width))))
        $image.Image=$bmp
      }}
      $image.Height=$h;$card.Height=$h+88
      $card.Controls.Add($image)
      $label=New-Object Windows.Forms.Label
      $label.Location=[Drawing.Point]::new(12,($h+19));$label.Size=[Drawing.Size]::new(196,28)
      $label.Text=[string]$a.name;$label.AutoEllipsis=$true
      $label.Font=[Drawing.Font]::new('Microsoft YaHei UI',9,[Drawing.FontStyle]::Bold)
      $card.Controls.Add($label)
      $bottom=New-Object Windows.Forms.Label
      $bottom.Location=[Drawing.Point]::new(12,($h+51));$bottom.Size=[Drawing.Size]::new(195,20)
      $bottom.Text=([string]$a.ext)+'  ·  '+([string]$a.kind)
      $bottom.ForeColor=[Drawing.Color]::FromArgb(115,135,167)
      $bottom.Font=[Drawing.Font]::new('Microsoft YaHei UI',8)
      $card.Controls.Add($bottom)
      foreach($c in @($card,$image,$label,$bottom)){Wire-CardDrag $c $a}
      $script:gallery.Controls.Add($card)
      $script:cardControls+=,$card
    }
    Layout-Masonry
    $script:range.Text=('显示 '+$(if($end -gt 0){$start+1}else{0})+' - '+$end+' / '+$found.Count+' 条')
    $script:prev.Enabled=($start -gt 0)
    $script:next.Enabled=($end -lt $found.Count)
  } finally {$script:gallery.ResumeLayout()}
  if($script:darkMode){Apply-Theme}
}

# Refresh native WinForms colors without changing the source catalog or drag/drop behavior.
function Apply-Theme {
  $night=[bool]$script:darkMode
  $surface=if($night){[Drawing.Color]::FromArgb(31,39,55)}else{[Drawing.Color]::White}
  $canvas=if($night){[Drawing.Color]::FromArgb(19,25,37)}else{[Drawing.Color]::FromArgb(246,248,252)}
  $ink=if($night){[Drawing.Color]::FromArgb(232,238,249)}else{[Drawing.Color]::FromArgb(30,46,75)}
  $muted=if($night){[Drawing.Color]::FromArgb(157,174,197)}else{[Drawing.Color]::FromArgb(99,115,139)}
  $tileImage=if($night){[Drawing.Color]::FromArgb(38,48,65)}else{[Drawing.Color]::FromArgb(242,245,250)}
  $line=if($night){[Drawing.Color]::FromArgb(72,88,111)}else{[Drawing.Color]::FromArgb(220,228,240)}
  $form.BackColor=$canvas
  $top.BackColor=$surface
  $foot.BackColor=$surface
  $pages.BackColor=$canvas
  $script:gallery.BackColor=$canvas
  $title.ForeColor=$ink
  foreach($c in @($subtitle,$hint,$script:modeLabel,$script:range,$script:status)){$c.ForeColor=$muted}
  $script:selection.ForeColor=$ink
  $script:keyword.BackColor=if($night){[Drawing.Color]::FromArgb(42,53,70)}else{[Drawing.Color]::FromArgb(248,250,253)}
  $script:keyword.ForeColor=$ink
  foreach($b in @($paste,$findImage,$clearImage,$updates,$theme,$script:prev,$script:next,$locate)){
    $b.FlatStyle=[Windows.Forms.FlatStyle]::Flat
    $b.FlatAppearance.BorderSize=1
    $b.FlatAppearance.BorderColor=$line
    $b.BackColor=$surface
    $b.ForeColor=$ink
    $b.Cursor=[Windows.Forms.Cursors]::Hand
  }
  $choose.BackColor=[Drawing.Color]::FromArgb(57,95,227)
  $choose.ForeColor=[Drawing.Color]::White
  $choose.FlatAppearance.BorderColor=[Drawing.Color]::FromArgb(57,95,227)
  $theme.Text=if($night){'☀ 白天模式'}else{'☾ 夜晚模式'}
  foreach($card in @($script:gallery.Controls)){
    $card.BackColor=$surface
    foreach($child in @($card.Controls)){
      if($child -is [Windows.Forms.PictureBox]){$child.BackColor=$tileImage}
      elseif($child -is [Windows.Forms.Label]){
        $child.ForeColor=if($child.Font.Bold){$ink}else{$muted}
      }
    }
  }
}

$form=New-Object Windows.Forms.Form
$form.Text=('3D 素材管理器 · 原生 EXE v'+$script:version)
$form.Width=1450;$form.Height=860;$form.MinimumSize=[Drawing.Size]::new(1100,620)
$form.StartPosition='CenterScreen'
$form.BackColor=[Drawing.Color]::FromArgb(246,248,252)
$form.Font=[Drawing.Font]::new('Microsoft YaHei UI',9)
$layout=New-Object Windows.Forms.TableLayoutPanel
$layout.Dock='Fill';$layout.ColumnCount=1;$layout.RowCount=4
$layout.ColumnStyles.Add([Windows.Forms.ColumnStyle]::new([Windows.Forms.SizeType]::Percent,100)) | Out-Null
$layout.RowStyles.Add([Windows.Forms.RowStyle]::new([Windows.Forms.SizeType]::Absolute,158)) | Out-Null
$layout.RowStyles.Add([Windows.Forms.RowStyle]::new([Windows.Forms.SizeType]::Percent,100)) | Out-Null
$layout.RowStyles.Add([Windows.Forms.RowStyle]::new([Windows.Forms.SizeType]::Absolute,48)) | Out-Null
$layout.RowStyles.Add([Windows.Forms.RowStyle]::new([Windows.Forms.SizeType]::Absolute,106)) | Out-Null
$form.Controls.Add($layout)
$top=New-Object Windows.Forms.Panel
$top.Dock='Fill';$top.Height=158;$top.BackColor=[Drawing.Color]::White
$layout.Controls.Add($top,0,0)
$title=New-Object Windows.Forms.Label
$title.Text='我的三维素材库';$title.Font=[Drawing.Font]::new('Microsoft YaHei UI',19,[Drawing.FontStyle]::Bold);$title.ForeColor=[Drawing.Color]::FromArgb(30,46,75)
$title.Location=[Drawing.Point]::new(24,16);$title.Size=[Drawing.Size]::new(270,44)
$top.Controls.Add($title)
$subtitle=New-Object Windows.Forms.Label
$subtitle.Text='本地离线素材墙  ·  模型拖入 C4D 时按 Shift 合并至当前工程'
$subtitle.ForeColor=[Drawing.Color]::FromArgb(98,110,128)
$subtitle.Location=[Drawing.Point]::new(302,30);$subtitle.Size=[Drawing.Size]::new(680,25)
$top.Controls.Add($subtitle)
$choose=New-Object Windows.Forms.Button;$choose.Text='＋  导入素材文件夹';$choose.Location=[Drawing.Point]::new(24,80);$choose.Size=[Drawing.Size]::new(167,39);$top.Controls.Add($choose)
$script:keyword=New-Object Windows.Forms.TextBox
$script:keyword.Location=[Drawing.Point]::new(206,86);$script:keyword.Size=[Drawing.Size]::new(310,29)
$top.Controls.Add($script:keyword)
$hint=New-Object Windows.Forms.Label;$hint.Text='搜索模型名称、文件夹或路径';$hint.ForeColor=[Drawing.Color]::FromArgb(115,127,143)
$hint.Location=[Drawing.Point]::new(208,124);$hint.Size=[Drawing.Size]::new(250,22);$top.Controls.Add($hint)
$paste=New-Object Windows.Forms.Button;$paste.Text='粘贴封面';$paste.Location=[Drawing.Point]::new(532,80);$paste.Size=[Drawing.Size]::new(120,39);$top.Controls.Add($paste)
$findImage=New-Object Windows.Forms.Button;$findImage.Text='以图搜图';$findImage.Location=[Drawing.Point]::new(660,80);$findImage.Size=[Drawing.Size]::new(120,39);$top.Controls.Add($findImage)
$clearImage=New-Object Windows.Forms.Button;$clearImage.Text='全部素材';$clearImage.Location=[Drawing.Point]::new(788,80);$clearImage.Size=[Drawing.Size]::new(98,39);$top.Controls.Add($clearImage)
$updates=New-Object Windows.Forms.Button;$updates.Text='检查更新';$updates.Location=[Drawing.Point]::new(894,80);$updates.Size=[Drawing.Size]::new(107,39);$top.Controls.Add($updates)
$theme=New-Object Windows.Forms.Button
$theme.Text='☾ 夜晚模式';$theme.Location=[Drawing.Point]::new(954,18);$theme.Size=[Drawing.Size]::new(128,36);$top.Controls.Add($theme)
$script:modeLabel=New-Object Windows.Forms.Label
$script:modeLabel.Location=[Drawing.Point]::new(535,125);$script:modeLabel.Size=[Drawing.Size]::new(515,22)
$script:modeLabel.ForeColor=[Drawing.Color]::FromArgb(89,108,152)
$script:modeLabel.Text='粘贴模式：设置选中素材的封面';$top.Controls.Add($script:modeLabel)
# Visual-only WinForms theme: no changes to scanning, search, drag-and-drop or updater.
$accent=New-Object Windows.Forms.Panel
$accent.Location=[Drawing.Point]::new(0,0);$accent.Size=[Drawing.Size]::new(1500,4)
$accent.BackColor=[Drawing.Color]::FromArgb(56,97,229);$top.Controls.Add($accent)
$top.Controls.SetChildIndex($accent,0)
foreach($b in @($choose,$paste,$findImage,$clearImage,$updates,$theme)){
  $b.FlatStyle=[Windows.Forms.FlatStyle]::Flat
  $b.FlatAppearance.BorderSize=1
  $b.FlatAppearance.BorderColor=[Drawing.Color]::FromArgb(220,228,240)
  $b.BackColor=[Drawing.Color]::White
  $b.ForeColor=[Drawing.Color]::FromArgb(53,72,104)
  $b.Font=[Drawing.Font]::new('Microsoft YaHei UI',9,[Drawing.FontStyle]::Regular)
  $b.Cursor=[Windows.Forms.Cursors]::Hand
}
$choose.BackColor=[Drawing.Color]::FromArgb(57,95,227)
$choose.ForeColor=[Drawing.Color]::White
$choose.FlatAppearance.BorderColor=[Drawing.Color]::FromArgb(57,95,227)
$updates.ForeColor=[Drawing.Color]::FromArgb(44,81,211)
$updates.FlatAppearance.BorderColor=[Drawing.Color]::FromArgb(180,195,249)
$script:keyword.BorderStyle=[Windows.Forms.BorderStyle]::FixedSingle
$script:keyword.BackColor=[Drawing.Color]::FromArgb(248,250,253)
$script:keyword.ForeColor=[Drawing.Color]::FromArgb(44,61,87)
$foot=New-Object Windows.Forms.Panel;$foot.Dock='Fill';$foot.Height=106;$foot.BackColor=[Drawing.Color]::White;$layout.Controls.Add($foot,0,3)
$script:selection=New-Object Windows.Forms.Label
$script:selection.Location=[Drawing.Point]::new(26,10);$script:selection.Size=[Drawing.Size]::new(1080,26)
$script:selection.AutoEllipsis=$true;$script:selection.Text='尚未选择素材';$foot.Controls.Add($script:selection)
$locate=New-Object Windows.Forms.Button;$locate.Text='在资源管理器定位';$locate.Location=[Drawing.Point]::new(25,47);$locate.Size=[Drawing.Size]::new(154,34);$foot.Controls.Add($locate)
$script:status=New-Object Windows.Forms.Label
$script:status.Location=[Drawing.Point]::new(195,52);$script:status.Size=[Drawing.Size]::new(960,42)
$script:status.ForeColor=[Drawing.Color]::FromArgb(74,99,135)
$script:status.Text='准备就绪。模型拖拽已使用你此前测试成功的 WinForms FileDrop 机制。'
$foot.Controls.Add($script:status)
$pages=New-Object Windows.Forms.Panel;$pages.Dock='Fill';$pages.Height=48;$pages.BackColor=[Drawing.Color]::FromArgb(246,248,252);$layout.Controls.Add($pages,0,2)
$script:prev=New-Object Windows.Forms.Button;$script:prev.Text='上一页';$script:prev.Location=[Drawing.Point]::new(18,4);$script:prev.Size=[Drawing.Size]::new(80,27);$pages.Controls.Add($script:prev)
$script:next=New-Object Windows.Forms.Button;$script:next.Text='下一页';$script:next.Location=[Drawing.Point]::new(105,4);$script:next.Size=[Drawing.Size]::new(80,27);$pages.Controls.Add($script:next)
$script:range=New-Object Windows.Forms.Label;$script:range.Location=[Drawing.Point]::new(207,12);$script:range.Size=[Drawing.Size]::new(600,23);$script:range.ForeColor=[Drawing.Color]::FromArgb(99,115,139);$pages.Controls.Add($script:range)
$script:gallery=New-Object Windows.Forms.Panel
$script:gallery.Dock='Fill';$script:gallery.AutoScroll=$true
$script:gallery.BackColor=[Drawing.Color]::FromArgb(246,248,252);$script:gallery.Padding=[Windows.Forms.Padding]::new(14,14,14,14)
$center=New-Object Windows.Forms.TableLayoutPanel
$center.Dock='Fill';$center.ColumnCount=3;$center.RowCount=1
[void]$center.ColumnStyles.Add([Windows.Forms.ColumnStyle]::new('Absolute',204))
[void]$center.ColumnStyles.Add([Windows.Forms.ColumnStyle]::new('Percent',100))
[void]$center.ColumnStyles.Add([Windows.Forms.ColumnStyle]::new('Absolute',220))
$layout.Controls.Add($center,0,1)
$script:nav=New-Object Windows.Forms.FlowLayoutPanel
$script:nav.Dock='Fill';$script:nav.AutoScroll=$true;$script:nav.WrapContents=$false
$script:nav.FlowDirection='TopDown';$script:nav.BackColor=[Drawing.Color]::White
$center.Controls.Add($script:nav,0,0)
$center.Controls.Add($script:gallery,1,0)
$script:gallery.Add_Resize({Layout-Masonry})
$details=New-Object Windows.Forms.Panel;$details.Dock='Fill';$details.BackColor=[Drawing.Color]::White
$center.Controls.Add($details,2,0)
$script:detail=New-Object Windows.Forms.Label
$script:detail.Location=[Drawing.Point]::new(13,22);$script:detail.Size=[Drawing.Size]::new(192,380)
$script:detail.Text='素材详情'+[Environment]::NewLine+[Environment]::NewLine+'选中卡片后显示模型位置与信息。'
$details.Controls.Add($script:detail)
$categoryTitle=New-Object Windows.Forms.Label
$categoryTitle.Text='虚拟分类';$categoryTitle.Location=[Drawing.Point]::new(13,411);$categoryTitle.Width=180
$details.Controls.Add($categoryTitle)
$script:catPick=New-Object Windows.Forms.ComboBox
$script:catPick.DropDownStyle='DropDownList'
$script:catPick.Location=[Drawing.Point]::new(13,442);$script:catPick.Width=190;$script:catPick.Enabled=$false
$details.Controls.Add($script:catPick)
$saveCat=New-Object Windows.Forms.Button
$saveCat.Text='保存分类';$saveCat.Location=[Drawing.Point]::new(13,479);$saveCat.Width=190
$saveCat.Add_Click({
  $a=Find-AssetById $script:selectedId
  if(!$a){return}
  $id=[string]$a.id
  $choice=[string]$script:catPick.SelectedItem
  $before=Category-For $a
  if($choice -eq '未分类'){$script:categoryMap.Remove($id)|Out-Null}else{$script:categoryMap[$id]=$choice}
  try{Save-Categories;Refresh-Cards;Refresh-PreviewNav;Set-Status ('分类已保存：'+$choice)}
  catch{
    if($before -eq '未分类'){$script:categoryMap.Remove($id)|Out-Null}else{$script:categoryMap[$id]=$before}
    Show-Problem ('分类保存失败：'+$_.Exception.Message)
  }
})
$details.Controls.Add($saveCat)
function Refresh-PreviewNav {
  $script:nav.SuspendLayout()
  try {
    $script:nav.Controls.Clear()
    $items=@(
      [pscustomobject]@{Name='全部素材';Value='all'},
      [pscustomobject]@{Name='模型';Value='model'},
      [pscustomobject]@{Name='图片';Value='image'}
    )
    $items+=,[pscustomobject]@{Name='未分类';Value='cat:未分类'}
    foreach($c in $script:categories){$items+=,[pscustomobject]@{Name=('分类 · '+$c);Value=('cat:'+$c)}}
    $folders=@($script:assets | Where-Object {([string]$_.rel).Contains('/')} | ForEach-Object {([string]$_.rel).Split('/')[0]} | Select-Object -Unique | Select-Object -First 16)
    foreach($folder in $folders){$items+=,[pscustomobject]@{Name=('目录 · '+$folder);Value=('folder:'+$folder)}}
    foreach($item in $items){
      $button=New-Object Windows.Forms.Button
      $button.Width=180;$button.Height=35;$button.Text=$item.Name;$button.Tag=$item.Value
      $button.FlatStyle='Flat';$button.FlatAppearance.BorderSize=0
      $button.BackColor=if($item.Value -eq $script:navFilter){[Drawing.Color]::FromArgb(222,231,251)}else{[Drawing.Color]::White}
      $button.Margin=[Windows.Forms.Padding]::new(5,3,4,3)
      $button.Add_Click({param($sender,$e) $script:navFilter=[string]$sender.Tag;$script:cardOffset=0;Refresh-Cards;Refresh-PreviewNav})
      $script:nav.Controls.Add($button)
    }
    $newCategory=New-Object Windows.Forms.Button
    $newCategory.Text='＋ 新建虚拟分类';$newCategory.Width=180;$newCategory.Height=34
    $newCategory.Margin=[Windows.Forms.Padding]::new(5,8,4,3)
    $newCategory.Add_Click({
      try{
        Add-Type -AssemblyName Microsoft.VisualBasic
        $name=[Microsoft.VisualBasic.Interaction]::InputBox('分类只保存在本机，不移动磁盘上的模型。','新建虚拟分类','').Trim()
        if(!$name){return}
        if($name.Length -gt 25 -or $script:categories -contains $name){Show-Problem '分类名重复或超过25字。';return}
        $script:categories+=,$name
        Save-Categories
        Refresh-PreviewNav
        Set-Status ('已创建虚拟分类：'+$name)
      }catch{Show-Problem ('新建分类失败：'+$_.Exception.Message)}
    })
    $script:nav.Controls.Add($newCategory)
    $note=New-Object Windows.Forms.Label;$note.Width=180;$note.Height=84
    $note.Text='虚拟分类保存在本机，真实目录只读。不会移动原始模型。'
    $script:nav.Controls.Add($note)
  }finally{$script:nav.ResumeLayout()}
}
$script:keyword.Add_TextChanged({$script:mode='cover';$script:cardOffset=0;Refresh-Cards})
$choose.Add_Click({
  $picker=New-Object Windows.Forms.FolderBrowserDialog
  $picker.Description='选择素材文件夹。原始文件不会被修改。';$picker.ShowNewFolderButton=$false
  if($script:summary.root -and (Test-Path -LiteralPath ([string]$script:summary.root))){$picker.SelectedPath=[string]$script:summary.root}
  try {
    if($picker.ShowDialog($form) -eq [Windows.Forms.DialogResult]::OK){
      Set-Status '正在扫描，首次扫描大目录可能需要一些时间……'
      try {Scan-Folder $picker.SelectedPath;$script:cardOffset=0;$script:searchRank=@();$script:keyword.Text='';Refresh-Cards;Refresh-PreviewNav;Set-Status ('扫描完成：'+$script:assets.Count+' 条素材，隐藏 '+$script:summary.suppressedTextures+' 个贴图文件。')}
      catch {Show-Problem ('扫描失败：'+$_.Exception.Message);Set-Status '扫描失败。原始素材未修改。'}
    }
  }finally {$picker.Dispose()}
})
$paste.Add_Click({try{Cover-FromClipboard}catch{Show-Problem $_.Exception.Message}})
$findImage.Add_Click({
  if(-not [Windows.Forms.Clipboard]::ContainsImage()){Show-Problem '先截图或复制一张图片，再点击「截图相似搜索」。';return}
  $im=[Windows.Forms.Clipboard]::GetImage()
  try{Sort-ByImage $im}catch{Show-Problem ('相似搜索失败：'+$_.Exception.Message)}finally{if($im){$im.Dispose()}}
  $script:modeLabel.Text='当前：截图相似搜索（测试算法）'
})
$clearImage.Add_Click({$script:searchRank=@();$script:mode='cover';$script:cardOffset=0;$script:keyword.Text='';$script:modeLabel.Text='粘贴模式：设置选中素材的封面';Refresh-Cards})
$locate.Add_Click({
  $a=Find-AssetById $script:selectedId
  if(!$a){Show-Problem '请先选择一个素材。';return}
  if(!(Test-Path -LiteralPath $a.full)){Show-Problem '文件不存在：可能是移动硬盘未连接。';return}
  Start-Process -FilePath 'explorer.exe' -ArgumentList ('/select,"'+$a.full+'"')
})
$script:prev.Add_Click({$script:cardOffset=[Math]::Max(0,$script:cardOffset-100);Refresh-Cards})
$script:next.Add_Click({$script:cardOffset+=100;Refresh-Cards})
$form.KeyPreview=$true
$form.Add_KeyDown({param($sender,$e)
  if($e.Control -and $e.KeyCode -eq [Windows.Forms.Keys]::V){
    $e.SuppressKeyPress=$true
    try{Cover-FromClipboard}catch{Set-Status ('粘贴失败：'+$_.Exception.Message)}
  }
})
$updates.Add_Click({
  Set-Status '正在检查原生 EXE 更新……'
  try {
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    $m=Invoke-RestMethod -Uri $script:manifestUrl -TimeoutSec 15
    if([version]$m.version -le [version]$script:version){Set-Status ('当前已是最新版 '+$script:version);return}
    if(!$m.scriptUrl -or !$m.sha256){Set-Status '发现新版本，但尚无可校验的更新文件。';return}
    $uri=[Uri]([string]$m.scriptUrl)
    $prefix='https://raw.githubusercontent.com/lvbihiu-ctrl/3d-asset-library/'
    if($uri.Scheme -ne 'https' -or !$uri.AbsoluteUri.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {throw '更新文件必须来自本项目的 GitHub HTTPS 地址'}
    $result=[Windows.Forms.MessageBox]::Show(('发现 '+$m.version+' 版。是否在线更新并重新启动？'),'更新',[Windows.Forms.MessageBoxButtons]::YesNo,[Windows.Forms.MessageBoxIcon]::Question)
    if($result -ne [Windows.Forms.DialogResult]::Yes){Set-Status '已取消更新。';return}
    $rt=Join-Path $script:installRoot '.runtime'
    if(!(Test-Path -LiteralPath $rt)){[void](New-Item -ItemType Directory -Path $rt)}
    $download=Join-Path $rt 'override.new'
    Invoke-WebRequest -Uri $uri.AbsoluteUri -OutFile $download -UseBasicParsing -TimeoutSec 90
    $actual=(Get-FileHash -Algorithm SHA256 -LiteralPath $download).Hash.ToLowerInvariant()
    if($actual -ne ([string]$m.sha256).ToLowerInvariant()){Remove-Item -LiteralPath $download -Force;throw 'SHA256 校验失败，旧版本保留。'}
    $installed=Join-Path $rt 'override.ps1'
    $hashPath=Join-Path $rt 'override.sha256'
    $oldOverride=Join-Path $rt 'override.backup.ps1'
    $oldHash=Join-Path $rt 'override.backup.sha256'
    if(Test-Path -LiteralPath $installed){Copy-Item -LiteralPath $installed -Destination $oldOverride -Force}
    if(Test-Path -LiteralPath $hashPath){Copy-Item -LiteralPath $hashPath -Destination $oldHash -Force}
    Move-Item -LiteralPath $download -Destination $installed -Force
    [IO.File]::WriteAllText($hashPath,$actual,$script:utf8)
    Set-Status ('更新到 '+$m.version+'，正在重新打开客户端……')
    # The stable launcher waits for this PowerShell process to exit before
    # acquiring the single-instance mutex. This avoids starting two clients or
    # trying to overwrite the EXE that is still running.
    Start-Process -FilePath (Join-Path $script:installRoot '3D素材管理器.exe') -ArgumentList @('--restart-after',[string]$PID)
    $form.Close()
  }catch {Set-Status ('检查/更新失败：'+$_.Exception.Message)}
})
$theme.Add_Click({
  $script:darkMode=-not $script:darkMode
  Apply-Theme
  try {
    $folder=Split-Path -Parent $script:themePrefPath
    if(-not (Test-Path -LiteralPath $folder)){[void](New-Item -ItemType Directory -Path $folder -Force)}
    $mode=if($script:darkMode){'dark'}else{'light'}
    [IO.File]::WriteAllText($script:themePrefPath,$mode,$script:utf8)
  } catch {Set-Status ('模式切换成功，但无法保存偏好：'+$_.Exception.Message)}
})
$form.Add_FormClosed({
  foreach($card in @($script:cardControls)){foreach($c in @($card.Controls)){if($c -is [Windows.Forms.PictureBox] -and $c.Image){$c.Image.Dispose()}}}
})
try {
  if($script:smoke){
    $fakeModels=Join-Path $script:dataRoot 'fixture-models'
    [void](New-Item -ItemType Directory -Force -Path $fakeModels)
    $fakeA=Join-Path $fakeModels 'demo_phone_a.c4d'
    $fakeB=Join-Path $fakeModels 'demo_phone_b.fbx'
    [IO.File]::WriteAllText($fakeA,'synthetic smoke data - not a real model')
    [IO.File]::WriteAllText($fakeB,'synthetic smoke data - not a real model')
    $bmp=New-Object Drawing.Bitmap(160,110)
    $gfx=[Drawing.Graphics]::FromImage($bmp)
    try{$gfx.Clear([Drawing.Color]::CornflowerBlue);$bmp.Save((Join-Path $script:coverRoot 'synthetic.jpg'),[Drawing.Imaging.ImageFormat]::Jpeg)}
    finally{$gfx.Dispose();$bmp.Dispose()}
    $idA=Get-Key $fakeA.ToLowerInvariant();$idB=Get-Key $fakeB.ToLowerInvariant()
    $fixture=@(
      [pscustomobject]@{id=$idA;name='演示手机A';kind='模型';ext='C4D';rel='手机/演示手机A.c4d';full=$fakeA;preview='';bytes=10;changed='2026-01-01';resources=''},
      [pscustomobject]@{id=$idB;name='演示手机B';kind='模型';ext='FBX';rel='手机/演示手机B.fbx';full=$fakeB;preview='';bytes=20;changed='2026-01-01';resources=''}
    )
    $script:assets=$fixture
    $script:summary=@{root=$fakeModels;assetCount=2}
    $script:covers=@{};$script:covers[$idA]='synthetic.jpg'
    Save-Catalog;Save-Covers
    $script:categoryMap=@{};$script:categoryMap[$idA]='手机数码'
    Save-Categories
    $script:assets=@();$script:covers=@{};$script:categoryMap=@{}
  }
  Load-Covers;Load-Catalog;Load-Categories;Refresh-Cards;Refresh-PreviewNav;Apply-Theme
  if($script:smoke){
    if(!$script:gallery -or !$script:nav -or !$script:catPick){throw 'Smoke: missing widgets'}
    if($script:assets.Count -ne 2){throw 'Smoke: expected two fixture models'}
    if($script:gallery.Controls.Count -ne 2){throw 'Smoke: expected two cards'}
    if($script:covers.Count -ne 1){throw 'Smoke: cover map not persisted'}
    if((Category-For $script:assets[0]) -ne '手机数码'){throw 'Smoke: category mapping not persisted'}
    $script:navFilter='cat:手机数码'
    Refresh-Cards
    if($script:gallery.Controls.Count -ne 1){throw 'Smoke: category filtering failed'}
    $script:navFilter='all';Refresh-Cards
    Write-Host ('SMOKE_PASS: v'+$script:version+' assets='+$script:assets.Count+' nav='+$script:nav.Controls.Count+' cover=1 category=1')
    return
  }
  if($script:assets.Count){Set-Status ('已加载旧素材索引：'+$script:assets.Count+' 条。可继续选择文件夹重新扫描。')}
  [void]$form.ShowDialog()
}catch{Show-Problem ('客户端发生错误：'+$_.Exception.Message)}
finally{$form.Dispose()}
