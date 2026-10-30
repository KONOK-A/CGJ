#requires -Version 7.6
#requires -PSEdition Core

function Invoke-HybridCLROwnedProcess {
    <#
    .SYNOPSIS
    Runs one explicitly owned process; only an observed, unforced exit0 can return successfully.
    .DESCRIPTION
    Dot-source this file. Arguments are recorded verbatim and MUST NOT contain secrets.
    The caller validates the actual project/owner binding; this function records that fixed binding.
    Log must be EvidenceRoot/Run.log. Existing logs/receipts and reparse-point paths are refused.
    No redirected streams are supported: the executable owns its explicit log file.
    AfterExit runs only after an observed exit0 and must return exactly one System.Boolean.
    Optional CompletionSealPath must be EvidenceRoot/Run.completion.json, initially absent.
    Only its presence/immutable bytes are observed, not business success. Before first observation,
    execution timeout wins over a late seal (including time spent reading/publishing its fact).
    A seal observed within that budget switches to ExitGraceMilliseconds from first detection.
    A still-live child at grace expiry is PostCompletionExitStalled, never process success.
    Seal hashes/links are checked each observation and again during finalization; transient changes
    between observations are not detectable. Producers must atomically publish then preserve seals.
    The process timeout does not bound Process.Start or a caller's synchronous AfterExit validator.
    Missing terminal evidence means ObserverLost/ExitUnknown, never an inferred exit0. OS termination
    of this observer can bypass finally, including the small Start-to-process-receipt window.
    Kill(true) is best effort; alive/observedExit describe ONLY the direct child, not all descendants.
    Filesystem checks reject links at inspection time, not hostile concurrent directory replacement.
    .OUTPUTS
    A result with succeeded=true and an int exitCode=0. Every other outcome is a terminating error;
    After lifecycle initialization, Exception.Data['HybridCLRProcessResult'] carries the facts, not
    an implicitly usable exit code. Preflight rejection starts no child and writes no receipts.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateNotNull()][Diagnostics.ProcessStartInfo]$StartInfo,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$EvidenceRoot,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Log,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Project,
        [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run,
        [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$OwnerSha256,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$TimeoutMilliseconds,
        [scriptblock]$AfterExit,
        [Threading.CancellationToken]$CancellationToken = [Threading.CancellationToken]::None,
        [ValidateNotNullOrEmpty()][string]$CompletionSealPath,
        [ValidateRange(1, 2147483647)][int]$ExitGraceMilliseconds = 120000
    )
    Set-StrictMode -Version 3.0
    $ErrorActionPreference = 'Stop'
    if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -ne 7 -or $PSVersionTable.PSVersion.Minor -ne 6) {
        throw 'Invoke-HybridCLROwnedProcess requires Windows PowerShell Core 7.6.x.'
    }

    function Get-OwnedPath([string]$Path) {
        if ($Path -notmatch '^[A-Za-z]:[\\/]' -or -not [IO.Path]::IsPathFullyQualified($Path) -or
            $Path.Substring(2).Contains(':') -or $Path -match '[<>"|?*]' -or
            $Path.Split([char[]]'\/') -contains '.' -or $Path.Split([char[]]'\/') -contains '..') {
            throw "An explicit local filesystem path is required: $Path"
        }
        foreach ($part in $Path.Split([char[]]'\/')) {
            if ($part.EndsWith(' ') -or $part.EndsWith('.')) { throw "Ambiguous Windows path: $Path" }
        }
        $full = [IO.Path]::GetFullPath($Path)
        for ($cursor = $full; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
            try { $attributes = [IO.File]::GetAttributes($cursor) }
            catch [IO.FileNotFoundException] { continue }
            catch [IO.DirectoryNotFoundException] { continue }
            if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked process paths are forbidden: $cursor" }
        }
        return $full
    }

    $EvidenceRoot = (Get-OwnedPath $EvidenceRoot).TrimEnd('\')
    $Log = Get-OwnedPath $Log
    $Project = Get-OwnedPath $Project
    $exe = Get-OwnedPath $StartInfo.FileName
    $null = Get-OwnedPath $StartInfo.WorkingDirectory
    if (-not [IO.Directory]::Exists($EvidenceRoot) -or $EvidenceRoot.Length -le 3 -or
        -not $Log.Equals([IO.Path]::Combine($EvidenceRoot, $Run + '.log'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Use an existing evidence directory and its exact Run.log child.'
    }
    if ($StartInfo.UseShellExecute -or $StartInfo.Arguments.Length -ne 0 -or $StartInfo.RedirectStandardOutput -or
        $StartInfo.RedirectStandardError -or $StartInfo.RedirectStandardInput) {
        throw 'UseShellExecute/redirected streams/raw Arguments are unsupported; use ArgumentList and an explicit child log.'
    }
    $identityPath = $Log + '.identity.json'
    $processPath = $Log + '.process.json'
    $killPath = $Log + '.kill.json'
    $terminalPath = $Log + '.exit.json'
    $monitorCompletion = $PSBoundParameters.ContainsKey('CompletionSealPath')
    $completionReceiptPath = $null
    $newPaths = @($Log, $identityPath, $processPath, $killPath, $terminalPath)
    if ($monitorCompletion) {
        $CompletionSealPath = Get-OwnedPath $CompletionSealPath
        if (-not $CompletionSealPath.Equals([IO.Path]::Combine($EvidenceRoot, $Run + '.completion.json'), [StringComparison]::OrdinalIgnoreCase)) {
            throw 'CompletionSealPath must be the exact EvidenceRoot/Run.completion.json child.'
        }
        $completionReceiptPath = $Log + '.completion.json'
        $newPaths += @($CompletionSealPath, $completionReceiptPath)
    }
    foreach ($path in $newPaths) {
        $null = Get-OwnedPath $path
        if ([IO.Path]::Exists($path)) { throw "Preserve existing process evidence: $path" }
    }

    $jsonOptions = [Text.Json.JsonSerializerOptions]::new()
    $jsonOptions.WriteIndented = $true
    $utf8 = [Text.UTF8Encoding]::new($false)
    $record = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $wrapper = [Diagnostics.Process]::GetCurrentProcess()
    try {
        $initial = @{
            schemaVersion = 1; purpose = 'YLFramework-HybridCLR-owned-process-v1'; invocationId = [Guid]::NewGuid().ToString('N')
            wrapperPid = $PID; wrapperStartUtc = $wrapper.StartTime.ToUniversalTime().ToString('o'); wrapperExe = [Environment]::ProcessPath
            requestedUtc = [DateTime]::UtcNow.ToString('o'); startUtc = $null; pid = $null; exe = $exe
            project = $Project; evidenceRoot = $EvidenceRoot; log = $Log; run = $Run; ownerSha256 = $OwnerSha256.ToLowerInvariant()
            arguments = [string[]]@($StartInfo.ArgumentList); timeoutMilliseconds = $TimeoutMilliseconds
            terminalStatus = 'InProgress'; observedExit = $false; exitCode = $null; observerError = $null
            cancellationReason = $null; timedOut = $false; editorForcedCleanup = $false
            killRequestedUtc = $null; killRequestPersisted = $false; killInvoked = $false; cleanupError = $null
            alive = $null; childState = 'NotStarted'; handleRetained = $false
            validationRequested = ($null -ne $AfterExit); artifactPassed = $false; succeeded = $false; utc = $null
            completionSealPath = $(if ($monitorCompletion) { $CompletionSealPath } else { $null })
            completionReceiptPath = $completionReceiptPath; completionObserved = $false; completionObservedUtc = $null
            completionObservedElapsedMilliseconds = $null; completionSealSha256 = $null; exitGraceMilliseconds = $ExitGraceMilliseconds
            observerLostPolicy = 'Missing terminal = ObserverLost/ExitUnknown; in-progress facts never prove exit0.'
        }
        foreach ($entry in $initial.GetEnumerator()) { $record.Add($entry.Key, $entry.Value) }
    } finally { $wrapper.Dispose() }

    function Write-OwnedFact([string]$Path) {
        $null = Get-OwnedPath $Path
        $temporary = $Path + '.pending-' + $record['invocationId']
        $json = [Text.Json.JsonSerializer]::Serialize($record, $record.GetType(), $jsonOptions)
        $bytes = $utf8.GetBytes($json)
        $stream = [IO.File]::Open($temporary, 'CreateNew', 'Write', 'None')
        try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
        [IO.File]::Move($temporary, $Path, $false)
    }

    function Observe-CompletionSeal {
        $null = Get-OwnedPath $CompletionSealPath
        try { $attributes = [IO.File]::GetAttributes($CompletionSealPath) }
        catch [IO.FileNotFoundException] {
            if ($record['completionObserved']) { throw 'Observed completion seal disappeared.' }
            return
        } catch [IO.DirectoryNotFoundException] {
            if ($record['completionObserved']) { throw 'Observed completion seal parent disappeared.' }
            return
        }
        if ($attributes -band [IO.FileAttributes]::Directory) { throw 'Completion seal must be a regular file.' }
        $detectedUtc = [DateTime]::UtcNow.ToString('o')
        $detectedElapsed = $clock.ElapsedMilliseconds
        $stream = [IO.File]::Open($CompletionSealPath, 'Open', 'Read', 'Read')
        try { $sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
        finally { $stream.Dispose() }
        $null = Get-OwnedPath $CompletionSealPath
        if ([IO.File]::GetAttributes($CompletionSealPath) -band [IO.FileAttributes]::Directory) {
            throw 'Completion seal was replaced by a directory.'
        }
        if ($record['completionObserved']) {
            if ($sha256 -cne $record['completionSealSha256']) { throw 'Observed completion seal bytes changed.' }
        } else {
            $record['completionObserved'] = $true
            $record['completionObservedUtc'] = $detectedUtc
            $record['completionObservedElapsedMilliseconds'] = $detectedElapsed
            $record['completionSealSha256'] = $sha256
            Write-OwnedFact $completionReceiptPath
        }
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $StartInfo
    $identityWritten = $false
    $startAttempted = $false
    $started = $false
    $observationCompleted = $false
    $failureException = $null
    try {
        Write-OwnedFact $identityPath
        $identityWritten = $true
        $CancellationToken.ThrowIfCancellationRequested()
        $startAttempted = $true
        $clock = [Diagnostics.Stopwatch]::StartNew()
        $started = $process.Start()
        if (-not $started) { throw 'Process.Start did not create a child.' }
        # Keep the original kernel handle through observation and bounded cleanup; never reopen by PID.
        $handle = $process.SafeHandle
        if ($handle.IsInvalid -or $handle.IsClosed) { throw 'No usable child process handle.' }
        $record['handleRetained'] = $true
        $record['pid'] = $process.Id
        $record['startUtc'] = $process.StartTime.ToUniversalTime().ToString('o')
        $record['childState'] = 'Running'
        $record['alive'] = $true
        Write-OwnedFact $processPath
        while (-not $process.HasExited) {
            $CancellationToken.ThrowIfCancellationRequested()
            if (-not $record['completionObserved']) {
                $remaining = [long]$TimeoutMilliseconds - $clock.ElapsedMilliseconds
                if ($remaining -le 0) {
                    $record['terminalStatus'] = 'TimedOut'
                    $record['timedOut'] = $true
                    break
                }
            }
            if ($monitorCompletion) {
                $previouslyObserved = $record['completionObserved']
                Observe-CompletionSeal
                $CancellationToken.ThrowIfCancellationRequested()
                if (-not $previouslyObserved -and $clock.ElapsedMilliseconds -ge $TimeoutMilliseconds) {
                    $record['terminalStatus'] = 'TimedOut'
                    $record['timedOut'] = $true
                    break
                }
            }
            if ($record['completionObserved']) {
                $remaining = [long]$ExitGraceMilliseconds - ($clock.ElapsedMilliseconds - $record['completionObservedElapsedMilliseconds'])
                if ($remaining -le 0) {
                    $CancellationToken.ThrowIfCancellationRequested()
                    if ($process.HasExited) { break }
                    $record['terminalStatus'] = 'PostCompletionExitStalled'
                    break
                }
            }
            $null = $process.WaitForExit([int][Math]::Min(200, $remaining))
        }
        $CancellationToken.ThrowIfCancellationRequested()
        if ($record['terminalStatus'] -cnotin @('TimedOut', 'PostCompletionExitStalled')) {
            $CancellationToken.ThrowIfCancellationRequested()
            if (-not $process.HasExited) { throw 'The wait ended without an observed exit.' }
            if ($monitorCompletion) { Observe-CompletionSeal }
            $CancellationToken.ThrowIfCancellationRequested()
            $record['exitCode'] = $process.ExitCode
            $record['observedExit'] = $true
            $record['terminalStatus'] = $(if ($record['exitCode'] -eq 0) { 'Exited' } else { 'NonZeroExit' })
            if ($record['exitCode'] -eq 0 -and $null -ne $AfterExit) {
                $validated = & $AfterExit
                if ($validated -isnot [bool]) { throw 'AfterExit must return exactly one System.Boolean, with no other success output.' }
                $record['artifactPassed'] = $validated
                if (-not $validated) { $record['terminalStatus'] = 'ArtifactFailed' }
            }
            $CancellationToken.ThrowIfCancellationRequested()
        }
        $observationCompleted = $true
    } catch [OperationCanceledException] {
        $failureException = $_.Exception
        $record['terminalStatus'] = 'Cancelled'
        $record['cancellationReason'] = $_.Exception.ToString()
    } catch [Management.Automation.PipelineStoppedException] {
        $failureException = $_.Exception
        $record['terminalStatus'] = 'Cancelled'
        $record['cancellationReason'] = 'PowerShell pipeline stopped.'
    } catch {
        $failureException = $_.Exception
        $record['terminalStatus'] = $(if ($startAttempted -and -not $started) { 'StartFailed' } else { 'ObserverError' })
        $record['observerError'] = $_.Exception.ToString()
    } finally {
        # Stopping a pipeline can skip catch. All critical finalization below uses .NET directly,
        # including JSON serialization: no cmdlets, scriptblock writers, or output pipelines.
        if (-not $observationCompleted -and $record['terminalStatus'] -cin @('InProgress', 'Exited', 'NonZeroExit', 'ArtifactFailed', 'PostCompletionExitStalled')) {
            $record['terminalStatus'] = 'Cancelled'
            $record['cancellationReason'] = 'Observer interrupted before a terminal outcome.'
        }
        if ($started) {
            try {
                if ($process.HasExited) {
                    $record['exitCode'] = $process.ExitCode
                    $record['observedExit'] = $true
                    $record['alive'] = $false
                } else { $record['alive'] = $true }
            } catch {
                $record['alive'] = $null
                $record['cleanupError'] = $_.Exception.ToString()
            }
            if (-not $record['observedExit']) {
                $record['editorForcedCleanup'] = $true
                $record['killRequestedUtc'] = [DateTime]::UtcNow.ToString('o')
                try {
                    for ($cursor = $killPath; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
                        try { $attributes = [IO.File]::GetAttributes($cursor) }
                        catch [IO.FileNotFoundException] { continue }
                        catch [IO.DirectoryNotFoundException] { continue }
                        if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked kill receipt path: $cursor" }
                    }
                    $killFact = [Collections.Generic.Dictionary[string, object]]::new($record, [StringComparer]::Ordinal)
                    $killFact['terminalStatus'] = 'KillRequested'
                    $killFact['killRequestPersisted'] = $true
                    $temporary = $killPath + '.pending-' + $record['invocationId']
                    $json = [Text.Json.JsonSerializer]::Serialize($killFact, $killFact.GetType(), $jsonOptions)
                    $bytes = $utf8.GetBytes($json)
                    $stream = [IO.File]::Open($temporary, 'CreateNew', 'Write', 'None')
                    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
                    [IO.File]::Move($temporary, $killPath, $false)
                    $record['killRequestPersisted'] = $true
                    $record['killInvoked'] = $true
                    $process.Kill($true)
                } catch {
                    $record['cleanupError'] = [string]$record['cleanupError'] + [Environment]::NewLine + $_.Exception.ToString()
                }
                try {
                    if ($record['killInvoked']) { $null = $process.WaitForExit(30000) }
                    if ($process.HasExited) {
                        $record['exitCode'] = $process.ExitCode
                        $record['observedExit'] = $true
                        $record['alive'] = $false
                    } else {
                        $record['alive'] = $true
                        $record['cleanupError'] = [string]$record['cleanupError'] + [Environment]::NewLine + 'Child is still alive; cleanup was not confirmed.'
                    }
                } catch {
                    $record['alive'] = $null
                    $record['cleanupError'] = [string]$record['cleanupError'] + [Environment]::NewLine + $_.Exception.ToString()
                }
            }
            if ($record['observedExit']) { $record['childState'] = 'Exited' }
            elseif ($record['alive'] -eq $true) { $record['childState'] = 'Alive' }
            else { $record['childState'] = 'Unknown' }
        }
        if ($record['completionObserved']) {
            try {
                for ($cursor = $CompletionSealPath; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
                    $attributes = [IO.File]::GetAttributes($cursor)
                    if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked completion seal path: $cursor" }
                }
                $stream = [IO.File]::Open($CompletionSealPath, 'Open', 'Read', 'Read')
                try { $sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
                finally { $stream.Dispose() }
                if ($sha256 -cne $record['completionSealSha256']) { throw 'Observed completion seal bytes changed before terminal persistence.' }
            } catch {
                if ($record['terminalStatus'] -cne 'Cancelled') { $record['terminalStatus'] = 'ObserverError' }
                $record['observerError'] = [string]$record['observerError'] + [Environment]::NewLine + 'Completion seal revalidation failed: ' + $_.Exception.ToString()
            }
        }
        if ($monitorCompletion -and ($CancellationToken.IsCancellationRequested -or $PSCmdlet.Stopping)) {
            $record['terminalStatus'] = 'Cancelled'
            if ($null -eq $record['cancellationReason']) { $record['cancellationReason'] = 'Observer stopped or cancellation requested during completion lifecycle.' }
        }
        try { $process.Dispose() } catch {
            $record['cleanupError'] = [string]$record['cleanupError'] + [Environment]::NewLine + $_.Exception.ToString()
        }
        $record['succeeded'] = ($record['terminalStatus'] -ceq 'Exited' -and $record['observedExit'] -and
            $null -ne $record['exitCode'] -and $record['exitCode'] -eq 0 -and -not $record['editorForcedCleanup'] -and
            $null -eq $record['observerError'] -and $null -eq $record['cleanupError'] -and
            (-not $record['validationRequested'] -or $record['artifactPassed']))
        $record['utc'] = [DateTime]::UtcNow.ToString('o')
        if ($identityWritten) {
            try {
                for ($cursor = $terminalPath; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
                    try { $attributes = [IO.File]::GetAttributes($cursor) }
                    catch [IO.FileNotFoundException] { continue }
                    catch [IO.DirectoryNotFoundException] { continue }
                    if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked terminal receipt path: $cursor" }
                }
                $temporary = $terminalPath + '.pending-' + $record['invocationId']
                $json = [Text.Json.JsonSerializer]::Serialize($record, $record.GetType(), $jsonOptions)
                $bytes = $utf8.GetBytes($json)
                $stream = [IO.File]::Open($temporary, 'CreateNew', 'Write', 'None')
                try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
                [IO.File]::Move($temporary, $terminalPath, $false)
            } catch {
                $failureException = $_.Exception
                $record['terminalStatus'] = 'ObserverError'
                $record['succeeded'] = $false
                $record['observerError'] = [string]$record['observerError'] + [Environment]::NewLine + 'Terminal persistence failed: ' + $_.Exception.ToString()
            }
        }
    }
    if (-not $record['succeeded']) {
        $exception = [InvalidOperationException]::new("Owned process failed: $($record['terminalStatus']); evidence=$terminalPath", $failureException)
        $exception.Data['HybridCLRProcessResult'] = $record
        $PSCmdlet.ThrowTerminatingError([Management.Automation.ErrorRecord]::new(
            $exception, ('HybridCLROwnedProcess.' + $record['terminalStatus']), [Management.Automation.ErrorCategory]::OperationStopped, $Log))
    }
    return [pscustomobject]$record
}
