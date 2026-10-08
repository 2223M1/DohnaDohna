#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('stable','preview')][string]$Channel,
    [string]$DataDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Build.ps1') -Channel $Channel -DataDirectory $DataDirectory
$manifest = Get-Content -LiteralPath (Join-Path $root 'DohnaDohna.json') -Raw | ConvertFrom-Json
if (-not $manifest.has_pck) { throw 'DohnaDohna gameplay requires its resource PCK.' }
$hosts = Get-Content -LiteralPath (Join-Path $root '.local/hosts.json') -Raw | ConvertFrom-Json
$pck = Join-Path $root "build/DohnaDohna.$Channel.pck"
$priorInput = $env:Sts2DataDir
$priorChannel = $env:DohnaDohnaHostChannel
try {
    $env:Sts2DataDir = if ($DataDirectory) { $DataDirectory } else { $hosts."${Channel}DataDir" }
    $env:DohnaDohnaHostChannel = $Channel
    & $hosts.godotConsole --headless --path $root --editor --import *> (Join-Path $root "build/godot-import.$Channel.log")
    if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
    & $hosts.godotConsole --headless --path $root --export-pack 'Mod Resources' $pck *> (Join-Path $root "build/godot-export.$Channel.log")
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $pck)) { throw 'Godot resource export failed.' }
    if (Select-String -LiteralPath (Join-Path $root "build/godot-export.$Channel.log") -Pattern 'ERROR:|System\..*Exception') { throw 'Godot export logged errors; package rejected.' }
} finally {
    $env:Sts2DataDir = $priorInput
    $env:DohnaDohnaHostChannel = $priorChannel
}
& python (Join-Path $root 'tools/verify-pck.py') $pck
if ($LASTEXITCODE -ne 0) { throw 'PCK contract failed.' }
$target = Join-Path $root "build/packages/$Channel/DohnaDohna"
New-Item -ItemType Directory -Path $target -Force | Out-Null
$allowed = @('DohnaDohna.dll','DohnaDohna.json','DohnaDohna.pck','SHA256SUMS')
$unexpected = @(Get-ChildItem -LiteralPath $target -Force | Where-Object Name -NotIn $allowed)
if ($unexpected.Count) { throw "Package directory has unexpected files; inspect it before packaging: $target" }
$manifest.min_game_version = if ($Channel -eq 'stable') { '0.107.1' } else { '0.111.0' }
Copy-Item -LiteralPath (Join-Path $root "build/bin/$Channel/Release/net9.0/DohnaDohna.dll") -Destination (Join-Path $target 'DohnaDohna.dll')
Copy-Item -LiteralPath $pck -Destination (Join-Path $target 'DohnaDohna.pck')
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $target 'DohnaDohna.json') -Encoding utf8NoBOM
$hashes = foreach ($name in @('DohnaDohna.dll','DohnaDohna.json','DohnaDohna.pck')) { '{0}  {1}' -f (Get-FileHash -LiteralPath (Join-Path $target $name) -Algorithm SHA256).Hash.ToLowerInvariant(), $name }
$hashes | Set-Content -LiteralPath (Join-Path $target 'SHA256SUMS') -Encoding utf8NoBOM
Write-Output "Created per-host DLL/PCK development package: $target"
Write-Output 'No installation or publication was performed. Do not use this per-host package as a dual-host Workshop release.'
