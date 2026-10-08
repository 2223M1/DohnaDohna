param(
    [Parameter(Mandatory)][string]$ReferenceRoot,
    [Parameter(Mandatory)][string]$ScriptDirectory
)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path -LiteralPath $ReferenceRoot).ProviderPath
$taskScripts = (Resolve-Path -LiteralPath $ScriptDirectory).ProviderPath
$taskGhidra = Join-Path $taskRoot 'tools/ghidra/ghidra_12.1.4_PUBLIC/support/analyzeHeadless.bat'
$taskJdk = Join-Path $taskRoot 'tools/jdk21/jdk-21.0.12.1+1'
if (!(Test-Path -LiteralPath $taskGhidra) -or !(Test-Path -LiteralPath (Join-Path $taskJdk 'bin/java.exe'))) {
    throw 'Verified portable Ghidra/JDK installation is missing.'
}
$env:JAVA_HOME = $taskJdk
$env:JAVA_TOOL_OPTIONS = '-Dfile.encoding=UTF-8'
$taskModules = Get-Content -LiteralPath (Join-Path $taskRoot 'reverse/native/modules.json') -Raw | ConvertFrom-Json
$taskProjects = Join-Path $taskRoot 'reverse/native/ghidra_projects'
New-Item -ItemType Directory -Path $taskProjects -Force | Out-Null
foreach ($taskModule in $taskModules) {
    if ($taskModule.duplicate_of) { continue }
    $taskOutput = Join-Path $taskRoot ('reverse/native/' + $taskModule.label)
    $taskCoverage = Join-Path $taskOutput 'coverage.json'
    if (Test-Path -LiteralPath $taskCoverage) {
        $taskPrevious = Get-Content -LiteralPath $taskCoverage -Raw | ConvertFrom-Json
        if ($taskPrevious.executable_sha256 -ne $taskModule.sha256) { throw 'Existing native output has a different source hash.' }
        Write-Output ('SKIP validated native output: ' + $taskModule.label)
        continue
    }
    $taskHash = (Get-FileHash -LiteralPath $taskModule.source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($taskHash -ne $taskModule.sha256) { throw ('Original binary changed: ' + $taskModule.source) }
    Write-Output ('ANALYZE ' + $taskModule.label + ': ' + $taskModule.source)
    $taskGhidraLog = Join-Path $taskOutput 'ghidra.log'
    $taskScriptLog = Join-Path $taskOutput 'export.log'
    & $taskGhidra $taskProjects ('DohnaDohna_' + $taskModule.label) `
        -import $taskModule.source -scriptPath $taskScripts `
        -postScript 'ExportOriginalGame.java' $taskOutput `
        -analysisTimeoutPerFile 1800 -max-cpu 4 -log $taskGhidraLog -scriptlog $taskScriptLog
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $taskCoverage)) {
        throw ('Native export incomplete for ' + $taskModule.label + '; inspect its logs and retained Ghidra project.')
    }
}
