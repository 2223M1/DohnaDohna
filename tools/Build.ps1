#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('stable','preview')][string]$Channel = 'preview',
    [string]$DataDirectory,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $DataDirectory) {
    $settingsPath = Join-Path $root '.local/hosts.json'
    if (-not (Test-Path -LiteralPath $settingsPath)) { throw 'Copy eng/hosts.example.json to .local/hosts.json and fill host paths, or pass -DataDirectory.' }
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $DataDirectory = $settings."${Channel}DataDir"
}
if (-not $DataDirectory -or -not (Test-Path -LiteralPath (Join-Path $DataDirectory 'sts2.dll'))) { throw "Missing real $Channel host input: $DataDirectory" }
$dataPath = (Resolve-Path -LiteralPath $DataDirectory).Path
New-Item -ItemType Directory -Path (Join-Path $root 'build') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $root 'build/.gdignore') -Value '# Generated build output is not a Godot resource.' -Encoding utf8NoBOM
Push-Location $root
try {
    & dotnet build DohnaDohna.csproj -c $Configuration -v:minimal "-p:DohnaDohnaHostChannel=$Channel" "-p:Sts2DataDir=$dataPath"
    if ($LASTEXITCODE -ne 0) { throw "DohnaDohna $Channel build failed ($LASTEXITCODE)." }
} finally { Pop-Location }
