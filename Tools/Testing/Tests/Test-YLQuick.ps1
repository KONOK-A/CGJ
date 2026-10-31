#requires -Version 7.6
#requires -PSEdition Core
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $WorkspaceRoot,
    [Parameter(Mandatory)][string] $Unity,
    [ValidateNotNullOrEmpty()][string] $Module = 'Reddot',
    [ValidateSet('Quick', 'Special')][string] $Suite = 'Quick'
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Common/Process.ps1')
. (Join-Path $PSScriptRoot '../Common/Results.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$entry = Join-Path $PSScriptRoot '../Invoke-YLTests.ps1'
$workspace = Initialize-YLTestWorkspace -WorkspaceRoot $WorkspaceRoot -Repository $repository
$profile = [Diagnostics.FileVersionInfo]::GetVersionInfo($Unity).ProductVersion
$run = New-YLTestDirectory -Workspace $workspace -Kind Result -Key "Tooling-QuickCLI-$Suite-$Module-$profile"
$lease = Lock-YLTestDirectory -Workspace $workspace -Path $run.path
$summary = [ordered]@{
    schemaVersion = 1; ownerId = $run.id; action = 'Run'; suite = 'Tooling'; module = 'QuickCLI'; profile = $profile
    targetModule = $Module; targetSuite = $Suite; status = 'Failed'; endUtc = $null; reason = $null; processes = @(); checks = 0
}
$script:steps = 0

function Invoke-QuickEntry([string[]] $Arguments, [int] $ExpectedExit = 0) {
    $script:steps++
    $start = [Diagnostics.ProcessStartInfo]::new([Environment]::ProcessPath)
    $start.UseShellExecute = $false
    $start.WorkingDirectory = $repository
    foreach ($argument in @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $entry) + $Arguments) { $start.ArgumentList.Add($argument) }
    $log = Join-Path $run.path "cli-$script:steps.log"
    $fact = Invoke-YLTestProcess -StartInfo $start -Log $log -TimeoutMilliseconds 120000
    $summary.processes += $fact
    if (-not $fact.observedExit -or $fact.exitCode -ne $ExpectedExit -or $fact.forcedCleanup -or $fact.observerError -or $fact.cleanupError) {
        throw "CLI exit must be $ExpectedExit, got $($fact.exitCode) / $($fact.status). Logs: $log"
    }
    return [IO.File]::ReadAllText($log) | ConvertFrom-Json -DateKind String
}

try {
    $absent = Join-Path $run.path 'ListMustNotCreate'
    $list = Invoke-QuickEntry -Arguments @('-WorkspaceRoot', $absent)
    if ($list.action -cne 'List' -or [IO.Path]::Exists($absent) -or @($list.modules | Where-Object { $_.name -ceq $Module }).Count -ne 1) {
        throw 'Default List must describe real configuration without creating a workspace or starting Unity.'
    }
    $summary.checks++
    "PASS: default List is read-only and describes the $Module suite"

    $arguments = @('-Suite', $Suite, '-Module', $Module, '-Unity', $Unity, '-WorkspaceRoot', $WorkspaceRoot)
    $discovery = Invoke-QuickEntry -Arguments (@('-Action', 'Discover') + $arguments)
    if ($discovery.status -cne 'Completed' -or $discovery.testResult.status -cne 'NotRun') { throw 'Discovery is an operation, not a test pass.' }
    $selection = [IO.File]::ReadAllText($discovery.selection) | ConvertFrom-Json -DateKind String
    if ($selection.tests.Count -lt 2 -or ($Module -ceq 'Reddot' -and @($selection.tests | Where-Object { $_.fullName -cnotlike '*.ReddotTests.*' }).Count -ne 0)) {
        throw 'Quick must discover the actual nonempty module category, not an unrelated namespace.'
    }
    $summary.checks++
    "PASS: real $Module discovery is nonempty, categorized and not marked passed"

    $execution = Invoke-QuickEntry -Arguments (@('-Action', 'Run', '-Selection', $discovery.selection) + $arguments)
    if ($execution.status -cne 'Passed' -or $execution.testResult.total -ne $selection.tests.Count) { throw 'Quick execution did not match its actual selected leaves.' }
    $summary.checks++
    "PASS: real $Module selected leaves execute and XML/process facts close"

    $stale = [IO.File]::ReadAllText($discovery.selection) | ConvertFrom-Json -DateKind String
    $stale.inputIdentity.profile = 'stale-profile'
    $stalePath = Join-Path $run.path 'stale-selection.json'
    Write-YLTestJson -Path $stalePath -Value $stale
    $rejected = Invoke-QuickEntry -Arguments (@('-Action', 'Run', '-Selection', $stalePath) + $arguments) -ExpectedExit 1
    if ($rejected.status -cne 'Failed' -or $rejected.reason -notmatch 'Stale test selection') { throw 'The CLI consumed a stale selection.' }
    $subset = [IO.File]::ReadAllText($discovery.selection) | ConvertFrom-Json -DateKind String
    $subset.tests = @($subset.tests[0])
    $subsetPath = Join-Path $run.path 'partial-selection.json'
    Write-YLTestJson -Path $subsetPath -Value $subset
    $rejected = Invoke-QuickEntry -Arguments (@('-Action', 'Run', '-Selection', $subsetPath) + $arguments) -ExpectedExit 1
    if ($rejected.status -cne 'Failed' -or $rejected.reason -notmatch 'selection.*discovery|discovery.*selection') { throw 'The CLI silently consumed a partial module selection.' }
    $summary.checks++
    'PASS: stale and partial saved selections are refused by the real CLI'

    $clean = Invoke-QuickEntry -Arguments (@('-Action', 'Clean') + $arguments)
    if ($clean.action -cne 'Clean' -or $clean.status -cne 'Completed' -or $clean.apply) { throw 'Clean must default to a non-test-pass preview.' }
    $summary.checks++
    'PASS: CLI cleanup defaults to a read-only retention preview'
    $summary.status = 'Passed'
}
catch { $summary.reason = $_.Exception.Message; throw }
finally {
    $summary.endUtc = [DateTime]::UtcNow.ToString('o')
    try { Write-YLTestJson -Path (Join-Path $run.path 'summary.json') -Value $summary }
    finally { $lease.Dispose() }
    $null = Invoke-YLTestRotation -Workspace $workspace -Key $run.key
    "Quick CLI contract evidence: $($run.path)"
}
