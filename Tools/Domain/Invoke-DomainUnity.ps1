[CmdletBinding()]
param(
    [ValidateSet('Import', 'EditMode', 'PlayMode', 'Execute')][string] $Action = 'Import',
    [Parameter(Mandatory = $true)][string] $Run,
    [string] $Filter = 'YLFramework.Tests.Extensions.Domain',
    [string] $Method,
    [switch] $AsyncMethod,
    [switch] $Graphics,
    [string] $Project,
    [string] $Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe',
    [string[]] $Extra = @()
)
$ErrorActionPreference = 'Stop'
$owner = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not (Test-Path -LiteralPath (Join-Path $owner '.kilo\handovers\xf-ext-domain.md'))) { throw 'Wrong C worktree.' }
if ($Run -notmatch '^[A-Za-z0-9_-]+$') { throw 'Unsafe Run.' }
if (-not $Project) { $Project = $owner }
$Project = [IO.Path]::GetFullPath($Project).TrimEnd('\', '/')
if ($Project -ne $owner -and -not $Project.StartsWith($owner + '\Logs\YLFramework.Domain\Carriers\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Project must be the C worktree or its owned acceptance carrier.' }
if (-not (Test-Path -LiteralPath (Join-Path $Project 'ProjectSettings\ProjectVersion.txt'))) { throw 'Not an existing Unity project.' }
if (-not (Test-Path -LiteralPath $Unity)) { throw 'Approved Unity installation not found.' }
$busy = @()
foreach ($process in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") {
    $command = $process.CommandLine
    if (-not $command -or $command -notmatch '(?i)-projectpath\s+(?:"([^"]+)"|(\S+))') { continue }
    $path = if ($matches[1]) { $matches[1] } else { $matches[2] }
    $path = [IO.Path]::GetFullPath($path).TrimEnd('\', '/')
    if ($path -eq $Project) { throw "Project already has Unity PID $($process.ProcessId). No second Editor permitted." }
    if ($command -match '(?i)-batchmode' -and $path -match 'fusion-xf-ext-(behavior|data)') { $busy += $process.ProcessId }
}
if ($busy.Count -ne 0) { throw "A/B heavy Unity jobs active: $($busy -join ','). Defer this run; do not stop them." }
$evidence = Join-Path $owner "Logs\YLFramework.Domain\$Run"
if (Test-Path -LiteralPath $evidence) { throw "Run already exists. Recover its result instead of launching again: $evidence" }
$null = New-Item -ItemType Directory -Path $evidence -Force
$arguments = @('-batchmode', '-projectPath', $Project, '-logFile', (Join-Path $evidence 'Editor.log'))
if (-not $Graphics) { $arguments += '-nographics' }
if ($Action -eq 'Import') { $arguments += '-quit' }
elseif ($Action -eq 'Execute') {
    if (-not $Method) { throw 'Execute requires Method.' }
    $arguments += @('-executeMethod', $Method)
    if (-not $AsyncMethod) { $arguments += '-quit' }
} else {
    $arguments += @('-runTests', '-testPlatform', $Action, '-testFilter', $Filter, '-testResults', (Join-Path $evidence 'results.xml'))
}
$arguments += $Extra
$record = [ordered]@{ action = $Action; projectPath = $Project; unity = $Unity; arguments = $arguments; startedUtc = [DateTime]::UtcNow.ToString('o'); sourceCommit = (& git -C $owner rev-parse HEAD) }
[IO.File]::WriteAllText((Join-Path $evidence 'invocation.json'), ($record | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
"Unity $Action project=$Project evidence=$evidence"
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $Unity
$start.WorkingDirectory = $Project
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.EnvironmentVariables['UNITY_MCP_KEEP_CONNECTED'] = 'false'
$start.EnvironmentVariables['UNITY_MCP_START_SERVER'] = 'false'
$start.EnvironmentVariables['UNITY_MCP_DEV_CONTROL'] = '0'
$record['childProcessOnlyEnvironment'] = @{ UNITY_MCP_KEEP_CONNECTED = 'false'; UNITY_MCP_START_SERVER = 'false'; UNITY_MCP_DEV_CONTROL = '0' }
$start.Arguments = (@($arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' ')
# GUI executables need an explicit synchronous wait; this is not a detached launch.
$child = [Diagnostics.Process]::Start($start)
$record['processId'] = $child.Id
[IO.File]::WriteAllText((Join-Path $evidence 'invocation.json'), ($record | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
$child.WaitForExit()
$exit = $child.ExitCode
$child.Dispose()
$record['finishedUtc'] = [DateTime]::UtcNow.ToString('o')
$record['exitCode'] = $exit
[IO.File]::WriteAllText((Join-Path $evidence 'invocation.json'), ($record | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
"Unity exited=$exit; evidence=$evidence"
if ($exit -ne 0) { throw "Unity $Action failed with exit $exit. Inspect the retained log; do not relaunch this Run." }
