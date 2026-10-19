#requires -Version 7.6
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $EvidenceRoot,
    [ValidateSet('Generate','Build','Export','Player')][string] $Operation = 'Player',
    [ValidateSet('Mono2x','IL2CPP')][string] $Backend = 'Mono2x', [switch] $CandidateValidation,
    [string] $BuildName = 'Original', [string] $RunId, [string] $Command = 'offline',
    [string] $StoreName = 'none', [ValidateSet('none','encrypted')][string] $Mode = 'none',
    [int] $Expect = 1, [int] $Target = 2, [string] $Checkpoint, [string] $Fault,
    [ValidateSet('valid','missing','wrong')][string] $Key = 'valid',
    [ValidateSet('pin','default','wrong')][string] $Trust = 'pin', [switch] $RemoteMounts, [string] $ErrorKind, [string] $ErrorReason, [int] $TimeoutSeconds = 300
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($EvidenceRoot)
$carrier = (Get-Content -LiteralPath (Join-Path $root 'carrier-location.json') -Raw | ConvertFrom-Json).Carrier
$owner = Get-Content -LiteralPath (Join-Path $carrier 'r810-owner.json') -Raw | ConvertFrom-Json
if ($owner.Purpose -cne 'Resource-8.10-owned-carrier' -or $owner.Evidence -cne $root -or $owner.Carrier -cne $carrier) { throw 'Owned carrier mismatch.' }
if ($BuildName -notmatch '^[A-Za-z0-9-]+$' -or ($StoreName -notmatch '^[a-z0-9-]+$')) { throw 'Simple explicit build and Store names required.' }
# 固定非生产夹具材料，只以子进程环境注入，绝不写内容key文件。
$env:YL_RESOURCE_CONTENT_KEY = [Convert]::ToBase64String([Security.Cryptography.SHA256]::HashData([Text.Encoding]::ASCII.GetBytes('R810 isolated nonproduction acceptance content only')))
try {
    if ($Operation -ne 'Player') {
        $unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
        $method = if ($Operation -eq 'Generate') { 'Generate' } elseif ($Operation -eq 'Export') { 'Export' } else { 'Build' }
        $log = Join-Path $root ($Operation + '-' + $BuildName + '.log')
        if (Test-Path -LiteralPath $log) { throw 'Never overwrite a prior Editor log.' }
        $others = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like ('*' + $carrier + '*') -and $_.CommandLine -notlike '*AssetImportWorker*' })
        if ($others.Count) { throw 'This carrier already has a Unity instance; recover that same run.' }
        $editorStart = [Diagnostics.ProcessStartInfo]::new($unity)
        $editorStart.UseShellExecute = $false
        foreach ($argument in @('-batchmode','-quit','-projectPath',$carrier,'-buildTarget','Win64','-executeMethod',('ResourcePlayerAcceptanceBuild.' + $method),'-r810-build',$BuildName,'-logFile',$log)) { $editorStart.ArgumentList.Add($argument) }
        $editorStart.ArgumentList.Add('-r810-backend'); $editorStart.ArgumentList.Add($Backend)
        if ($CandidateValidation) { $editorStart.ArgumentList.Add('-r810-il2cpp-candidate') }
        $editor = [Diagnostics.Process]::Start($editorStart)
        try { $editor.WaitForExit(); if ($editor.ExitCode -ne 0) { throw "Unity $Operation failed with code $($editor.ExitCode). Preserve $log" } }
        finally { $editor.Dispose() }
        "R810 $Operation finished: $log"
        return
    }
    if ($RunId -notmatch '^[a-z0-9-]+$') { throw 'Explicit fresh RunId required.' }
    $run = Join-Path $root ('Runs/' + $RunId)
    if (Test-Path -LiteralPath $run) { throw 'Never overwrite a prior Player run.' }
    if (-not (Test-Path -LiteralPath (Join-Path $root 'Runs') -PathType Container)) { throw 'Run parent missing.' }
    $build = Join-Path $carrier ('Builds/' + $BuildName)
    $report = Get-Content -LiteralPath (Join-Path $build 'BuildReport.json') -Raw | ConvertFrom-Json
    if ($report.Result -cne 'Succeeded' -or $report.Errors -ne 0 -or $report.Backend -cne $Backend) { throw "A successful exact $Backend Player BuildReport is required." }
    foreach ($row in $report.PlayerFiles) {
        $path = Join-Path $build $row.Path
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $row.Sha256) { throw "Player file identity changed: $($row.Path)" }
    }
    $null = [IO.Directory]::CreateDirectory($run)
    $null = [IO.Directory]::CreateDirectory((Join-Path $run 'tmp'))
    $result = Join-Path $run 'result.json'
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $build 'ResourceAcceptance.exe'))
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.WorkingDirectory = $build
    foreach ($arg in @('-batchmode','-screen-fullscreen','0','-screen-width','160','-screen-height','120','-logFile',(Join-Path $run 'Player.log'),
        '--r810-out',$result,'--r810-store',(Join-Path $root ('Stores/' + $StoreName)),'--r810-command',$Command,'--r810-mode',$Mode,
        '--r810-expect',"$Expect",'--r810-target',"$Target",'--r810-key',$Key,'--r810-trust',$Trust,'--r810-remote',$RemoteMounts.IsPresent.ToString().ToLowerInvariant())) { $start.ArgumentList.Add($arg) }
    $start.ArgumentList.Add('--r810-backend'); $start.ArgumentList.Add($Backend)
    if ($ErrorKind) { $start.ArgumentList.Add('--r810-error'); $start.ArgumentList.Add($ErrorKind) }
    if ($ErrorReason) { $start.ArgumentList.Add('--r810-reason'); $start.ArgumentList.Add($ErrorReason) }
    if ($Checkpoint) { $start.ArgumentList.Add('--r810-checkpoint'); $start.ArgumentList.Add($Checkpoint) }
    if ($Fault) { $start.ArgumentList.Add('--r810-fault'); $start.ArgumentList.Add($Fault) }
    $start.Environment['TEMP'] = Join-Path $run 'tmp'; $start.Environment['TMP'] = Join-Path $run 'tmp'
    $start.Environment['YL_RESOURCE_CONTENT_KEY'] = $env:YL_RESOURCE_CONTENT_KEY
    $fileWatch = $null
    if ($Mode -eq 'encrypted') {
        Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ResourcePlayerFileWatch.cs.txt')))
        $fileWatch = [ResourcePlayerFileWatch]::new($root, $run, (Join-Path $root ('Stores/' + $StoreName)))
    }
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    $processId = $process.Id
    [IO.File]::WriteAllText((Join-Path $run 'process-start.json'), (@{ Pid=$processId; Executable=$start.FileName; Arguments=$start.ArgumentList; StartedUtc=[DateTime]::UtcNow.ToString('O') } | ConvertTo-Json -Depth 6))
    $killed = $false; $timedOut = $false
    try {
        if (($Checkpoint -and $Fault -ne 'throw') -or $Command -eq 'remote-kill-partial') {
            $watch = [IO.FileSystemWatcher]::new($run,'result.json.checkpoint')
            try {
                if (-not [IO.File]::Exists($result + '.checkpoint')) { $null = $watch.WaitForChanged([IO.WatcherChangeTypes]::Created, $TimeoutSeconds * 1000) }
                if (-not [IO.File]::Exists($result + '.checkpoint')) { throw 'The owned Player did not reach its declared checkpoint.' }
                $reported = [int][IO.File]::ReadAllText($result + '.checkpoint')
                if ($reported -ne $processId -or $process.HasExited -or $process.MainModule.FileName -cne $start.FileName) { throw 'Checkpoint process identity mismatch; refuse to stop it.' }
                $process.Kill($true); $killed = $true
            } finally { $watch.Dispose() }
        }
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { $timedOut=$true; $process.Kill($true); $process.WaitForExit(); }
        $body = if ([IO.File]::Exists($result)) { Get-Content -LiteralPath $result -Raw | ConvertFrom-Json } else { $null }
        if ($fileWatch) { $fileWatch.Dispose() }
        $passed = -not $timedOut -and (($killed -and $null -ne $body) -or (-not $killed -and $process.ExitCode -eq 0 -and $null -ne $body -and $body.Passed)) -and ($null -eq $fileWatch -or ($fileWatch.Errors -eq 0 -and $fileWatch.Plaintext -eq 0))
        $receipt = @{ Pid=$processId; ExitCode=$process.ExitCode; ControlledKill=$killed; TimedOut=$timedOut; Passed=$passed; Result=$result; CompletedUtc=[DateTime]::UtcNow.ToString('O'); WriteObservation=if ($fileWatch) { @{ Errors=$fileWatch.Errors; Plaintext=$fileWatch.Plaintext; ProbeMisses=$fileWatch.Misses; ControlEntriesNotProbed=$fileWatch.ControlEntriesNotProbed; Attribution='Unavailable'; Scope='Owned run/TEMP/Store event observation; mutable Store JSON/next/ReplaceFile temporary controls are metadata-only, not read; not proof of all transient writes' } } else { $null } }
        [IO.File]::WriteAllText((Join-Path $run 'stdout.log'), $stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $run 'stderr.log'), $stderr.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $run 'process-result.json'), ($receipt | ConvertTo-Json))
        $receipt | ConvertTo-Json -Compress
        if (-not $passed) { throw 'Actual Player gate failed; preserve this run and original failure.' }
    } finally {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit(); }
        if ($fileWatch) { $fileWatch.Dispose() }
        $process.Dispose()
    }
} finally { $env:YL_RESOURCE_CONTENT_KEY = $null }
