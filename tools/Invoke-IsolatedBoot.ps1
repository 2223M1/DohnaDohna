#requires -Version 7.0
[CmdletBinding()]
param([ValidateRange(1,1800)][int]$Seconds = 20, [switch]$Probe, [switch]$Rendered, [switch]$Interactive, [switch]$FreshProfile,
    [string]$InteractiveProfile,
    [string]$ModPackageDirectory, [switch]$MenuFlow, [string[]]$AdditionalModDirectory = @(),
    [string[]]$MenuRoster = @('kuma','alyce','antena','tora'), [switch]$CaptureAudio, [switch]$CaptureMovie,
    [ValidateSet('Full','Boundaries','Presentation','Recovery','Lifecycle','Motion','Feedback','Finisher','Routing','Alignment','Shadows','Impacts','Scaling','Display','Pacing','Catalog','CatalogCards','Relics','CatalogRules','Costs','Core')][string]$CommandScenario = 'Full')
$ErrorActionPreference = 'Stop'
if ($MenuFlow) { $Probe = $true; $Rendered = $true }
if ($CaptureAudio) { $Probe = $true; $Rendered = $true }
if ($CaptureMovie) { $Probe = $true; $Rendered = $true }
if ($CommandScenario -ne 'Full') {
    if ($MenuFlow -or $Interactive) { throw 'Command scenarios cannot run with menu/manual flow.' }
    $Probe = $true; $Rendered = $true
}
$commandPassMarker = if ($CommandScenario -eq 'Full') { 'DOHNA_SMOKE_COMBAT_PASS' } else { 'DOHNA_SMOKE_' + $CommandScenario.ToUpperInvariant() + '_PASS' }
if ($Interactive -and ($Probe -or $Rendered)) { throw 'Interactive play must not load the test driver or the background desktop helper.' }
if ($FreshProfile -and -not $Interactive) { throw 'FreshProfile is only for manual isolated acceptance.' }
$projectRoot = Split-Path $PSScriptRoot -Parent
$sessionRoot = Join-Path $projectRoot 'build/smoke/preview'
if ($InteractiveProfile) {
    if (-not $Interactive -or $FreshProfile) { throw 'InteractiveProfile only resumes an existing manual isolated profile.' }
    $manualProfile = Get-Item -LiteralPath $InteractiveProfile
    if (-not $manualProfile.PSIsContainer -or $manualProfile.LinkType -or
        $manualProfile.Parent.FullName -ne [System.IO.Path]::GetFullPath($sessionRoot) -or
        $manualProfile.Name -notmatch '^manual-flow-\d{8}-\d{6}-\d{3}$') {
        throw 'Only an existing manual-flow profile under build/smoke/preview can be resumed.'
    }
}
$gameRoot = Join-Path $sessionRoot 'game'
if (Get-CimInstance Win32_Process -Filter "Name = 'SlayTheSpire2.exe'" | Where-Object {
    $_.ExecutablePath -and [System.IO.Path]::GetDirectoryName($_.ExecutablePath) -eq [System.IO.Path]::GetFullPath($gameRoot)
}) { throw 'The isolated game is still running; do not replace its loaded package.' }
$sourceGame = 'C:/Program Files/steam/steamapps/common/Slay the Spire 2'
New-Item -ItemType Directory -Path $gameRoot -Force | Out-Null
foreach ($file in Get-ChildItem -LiteralPath $sourceGame -File) {
    $destination = Join-Path $gameRoot $file.Name
    if (-not (Test-Path -LiteralPath $destination)) { Copy-Item -LiteralPath $file.FullName -Destination $destination }
}
$sourceData = Join-Path $sourceGame 'data_sts2_windows_x86_64'
$destinationData = Join-Path $gameRoot 'data_sts2_windows_x86_64'
if (-not (Test-Path -LiteralPath $destinationData)) { Copy-Item -LiteralPath $sourceData -Destination $destinationData -Recurse }
$mods = Join-Path $gameRoot 'mods'
New-Item -ItemType Directory -Path $mods -Force | Out-Null
$ownMod = Join-Path $mods 'DohnaDohna'
New-Item -ItemType Directory -Path $ownMod -Force | Out-Null
$packageDirectory = if ($ModPackageDirectory) { (Resolve-Path -LiteralPath $ModPackageDirectory).Path } else { Join-Path $projectRoot 'build/packages/preview/DohnaDohna' }
Get-ChildItem -LiteralPath $packageDirectory | Copy-Item -Destination $ownMod -Recurse -Force
$ritsuMod = Join-Path $mods 'STS2-RitsuLib'
New-Item -ItemType Directory -Path $ritsuMod -Force | Out-Null
Get-ChildItem -LiteralPath 'C:/Program Files/steam/steamapps/workshop/content/2868840/3747602295' | Copy-Item -Destination $ritsuMod -Recurse -Force
$additionalMods = foreach ($directory in $AdditionalModDirectory) {
    $resolved = (Resolve-Path -LiteralPath $directory).Path
    $manifests = @(Get-ChildItem -LiteralPath $resolved -Filter '*.json' -File | Where-Object {
        (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json).id
    })
    if ($manifests.Count -ne 1) { throw "Expected one mod manifest: $resolved" }
    $id = (Get-Content -LiteralPath $manifests[0].FullName -Raw | ConvertFrom-Json).id
    if ($id -notmatch '^[A-Za-z0-9_-]+$' -or $id -in @('DohnaDohna','STS2-RitsuLib','DohnaDohna-SmokeDriver')) { throw "Invalid additional mod id: $id" }
    $destination = Join-Path $mods $id
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath $resolved | Copy-Item -Destination $destination -Recurse -Force
    @{id=$id;is_enabled=$true;source='mods_directory'}
}
$allowedModFolders = @('DohnaDohna','STS2-RitsuLib') + @($additionalMods | ForEach-Object id)
if ($Probe) { $allowedModFolders += 'DohnaDohna-SmokeDriver' }
# The host can auto-enable a discovered mod absent from its settings list. Keep
# the copied installation's mod set exact; archive leftovers recoverably.
$leftovers = @(Get-ChildItem -LiteralPath $mods -Directory | Where-Object Name -NotIn $allowedModFolders)
if ($leftovers.Count) {
    $disabledRoot = Join-Path $sessionRoot ('disabled-test-mods-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $disabledRoot | Out-Null
    foreach ($folder in $leftovers) {
        if ($folder.LinkType -or $folder.Parent.FullName -ne [System.IO.Path]::GetFullPath($mods)) { throw 'Unexpected isolated mod path; do not move it.' }
        Move-Item -LiteralPath $folder.FullName -Destination (Join-Path $disabledRoot $folder.Name)
    }
    Write-Output "Recoverably archived unrequested isolated test mods: $disabledRoot"
}
$probeRoot = Join-Path $mods 'DohnaDohna-SmokeDriver'
if (-not $Probe -and (Test-Path -LiteralPath $probeRoot)) {
    $resolvedProbe = (Resolve-Path -LiteralPath $probeRoot).Path
    $expectedProbe = [System.IO.Path]::GetFullPath((Join-Path $gameRoot 'mods/DohnaDohna-SmokeDriver'))
    if ($resolvedProbe -ne $expectedProbe -or (Get-Item -LiteralPath $resolvedProbe).LinkType) { throw 'Unexpected test-driver path; do not move it.' }
    $probeBackup = Join-Path $sessionRoot ('disabled-test-driver-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    Move-Item -LiteralPath $resolvedProbe -Destination $probeBackup
    Write-Output "Quarantined prior test driver for normal play: $probeBackup"
}
if ($Probe) {
    & dotnet build (Join-Path $projectRoot 'Tests/DohnaDohna.SmokeDriver/DohnaDohna.SmokeDriver.csproj') -c Release -o (Join-Path $projectRoot 'build/bin/preview/Release/net9.0') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Test driver build failed; do not run a stale driver.' }
    New-Item -ItemType Directory -Path $probeRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot 'build/bin/preview/Release/net9.0/DohnaDohna-SmokeDriver.dll') -Destination $probeRoot
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Tests/DohnaDohna.SmokeDriver/mod_manifest.json') -Destination $probeRoot
}
$savedAppData = $env:APPDATA
$savedLocalAppData = $env:LOCALAPPDATA
$savedMenuFlow = $env:DOHNA_MENU_FLOW
$savedMenuRoster = $env:DOHNA_MENU_ROSTER
$savedCaptureDir = $env:DOHNA_CAPTURE_DIR
$savedAudioTrace = $env:DOHNA_AUDIO_TRACE
$savedCommandScenario = $env:DOHNA_COMMAND_SCENARIO
$savedCaptureMovie = $env:DOHNA_CAPTURE_MOVIE
$capture = $null
try {
    $profileRoot = if ($MenuFlow) { Join-Path $sessionRoot ('menu-flow-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')) }
        elseif ($CaptureAudio) { Join-Path $sessionRoot ('audio-flow-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')) }
        elseif ($Probe) { Join-Path $sessionRoot ('command-flow-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')) }
        elseif ($FreshProfile) { Join-Path $sessionRoot ('manual-flow-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')) }
        elseif ($InteractiveProfile) { $manualProfile.FullName } else { $sessionRoot }
    $env:APPDATA = Join-Path $profileRoot 'appdata'
    $env:LOCALAPPDATA = Join-Path $profileRoot 'localappdata'
    $env:DOHNA_MENU_FLOW = if ($MenuFlow) { '1' } else { $null }
    $env:DOHNA_MENU_ROSTER = if ($MenuFlow) { $MenuRoster -join ',' } else { $null }
    $env:DOHNA_CAPTURE_DIR = if ($CaptureAudio) { Join-Path $env:LOCALAPPDATA 'audio' } else { $null }
    $env:DOHNA_AUDIO_TRACE = if ($CaptureAudio) { '1' } else { $null }
    $env:DOHNA_COMMAND_SCENARIO = $CommandScenario
    $env:DOHNA_CAPTURE_MOVIE = if ($CaptureMovie) { '1' } else { $null }
    New-Item -ItemType Directory -Path $env:APPDATA, $env:LOCALAPPDATA -Force | Out-Null
    $settingsDirectory = Join-Path $env:APPDATA 'SlayTheSpire2/default/1'
    New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
    $settingsFile = Join-Path $settingsDirectory 'settings.save'
    $settings = if (Test-Path -LiteralPath $settingsFile) { Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json -AsHashtable } else { @{schema_version=5} }
    $settings.mod_settings = @{mods_enabled=$true;mod_list=@(
        @{id='STS2-RitsuLib';is_enabled=$true;source='mods_directory'},
        @{id='DohnaDohna';is_enabled=$true;source='mods_directory'})}
    $settings.mod_settings.mod_list += @($additionalMods)
    if ($Probe) { $settings.mod_settings.mod_list += @{id='DohnaDohna-SmokeDriver';is_enabled=$true;source='mods_directory'} }
    $settings.seen_ea_disclaimer = $true
    $settings.skip_intro_logo = $true
    $settings.language = 'zhs'
    $settings.fullscreen = $false
    $settings.window_size = @{X=1600;Y=900}
    # Let the host center this launch instead of restoring a stale/offscreen
    # position. Manual and rendered launches must start in the same native mode.
    $settings.window_position = @{X=-1;Y=-1}
    $settings.resize_windows = $true
    $windowArguments = @('--windowed', '--resolution', '1600x900')
    if ($CaptureAudio) {
        # Only the throwaway signal-identification fixture: do not let unrelated
        # music/ambience mask waveform witnesses for the attenuated mod events.
        $settings.volume_bgm = 0.0
        $settings.volume_ambience = 0.0
    }
    $settings | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $settingsFile -Encoding utf8NoBOM
    if ($FreshProfile -and @($additionalMods | Where-Object id -eq 'NinjaSlayer').Count) {
        # The confirmed current RitsuLib test-profile schema. No daily settings,
        # applicant permissions, or uploads are touched by a manual smoke run.
        $consentDirectory = Join-Path $settingsDirectory 'mod_data/com.ritsukage.sts2-RitsuLib/telemetry'
        New-Item -ItemType Directory -Path $consentDirectory -Force | Out-Null
        '{"schema_version":1,"applicants":{"NinjaSlayer":{"consent":"Denied","granted_requests":[],"shared_contribution_sources":{}}}}' |
            Set-Content -LiteralPath (Join-Path $consentDirectory 'consent.json') -Encoding utf8NoBOM
    }
    if ($CaptureAudio) {
        New-Item -ItemType Directory -Path $env:DOHNA_CAPTURE_DIR | Out-Null
        & dotnet build (Join-Path $PSScriptRoot 'AudioCapture/DohnaDohna.AudioCapture.csproj') -c Release -o (Join-Path $projectRoot 'build/audio-capture') --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Scoped audio capture build failed.' }
        $capture = Start-Process (Join-Path $projectRoot 'build/audio-capture/DohnaDohna.AudioCapture.exe') -ArgumentList ('"' + $env:DOHNA_CAPTURE_DIR + '"') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $env:DOHNA_CAPTURE_DIR 'capture.stdout.log') -RedirectStandardError (Join-Path $env:DOHNA_CAPTURE_DIR 'capture.stderr.log')
        $readyUntil = [DateTime]::UtcNow.AddSeconds(10)
        while (-not (Test-Path -LiteralPath (Join-Path $env:DOHNA_CAPTURE_DIR 'audio-ready')) -and [DateTime]::UtcNow -lt $readyUntil -and -not $capture.HasExited) { Start-Sleep -Milliseconds 50 }
        if (-not (Test-Path -LiteralPath (Join-Path $env:DOHNA_CAPTURE_DIR 'audio-ready'))) { throw 'Scoped audio capture did not become ready.' }
    }
    if ($Interactive) {
        # Only entered when the user explicitly runs -Interactive. The child
        # inherits this isolated profile; the parent restores its own variables.
        $play = Start-Process (Join-Path $gameRoot 'SlayTheSpire2.exe') -ArgumentList (@('--force-steam=off') + $windowArguments) -WorkingDirectory $gameRoot -WindowStyle Normal -PassThru
        Write-Output "Interactive isolated play started: PID=$($play.Id), profile=$profileRoot"
        return
    }
    if ($Rendered) {
        & dotnet build (Join-Path $PSScriptRoot 'BackgroundGame/DohnaDohna.BackgroundGame.csproj') -c Release -o (Join-Path $projectRoot 'build/background-game') --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Background desktop helper build failed.' }
        $executable = Join-Path $projectRoot 'build/background-game/DohnaDohna.BackgroundGame.exe'
        $arguments = @('"' + (Join-Path $gameRoot 'SlayTheSpire2.exe') + '"', "$Seconds", '--force-steam=off', '--verbose') + $windowArguments
        if ($CaptureMovie) {
            # Godot's actual rendered viewport on the inactive desktop, not a
            # recreation of the animation. Fixed 60 fps is visual evidence only;
            # it is NOT a wall-clock/performance measurement or an FMOD recording.
            $moviePath = Join-Path $profileRoot 'rendered.avi'
            $arguments += @('--write-movie', '"' + $moviePath + '"', '--fixed-fps', '60')
        }
    } else {
        $executable = Join-Path $gameRoot 'SlayTheSpire2.exe'
        $arguments = @('--headless','--force-steam=off','--verbose')
    }
    $boot = Start-Process $executable -ArgumentList $arguments -WorkingDirectory $gameRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $sessionRoot 'stdout.log') -RedirectStandardError (Join-Path $sessionRoot 'stderr.log')
    if (-not $boot.WaitForExit(($Seconds + 10) * 1000)) { Stop-Process -Id $boot.Id; $boot.WaitForExit() }
    if ($CaptureAudio) {
        [System.IO.File]::WriteAllText((Join-Path $env:DOHNA_CAPTURE_DIR 'recording-stop.json'), '{}')
        if (-not $capture.WaitForExit(5000)) { Stop-Process -Id $capture.Id; throw 'Scoped audio capture did not finish.' }
        if ($capture.ExitCode -ne 0) { throw 'Scoped audio capture failed; see capture.stderr.log.' }
        foreach ($name in @('stdout.log','stderr.log')) { Copy-Item -LiteralPath (Join-Path $sessionRoot $name) -Destination (Join-Path $profileRoot $name) }
        Write-Output "Scoped game-only audio capture: $profileRoot"
        if (-not $MenuFlow) {
            $commandOutput = Get-Content -LiteralPath (Join-Path $profileRoot 'stdout.log') -Raw
            $commandErrors = Get-Content -LiteralPath (Join-Path $profileRoot 'stderr.log') -Raw
            if (-not $commandOutput.Contains($commandPassMarker) -or $commandErrors.Contains('DOHNA_SMOKE_REGISTRATION_FAIL')) { throw 'Native command/audio fixture failed; captured output is not a PASS.' }
        }
    }
    if ($MenuFlow) {
        foreach ($name in @('stdout.log','stderr.log')) { Copy-Item -LiteralPath (Join-Path $sessionRoot $name) -Destination (Join-Path $profileRoot $name) }
        $output = Get-Content -LiteralPath (Join-Path $profileRoot 'stdout.log') -Raw
        $errors = Get-Content -LiteralPath (Join-Path $profileRoot 'stderr.log') -Raw
        $passed = $boot.ExitCode -eq 0 -and $output.Contains('DOHNA_MENU_FLOW_PASS') -and -not $errors.Contains('DOHNA_MENU_FLOW_FAIL')
        $proof = @{host=(Get-Content -LiteralPath (Join-Path $gameRoot 'release_info.json') -Raw | ConvertFrom-Json).version;
            roster=$MenuRoster; mods=$allowedModFolders; exitCode=$boot.ExitCode; passed=$passed;
            dllSha256=(Get-FileHash -LiteralPath (Join-Path $ownMod 'DohnaDohna.dll')).Hash.ToLowerInvariant();
            driverSha256=(Get-FileHash -LiteralPath (Join-Path $probeRoot 'DohnaDohna-SmokeDriver.dll')).Hash.ToLowerInvariant();
            pckSha256=(Get-FileHash -LiteralPath (Join-Path $ownMod 'DohnaDohna.pck')).Hash.ToLowerInvariant();
            assertions=@($output -split "`n" | Where-Object { $_ -cmatch '^DOHNA_MENU_[A-Z_]+_PASS(?:\s|$)' } | ForEach-Object Trim);timestamp=[DateTimeOffset]::Now.ToString('o')}
        $proof | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $profileRoot 'menu-proof.json') -Encoding utf8NoBOM
        Write-Output "Native GUI proof: $profileRoot"
        if (-not $passed) { throw "Native GUI acceptance failed; see $profileRoot" }
    }
    if ($Probe -and -not $MenuFlow -and -not $CaptureAudio) {
        foreach ($name in @('stdout.log','stderr.log')) { Copy-Item -LiteralPath (Join-Path $sessionRoot $name) -Destination (Join-Path $profileRoot $name) }
        $commandOutput = [string](Get-Content -LiteralPath (Join-Path $profileRoot 'stdout.log') -Raw)
        $commandErrors = [string](Get-Content -LiteralPath (Join-Path $profileRoot 'stderr.log') -Raw)
        Write-Output "Native command proof: $profileRoot"
        if (-not $commandOutput.Contains($commandPassMarker) -or $commandErrors.Contains('DOHNA_SMOKE_REGISTRATION_FAIL')) {
            throw "Native command acceptance failed; see $profileRoot"
        }
    }
    Write-Output "Isolated boot output: $sessionRoot; rendered=$Rendered"
    Write-Output 'Only explicit driver PASS assertions count; booting alone is not playability or manual UI acceptance.'
} finally {
    if ($capture -and -not $capture.HasExited) {
        [System.IO.File]::WriteAllText((Join-Path $env:DOHNA_CAPTURE_DIR 'recording-stop.json'), '{}')
        if (-not $capture.WaitForExit(5000)) { Stop-Process -Id $capture.Id }
    }
    $env:APPDATA = $savedAppData
    $env:LOCALAPPDATA = $savedLocalAppData
    $env:DOHNA_MENU_FLOW = $savedMenuFlow
    $env:DOHNA_MENU_ROSTER = $savedMenuRoster
    $env:DOHNA_CAPTURE_DIR = $savedCaptureDir
    $env:DOHNA_AUDIO_TRACE = $savedAudioTrace
    $env:DOHNA_COMMAND_SCENARIO = $savedCommandScenario
    $env:DOHNA_CAPTURE_MOVIE = $savedCaptureMovie
}
