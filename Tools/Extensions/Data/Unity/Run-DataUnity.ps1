[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Import', 'EditMode', 'PlayMode', 'Execute')][string] $Mode,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string] $Run,
    [string] $Filter = '',
    [string] $Method = '',
    [string] $OutputRoot = 'G:\Unity\BD26'
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..')).TrimEnd('\')
$expected = 'G:\Unity\Projects\CGJ2024\.kilo\worktrees\fusion-xf-ext-data-14669bf18d117ed8'
if ($project -ne $expected -or -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) { throw 'Not the authorized B project.' }
$editor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $editor)) { throw 'The authorized Unity version is not installed.' }
foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")) {
    if ($process.CommandLine -and $process.CommandLine.Replace('/', '\').IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "B project already has Unity PID $($process.ProcessId). No second Editor will be started."
    }
}
$lock = Join-Path $project 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lock) {
    $probe = [IO.File]::Open($lock, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $probe.Dispose()
}
$outputRoot = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
if ($outputRoot -ne 'G:\Unity\BD26') { throw 'Only the dedicated short B output root is supported.' }
$marker = Join-Path $outputRoot 'owner.json'
if (Test-Path -LiteralPath $outputRoot) {
    if (-not (Test-Path -LiteralPath $marker)) { throw 'Refusing to take over an unmarked output root.' }
    $owner = [IO.File]::ReadAllText($marker) | ConvertFrom-Json
    if ($owner.projectPath -ne $project) { throw 'Output root belongs to another project.' }
} else {
    if (-not (Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($outputRoot)))) { throw 'Output parent does not exist.' }
    $null = [IO.Directory]::CreateDirectory($outputRoot)
    [IO.File]::WriteAllText($marker, (@{ projectPath = $project; purpose = 'T3-B Unity acceptance'; createdUtc = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
}
$folder = Join-Path $outputRoot $Run
if (Test-Path -LiteralPath $folder) { throw 'Run evidence already exists. Choose a fresh Run name.' }
$null = [IO.Directory]::CreateDirectory($folder)
$arguments = @('-batchmode', '-projectPath', $project, '-logFile', (Join-Path $folder 'editor.log'), '-UNITY_MCP_KEEP_CONNECTED=false', '-UNITY_MCP_START_SERVER=false', '-dataAcceptanceOutput', $folder)
if ($Mode -eq 'Import') { $arguments += '-quit' }
elseif ($Mode -eq 'Execute') {
    if ($Method -notmatch '^YLFramework\..+$') { throw 'Execute requires an explicit YLFramework method.' }
    $arguments += @('-executeMethod', $Method)
} else {
    if ([string]::IsNullOrWhiteSpace($Filter)) { throw 'A nonempty exact test scope is required.' }
    $arguments += @('-runTests', '-testPlatform', $Mode, '-testFilter', $Filter, '-testResults', (Join-Path $folder 'results.xml'), '-dataAcceptanceProfile')
}
foreach ($argument in $arguments) { if ($argument.Contains('"') -or $argument.EndsWith('\')) { throw 'Unsupported command argument quoting.' } }
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $editor
$start.Arguments = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
$start.UseShellExecute = $false
$start.EnvironmentVariables['CI'] = 'true'
$start.EnvironmentVariables['YL_DATA_UNITY_OUTPUT'] = $folder
$receipt = @{ projectPath = $project; editor = $editor; mode = $Mode; filter = $Filter; arguments = $arguments; processEnvironment = @{ CI = 'true' }; startedUtc = [DateTime]::UtcNow.ToString('o') }
$process = [Diagnostics.Process]::Start($start)
$receipt.pid = $process.Id
[IO.File]::WriteAllText((Join-Path $folder 'run.json'), ($receipt | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
"Unity PID=$($process.Id) projectPath=$project mode=$Mode output=$folder"
$process.WaitForExit()
$receipt.exitCode = $process.ExitCode
$receipt.endedUtc = [DateTime]::UtcNow.ToString('o')
[IO.File]::WriteAllText((Join-Path $folder 'run.json'), ($receipt | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
"Unity PID=$($process.Id) exit=$($process.ExitCode). Inspect the retained editor log and test result separately."
if ($process.ExitCode -ne 0) { throw "Unity exited $($process.ExitCode); evidence retained at $folder" }
