#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$GameDirectory = 'C:/Program Files/steam/steamapps/common/Slay the Spire 2',
    [string]$SettingsFile
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$gameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path
$release = Get-Content -LiteralPath (Join-Path $gameRoot 'release_info.json') -Raw | ConvertFrom-Json
$channel = switch ($release.version) {
    'v0.111.0' { 'preview' }
    'v0.107.1' { 'stable' }
    default { throw "Unsupported installed host $($release.version). No files were installed." }
}
if (Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue) { throw 'Close Slay the Spire 2 before installing.' }
$package = Join-Path $projectRoot "build/packages/$channel/DohnaDohna"
$names = @('DohnaDohna.dll', 'DohnaDohna.pck', 'DohnaDohna.json')
$checksums = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $package 'SHA256SUMS')) {
    if ($line -notmatch '^([0-9a-f]{64})  (DohnaDohna\.(dll|pck|json))$') { throw "Invalid package checksum entry: $line" }
    $checksums[$Matches[2]] = $Matches[1]
}
foreach ($name in $names) {
    if ((Get-FileHash -LiteralPath (Join-Path $package $name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $checksums[$name]) {
        throw "Candidate package checksum mismatch: $name"
    }
}
$manifest = Get-Content -LiteralPath (Join-Path $package 'DohnaDohna.json') -Raw | ConvertFrom-Json
if ($manifest.id -ne 'DohnaDohna' -or $manifest.min_game_version -ne $release.version.TrimStart('v')) { throw 'Candidate identity/host mismatch.' }
$target = Join-Path $gameRoot 'mods/DohnaDohna'
$backup = Join-Path $projectRoot ('build/local-install/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
if (Test-Path -LiteralPath $target) {
    if ((Get-Item -LiteralPath $target).LinkType) { throw 'Existing mod directory is a link; inspect it before installing.' }
    Copy-Item -LiteralPath $target -Destination (Join-Path $backup 'previous-mod') -Recurse
}
$config = $null
if ($SettingsFile) {
    $settingsPath = (Resolve-Path -LiteralPath $SettingsFile).Path
    if ((Split-Path $settingsPath -Leaf) -ne 'settings.save') { throw 'Only the game settings.save can be changed; gameplay saves are not installation targets.' }
    $config = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json -AsHashtable
    Copy-Item -LiteralPath $settingsPath -Destination (Join-Path $backup 'settings.save')
    $beforeMods = $config.mod_settings | ConvertTo-Json -Depth 20
    if (-not $config.mod_settings) { $config.mod_settings = @{mod_list=@();mods_enabled=$true} }
    $matching = @($config.mod_settings.mod_list | Where-Object id -eq 'DohnaDohna')
    if ($matching.Count -gt 1) { throw 'Duplicate DohnaDohna preferences; inspect them before installing.' }
    if ($matching.Count -eq 1) { $matching[0].is_enabled = $true; $matching[0].source = 'mods_directory' }
    else { $config.mod_settings.mod_list = @($config.mod_settings.mod_list) + @{id='DohnaDohna';is_enabled=$true;source='mods_directory'} }
    $config.mod_settings.mods_enabled = $true
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
try {
    foreach ($name in $names + 'SHA256SUMS') { Copy-Item -LiteralPath (Join-Path $package $name) -Destination (Join-Path $target $name) -Force }
    foreach ($name in $names) {
        if ((Get-FileHash -LiteralPath (Join-Path $target $name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $checksums[$name]) { throw "Installed checksum mismatch: $name" }
    }
    if ($config) {
        [System.IO.File]::WriteAllText($settingsPath, ($config | ConvertTo-Json -Depth 30), [System.Text.UTF8Encoding]::new($false))
        $verified = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json -AsHashtable
        if (@($verified.mod_settings.mod_list | Where-Object { $_.id -eq 'DohnaDohna' -and $_.is_enabled -and $_.source -eq 'mods_directory' }).Count -ne 1) { throw 'Installed preferences were not persisted.' }
    }
    $receipt = @{host=$release.version;channel=$channel;target=$target;backup=$backup;files=$checksums;settings=$SettingsFile;previousModSettings=$beforeMods;timestamp=[DateTimeOffset]::Now.ToString('o')}
    [System.IO.File]::WriteAllText((Join-Path $backup 'receipt.json'), ($receipt | ConvertTo-Json -Depth 20), [System.Text.UTF8Encoding]::new($false))
} catch {
    if ($config) { Copy-Item -LiteralPath (Join-Path $backup 'settings.save') -Destination $settingsPath -Force }
    if (Test-Path -LiteralPath (Join-Path $backup 'previous-mod')) {
        Get-ChildItem -LiteralPath (Join-Path $backup 'previous-mod') -File | Copy-Item -Destination $target -Force
    } else {
        # Recoverable rollback of the newly-created exact mod directory.
        Move-Item -LiteralPath $target -Destination (Join-Path $backup 'failed-install')
    }
    throw
}
Write-Output "Installed $($manifest.id) $($manifest.version) for $($release.version): $target"
Write-Output "Backup/receipt: $backup. No gameplay save was modified. No publication was performed."
