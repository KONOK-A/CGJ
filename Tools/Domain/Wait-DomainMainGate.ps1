[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9-]+$')][string] $Run,
    [ValidateSet('Tests', 'Configuration', 'Build')][string] $Kind = 'Tests',
    [int] $TimeoutSeconds = 1200
)
$ErrorActionPreference = 'Stop'
$root = 'G:\Unity\MC26-20261020T173208Z'
$project = 'G:\Unity\Projects\CGJ2024'
$owner = [IO.File]::ReadAllText((Join-Path $root 'owner.json')) | ConvertFrom-Json
if ([IO.Path]::GetFullPath($owner.projectPath) -ne $project -or $owner.sourceTip -ne 'aadc11b5099b76f16667aa9818ca927104cd12be') { throw 'Wrong main C evidence owner.' }
$folder = Join-Path $root $Run
if (-not (Test-Path -LiteralPath $folder)) { throw 'The main Editor has not started this gate.' }
$editor = Get-CimInstance Win32_Process -Filter "ProcessId = $($owner.pid)"
if (-not $editor -or $editor.Name -ne 'Unity.exe' -or $editor.CommandLine.Replace('/', '\').IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -lt 0) { throw 'Bound main Editor no longer exists.' }
$completePath = Join-Path $folder $(if ($Kind -eq 'Configuration') { 'configuration-complete.json' } else { 'gate-complete.json' })
$watcher = New-Object IO.FileSystemWatcher($folder)
$signal = 'MC26-Gate-' + [Guid]::NewGuid().ToString('N')
$null = Register-ObjectEvent -InputObject $watcher -EventName Created -SourceIdentifier $signal
$null = Register-ObjectEvent -InputObject $watcher -EventName Renamed -SourceIdentifier ($signal + '-renamed')
$null = Register-ObjectEvent -InputObject $watcher -EventName Changed -SourceIdentifier ($signal + '-changed')
$watcher.EnableRaisingEvents = $true
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
try {
    while (-not (Test-Path -LiteralPath $completePath)) {
        if (Test-Path -LiteralPath (Join-Path $folder 'restore-failed.json')) { throw 'Recovery failed; preserve the live Editor and original evidence.' }
        if (Test-Path -LiteralPath (Join-Path $folder 'configuration-failed.json')) { throw 'Configuration failed; preserve its original evidence and settings owner.' }
        $remaining = [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds
        if ($remaining -le 0) { throw 'Gate deadline exceeded; inspect the same XML/log/PID before any retry.' }
        # Registered events are buffered, including arrivals between the file check and Wait-Event.
        $event = Wait-Event -Timeout ([Math]::Max(1, [int][Math]::Ceiling($remaining / 1000.0)))
        if ($null -eq $event) { throw 'Gate deadline exceeded; original process and evidence are preserved.' }
        Remove-Event -EventIdentifier $event.EventIdentifier
    }
} finally {
    foreach ($source in @($signal, "${signal}-renamed", "${signal}-changed")) {
        Unregister-Event -SourceIdentifier $source
        Get-Event -SourceIdentifier $source -ErrorAction SilentlyContinue | Remove-Event
    }
    $watcher.Dispose()
}
if ($Kind -eq 'Configuration') {
    $configuration = [IO.File]::ReadAllText($completePath) | ConvertFrom-Json
    if (-not $configuration.passed -or $configuration.state.pid -ne $owner.pid -or $configuration.state.isCompiling -or $configuration.state.scriptCompilationFailed -or $configuration.state.utfActive) { throw 'Configuration did not reach the correct compiled terminal state.' }
    [pscustomobject]@{ Run = $Run; MainPID = $owner.pid; ExcelEnabled = $configuration.enabled; Compiled = $true } | ConvertTo-Json
    if (-not $configuration.enabled) {
        $settings = [IO.File]::ReadAllText((Join-Path $folder 'settings-restored.json')) | ConvertFrom-Json
        if (-not $settings.restored -or -not $settings.memoryRestored -or -not $settings.scenesRestored) { throw 'Optional configuration was not precisely restored.' }
    }
    return
}
$result = [IO.File]::ReadAllText((Join-Path $folder $(if ($Kind -eq 'Build') { 'build.json' } else { 'summary.json' }))) | ConvertFrom-Json
$settings = [IO.File]::ReadAllText((Join-Path $folder 'settings-restored.json')) | ConvertFrom-Json
$complete = [IO.File]::ReadAllText($completePath) | ConvertFrom-Json
if (-not $complete.closed -or -not $complete.utfInactive -or $complete.pid -ne $owner.pid -or -not $settings.restored -or -not $settings.memoryRestored -or -not $settings.scenesRestored) { throw 'Settings/scenes/UTF terminal closure failed.' }
if ($complete.infrastructureError) { throw "The Run recovered safely but its infrastructure failed: $($complete.infrastructureError)" }
if ($Kind -eq 'Build') {
    [pscustomobject]@{ Run = $Run; MainPID = $owner.pid; Result = $result.result; Backend = $result.backend; Stripping = $result.stripping; Compiler = $result.compiler; Options = $result.options; Errors = $result.errors; Warnings = $result.warnings; TerminalRestored = $true } | ConvertTo-Json
    if (-not $result.passed -or $result.error -or $result.result -ne 'Succeeded' -or $result.errors -ne 0 -or $result.development -or $result.options -ne 'None') { throw "Release build did not pass: $($result.error)" }
    return
}
[xml]$xml = [IO.File]::ReadAllText((Join-Path $folder 'results.xml'))
$leaves = @($xml.SelectNodes('//test-case'))
$discovered = [IO.File]::ReadAllText((Join-Path $folder 'discovered.json')) | ConvertFrom-Json
$passed = @($leaves | Where-Object { $_.result -eq 'Passed' }).Count
$failed = @($leaves | Where-Object { $_.result -eq 'Failed' })
$skipped = @($leaves | Where-Object { $_.result -eq 'Skipped' }).Count
[pscustomobject]@{ Run = $Run; MainPID = $owner.pid; Discovered = $discovered.Count; Executed = $leaves.Count; Passed = $passed; Failed = $failed.Count; Skipped = $skipped; TerminalRestored = $true } | ConvertTo-Json
foreach ($test in $failed) { [pscustomobject]@{ Name = $test.fullname; Message = $test.SelectSingleNode('failure/message').InnerText; Stack = $test.SelectSingleNode('failure/stack-trace').InnerText } | ConvertTo-Json }
if ($leaves.Count -eq 0 -or $leaves.Count -ne $discovered.Count -or $passed -ne $leaves.Count -or $result.FailCount -ne 0 -or $result.SkipCount -ne 0 -or $result.InconclusiveCount -ne 0) { throw 'Actual test discovery/execution did not fully pass.' }
