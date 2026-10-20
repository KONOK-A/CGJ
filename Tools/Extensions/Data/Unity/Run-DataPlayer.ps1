[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string] $BuildRun,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string] $Run,
    [switch] $NoGraphics
)
$ErrorActionPreference = 'Stop'
$root = 'G:\Unity\MB26'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..')).TrimEnd('\')
if ($project -ne 'G:\Unity\Projects\CGJ2024') { throw 'Not the authorized main workspace.' }
$owner = [IO.File]::ReadAllText((Join-Path $root 'owner.json')) | ConvertFrom-Json
if ([IO.Path]::GetFullPath($owner.projectPath).TrimEnd('\') -ne $project -or $owner.sourceTip -ne '5dce4f3e39e2f4bd11602e65cabace5f56108cf8') { throw 'Not the main B output owner.' }
$build = [IO.File]::ReadAllText((Join-Path $root "$BuildRun/build.json")) | ConvertFrom-Json
if (-not $build.passed -or $build.exitCode -ne 0 -or $build.error -or $build.restoreError -or $build.result -ne 'Succeeded' -or $build.development -or $build.options -ne 'None' -or -not $build.settingsRestored) { throw 'A successful owned Release build and all post-build checks are required.' }
$settings = [IO.File]::ReadAllText((Join-Path $root "$BuildRun/settings-restored.json")) | ConvertFrom-Json
$complete = [IO.File]::ReadAllText((Join-Path $root "$BuildRun/gate-complete.json")) | ConvertFrom-Json
if (-not $settings.restored -or -not $settings.memoryRestored -or -not $complete.closed -or $complete.pid -ne $owner.pid -or $build.pid -ne $owner.pid -or [IO.Path]::GetFullPath($build.projectPath) -ne $project) { throw 'Main build owner/settings-byte closure is not verified.' }
$exe = Join-Path $root "$BuildRun/Player/BData.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw 'No built B Player.' }
$exeHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
if ($exeHash -ne $build.executableSha256 -or [IO.Path]::GetFullPath($build.output) -ne $exe) { throw 'Build executable bytes/path changed.' }
$folder = Join-Path $root $Run
if (Test-Path -LiteralPath $folder) { throw 'Evidence already exists.' }
$null = [IO.Directory]::CreateDirectory($folder)
$arguments = @('-screen-width', '960', '-screen-height', '540', '-screen-fullscreen', '0', '-logFile', (Join-Path $folder 'player.log'), '-dataProbeOutput', $folder, '-UNITY_MCP_KEEP_CONNECTED=false', '-UNITY_MCP_START_SERVER=false')
if ($NoGraphics) { $arguments += '-nographics' }
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $exe; $start.Arguments = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '; $start.UseShellExecute = $false
$start.EnvironmentVariables['CI'] = 'true'
$receipt = @{ executable = $exe; executableSha256 = $exeHash; buildRun = $BuildRun; backend = $build.backend; arguments = $arguments; startedUtc = [DateTime]::UtcNow.ToString('o') }
$process = [Diagnostics.Process]::Start($start)
$receipt.pid = $process.Id
[IO.File]::WriteAllText((Join-Path $folder 'run.json'), ($receipt | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
"B Player PID=$($process.Id) exe=$exe output=$folder"
$deadline = [DateTime]::UtcNow.AddMinutes(5)
do {
    $exited = $process.WaitForExit(250)
    if (-not $exited) { $process.Refresh(); if ($process.MainWindowHandle -ne [IntPtr]::Zero) { $receipt.windowHandle = $process.MainWindowHandle.ToInt64() } }
    if (-not $exited -and [DateTime]::UtcNow -gt $deadline) { throw "Owned Player exceeded five minutes; PID $($process.Id) and evidence are retained, not killed." }
} while (-not $exited)
$receipt.exitCode = $process.ExitCode; $receipt.endedUtc = [DateTime]::UtcNow.ToString('o')
[IO.File]::WriteAllText((Join-Path $folder 'run.json'), ($receipt | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
"B Player PID=$($process.Id) exit=$($process.ExitCode)"
if ($process.ExitCode -ne 0) { throw "Player exited $($process.ExitCode); original evidence retained." }
if (-not (Test-Path -LiteralPath (Join-Path $folder 'player-result.json'))) { throw 'Normal process exit is not a passing probe: result missing.' }
$result = [IO.File]::ReadAllText((Join-Path $folder 'player-result.json')) | ConvertFrom-Json
if (-not $result.passed) { throw 'The actual Player probe failed.' }
if (@($result.errorLogs).Count -ne 0) { throw 'The Player reported Error/Assert/Exception logs.' }
if ($result.development -or $result.actualBackend -ne $build.backend -or $result.pid -ne $receipt.pid) { throw 'Actual Player identity/backend differs from this build/run.' }
if (-not $NoGraphics -and (-not $receipt.windowHandle -or $result.graphics -match '^Null')) { throw 'No actual graphical Player window/device was observed.' }
$log = Join-Path $folder 'player.log'
$text = [IO.File]::ReadAllText($log)
if ([regex]::Matches($text, 'B_DATA_PLAYER_OK').Count -ne 1) { throw 'Expected one actual Player success marker.' }
if ($text -match '(?im)Exception:|error CS[0-9]+|Crash!!!|Assertion failed|^ERROR:|UnityEngine[.]Debug:LogError|\[Error\]|\[Exception\]') { throw 'Player log contains an error; retain and inspect original output.' }
$receipt.playerResultSha256 = (Get-FileHash -LiteralPath (Join-Path $folder 'player-result.json') -Algorithm SHA256).Hash.ToLowerInvariant()
$receipt.logSha256 = (Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash.ToLowerInvariant()
$receipt.logChecked = $true
[IO.File]::WriteAllText((Join-Path $folder 'run.json'), ($receipt | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
