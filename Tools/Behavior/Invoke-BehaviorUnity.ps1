#requires -Version 7.6
[CmdletBinding()]
param(
    [ValidateSet('Import', 'EditMode', 'PlayMode', 'Execute')][string] $Gate = 'Import',
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string] $Run,
    [string] $OutputRoot = 'C:\Users\Kano\AppData\Local\Temp\kilo\BA26',
    [string] $Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe',
    [string] $Method,
    [string] $TestFilter = 'YLFramework.Tests.Extensions.Behavior',
    [string] $AssemblyNames,
    [switch] $RecoverOnly,
    [switch] $NoGraphics,
    [switch] $AsyncExecute,
    [switch] $Win64BuildTarget,
    [string[]] $ExtraArguments = @()
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\')
$expected = 'G:\Unity\Projects\CGJ2024\.kilo\worktrees\fusion-xf-ext-behavior-901d14f1ae336a53'
if ($project -ne $expected) { throw "Refusing unexpected project: $project" }
if (-not (Test-Path -LiteralPath $Unity)) { throw 'Unity executable is missing.' }
$version = (Get-Item -LiteralPath $Unity).VersionInfo.ProductVersion
if ($version -ne '2022.3.62f3c1_1623fc0bbb97') { throw "Unexpected Unity version: $version" }
if ((& git -C $project branch --show-current) -ne 'fusion/xf-ext-behavior') { throw 'Unexpected branch.' }
$processes = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | ForEach-Object {
    $command = $_.CommandLine
    $path = if ($command -match '(?i)-projectPath"?\s+"([^"]+)"') { $Matches[1] } elseif ($command -match '(?i)-projectPath\s+([^\s"]+)') { $Matches[1] } else { '' }
    [ordered]@{ pid = $_.ProcessId; executable = $_.ExecutablePath; project = $path; batch = [bool]($command -match '(?i)-batchmode'); worker = [bool]($command -match '(?i)AssetImportWorker') }
})
foreach ($item in $processes) {
    if ($item.project.Replace('/', '\').TrimEnd('\') -eq $project) { throw "A Unity instance already exists: PID $($item.pid)" }
    if (-not $RecoverOnly -and $item.batch -and -not $item.worker -and $item.project -match '[\\/]\.kilo[\\/]worktrees[\\/]') { throw "Another worktree owns BatchMode: PID $($item.pid). Schedule a wait instead of starting A." }
}
$lockPath = Join-Path $project 'Temp\UnityLockfile'
$unownedLock = $false
if (Test-Path -LiteralPath $lockPath) {
    try { $handle = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); $handle.Dispose(); $unownedLock = $true }
    catch [IO.IOException] { throw 'The A project lock has an exclusive owner. Do not delete it or start another Editor.' }
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if ($OutputRoot -ne 'C:\Users\Kano\AppData\Local\Temp\kilo\BA26') { throw 'Use the dedicated pre-approved A evidence root.' }
if (-not (Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($OutputRoot)))) { throw 'Missing evidence parent.' }
$null = New-Item -ItemType Directory -Path $OutputRoot -Force
$output = Join-Path $OutputRoot $Run
$utf8 = [Text.UTF8Encoding]::new($false)
if ($RecoverOnly) {
    $jobPath = Join-Path $output 'job.json'
    $beforePath = Join-Path $output 'protected-before.json'
    if (-not (Test-Path -LiteralPath $jobPath) -or -not (Test-Path -LiteralPath $beforePath)) { throw 'Recovery requires retained evidence of the same run.' }
    $job = [IO.File]::ReadAllText($jobPath) | ConvertFrom-Json
    if ($job.project -ne $project) { throw 'Recovery project does not match.' }
    $alive = @(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -in @($job.unityPid, $job.wrapperPid) })
    if ($alive.Count -ne 0) { throw 'A recorded PID still exists; inspect its actual ownership before recovery.' }
    $changes = @(([IO.File]::ReadAllText($beforePath) | ConvertFrom-Json) | Where-Object {
        $path = Join-Path $project $_.path
        -not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $_.sha256
    } | ForEach-Object path)
    $log = [IO.File]::ReadAllText((Join-Path $output 'editor.log'))
    $recovery = [ordered]@{ observedUtc = [DateTime]::UtcNow.ToString('o'); project = $project; unityPid = $job.unityPid; wrapperPid = $job.wrapperPid; bothPidsAbsent = $true; exitCode = $job.exitCode; unownedLockObserved = $unownedLock; cleanShutdownMarker = [bool]($log -match 'Exiting batchmode successfully|Exiting batchmode with returncode'); protectedChanges = $changes; result = 'InterruptedOrUnrecordedExit_NotPassed' }
    [IO.File]::WriteAllText((Join-Path $output 'recovery.json'), ($recovery | ConvertTo-Json -Depth 6), $utf8)
    "Recovered same run $Run. No exit code inferred; protected changes=$($changes.Count); unlocked stale lock=$unownedLock."
    return
}
if (Test-Path -LiteralPath $output) { throw "Run output already exists; preserve it: $output" }
$null = New-Item -ItemType Directory -Path $output
$protectedPaths = @('Packages', 'ProjectSettings', 'Assets/YLFramework/Runtime/Foundation', 'Assets/YLFramework/Runtime/Core', 'Assets/YLFramework/Runtime/Utilities/FSM', 'Assets/YLFramework/Runtime/Systems/Resource', 'Assets/YLFramework/Editor/Systems/Resource', 'Assets/Games', 'Assets/Scenes', 'Assets/StreamingAssets', 'Assets/YLFramework/Runtime/YLFramework.Runtime.asmdef', 'Assets/YLFramework/Editor/YLFramework.Editor.asmdef', 'Assets/YLFramework/Tests/EditMode/YLFramework.Tests.EditMode.asmdef', 'Assets/YLFramework/Tests/PlayMode/YLFramework.Tests.PlayMode.asmdef', '.kilo/plans/1792420261057-xf-ecosystem-integration.md', 'Tools/Verify-UIPanel.ps1', 'Tools/UIPanelOfflineRunner.cs')
$snapshot = @(& git -C $project ls-files -- @protectedPaths | ForEach-Object {
    $path = Join-Path $project $_
    if (Test-Path -LiteralPath $path -PathType Leaf) { [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() } }
})
if ($LASTEXITCODE -ne 0) { throw 'Cannot snapshot protected inputs.' }
[IO.File]::WriteAllText((Join-Path $output 'protected-before.json'), ($snapshot | ConvertTo-Json -Depth 5), $utf8)
$pending = Join-Path $project 'Temp\Behavior.Offline\behavior-final\pending-meta.txt'
$frozenPending = Join-Path $OutputRoot 'expected-meta-146.txt'
if (-not (Test-Path -LiteralPath $frozenPending)) {
    $initialCopy = Join-Path $OutputRoot 'import-01\expected-meta-146.txt'
    if (Test-Path -LiteralPath $pending) { Copy-Item -LiteralPath $pending -Destination $frozenPending }
    elseif (Test-Path -LiteralPath $initialCopy) { Copy-Item -LiteralPath $initialCopy -Destination $frozenPending }
    else { throw 'Missing original pre-import meta expectation. Do not reconstruct it from imported results.' }
}
Copy-Item -LiteralPath $frozenPending -Destination (Join-Path $output 'expected-meta-146.txt')
$arguments = @('-batchmode', '-projectPath', $project, '-logFile', (Join-Path $output 'editor.log'), '-UNITY_MCP_KEEP_CONNECTED=false', '-UNITY_MCP_START_SERVER=false')
if ($NoGraphics) { $arguments += '-nographics' }
if ($Win64BuildTarget) { $arguments += @('-buildTarget', 'Win64') }
if ($Gate -in @('EditMode', 'PlayMode')) {
    $arguments += @('-runTests', '-testPlatform', $Gate, '-testResults', (Join-Path $output 'tests.xml'), '-testFilter', $TestFilter)
    if ($AssemblyNames) { $arguments += @('-assemblyNames', $AssemblyNames) }
} else {
    if (-not $AsyncExecute) { $arguments += '-quit' }
    if ($Gate -eq 'Execute') {
        if (-not $Method) { throw 'Execute requires a precise method.' }
        $arguments += @('-executeMethod', $Method, '-behaviorOutput', $output)
    }
}
$arguments += $ExtraArguments
$record = [ordered]@{ project = $project; unity = $Unity; version = $version; head = (& git -C $project rev-parse HEAD); gate = $Gate; run = $Run; startUtc = [DateTime]::UtcNow.ToString('o'); wrapperPid = $PID; otherProcesses = $processes; unownedLockObserved = $unownedLock; arguments = $arguments; unityPid = $null; endUtc = $null; exitCode = $null; protectedChanges = @() }
$start = [Diagnostics.ProcessStartInfo]::new($Unity)
$start.UseShellExecute = $false
$start.WorkingDirectory = $project
foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
if (-not $process.Start()) { throw 'Unity failed to start.' }
$record.unityPid = $process.Id
[IO.File]::WriteAllText((Join-Path $output 'job.json'), ($record | ConvertTo-Json -Depth 8), $utf8)
"Started A Unity PID $($process.Id), gate=$Gate, project=$project, output=$output"
$process.WaitForExit()
$record.exitCode = $process.ExitCode
$record.endUtc = [DateTime]::UtcNow.ToString('o')
$record.protectedChanges = @($snapshot | Where-Object {
    $path = Join-Path $project $_.path
    -not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $_.sha256
} | ForEach-Object path)
[IO.File]::WriteAllText((Join-Path $output 'job.json'), ($record | ConvertTo-Json -Depth 8), $utf8)
"A Unity PID $($process.Id) exited $($process.ExitCode). Protected changes: $($record.protectedChanges.Count). Evidence: $output"
if ($record.protectedChanges.Count -gt 0) { throw "Unity changed protected inputs; preserve evidence and do not revert automatically: $($record.protectedChanges -join ', ')" }
if ($process.ExitCode -ne 0) { throw "Unity $Gate failed: exit $($process.ExitCode). Read this same run's log." }
