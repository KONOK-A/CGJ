#requires -Version 7.6
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run,
    [string]$Filter = 'YLFramework.Tests.Integrations.HybridCLR.HybridCLRBuildPipelineTests',
    [string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021'
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$owner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json
if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-independent-reception') { throw 'Wrong receiver owner.' }
$project = [IO.Path]::GetFullPath($owner.project)
$active = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($project) })
if ($active.Count) { throw 'Only one Editor per receiver is allowed.' }
$log = Join-Path $Root ($Run + '.log')
$result = Join-Path $Root ($Run + '.xml')
if ([IO.File]::Exists($log) -or [IO.File]::Exists($result)) { throw 'Preserve prior test evidence.' }
$unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $Root) -or -not (Test-Path -LiteralPath $unity)) { throw 'Explicit execution parents must exist.' }
$start = [Diagnostics.ProcessStartInfo]::new($unity)
$start.UseShellExecute = $false; $start.WorkingDirectory = $Root
foreach ($argument in @('-batchmode','-projectPath',$project,'-buildTarget','Win64','-runTests','-testPlatform','EditMode','-testFilter',$Filter,'-testResults',$result,'-logFile',$log)) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($start)
[IO.File]::WriteAllText($log + '.process.json', (@{ pid=$process.Id; project=$project; filter=$Filter; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
if (-not $process.WaitForExit(1800000)) { $process.Kill($true); throw 'The actual Unity test run exceeded its bounded execution window.' }
$code = $process.ExitCode; $process.Dispose()
[IO.File]::WriteAllText($log + '.exit.json', (@{ exitCode=$code; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
if (-not [IO.File]::Exists($result)) { throw "No actual NUnit XML was produced; Unity exit=$code" }
$xml = [xml][IO.File]::ReadAllText($result)
$summary = $xml.'test-run'
if ($code -ne 0 -or $summary.result -ne 'Passed' -or [int]$summary.total -le 0 -or [int]$summary.failed -ne 0 -or [int]$summary.skipped -ne 0) { throw "Actual Unity tests did not pass: result=$($summary.result), passed=$($summary.passed), failed=$($summary.failed), skipped=$($summary.skipped); XML=$result" }
"Unity actual NUnit result=$($summary.result); passed=$($summary.passed); failed=$($summary.failed); skipped=$($summary.skipped); XML=$result"
