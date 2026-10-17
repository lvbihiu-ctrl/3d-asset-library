param(
    [string]$InstallRoot = 'D:\Codex\3D-Asset-Library-EXE'
)

$ErrorActionPreference = 'Stop'
$runtime = Join-Path $InstallRoot '.runtime'
$source = Join-Path $runtime 'app-v0.1.2.ps1'
$target = Join-Path $runtime 'app-v0.1.3.ps1'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Runtime source not found: $source"
}

$text = Get-Content -LiteralPath $source -Raw -Encoding UTF8
$text = $text.Replace("`$script:version = '0.1.2'", "`$script:version = '0.1.3'")
$restartPattern = "(?m)^(\s*Start-Process -FilePath \(Join-Path \`$script:installRoot ')([^']+)-v0\.1\.2(\.exe'\))\s*$"
$text = [regex]::Replace($text, $restartPattern, {
    param($match)
    $match.Groups[1].Value + $match.Groups[2].Value + $match.Groups[3].Value +
        " -ArgumentList @('--restart-after',[string]`$PID)"
})
if ($text -notmatch '(?m)\$script:version\s*=\s*''0\.1\.3''') {
    throw 'Runtime version migration did not apply.'
}
if ($text -notmatch '--restart-after' -or $text -match "(?m)Start-Process.*-v0\.1\.2\.exe") {
    throw 'Safe restart migration did not apply.'
}

$utf8 = New-Object Text.UTF8Encoding($true)
[IO.File]::WriteAllText($target, $text, $utf8)
Write-Host "Created $target"
