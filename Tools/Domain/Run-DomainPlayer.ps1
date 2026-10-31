[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $Carrier, [ValidateSet('Mono', 'IL2CPP')][string] $Backend, [Parameter(Mandatory = $true)][string] $Run)
$ErrorActionPreference = 'Stop'
if ($Run -notmatch '^[A-Za-z0-9_-]+$' -or $Carrier -notmatch '^[A-Za-z0-9_-]+$') { throw 'Unsafe identity.' }
$owner = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$project = Join-Path $owner "Logs\YLFramework.Domain\Carriers\$Carrier"
if (-not (Test-Path -LiteralPath (Join-Path $project 'domain-carrier.json'))) { throw 'Not an owned carrier.' }
$build = Join-Path $project "Builds\$Backend"
$report = [IO.File]::ReadAllText((Join-Path $build 'BuildReport.json')) | ConvertFrom-Json
$expected = if ($Backend -eq 'Mono') { 'Mono2x' } else { 'IL2CPP' }
if ($report.result -ne 'Succeeded' -or $report.errors -ne 0 -or $report.options -ne 'None' -or $report.backend -ne $expected -or ($Backend -eq 'IL2CPP' -and $report.stripping -ne 'Low')) { throw 'Build report does not establish the requested backend/non-development build.' }
$exe = Join-Path $build 'DomainProbe.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'No real Player executable.' }
if ($Backend -eq 'IL2CPP') { foreach ($file in @('GameAssembly.dll', 'DomainProbe_Data/il2cpp_data/Metadata/global-metadata.dat')) { if (-not (Test-Path -LiteralPath (Join-Path $build $file))) { throw "Missing native artifact: $file" } } }
$output = Join-Path $owner "Logs\YLFramework.Domain\$Run"
if (Test-Path -LiteralPath $output) { throw 'Recover the existing Player result instead of overwriting it.' }
$null = New-Item -ItemType Directory -Path $output -Force
$log = Join-Path $output 'Player.log'
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $exe; $start.WorkingDirectory = $build; $start.UseShellExecute = $false
$start.Arguments = '-screen-fullscreen 0 -screen-width 1024 -screen-height 640 -logFile "' + $log + '"'
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
$process = [Diagnostics.Process]::Start($start)
$pidOwned = $process.Id
$completed = $process.WaitForExit(240000)
if (-not $completed) { $process.Kill(); $process.WaitForExit() }
$exit = $process.ExitCode; $process.Dispose()
$text = if (Test-Path -LiteralPath $log) { [IO.File]::ReadAllText($log) } else { '' }
$pass = $completed -and $exit -eq 0 -and $text.Contains('YL_DOMAIN_PLAYER_PASS') -and $text -match 'GfxDevice:' -and $text -notmatch 'GfxDevice:\s*Null|Crash!!!|Fatal Error|Aborting batchmode'
$record = @{ projectPath = $project; backend = $Backend; buildGuid = $report.guid; exeSha256 = $hash; processId = $pidOwned; completed = $completed; exitCode = $exit; marker = $text.Contains('YL_DOMAIN_PLAYER_PASS'); graphics = ($text -match 'GfxDevice:'); noGraphicsFlag = $false; passed = $pass }
[IO.File]::WriteAllText((Join-Path $output 'execution.json'), ($record | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
$record | ConvertTo-Json -Depth 5
if (-not $pass) { throw "Player execution did not pass. Retained evidence: $output" }
