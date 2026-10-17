# Execute the real updater functions without launching WinForms or touching user files.
$ErrorActionPreference='Stop'
$source=(Resolve-Path (Join-Path $PSScriptRoot '..\app-v0.1.7.ps1')).Path
$tokens=$null;$parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($source,[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count){throw "Syntax errors: $($parseErrors.Count)"}
foreach($name in @('Assert-UpdateManifest','Get-FreshUpdateManifest')){
  $fn=$ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name},$true) | Select-Object -First 1
  if(!$fn){throw "Missing function: $name"}
  Invoke-Expression $fn.Extent.Text
}
$script:version='0.1.7'
$script:manifestUrl='https://raw.githubusercontent.com/lvbihiu-ctrl/3d-asset-library/refs/heads/main/desktop-updates/native/latest.json'
$script:manifestApiUrl='https://api.github.com/repos/lvbihiu-ctrl/3d-asset-library/contents/desktop-updates/native/latest.json?ref=main'
function New-Fixture([string]$version){
  return [pscustomobject]@{
    schema=1; version=$version;
    scriptUrl=('https://raw.githubusercontent.com/lvbihiu-ctrl/3d-asset-library/'+('a'*40)+'/desktop-updates/native/app-v'+$version+'.ps1');
    sha256=('b'*64)
  }
}
$script:rawVersion='0.1.7'
$script:apiVersion='0.1.8'
$script:rawUnavailable=$false
$script:apiUnavailable=$false
$script:rawCalls=0
$script:apiCalls=0
function Invoke-RestMethod {
  param([string]$Uri,$Headers,[int]$TimeoutSec,[string]$ErrorAction)
  if($Uri -like '*raw.githubusercontent.com*'){
    $script:rawCalls++
    if($Uri -notlike '*cacheBust=*'){throw 'Cache-buster missing on raw URL'}
    if($script:rawUnavailable){throw 'Simulated CDN connection failure'}
    return (New-Fixture $script:rawVersion)
  }
  if($Uri -like '*api.github.com*'){
    $script:apiCalls++
    if($Uri -notlike '*cacheBust=*'){throw 'Cache-buster missing on API URL'}
    if($script:apiUnavailable){throw 'Simulated API connection failure'}
    $payload=(New-Fixture $script:apiVersion)|ConvertTo-Json -Compress
    return [pscustomobject]@{encoding='base64';content=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($payload))}
  }
  throw "Unexpected URL $Uri"
}
# Old CDN cache, API has new version.
$m=Get-FreshUpdateManifest
if($m.version -ne '0.1.8' -or $script:rawCalls -ne 1 -or $script:apiCalls -ne 1){throw 'FAIL stale CDN/API recovery'}
Write-Host 'PASS stale CDN recovered using API'
# Raw already fresh: do not consume GitHub API rate limit.
$script:rawVersion='0.1.9';$script:apiVersion='0.1.8';$script:apiCalls=0
$m=Get-FreshUpdateManifest
if($m.version -ne '0.1.9' -or $script:apiCalls -ne 0){throw 'FAIL fresh CDN fast path'}
Write-Host 'PASS fast path and API quota savings'
# CDN offline, GitHub API succeeds.
$script:rawUnavailable=$true;$script:apiVersion='0.2.0'
$m=Get-FreshUpdateManifest
if($m.version -ne '0.2.0'){throw 'FAIL CDN offline fallback'}
Write-Host 'PASS CDN unavailable fallback'
# API cannot confirm old CDN: show error rather than falsely claim up-to-date.
$script:rawUnavailable=$false;$script:rawVersion='0.1.7';$script:apiUnavailable=$true
$thrown=$false
try {$null=Get-FreshUpdateManifest}catch{$thrown=$true}
if(!$thrown){throw 'FAIL unverified latest claim was accepted'}
Write-Host 'PASS API unavailable rejects false latest'
# Invalid SHA/URL must be rejected.
$bad=New-Fixture '0.1.8';$bad.sha256='1234'
$rejected=$false
try {$null=Assert-UpdateManifest $bad}catch{$rejected=$true}
if(!$rejected){throw 'FAIL invalid checksum accepted'}
Write-Host 'PASS invalid manifest rejected'
