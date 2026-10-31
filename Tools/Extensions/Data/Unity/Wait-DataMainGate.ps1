[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9-]+$')][string] $Run,
    [ValidateSet('Tests', 'Build')][string] $Kind = 'Tests',
    [int] $TimeoutSeconds = 1200
)
$ErrorActionPreference = 'Stop'
$root = 'G:\Unity\MB26'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..')).TrimEnd('\')
if ($project -ne 'G:\Unity\Projects\CGJ2024') { throw 'Wrong main workspace.' }
$owner = [IO.File]::ReadAllText((Join-Path $root 'owner.json')) | ConvertFrom-Json
if ([IO.Path]::GetFullPath($owner.projectPath) -ne $project -or $owner.sourceTip -ne '5dce4f3e39e2f4bd11602e65cabace5f56108cf8') { throw 'Wrong evidence owner.' }
$folder = Join-Path $root $Run
if (-not (Test-Path -LiteralPath $folder)) { throw 'Gate was not started by the main Editor.' }
$process = Get-CimInstance Win32_Process -Filter "ProcessId = $($owner.pid)"
if (-not $process -or $process.Name -ne 'Unity.exe' -or $process.CommandLine.Replace('/', '\').IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -lt 0) { throw 'Bound main Editor no longer exists.' }
$resultPath = Join-Path $folder $(if ($Kind -eq 'Tests') { 'summary.json' } else { 'build.json' })
$restoredPath = Join-Path $folder 'settings-restored.json'
$completePath = Join-Path $folder 'gate-complete.json'
$watcher = New-Object IO.FileSystemWatcher($folder)
$watcher.EnableRaisingEvents = $true
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
try {
    while (-not (Test-Path -LiteralPath $completePath)) {
        if (Test-Path -LiteralPath (Join-Path $folder 'restore-failed.json')) { throw 'Settings recovery failed; original evidence/live state preserved.' }
        $remaining = [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds
        if ($remaining -le 0) { throw 'Gate deadline exceeded; original process/evidence preserved.' }
        $change = $watcher.WaitForChanged([IO.WatcherChangeTypes]::All, $remaining)
        if ($change.TimedOut) { throw 'Gate deadline exceeded; original process/evidence preserved.' }
    }
} finally { $watcher.Dispose() }
$result = [IO.File]::ReadAllText($resultPath) | ConvertFrom-Json
$settings = [IO.File]::ReadAllText($restoredPath) | ConvertFrom-Json
$complete = [IO.File]::ReadAllText($completePath) | ConvertFrom-Json
if (-not $complete.closed -or $complete.pid -ne $owner.pid -or -not $settings.restored -or -not $settings.memoryRestored) { throw 'Exact settings-byte/memory/UTF-owner closure failed.' }
if ($Kind -eq 'Tests') {
    [xml]$xml = [IO.File]::ReadAllText((Join-Path $folder 'results.xml'))
    $leaves = @($xml.SelectNodes('//test-case'))
    $discovered = [IO.File]::ReadAllText((Join-Path $folder 'discovered.json')) | ConvertFrom-Json
    $passed = @($leaves | Where-Object { $_.result -eq 'Passed' }).Count
    $failed = @($leaves | Where-Object { $_.result -eq 'Failed' })
    $skipped = @($leaves | Where-Object { $_.result -eq 'Skipped' }).Count
    [pscustomobject]@{ Run = $Run; PID = $owner.pid; Discovered = $discovered.Count; Executed = $leaves.Count; Passed = $passed; Failed = $failed.Count; Skipped = $skipped; SettingsBytesRestored = $settings.restored } | ConvertTo-Json
    foreach ($test in $failed) { [pscustomobject]@{ Name = $test.fullname; Message = $test.SelectSingleNode('failure/message').InnerText; Stack = $test.SelectSingleNode('failure/stack-trace').InnerText } | ConvertTo-Json }
    if ($leaves.Count -eq 0 -or $leaves.Count -ne $discovered.Count -or $passed -ne $leaves.Count -or $result.FailCount -ne 0 -or $result.SkipCount -ne 0 -or $result.InconclusiveCount -ne 0) { throw 'Actual test discovery/execution did not fully pass.' }
} else {
    [pscustomobject]@{ Run = $Run; Result = $result.result; Backend = $result.backend; Development = $result.development; Errors = $result.errors; Warnings = $result.warnings; SettingsBytesRestored = $settings.restored } | ConvertTo-Json
    if (-not $result.passed -or $result.exitCode -ne 0 -or $result.error -or $result.restoreError -or $result.result -ne 'Succeeded' -or $result.development -or $result.errors -ne 0) { throw 'Actual Release build or its post-build audit failed.' }
}
