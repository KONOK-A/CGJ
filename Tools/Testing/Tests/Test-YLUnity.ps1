#requires -Version 7.6
#requires -PSEdition Core
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $WorkspaceRoot,
    [Parameter(Mandatory)][string] $Unity,
    [ValidateNotNullOrEmpty()][string] $Module = 'Reddot',
    [ValidateSet('Daily', 'Special')][string] $Suite = 'Daily',
    [ValidateRange(1, 86400)][int] $TimeoutSeconds = 120
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Common/Process.ps1')
. (Join-Path $PSScriptRoot '../Common/Results.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$workspace = Initialize-YLTestWorkspace -WorkspaceRoot $WorkspaceRoot -Repository $repository
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($Unity)
$versionKey = $version.ProductVersion
$run = New-YLTestDirectory -Workspace $workspace -Kind Result -Key "Tooling-UnityCLI-$Suite-$Module-$versionKey"
$lease = Lock-YLTestDirectory -Workspace $workspace -Path $run.path
$summary = [ordered]@{
    schemaVersion = 1; ownerId = $run.id; action = 'Run'; suite = 'Tooling'; module = 'UnityCLI'; profile = $versionKey
    targetModule = $Module; targetSuite = $Suite; status = 'Failed'; endUtc = $null; reason = $null; processes = @(); checks = 0
}
$script:steps = 0

function Invoke-UnityEntry([string] $Action, [string] $Selection) {
    $script:steps++
    $start = [Diagnostics.ProcessStartInfo]::new([Environment]::ProcessPath)
    $start.UseShellExecute = $false
    $start.WorkingDirectory = $repository
    $arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', (Join-Path $PSScriptRoot '../Invoke-YLTests.ps1'),
        '-Action', $Action, '-Suite', $Suite, '-Module', $Module, '-Unity', $Unity, '-WorkspaceRoot', $WorkspaceRoot, '-TimeoutSeconds', [string]$TimeoutSeconds)
    if ($Selection) { $arguments += @('-Selection', $Selection) }
    foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
    $log = Join-Path $run.path "cli-$script:steps.log"
    $fact = Invoke-YLTestProcess -StartInfo $start -Log $log -TimeoutMilliseconds 1800000
    $summary.processes += $fact
    Assert-YLTestProcessSucceeded $fact
    return [IO.File]::ReadAllText($log) | ConvertFrom-Json -DateKind String
}

try {
    $discovery = Invoke-UnityEntry -Action Discover
    if ($discovery.status -cne 'Completed' -or $discovery.testResult.status -cne 'NotRun') { throw 'Unity discovery must not claim a test pass.' }
    $selection = [IO.File]::ReadAllText($discovery.selection) | ConvertFrom-Json -DateKind String
    $modes = @($selection.tests.mode | Sort-Object -Unique)
    if ($modes.Count -eq 0 -or @($modes | Where-Object { $_ -cnotin @('EditMode', 'PlayMode') }).Count -ne 0) { throw 'Discovery must report actual supported nonempty modes.' }
    $definition = @(([IO.File]::ReadAllText((Join-Path $PSScriptRoot '../TestSuites.json')) | ConvertFrom-Json -DateKind String).modules | Where-Object { $_.name -ceq $Module })[0]
    $expectedModes = if ($Suite -cne 'Daily') { $modes } elseif ('modes' -cin $definition.PSObject.Properties.Name) { @($definition.modes) } else { @('EditMode', 'PlayMode') }
    if (-not [Collections.Generic.HashSet[string]]::new([string[]]$modes, [StringComparer]::Ordinal).SetEquals([string[]]$expectedModes)) { throw 'Actual mode discovery does not match the declared module profile.' }
    foreach ($mode in $expectedModes) {
        if (@($selection.tests | Where-Object { $_.mode -ceq $mode }).Count -eq 0) { throw "No real $mode leaves were discovered." }
    }
    $summary.checks++
    'PASS: official UTF discovery returns real leaves for the declared execution profile'

    $execution = Invoke-UnityEntry -Action Run -Selection $discovery.selection
    if ($execution.status -cne 'Passed' -or $execution.testResult.total -ne $selection.tests.Count -or $execution.xml.Count -ne $modes.Count) {
        throw 'Unity selected leaves, all selected mode XML files and actual exits did not close.'
    }
    if ($execution.host.id -cne $discovery.host.id -or $execution.unity.actualVersion -cne $discovery.unity.actualVersion) { throw 'Compatible runs did not retain the same owned host/version.' }
    if (@($execution.runtimeReferences | Where-Object { $_ -match 'Tests|TestRunner|Acceptance|Analyzer|^YLFramework(?:\..*)?\.Editor(?:\.|$)' }).Count -ne 0) {
        throw 'The runtime assembly gained a test/tooling dependency in the actual Editor compilation graph.'
    }
    if ($Suite -ceq 'Special' -and 'prepareSessionScene' -cin $definition.PSObject.Properties.Name -and $definition.prepareSessionScene) {
        foreach ($xml in $execution.xml) {
            $scenes = [IO.File]::ReadAllText((Join-Path ([IO.Path]::GetDirectoryName($xml)) 'session-scenes.json')) | ConvertFrom-Json -DateKind String
            if ($scenes.savedScene -cne 'Assets/YLTestingProfile/Empty.unity' -or $scenes.sceneCount -ne 2 -or $scenes.activeRootCount -ne 0) {
                throw 'The actual owned session scene prerequisites were not established before the test run.'
            }
        }
    }
    $summary.checks++
    'PASS: real selected-mode results and normal exits close without runtime test references'

    $repeat = Invoke-UnityEntry -Action Run
    if ($repeat.status -cne 'Passed' -or $repeat.host.id -cne $execution.host.id -or $repeat.testResult.total -ne $execution.testResult.total) {
        throw 'The reusable host did not repeat the real module lifecycle successfully.'
    }
    $summary.checks++
    'PASS: repeated real execution reuses the host and preserves the discovered module contract'
    $summary.status = 'Passed'
}
catch { $summary.reason = $_.Exception.Message; throw }
finally {
    $summary.endUtc = [DateTime]::UtcNow.ToString('o')
    try { Write-YLTestJson -Path (Join-Path $run.path 'summary.json') -Value $summary }
    finally { $lease.Dispose() }
    $null = Invoke-YLTestRotation -Workspace $workspace -Key $run.key
    "Unity CLI contract evidence: $($run.path)"
}
