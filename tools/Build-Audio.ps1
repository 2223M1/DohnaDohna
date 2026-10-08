#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$chain = Get-Content -LiteralPath (Join-Path $projectRoot '../Tools/STS2/toolchain.json') -Raw | ConvertFrom-Json
& $chain.python (Join-Path $PSScriptRoot 'Import-OriginalRoles.py')
if ($LASTEXITCODE -ne 0) { throw 'Selected original resource import failed.' }
& $chain.python (Join-Path $PSScriptRoot 'Prepare-Fmod.py')
if ($LASTEXITCODE -ne 0) { throw 'Own FMOD preparation failed.' }
$studio = Join-Path $projectRoot '.local/fmod'
& $chain.fmodCli -script (Join-Path $studio 'build-events.js') (Join-Path $studio 'DohnaDohna.fspro') *> (Join-Path $projectRoot 'build/fmod-prepare.log')
if ($LASTEXITCODE -ne 0) { throw 'Own FMOD event authoring failed.' }
& $chain.fmodCli -build -export-guids (Join-Path $studio 'DohnaDohna.fspro') *> (Join-Path $projectRoot 'build/fmod-build.log')
if ($LASTEXITCODE -ne 0) { throw 'Own FMOD bank export failed.' }
# Studio appends its configured platform folder. Never mix new GUIDs with an
# old bank or copy the host Master bank into this mod.
$bank = Join-Path $studio 'Build/desktop/DohnaDohna.bank'
$guids = Join-Path $studio 'Build/GUIDs.txt'
if (-not (Test-Path -LiteralPath $bank) -or -not (Test-Path -LiteralPath $guids)) { throw 'Expected Desktop bank and GUID export missing.' }
Copy-Item -LiteralPath $bank -Destination (Join-Path $projectRoot 'DohnaDohna/audio/fmod/DohnaDohna.bank') -Force
Copy-Item -LiteralPath $guids -Destination (Join-Path $projectRoot 'DohnaDohna/audio/fmod/GUIDs.txt') -Force
Write-Output 'Exported own DohnaDohna bank and GUIDs. Master bank, installation and publication were not changed.'
