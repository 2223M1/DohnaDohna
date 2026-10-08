#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRepoRoot = Split-Path $PSScriptRoot -Parent
$taskSourceRoot = Join-Path $taskRepoRoot '.agents/skills'
$taskCodexBase = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $env:USERPROFILE '.codex' }
$taskInstallRoot = Join-Path $taskCodexBase 'skills'
$taskDiscoveryRoot = Join-Path $env:USERPROFILE '.agents/skills'
$taskNames = @('dohnadohna-lore','dohnadohna-code-quality','dohnadohna-modding','dohnadohna-sts2-reference')
$taskRecords = @()
foreach ($taskName in $taskNames) {
    $taskSource = Join-Path $taskSourceRoot $taskName
    if (!(Test-Path -LiteralPath (Join-Path $taskSource 'SKILL.md'))) { throw "Missing source skill: $taskName" }
    $taskDestination = Join-Path $taskInstallRoot $taskName
    $taskDiscovery = Join-Path $taskDiscoveryRoot $taskName
    $taskSourceFiles = @(Get-ChildItem -LiteralPath $taskSource -File -Recurse)
    if (Test-Path -LiteralPath $taskDestination) {
        $taskInstalledFiles = @(Get-ChildItem -LiteralPath $taskDestination -File -Recurse)
        if ($taskInstalledFiles.Count -ne $taskSourceFiles.Count) { throw "Existing skill differs; refusing overwrite: $taskDestination" }
        foreach ($taskFile in $taskSourceFiles) {
            $taskRelative = [IO.Path]::GetRelativePath($taskSource, $taskFile.FullName)
            $taskInstalled = Join-Path $taskDestination $taskRelative
            if (!(Test-Path -LiteralPath $taskInstalled) -or (Get-FileHash -LiteralPath $taskInstalled).Hash -ne (Get-FileHash -LiteralPath $taskFile.FullName).Hash) {
                throw "Existing skill differs; refusing overwrite: $taskInstalled"
            }
        }
    }
    if (Test-Path -LiteralPath $taskDiscovery) {
        $taskLink = Get-Item -LiteralPath $taskDiscovery -Force
        if ($taskLink.LinkType -ne 'Junction' -or [IO.Path]::GetFullPath([string]$taskLink.Target) -ne [IO.Path]::GetFullPath($taskDestination)) {
            throw "Discovery path is owned by another installation: $taskDiscovery"
        }
    }
}
New-Item -ItemType Directory -Path $taskInstallRoot,$taskDiscoveryRoot -Force | Out-Null
foreach ($taskName in $taskNames) {
    $taskSource = Join-Path $taskSourceRoot $taskName
    $taskDestination = Join-Path $taskInstallRoot $taskName
    $taskDiscovery = Join-Path $taskDiscoveryRoot $taskName
    if (!(Test-Path -LiteralPath $taskDestination)) {
        Copy-Item -LiteralPath $taskSource -Destination $taskDestination -Recurse
    }
    if (!(Test-Path -LiteralPath $taskDiscovery)) {
        New-Item -ItemType Junction -Path $taskDiscovery -Target $taskDestination | Out-Null
    }
    $taskFiles = foreach ($taskFile in Get-ChildItem -LiteralPath $taskSource -File -Recurse) {
        $taskRelative = [IO.Path]::GetRelativePath($taskSource, $taskFile.FullName)
        $taskInstalled = Join-Path $taskDestination $taskRelative
        $taskSourceHash = (Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $taskInstalled -Algorithm SHA256).Hash -ne $taskSourceHash) { throw "Installation hash mismatch: $taskInstalled" }
        [PSCustomObject]@{relativePath=$taskRelative;sha256=$taskSourceHash}
    }
    $taskRecords += [PSCustomObject]@{name=$taskName;source=$taskSource;installed=$taskDestination;discovery=$taskDiscovery;files=@($taskFiles)}
    Write-Output "INSTALLED $taskName -> $taskDestination"
}
$taskLocalRoot = Join-Path $taskRepoRoot '.local'
New-Item -ItemType Directory -Path $taskLocalRoot -Force | Out-Null
$taskReceipt = [PSCustomObject]@{skills=$taskRecords;existingSkillsOverwritten=$false;sourceNinjaSlayerSkillsModified=$false}
[IO.File]::WriteAllText((Join-Path $taskLocalRoot 'dohnadohna-skills-installed.json'), ($taskReceipt | ConvertTo-Json -Depth 8) + "`n", [Text.UTF8Encoding]::new($false))
