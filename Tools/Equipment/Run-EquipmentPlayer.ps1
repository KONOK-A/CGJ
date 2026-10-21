param([Parameter(Mandatory = $true)][string]$Gate, [Parameter(Mandatory = $true)][string]$Name, [int]$TimeoutSeconds = 300)
$ErrorActionPreference = 'Stop'
if ($Name -notmatch '^[A-Za-z0-9_-]+$') { throw 'Unsafe run name.' }
$root = [System.IO.Path]::GetFullPath($Gate)
$gateResult = [System.IO.File]::ReadAllText((Join-Path $root 'gate-complete.json')) | ConvertFrom-Json
$build = [System.IO.File]::ReadAllText((Join-Path $root 'BuildReport.json')) | ConvertFrom-Json
if (-not $gateResult.succeeded -or $build.result -ne 'Succeeded' -or $build.options -ne 'None' -or $build.totalErrors -ne 0) { throw 'Actual successful non-Development build is required.' }
if (-not (Test-Path -LiteralPath (Join-Path $root 'settings-restored.json'))) { throw 'Settings restoration evidence is missing.' }
$backend = if ($gateResult.il2cpp) { 'IL2CPP' } else { 'Mono' }
if (($gateResult.il2cpp -and ($build.backend -ne 'IL2CPP' -or $build.stripping -ne 'Low' -or $build.compiler -ne 'Release')) -or
    (-not $gateResult.il2cpp -and ($build.backend -ne 'Mono2x' -or $build.stripping -ne 'Disabled'))) { throw 'Unexpected build configuration.' }
$exe = Join-Path $gateResult.output 'EquipmentProbe.exe'
$entry = @($build.files | Where-Object { $_.path -eq $exe -or $_.path.Replace('/', '\') -eq $exe.Replace('/', '\') })
if ($entry.Count -ne 1 -or (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $entry[0].sha256) { throw 'Executable does not match this BuildReport.' }
$run = Join-Path $root $Name
if (Test-Path -LiteralPath $run) { throw 'Run output already exists; do not overwrite evidence.' }
[System.IO.Directory]::CreateDirectory($run) | Out-Null
$report = Join-Path $run 'player-result.json'
$log = Join-Path $run 'player.log'
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $exe
$start.WorkingDirectory = $gateResult.output
$start.UseShellExecute = $false
$start.Arguments = '-force-d3d11 -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile "' + $log + '" --eq-run "' + $gateResult.run + '" --eq-result "' + $report + '"'
$process = New-Object System.Diagnostics.Process
$process.StartInfo = $start
if (-not $process.Start()) { throw 'Could not start the owned Player.' }
[System.IO.File]::WriteAllText((Join-Path $run 'process.json'), (@{ pid = $process.Id; exe = $exe; run = $gateResult.run; started = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { throw "Player timeout; owned process $($process.Id) and evidence are preserved, not killed." }
$exitCode = $process.ExitCode
$process.Dispose()
if (-not (Test-Path -LiteralPath $report)) { throw "Player exited $exitCode without a report. Full log: $log" }
$value = [System.IO.File]::ReadAllText($report) | ConvertFrom-Json
$text = [System.IO.File]::ReadAllText($log)
$errors = @($text -split '\r?\n' | Where-Object { $_ -match '(?i)(\b[A-Za-z]*Exception\b|\bError:|Assertion failed|\bFatal\b|\bCrash!!!\b|YL_EQUIPMENT_PLAYER_FAIL)' })
$markers = [regex]::Matches($text, 'YL_EQUIPMENT_PLAYER_PASS run=' + [regex]::Escape($gateResult.run) + '; backend=' + $backend + '; checks=15')
$passed = $exitCode -eq 0 -and $value.passed -and $value.quitObserved -and -not $value.development -and $value.graphical -and
    $value.window -ne '0' -and $value.width -gt 0 -and $value.height -gt 0 -and $value.backend -eq $backend -and
    $value.unity -eq '2022.3.62f3c1' -and $value.run -eq $gateResult.run -and $value.checks.Count -eq 15 -and $markers.Count -eq 1 -and $errors.Count -eq 0
$verdict = @{ passed = $passed; exitCode = $exitCode; backend = $backend; run = $gateResult.run; checks = $value.checks; errors = $errors; logSha256 = (Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash; completed = [DateTime]::UtcNow.ToString('o') }
$json = $verdict | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText((Join-Path $run 'verdict.json'), $json)
$json
if (-not $passed) { exit 1 }
