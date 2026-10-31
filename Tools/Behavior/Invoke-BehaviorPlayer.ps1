#requires -Version 7.6
[CmdletBinding()]
param([Parameter(Mandatory)][string] $RunDirectory, [switch] $VerifyExisting)
$ErrorActionPreference = 'Stop'
$RunDirectory = [IO.Path]::GetFullPath($RunDirectory)
if (-not $RunDirectory.StartsWith('C:\Users\Kano\AppData\Local\Temp\kilo\BA26\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $RunDirectory)) { throw 'Use an existing dedicated A run.' }
$build = [IO.File]::ReadAllText((Join-Path $RunDirectory 'build-results.json')) | ConvertFrom-Json
if ($build.status -ne 'BuildSucceeded_PlayerNotRun' -or $build.development -or $build.errors -ne 0) { throw 'The report is not a successful non-Development build.' }
$exe = [IO.Path]::GetFullPath($build.output)
if (-not $exe.StartsWith($RunDirectory + '\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $exe)) { throw 'Player output does not belong to this run.' }
$jobPath = Join-Path $RunDirectory 'player-job.json'
if ($VerifyExisting) {
    $job = [IO.File]::ReadAllText($jobPath) | ConvertFrom-Json
    $log = Join-Path $RunDirectory 'player.log'
    if ($job.executableSha256 -ne (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant() -or $job.logSha256 -ne (Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Existing executable/log identity changed.' }
    $pass = @([IO.File]::ReadAllLines($log) | Where-Object { $_ -match '^YL_BEHAVIOR_ACCEPTANCE result=PASS;' })
    $expectedCompany = $build.company.Replace('.', '_')
    $valid = $job.exitCode -eq 0 -and $pass.Count -eq 1 -and $pass[0] -match 'development=False' -and $pass[0] -match ([regex]::Escape('/' + $expectedCompany + '/' + $build.product) + '$')
    $validation = [ordered]@{ utc = [DateTime]::UtcNow.ToString('o'); originalJob = $jobPath; originalPid = $job.pid; originalExitCode = $job.exitCode; executableSha256 = $job.executableSha256; logSha256 = $job.logSha256; passLine = $pass; unityCompanyPathNormalization = 'periods become underscores'; passed = [bool]$valid; graphicalPresentationVerified = $false }
    $validationPath = Join-Path $RunDirectory 'player-validation.json'
    if (Test-Path -LiteralPath $validationPath) { throw 'Preserve previous validation evidence.' }
    [IO.File]::WriteAllText($validationPath, ($validation | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    if (-not $valid) { throw 'Same-run Player validation failed.' }
    'Same actual Player PID/log/executable validated: exit 0, non-Development PASS, isolated data path.'
    return
}
if (Test-Path -LiteralPath $jobPath) { throw 'Preserve the prior Player run rather than overwrite its evidence.' }
$logPath = Join-Path $RunDirectory 'player.log'
$record = [ordered]@{ startUtc = [DateTime]::UtcNow.ToString('o'); backend = $build.actualBackend; buildGuid = $build.guid; executable = $exe; executableSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant(); company = $build.company; product = $build.product; pid = $null; endUtc = $null; exitCode = $null; passLine = $null; logSha256 = $null; graphicalPresentationVerified = $false }
$utf8 = [Text.UTF8Encoding]::new($false)
$start = [Diagnostics.ProcessStartInfo]::new($exe)
$start.UseShellExecute = $false
$start.WorkingDirectory = [IO.Path]::GetDirectoryName($exe)
foreach ($argument in @('-batchmode', '-nographics', '-logFile', $logPath)) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
if (-not $process.Start()) { throw 'Player did not start.' }
$record.pid = $process.Id
[IO.File]::WriteAllText($jobPath, ($record | ConvertTo-Json -Depth 5), $utf8)
"Started owned Player PID $($process.Id), backend=$($build.actualBackend), buildGuid=$($build.guid)"
$process.WaitForExit()
$record.endUtc = [DateTime]::UtcNow.ToString('o')
$record.exitCode = $process.ExitCode
if (Test-Path -LiteralPath $logPath) {
    $lines = [IO.File]::ReadAllLines($logPath)
    $record.passLine = @($lines | Where-Object { $_ -match '^YL_BEHAVIOR_ACCEPTANCE result=PASS;' })
    $record.logSha256 = (Get-FileHash -LiteralPath $logPath -Algorithm SHA256).Hash.ToLowerInvariant()
}
[IO.File]::WriteAllText($jobPath, ($record | ConvertTo-Json -Depth 5), $utf8)
if ($record.exitCode -ne 0 -or @($record.passLine).Count -ne 1 -or $record.passLine[0] -notmatch 'development=False') { throw "Owned Player failed or did not produce the required non-Development PASS. See $logPath" }
$expectedSuffix = '/' + $build.company.Replace('.', '_') + '/' + $build.product
if ($record.passLine[0] -notmatch ([regex]::Escape($expectedSuffix) + '$')) { throw 'Player persistent-data identity was not isolated as built.' }
"Actual Player exit 0 with one non-Development PASS. Log and executable SHA retained. No graphical presentation claim."
$record.passLine
