[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string] $BuildRun,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string] $Run,
    [switch] $NoGraphics
)
$ErrorActionPreference = 'Stop'
$root = 'G:\Unity\BD26'
$owner = [IO.File]::ReadAllText((Join-Path $root 'owner.json')) | ConvertFrom-Json
if ($owner.projectPath -ne 'G:\Unity\Projects\CGJ2024\.kilo\worktrees\fusion-xf-ext-data-14669bf18d117ed8') { throw 'Not the B output root.' }
$exe = Join-Path $root "$BuildRun/Player/BData.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw 'No built B Player.' }
$folder = Join-Path $root $Run
if (Test-Path -LiteralPath $folder) { throw 'Evidence already exists.' }
$null = [IO.Directory]::CreateDirectory($folder)
$arguments = @('-batchmode', '-logFile', (Join-Path $folder 'player.log'), '-dataProbeOutput', $folder, '-UNITY_MCP_KEEP_CONNECTED=false', '-UNITY_MCP_START_SERVER=false')
if ($NoGraphics) { $arguments += '-nographics' }
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $exe; $start.Arguments = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '; $start.UseShellExecute = $false
$start.EnvironmentVariables['CI'] = 'true'
$receipt = @{ executable = $exe; arguments = $arguments; startedUtc = [DateTime]::UtcNow.ToString('o') }
$process = [Diagnostics.Process]::Start($start)
$receipt.pid = $process.Id
[IO.File]::WriteAllText((Join-Path $folder 'run.json'), ($receipt | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
"B Player PID=$($process.Id) exe=$exe output=$folder"
$process.WaitForExit()
$receipt.exitCode = $process.ExitCode; $receipt.endedUtc = [DateTime]::UtcNow.ToString('o')
[IO.File]::WriteAllText((Join-Path $folder 'run.json'), ($receipt | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
"B Player PID=$($process.Id) exit=$($process.ExitCode)"
if ($process.ExitCode -ne 0) { throw "Player exited $($process.ExitCode); original evidence retained." }
if (-not (Test-Path -LiteralPath (Join-Path $folder 'player-result.json'))) { throw 'Normal process exit is not a passing probe: result missing.' }
$result = [IO.File]::ReadAllText((Join-Path $folder 'player-result.json')) | ConvertFrom-Json
if (-not $result.passed) { throw 'The actual Player probe failed.' }
