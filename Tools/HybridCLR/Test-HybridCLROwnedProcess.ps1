#requires -Version 7.6
#requires -PSEdition Core
[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run = 'suite',
    [ValidateSet('Suite', 'Child', 'Observer')][string]$Mode = 'Suite',
    [string]$FixtureDirectory,
    [ValidatePattern('^[A-Za-z0-9-]+$')][string]$CaseName = 'child',
    [int]$ChildExitCode = 0,
    [ValidateRange(0, 10000)][int]$DelayMilliseconds = 0,
    [string]$Payload = '',
    [ValidateSet('None', 'Publish', 'Change', 'Remove', 'Link', 'Lock', 'OccupyReceipt')][string]$SealBehavior = 'None',
    [ValidateRange(0, 10000)][int]$SealDelayMilliseconds = 0
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -ne 7 -or $PSVersionTable.PSVersion.Minor -ne 6) {
    throw 'These tests require Windows PowerShell Core 7.6.x.'
}
$parent = 'C:\Users\Kano\AppData\Local\Temp\kilo\U63-20261027-c63a1'
$helper = Join-Path $PSScriptRoot 'Invoke-HybridCLROwnedProcess.ps1'
$self = $PSCommandPath
$pwsh = [Environment]::ProcessPath
$launcher = $pwsh
# A Store installation may expose a versioned directory through a junction. Resolve the known
# installation to a non-linked path rather than weakening the helper's executable path guard.
for ($attempt = 0; $attempt -lt 8; $attempt++) {
    $resolvedLink = $false
    for ($cursor = $pwsh; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) {
            $item = $(if ([IO.Directory]::Exists($cursor)) { [IO.DirectoryInfo]::new($cursor) } else { [IO.FileInfo]::new($cursor) })
            $target = $item.ResolveLinkTarget($true)
            if ($null -eq $target) { throw "Cannot resolve installed PowerShell reparse path: $cursor" }
            $relative = [IO.Path]::GetRelativePath($cursor, $pwsh)
            $pwsh = $(if ($relative -ceq '.') { $target.FullName } else { [IO.Path]::Combine($target.FullName, $relative) })
            $resolvedLink = $true
            break
        }
    }
    if (-not $resolvedLink) { break }
}
if ($resolvedLink) { throw 'Installed PowerShell link resolution exceeded its bound.' }
$utf8 = [Text.UTF8Encoding]::new($false)
$ownerHash = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef'

function Assert-TestPath([string]$Path) {
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw 'An absolute test fixture is required.' }
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ([IO.Path]::GetDirectoryName($full) -cne $parent -or [IO.Path]::GetFileName($full) -cnotmatch '^b01-process-[A-Za-z0-9-]+$') {
        throw 'Only a new b01-process-* fixture under the approved parent is allowed.'
    }
    for ($cursor = $full; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked test fixtures are forbidden.' }
    }
}

if ($Mode -eq 'Child') {
    Assert-TestPath $FixtureDirectory
    $current = [Diagnostics.Process]::GetCurrentProcess()
    $sealLock = $null
    try {
        $fact = @{
            pid = $PID; startUtc = $current.StartTime.ToUniversalTime().ToString('o')
            executable = $pwsh; fixtureDirectory = $FixtureDirectory; payload = $Payload
        }
        $childPath = Join-Path $FixtureDirectory ($CaseName + '.log.child.json')
        $stream = [IO.File]::Open(($childPath + '.pending'), 'CreateNew', 'Write', 'None')
        try {
            $bytes = $utf8.GetBytes((ConvertTo-Json -InputObject $fact -Depth 5))
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.Flush($true)
        } finally { $stream.Dispose() }
        [IO.File]::Move(($childPath + '.pending'), $childPath, $false)
        if ($Payload -ceq 'occupy-kill') {
            $stream = [IO.File]::Open((Join-Path $FixtureDirectory ($CaseName + '.log.kill.json')), 'CreateNew', 'Write', 'None')
            try {
                $bytes = $utf8.GetBytes('foreign-kill-request')
                $stream.Write($bytes, 0, $bytes.Length)
                $stream.Flush($true)
            } finally { $stream.Dispose() }
        }
        if ($SealBehavior -cne 'None') {
            $sealPath = Join-Path $FixtureDirectory ($CaseName + '.completion.json')
            $completionReceipt = Join-Path $FixtureDirectory ($CaseName + '.log.completion.json')
            if ($SealDelayMilliseconds -gt 0) { [Threading.Thread]::Sleep($SealDelayMilliseconds) }
            if ($SealBehavior -ceq 'OccupyReceipt') {
                $stream = [IO.File]::Open($completionReceipt, 'CreateNew', 'Write', 'None')
                try {
                    $bytes = $utf8.GetBytes('foreign-completion-receipt')
                    $stream.Write($bytes, 0, $bytes.Length)
                    $stream.Flush($true)
                } finally { $stream.Dispose() }
            }
            $stream = [IO.File]::Open(($sealPath + '.pending'), 'CreateNew', 'Write', 'None')
            try {
                # Deliberately not a business-pass seal: the helper must only observe its bytes.
                $bytes = $utf8.GetBytes('{"businessPassed":false,"producer":"owned-pwsh"}')
                $stream.Write($bytes, 0, $bytes.Length)
                $stream.Flush($true)
            } finally { $stream.Dispose() }
            [IO.File]::Move(($sealPath + '.pending'), $sealPath, $false)
            if ($SealBehavior -cin @('Change', 'Remove', 'Link', 'Lock')) {
                $clock = [Diagnostics.Stopwatch]::StartNew()
                while (-not [IO.File]::Exists($completionReceipt) -and $clock.ElapsedMilliseconds -lt 8000) {
                    [Threading.Thread]::Sleep(10)
                }
                if (-not [IO.File]::Exists($completionReceipt)) { throw 'No actual completion observation was published.' }
                if ($SealBehavior -ceq 'Change') {
                    $stream = [IO.File]::Open(($sealPath + '.changed.pending'), 'CreateNew', 'Write', 'None')
                    try {
                        $bytes = $utf8.GetBytes('changed-after-observation')
                        $stream.Write($bytes, 0, $bytes.Length)
                        $stream.Flush($true)
                    } finally { $stream.Dispose() }
                }
                $clock.Restart()
                $changed = $false
                while (-not $changed -and $clock.ElapsedMilliseconds -lt 2000) {
                    try {
                        switch ($SealBehavior) {
                            'Change' { [IO.File]::Move(($sealPath + '.changed.pending'), $sealPath, $true) }
                            'Remove' { [IO.File]::Delete($sealPath) }
                            'Link' { [IO.File]::Delete($sealPath) }
                            'Lock' { $sealLock = [IO.File]::Open($sealPath, 'Open', 'ReadWrite', 'None') }
                        }
                        $changed = $true
                    } catch [IO.IOException] { [Threading.Thread]::Sleep(10) }
                }
                if (-not $changed) { throw 'Bounded seal mutation setup failed.' }
                if ($SealBehavior -ceq 'Link') {
                    $target = Join-Path $FixtureDirectory ($CaseName + '-link-target')
                    $null = [IO.Directory]::CreateDirectory($target)
                    $null = New-Item -ItemType Junction -Path $sealPath -Target $target -ErrorAction Stop
                }
            }
        }
        if ($DelayMilliseconds -gt 0) { [Threading.Thread]::Sleep($DelayMilliseconds) }
        exit $ChildExitCode
    } finally {
        if ($null -ne $sealLock) { $sealLock.Dispose() }
        $current.Dispose()
    }
}

function New-Invocation([string]$Name, [int]$Code = 0, [int]$Delay = 0, [string]$Text = '') {
    $start = [Diagnostics.ProcessStartInfo]::new($pwsh)
    $start.UseShellExecute = $false
    $start.WorkingDirectory = $FixtureDirectory
    foreach ($argument in @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $self, '-Mode', 'Child',
        '-FixtureDirectory', $FixtureDirectory, '-CaseName', $Name, '-ChildExitCode', [string]$Code,
        '-DelayMilliseconds', [string]$Delay, '-Payload', $Text)) {
        $start.ArgumentList.Add($argument)
    }
    return @{
        StartInfo = $start; EvidenceRoot = $FixtureDirectory; Log = Join-Path $FixtureDirectory ($Name + '.log')
        Project = Join-Path $FixtureDirectory 'project'; Run = $Name; OwnerSha256 = $ownerHash
        TimeoutMilliseconds = 10000
    }
}

function New-CompletionInvocation([string]$Name, [int]$Code = 0, [int]$Delay = 10000, [string]$Behavior = 'Publish') {
    $parameters = New-Invocation $Name $Code $Delay
    $parameters.CompletionSealPath = Join-Path $FixtureDirectory ($Name + '.completion.json')
    $parameters.StartInfo.ArgumentList.Add('-SealBehavior')
    $parameters.StartInfo.ArgumentList.Add($Behavior)
    return $parameters
}

if ($Mode -eq 'Observer') {
    Assert-TestPath $FixtureDirectory
    . $helper
    $parameters = New-Invocation $CaseName 0 10000
    Invoke-HybridCLROwnedProcess @parameters | Out-Null
    return
}

if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'The approved test parent does not exist.' }
$FixtureDirectory = Join-Path $parent ('b01-process-' + $Run + '-' + [Guid]::NewGuid().ToString('N'))
if ([IO.Path]::Exists($FixtureDirectory)) { throw 'Never adopt a previous test fixture.' }
$null = [IO.Directory]::CreateDirectory($FixtureDirectory)
Assert-TestPath $FixtureDirectory
$null = [IO.Directory]::CreateDirectory((Join-Path $FixtureDirectory 'project'))
$cases = [Collections.Generic.List[object]]::new()
$parseResults = [Collections.Generic.List[object]]::new()
$ownedProcesses = [Collections.Generic.List[Diagnostics.Process]]::new()
$cleanupErrors = [Collections.Generic.List[string]]::new()

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-Case([string]$Name, [scriptblock]$Body) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    try {
        & $Body
        $cases.Add(@{ name = $Name; passed = $true; elapsedMilliseconds = $clock.ElapsedMilliseconds; error = $null })
        "PASS $Name"
    } catch {
        $cases.Add(@{ name = $Name; passed = $false; elapsedMilliseconds = $clock.ElapsedMilliseconds; error = $_.ToString() })
        "FAIL ${Name}: $_"
    }
}

function Read-Fact([string]$Name, [string]$Suffix = '.exit.json') {
    return ConvertFrom-Json -AsHashtable -DateKind String -InputObject ([IO.File]::ReadAllText((Join-Path $FixtureDirectory ($Name + '.log' + $Suffix))))
}

function Invoke-Failure([hashtable]$Parameters) {
    $caught = $null
    $returned = $null
    try { $returned = Invoke-HybridCLROwnedProcess @Parameters } catch { $caught = $_ }
    Assert-True ($null -ne $caught) 'A failed/unknown process was returned as success.'
    Assert-True ($null -eq $returned) 'Failure leaked success-pipeline output.'
    return $caught
}

function Wait-Fact([string]$Path, [int]$Milliseconds = 8000) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while (-not [IO.File]::Exists($Path) -and $clock.ElapsedMilliseconds -lt $Milliseconds) {
        [Threading.Thread]::Sleep(20)
    }
    Assert-True ([IO.File]::Exists($Path)) "Bounded wait expired: $Path"
}

function Open-OwnedChild([hashtable]$Fact) {
    Assert-True ($Fact.fixtureDirectory -ceq $FixtureDirectory -and $Fact.executable -ceq $pwsh) 'Foreign child identity.'
    $process = [Diagnostics.Process]::GetProcessById([int]$Fact.pid)
    try {
        $null = $process.SafeHandle
        Assert-True ($process.StartTime.ToUniversalTime().ToString('o') -ceq $Fact.startUtc) 'PID was reused.'
        $ownedProcesses.Add($process)
        return $process
    } catch { $process.Dispose(); throw }
}

try {
    Invoke-Case 'helper-load-is-side-effect-free' {
        Assert-True ([IO.File]::Exists($helper)) 'Missing owned-process helper; old entry points can lose terminal evidence.'
        foreach ($path in @($helper, $self)) {
            $tokens = $null
            $parseErrors = $null
            $null = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors)
            $parseResults.Add(@{ path = $path; errors = $parseErrors.Count })
            Assert-True ($parseErrors.Count -eq 0) "PowerShell parser errors: $path"
        }
        $before = @([IO.Directory]::GetFiles($FixtureDirectory)).Count
        $output = @(. $helper)
        Assert-True ($output.Count -eq 0) 'Dot-sourcing must not execute or emit output.'
        Assert-True (@([IO.Directory]::GetFiles($FixtureDirectory)).Count -eq $before) 'Loading wrote evidence.'
    }
    if ([IO.File]::Exists($helper)) {
        . $helper
        Invoke-Case 'normal-zero-no-validator-and-literal-arguments' {
            $text = 'literal spaces [x] ''apostrophe'' "quote" $dollar; trailing\'
            $parameters = New-Invocation 'zero' 0 0 $text
            $result = Invoke-HybridCLROwnedProcess @parameters
            $fact = Read-Fact 'zero'
            $identity = Read-Fact 'zero' '.identity.json'
            $started = Read-Fact 'zero' '.process.json'
            $child = Read-Fact 'zero' '.child.json'
            Assert-True ($result.exitCode -is [int] -and $result.exitCode -eq 0 -and $result.succeeded) 'Normal zero failed.'
            Assert-True ($fact.terminalStatus -ceq 'Exited' -and $fact.observedExit -and $fact.exitCode -eq 0) 'OS exit not observed.'
            Assert-True (-not $fact.artifactPassed -and -not $fact.validationRequested -and -not $fact.editorForcedCleanup) 'No validator is not artifact failure.'
            Assert-True ($null -eq $fact.observerError -and $fact.alive -eq $false -and $fact.handleRetained) 'Incomplete terminal identity.'
            Assert-True ($identity.exitCode -eq $null -and -not $identity.observedExit -and $identity.terminalStatus -ceq 'InProgress') 'In-progress fact invented exit.'
            Assert-True ($identity.wrapperPid -eq $PID -and $identity.ownerSha256 -ceq $ownerHash -and $identity.project -ceq $parameters.Project) 'Missing ownership.'
            Assert-True ($started.pid -eq $child.pid -and $started.startUtc -ceq $child.startUtc -and $started.exe -ceq $pwsh) 'Wrong real process identity.'
            Assert-True ($started.invocationId -ceq $fact.invocationId -and $identity.invocationId -ceq $fact.invocationId) 'Receipts are not linked.'
            Assert-True ($child.payload -ceq $text -and $fact.arguments[-1] -ceq $text) 'ArgumentList was reparsed.'
        }
        Invoke-Case 'nonzero-is-terminal-failure' {
            $null = Invoke-Failure (New-Invocation 'nonzero' 23)
            $fact = Read-Fact 'nonzero'
            Assert-True ($fact.terminalStatus -ceq 'NonZeroExit' -and $fact.exitCode -eq 23 -and $fact.observedExit -and -not $fact.succeeded) 'Nonzero misclassified.'
        }
        Invoke-Case 'real-timeout-has-durable-kill-request-and-bounded-cleanup' {
            $parameters = New-Invocation 'timeout' 0 10000
            $parameters.TimeoutMilliseconds = 1200
            $clock = [Diagnostics.Stopwatch]::StartNew()
            $null = Invoke-Failure $parameters
            $fact = Read-Fact 'timeout'
            $kill = Read-Fact 'timeout' '.kill.json'
            Assert-True ($clock.ElapsedMilliseconds -lt 35000 -and $clock.ElapsedMilliseconds -ge 1100) 'Timeout was not bounded/real.'
            Assert-True ($fact.terminalStatus -ceq 'TimedOut' -and $fact.timedOut -and $fact.editorForcedCleanup -and $fact.killInvoked) 'Timeout lost intervention.'
            Assert-True ($kill.terminalStatus -ceq 'KillRequested' -and $kill.invocationId -ceq $fact.invocationId) 'No durable pre-kill fact.'
            Assert-True ($fact.observedExit -and $null -ne $fact.exitCode -and $fact.alive -eq $false -and $null -eq $fact.cleanupError) 'Cleanup did not observe exit.'
            Assert-True ([DateTime]::Parse($kill.killRequestedUtc) -le [DateTime]::Parse($fact.utc)) 'Kill request was not first.'
        }
        Invoke-Case 'start-failure-preserves-null-exit' {
            $parameters = New-Invocation 'start-failure'
            $parameters.StartInfo.FileName = Join-Path $FixtureDirectory 'missing.exe'
            $caught = Invoke-Failure $parameters
            $fact = Read-Fact 'start-failure'
            Assert-True ($fact.terminalStatus -ceq 'StartFailed' -and -not $fact.observedExit -and $null -eq $fact.exitCode -and $null -eq $fact.pid) 'Start failure fabricated exit0.'
            Assert-True ($null -ne $fact.observerError -and $null -ne $caught.Exception.Data['HybridCLRProcessResult']) 'Failure facts are inaccessible.'
        }
        Invoke-Case 'after-exit-throw-is-observer-error-not-os-success' {
            $parameters = New-Invocation 'validator-error'
            $parameters.AfterExit = { throw [IO.IOException]::new('Owned validator failure') }
            $null = Invoke-Failure $parameters
            $fact = Read-Fact 'validator-error'
            Assert-True ($fact.terminalStatus -ceq 'ObserverError' -and $fact.observedExit -and $fact.exitCode -eq 0 -and $fact.observerError.Contains('Owned validator failure')) 'Validator exception was green.'
        }
        Invoke-Case 'validator-true' {
            $parameters = New-Invocation 'validator-true'
            $parameters.AfterExit = { $true }
            $result = Invoke-HybridCLROwnedProcess @parameters
            Assert-True ($result.succeeded -and $result.artifactPassed -and $result.validationRequested) 'True validation failed.'
        }
        Invoke-Case 'validator-false' {
            $parameters = New-Invocation 'validator-false'
            $parameters.AfterExit = { $false }
            $null = Invoke-Failure $parameters
            Assert-True ((Read-Fact 'validator-false').terminalStatus -ceq 'ArtifactFailed') 'False validation was green.'
        }
        Invoke-Case 'validator-requires-single-real-bool' {
            $parameters = New-Invocation 'validator-text'
            $parameters.AfterExit = { 'false' }
            $null = Invoke-Failure $parameters
            Assert-True ((Read-Fact 'validator-text').terminalStatus -ceq 'ObserverError') 'Truthy string accepted as bool.'
        }
        Invoke-Case 'occupied-result-refused-before-start' {
            $parameters = New-Invocation 'occupied'
            $path = $parameters.Log + '.exit.json'
            [IO.File]::WriteAllText($path, 'foreign-result', $utf8)
            $null = Invoke-Failure $parameters
            Assert-True ([IO.File]::ReadAllText($path) -ceq 'foreign-result') 'Existing result overwritten.'
            Assert-True (-not [IO.File]::Exists($parameters.Log + '.identity.json')) 'Occupied result still launched.'
        }
        Invoke-Case 'late-locked-result-is-explicit-failure' {
            $parameters = New-Invocation 'late-locked'
            $path = $parameters.Log + '.exit.json'
            $locks = [Collections.Generic.List[IO.FileStream]]::new()
            $parameters.AfterExit = {
                $stream = [IO.File]::Open($path, 'CreateNew', 'Write', 'None')
                $locks.Add($stream)
                $bytes = [Text.Encoding]::UTF8.GetBytes('foreign-locked-result')
                $stream.Write($bytes, 0, $bytes.Length)
                $stream.Flush($true)
                $true
            }.GetNewClosure()
            try {
                $caught = Invoke-Failure $parameters
                $fact = $caught.Exception.Data['HybridCLRProcessResult']
                Assert-True ($fact.terminalStatus -ceq 'ObserverError' -and -not $fact.succeeded -and $null -ne $fact.observerError) 'Persistence error was green.'
            } finally { foreach ($stream in $locks) { $stream.Dispose() } }
            Assert-True ([IO.File]::ReadAllText($path) -ceq 'foreign-locked-result') 'Late result overwritten.'
        }
        Invoke-Case 'existing-log-and-reused-run-refused' {
            $null = Invoke-Failure (New-Invocation 'zero')
            $parameters = New-Invocation 'existing-log'
            [IO.File]::WriteAllText($parameters.Log, 'keep', $utf8)
            $null = Invoke-Failure $parameters
            Assert-True ([IO.File]::ReadAllText($parameters.Log) -ceq 'keep') 'Existing log overwritten.'
        }
        Invoke-Case 'junction-root-and-project-refused' {
            $target = Join-Path $FixtureDirectory 'junction-target'
            $link = Join-Path $FixtureDirectory 'junction-link'
            $null = [IO.Directory]::CreateDirectory($target)
            $null = New-Item -ItemType Junction -Path $link -Target $target -ErrorAction Stop
            try {
                $parameters = New-Invocation 'linked-root'
                $parameters.EvidenceRoot = $link
                $parameters.Log = Join-Path $link 'linked-root.log'
                $null = Invoke-Failure $parameters
                $parameters = New-Invocation 'linked-project'
                $parameters.Project = Join-Path $link 'project'
                $null = Invoke-Failure $parameters
                Assert-True (@([IO.Directory]::GetFiles($target)).Count -eq 0) 'Linked path received output.'
            } finally { [IO.Directory]::Delete($link) }
        }
        Invoke-Case 'timeout-must-be-positive-and-explicit' {
            foreach ($value in @(0, -1)) {
                $parameters = New-Invocation ('invalid-timeout-' + [Math]::Abs($value))
                $parameters.TimeoutMilliseconds = $value
                $null = Invoke-Failure $parameters
                Assert-True (-not [IO.File]::Exists($parameters.Log + '.identity.json')) 'Invalid timeout started work.'
            }
        }
        Invoke-Case 'cancel-before-start' {
            $source = [Threading.CancellationTokenSource]::new()
            try {
                $source.Cancel()
                $parameters = New-Invocation 'pre-cancel'
                $parameters.CancellationToken = $source.Token
                $null = Invoke-Failure $parameters
                $fact = Read-Fact 'pre-cancel'
                Assert-True ($fact.terminalStatus -ceq 'Cancelled' -and $null -eq $fact.exitCode -and -not $fact.observedExit -and $null -eq $fact.pid) 'Pre-cancel invented exit.'
            } finally { $source.Dispose() }
        }
        Invoke-Case 'cancel-during-wait-cleans-only-owned-child' {
            $source = [Threading.CancellationTokenSource]::new()
            try {
                $parameters = New-Invocation 'cancel-wait' 0 10000
                $parameters.CancellationToken = $source.Token
                $source.CancelAfter(1200)
                $null = Invoke-Failure $parameters
                $fact = Read-Fact 'cancel-wait'
                Assert-True ($fact.terminalStatus -ceq 'Cancelled' -and $fact.killInvoked -and $fact.observedExit -and $fact.alive -eq $false) 'Cancellation lost terminal cleanup.'
                Assert-True ((Read-Fact 'cancel-wait' '.kill.json').terminalStatus -ceq 'KillRequested') 'Cancellation killed without evidence.'
            } finally { $source.Dispose() }
        }
        Invoke-Case 'failed-pre-kill-persistence-records-alive-and-unknown-exit' {
            $parameters = New-Invocation 'blocked-kill' 0 10000 'occupy-kill'
            $parameters.TimeoutMilliseconds = 1200
            try {
                $null = Invoke-Failure $parameters
                $fact = Read-Fact 'blocked-kill'
                Assert-True ($fact.terminalStatus -ceq 'TimedOut' -and $fact.alive -eq $true -and $fact.childState -ceq 'Alive') 'Cleanup failure concealed a live child.'
                Assert-True (-not $fact.killInvoked -and -not $fact.killRequestPersisted -and $null -ne $fact.cleanupError) 'Kill ran without its durable request.'
                Assert-True (-not $fact.observedExit -and $null -eq $fact.exitCode -and -not $fact.succeeded) 'Unknown exit was green.'
                Assert-True ([IO.File]::ReadAllText($parameters.Log + '.kill.json') -ceq 'foreign-kill-request') 'Foreign kill evidence overwritten.'
            } finally {
                $child = Open-OwnedChild (Read-Fact 'blocked-kill' '.child.json')
                if (-not $child.HasExited) { $child.Kill($true) }
                Assert-True ($child.WaitForExit(3000)) 'Test-owned cleanup after persistence failure timed out.'
            }
        }
        Invoke-Case 'pipeline-stop-still-writes-finally-without-output-pipeline' {
            $parameters = New-Invocation 'pipeline-stop' 0 10000
            $shell = [Management.Automation.PowerShell]::Create()
            $stopped = $false
            try {
                $null = $shell.AddScript('param($helper, $parameters) . $helper; Invoke-HybridCLROwnedProcess @parameters').AddArgument($helper).AddArgument($parameters)
                $pending = $shell.BeginInvoke()
                Wait-Fact ($parameters.Log + '.process.json')
                Wait-Fact ($parameters.Log + '.child.json')
                $null = Open-OwnedChild (Read-Fact 'pipeline-stop' '.child.json')
                $stop = $shell.BeginStop($null, $null)
                Assert-True ($stop.AsyncWaitHandle.WaitOne(35000)) 'Pipeline stop exceeded bounded cleanup.'
                $shell.EndStop($stop)
                $stopped = $true
                try { $null = $shell.EndInvoke($pending) } catch [Management.Automation.PipelineStoppedException] { }
                $fact = Read-Fact 'pipeline-stop'
                Assert-True ($fact.terminalStatus -ceq 'Cancelled' -and $fact.observedExit -and $fact.alive -eq $false -and -not $fact.succeeded) 'Stopped pipeline lost terminal evidence.'
            } finally {
                if (-not $stopped) {
                    $stop = $shell.BeginStop($null, $null)
                    if ($stop.AsyncWaitHandle.WaitOne(35000)) { $shell.EndStop($stop); $stopped = $true }
                }
                if ($stopped) { $shell.Dispose() } else { $cleanupErrors.Add('Runspace did not stop; it was not disposed with an unbounded wait.') }
            }
        }
        Invoke-Case 'pipeline-stop-during-validator-is-cancelled-not-exited' {
            $parameters = New-Invocation 'validator-stop'
            $shell = [Management.Automation.PowerShell]::Create()
            $ready = [Threading.ManualResetEventSlim]::new($false)
            $stopped = $false
            try {
                $null = $shell.AddScript('param($helper, $parameters, $ready) . $helper; $parameters.AfterExit = { $ready.Set(); Start-Sleep -Milliseconds 10000; $true }; Invoke-HybridCLROwnedProcess @parameters').AddArgument($helper).AddArgument($parameters).AddArgument($ready)
                $pending = $shell.BeginInvoke()
                Assert-True ($ready.Wait(8000)) 'Validator was not reached.'
                $stop = $shell.BeginStop($null, $null)
                Assert-True ($stop.AsyncWaitHandle.WaitOne(35000)) 'Validator pipeline stop exceeded its bound.'
                $shell.EndStop($stop)
                $stopped = $true
                try { $null = $shell.EndInvoke($pending) } catch [Management.Automation.PipelineStoppedException] { }
                $fact = Read-Fact 'validator-stop'
                Assert-True ($fact.terminalStatus -ceq 'Cancelled' -and $fact.observedExit -and $fact.exitCode -eq 0 -and -not $fact.succeeded) 'Validator interruption was labelled successful OS completion.'
            } finally {
                if (-not $stopped) {
                    $stop = $shell.BeginStop($null, $null)
                    if ($stop.AsyncWaitHandle.WaitOne(35000)) { $shell.EndStop($stop); $stopped = $true }
                }
                if ($stopped) { $shell.Dispose(); $ready.Dispose() } else { $cleanupErrors.Add('Validator runspace did not stop within its bound.') }
            }
        }
        Invoke-Case 'force-killed-observer-leaves-only-in-progress-not-exit-zero' {
            $start = [Diagnostics.ProcessStartInfo]::new($pwsh)
            $start.UseShellExecute = $false
            $start.WorkingDirectory = $FixtureDirectory
            foreach ($argument in @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $self, '-Mode', 'Observer',
                '-FixtureDirectory', $FixtureDirectory, '-CaseName', 'observer-lost')) { $start.ArgumentList.Add($argument) }
            $observer = [Diagnostics.Process]::Start($start)
            $ownedProcesses.Add($observer)
            $null = $observer.SafeHandle
            Wait-Fact (Join-Path $FixtureDirectory 'observer-lost.log.process.json')
            Wait-Fact (Join-Path $FixtureDirectory 'observer-lost.log.child.json')
            $child = Open-OwnedChild (Read-Fact 'observer-lost' '.child.json')
            $observer.Kill($false)
            Assert-True ($observer.WaitForExit(3000)) 'Owned observer did not terminate.'
            Assert-True (-not $child.HasExited) 'Child did not survive parent-only termination.'
            Assert-True (-not [IO.File]::Exists((Join-Path $FixtureDirectory 'observer-lost.log.exit.json'))) 'Forced observer unexpectedly promised finally.'
            $identity = Read-Fact 'observer-lost' '.identity.json'
            Assert-True ($null -eq $identity.exitCode -and -not $identity.observedExit -and $identity.terminalStatus -ceq 'InProgress') 'Lost observer invented success.'
            $external = @{
                terminalStatus = 'ObserverLost'; invocationId = $identity.invocationId; observerPid = $observer.Id
                observerObservedExited = $observer.HasExited; childPid = $child.Id; childObservedAlive = -not $child.HasExited
                childExitCode = $null; utc = [DateTime]::UtcNow.ToString('o'); source = 'independent-test-handle'
            }
            [IO.File]::WriteAllText((Join-Path $FixtureDirectory 'observer-lost.external.json'), (ConvertTo-Json -InputObject $external), $utf8)
        }
        Invoke-Case 'completion-seal-normal-zero-is-still-exited-with-default-grace' {
            $parameters = New-CompletionInvocation 'seal-zero' 0 0
            $result = Invoke-HybridCLROwnedProcess @parameters
            $fact = Read-Fact 'seal-zero'
            $completion = Read-Fact 'seal-zero' '.completion.json'
            $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($parameters.CompletionSealPath))).ToLowerInvariant()
            Assert-True ($result.succeeded -and $fact.terminalStatus -ceq 'Exited' -and $fact.exitCode -eq 0 -and -not $fact.editorForcedCleanup) 'A normal seal exit changed OS success semantics.'
            Assert-True ($fact.completionObserved -and $fact.completionSealSha256 -ceq $hash -and $fact.completionSealPath -ceq $parameters.CompletionSealPath) 'Completion identity/hash was not observed.'
            Assert-True ($fact.exitGraceMilliseconds -eq 120000 -and $null -ne $fact.completionObservedUtc) 'Default completion grace/UTC is missing.'
            Assert-True ($completion.invocationId -ceq $fact.invocationId -and $completion.completionSealSha256 -ceq $hash -and $completion.completionObserved) 'Immutable completion fact was not linked.'
            Assert-True (-not $completion.succeeded -and $null -eq $completion.exitCode -and -not $completion.observedExit) 'Completion sidecar fabricated OS success.'
            Assert-True (-not $fact.artifactPassed -and -not $fact.validationRequested) 'The helper interpreted businessPassed from seal bytes.'
        }
        Invoke-Case 'completion-seal-nonzero-is-still-failure' {
            $parameters = New-CompletionInvocation 'seal-nonzero' 23 500
            $parameters.AfterExit = { throw 'A nonzero process must not run AfterExit.' }
            $null = Invoke-Failure $parameters
            $fact = Read-Fact 'seal-nonzero'
            Assert-True ($fact.terminalStatus -ceq 'NonZeroExit' -and $fact.completionObserved -and $fact.exitCode -eq 23 -and -not $fact.succeeded) 'A seal excused a real nonzero exit.'
        }
        Invoke-Case 'completion-seal-grace-stall-is-real-bounded-and-never-success' {
            $parameters = New-CompletionInvocation 'seal-stalled'
            $parameters.TimeoutMilliseconds = 1500
            $parameters.ExitGraceMilliseconds = 1800
            $parameters.AfterExit = { throw 'A forced cleanup must not invoke AfterExit.' }
            $clock = [Diagnostics.Stopwatch]::StartNew()
            $caught = Invoke-Failure $parameters
            $fact = Read-Fact 'seal-stalled'
            $completion = Read-Fact 'seal-stalled' '.completion.json'
            $kill = Read-Fact 'seal-stalled' '.kill.json'
            Assert-True ($clock.ElapsedMilliseconds -ge 1800 -and $clock.ElapsedMilliseconds -lt 35000) 'Exit grace was not real/bounded.'
            Assert-True ($fact.terminalStatus -ceq 'PostCompletionExitStalled' -and -not $fact.succeeded -and -not $fact.timedOut) 'Post-completion stall was excused or called execution timeout.'
            Assert-True ($fact.completionObservedElapsedMilliseconds -lt 1500 -and $clock.ElapsedMilliseconds -gt 1500) 'Completion did not switch from execution budget to grace.'
            Assert-True ($fact.editorForcedCleanup -and $fact.killInvoked -and $fact.observedExit -and $fact.exitCode -ne 0 -and $fact.alive -eq $false) 'Actual cleanup/exit facts were replaced.'
            Assert-True ($null -eq $fact.observerError -and $null -eq $fact.cleanupError -and -not $fact.artifactPassed) 'A stalled process was artifact-validated or had hidden errors.'
            Assert-True ($kill.terminalStatus -ceq 'KillRequested' -and $kill.completionSealSha256 -ceq $completion.completionSealSha256) 'Kill did not retain the earlier completion fact.'
            Assert-True ([DateTime]::Parse($completion.completionObservedUtc) -lt [DateTime]::Parse($kill.killRequestedUtc)) 'Completion sidecar was not published before cleanup.'
            Assert-True ($caught.Exception.Data['HybridCLRProcessResult'].terminalStatus -ceq 'PostCompletionExitStalled') 'The caller lost the explicit failing terminal status.'
        }
        Invoke-Case 'completion-monitor-without-seal-remains-execution-timeout' {
            $parameters = New-CompletionInvocation 'seal-missing' 0 10000 'None'
            $parameters.TimeoutMilliseconds = 1200
            $parameters.ExitGraceMilliseconds = 300
            $null = Invoke-Failure $parameters
            $fact = Read-Fact 'seal-missing'
            Assert-True ($fact.terminalStatus -ceq 'TimedOut' -and $fact.timedOut -and -not $fact.completionObserved -and -not $fact.succeeded) 'Missing seal was allowed a completion exception.'
            Assert-True (-not [IO.File]::Exists($parameters.Log + '.completion.json')) 'Missing seal fabricated a completion fact.'
        }
        Invoke-Case 'unrequested-completion-monitor-never-relaxes-default-timeout' {
            $parameters = New-CompletionInvocation 'seal-no-opt-in'
            $null = $parameters.Remove('CompletionSealPath')
            $parameters.TimeoutMilliseconds = 1200
            $null = Invoke-Failure $parameters
            $fact = Read-Fact 'seal-no-opt-in'
            Assert-True ($fact.terminalStatus -ceq 'TimedOut' -and -not $fact.completionObserved -and $null -eq $fact.completionSealPath) 'A producer file implicitly opted into exit grace.'
            Assert-True ([IO.File]::Exists((Join-Path $FixtureDirectory 'seal-no-opt-in.completion.json')) -and
                -not [IO.File]::Exists($parameters.Log + '.completion.json')) 'Strict default did not ignore the producer seal.'
        }
        Invoke-Case 'late-completion-cannot-rescue-original-execution-deadline' {
            $parameters = New-CompletionInvocation 'seal-late'
            $parameters.TimeoutMilliseconds = 800
            $parameters.ExitGraceMilliseconds = 5000
            $parameters.StartInfo.ArgumentList.Add('-SealDelayMilliseconds')
            $parameters.StartInfo.ArgumentList.Add('1800')
            $null = Invoke-Failure $parameters
            $fact = Read-Fact 'seal-late'
            Assert-True ($fact.terminalStatus -ceq 'TimedOut' -and -not $fact.completionObserved -and $fact.killInvoked) 'Late completion rescued execution timeout.'
        }
        Invoke-Case 'occupied-producer-seal-is-refused-before-start' {
            $parameters = New-CompletionInvocation 'seal-occupied'
            [IO.File]::WriteAllText($parameters.CompletionSealPath, 'foreign-seal', $utf8)
            $caught = Invoke-Failure $parameters
            Assert-True ($caught.Exception -isnot [Management.Automation.ParameterBindingException]) 'Completion interface is missing, not guarded.'
            Assert-True (-not [IO.File]::Exists($parameters.Log + '.identity.json') -and [IO.File]::ReadAllText($parameters.CompletionSealPath) -ceq 'foreign-seal') 'Occupied seal was adopted or overwritten.'
        }
        Invoke-Case 'occupied-completion-sidecar-is-refused-before-start' {
            $parameters = New-CompletionInvocation 'seal-receipt-occupied'
            [IO.File]::WriteAllText(($parameters.Log + '.completion.json'), 'foreign-sidecar', $utf8)
            $caught = Invoke-Failure $parameters
            Assert-True ($caught.Exception -isnot [Management.Automation.ParameterBindingException]) 'Completion interface is missing, not guarded.'
            Assert-True (-not [IO.File]::Exists($parameters.Log + '.identity.json') -and [IO.File]::ReadAllText($parameters.Log + '.completion.json') -ceq 'foreign-sidecar') 'Occupied sidecar was adopted or overwritten.'
        }
        Invoke-Case 'completion-path-must-be-exact-run-child' {
            $parameters = New-CompletionInvocation 'seal-wrong-path'
            $parameters.CompletionSealPath = Join-Path $FixtureDirectory 'other.completion.json'
            $caught = Invoke-Failure $parameters
            Assert-True ($caught.Exception -isnot [Management.Automation.ParameterBindingException]) 'Completion interface is missing, not guarded.'
            Assert-True (-not [IO.File]::Exists($parameters.Log + '.identity.json')) 'Wrong completion path started a child.'
        }
        Invoke-Case 'completion-reparse-path-is-refused-before-start' {
            $parameters = New-CompletionInvocation 'seal-linked'
            $target = Join-Path $FixtureDirectory 'seal-linked-target'
            $null = [IO.Directory]::CreateDirectory($target)
            $null = New-Item -ItemType Junction -Path $parameters.CompletionSealPath -Target $target -ErrorAction Stop
            try {
                $caught = Invoke-Failure $parameters
                Assert-True ($caught.Exception -isnot [Management.Automation.ParameterBindingException]) 'Completion interface is missing, not guarded.'
                Assert-True (-not [IO.File]::Exists($parameters.Log + '.identity.json')) 'Linked seal started a child.'
            } finally { [IO.Directory]::Delete($parameters.CompletionSealPath) }
        }
        Invoke-Case 'completion-grace-must-be-positive' {
            foreach ($value in @(0, -1)) {
                $parameters = New-CompletionInvocation ('seal-invalid-grace-' + [Math]::Abs($value))
                $parameters.ExitGraceMilliseconds = $value
                $caught = Invoke-Failure $parameters
                Assert-True (-not $caught.FullyQualifiedErrorId.Contains('NamedParameterNotFound')) 'Completion grace interface is missing.'
                Assert-True (-not [IO.File]::Exists($parameters.Log + '.identity.json')) 'Invalid grace started a child.'
            }
        }
        foreach ($behavior in @('Change', 'Remove', 'Link', 'Lock')) {
            Invoke-Case ('observed-seal-' + $behavior.ToLowerInvariant() + '-is-observer-error') {
                $parameters = New-CompletionInvocation ('seal-' + $behavior.ToLowerInvariant()) 0 10000 $behavior
                $parameters.ExitGraceMilliseconds = 5000
                try {
                    $null = Invoke-Failure $parameters
                    $fact = Read-Fact $parameters.Run
                    $completion = Read-Fact $parameters.Run '.completion.json'
                    Assert-True ($fact.terminalStatus -ceq 'ObserverError' -and $fact.completionObserved -and $null -ne $fact.observerError -and -not $fact.succeeded) 'Changed/unreadable seal became a known exit exception.'
                    Assert-True ($fact.completionSealSha256 -ceq $completion.completionSealSha256 -and -not $completion.succeeded) 'First completion observation was rewritten.'
                } finally {
                    if ([IO.Directory]::Exists($parameters.CompletionSealPath) -and
                        ([IO.File]::GetAttributes($parameters.CompletionSealPath) -band [IO.FileAttributes]::ReparsePoint)) {
                        [IO.Directory]::Delete($parameters.CompletionSealPath)
                    }
                }
            }
        }
        Invoke-Case 'late-occupied-completion-sidecar-is-observer-error' {
            $parameters = New-CompletionInvocation 'seal-receipt-race' 0 10000 'OccupyReceipt'
            $parameters.ExitGraceMilliseconds = 5000
            $null = Invoke-Failure $parameters
            $fact = Read-Fact 'seal-receipt-race'
            Assert-True ($fact.terminalStatus -ceq 'ObserverError' -and -not $fact.succeeded) 'Late occupied sidecar became an exit exception.'
            Assert-True ([IO.File]::ReadAllText($parameters.Log + '.completion.json') -ceq 'foreign-completion-receipt') 'Foreign sidecar was overwritten.'
        }
        Invoke-Case 'cancel-after-seal-remains-cancelled' {
            $source = [Threading.CancellationTokenSource]::new()
            try {
                $parameters = New-CompletionInvocation 'seal-cancel'
                $parameters.ExitGraceMilliseconds = 5000
                $parameters.CancellationToken = $source.Token
                $source.CancelAfter(1300)
                $null = Invoke-Failure $parameters
                $fact = Read-Fact 'seal-cancel'
                Assert-True ($fact.terminalStatus -ceq 'Cancelled' -and $fact.completionObserved -and -not $fact.succeeded -and $fact.killInvoked) 'A completion seal overrode cancellation.'
            } finally { $source.Dispose() }
        }
        Invoke-Case 'pipeline-stop-after-seal-remains-cancelled' {
            $parameters = New-CompletionInvocation 'seal-pipeline-stop'
            $parameters.ExitGraceMilliseconds = 5000
            $shell = [Management.Automation.PowerShell]::Create()
            $stopped = $false
            try {
                $null = $shell.AddScript('param($helper, $parameters) . $helper; Invoke-HybridCLROwnedProcess @parameters').AddArgument($helper).AddArgument($parameters)
                $pending = $shell.BeginInvoke()
                Wait-Fact ($parameters.Log + '.completion.json')
                $null = Open-OwnedChild (Read-Fact 'seal-pipeline-stop' '.child.json')
                $stop = $shell.BeginStop($null, $null)
                Assert-True ($stop.AsyncWaitHandle.WaitOne(35000)) 'Completion observer stop exceeded bounded cleanup.'
                $shell.EndStop($stop)
                $stopped = $true
                try { $null = $shell.EndInvoke($pending) } catch [Management.Automation.PipelineStoppedException] { }
                $fact = Read-Fact 'seal-pipeline-stop'
                Assert-True ($fact.terminalStatus -ceq 'Cancelled' -and $fact.completionObserved -and -not $fact.succeeded -and $fact.observedExit) 'Pipeline cancellation became post-completion stall.'
            } finally {
                if (-not $stopped) {
                    $stop = $shell.BeginStop($null, $null)
                    if ($stop.AsyncWaitHandle.WaitOne(35000)) { $shell.EndStop($stop); $stopped = $true }
                }
                if ($stopped) { $shell.Dispose() } else { $cleanupErrors.Add('Completion runspace did not stop within its bound.') }
            }
        }
    }
} finally {
    # Reopen only this fixture's explicit child identities, checking creation time before cleanup.
    foreach ($path in [IO.Directory]::GetFiles($FixtureDirectory, '*.log.child.json')) {
        try {
            $fact = ConvertFrom-Json -AsHashtable -DateKind String -InputObject ([IO.File]::ReadAllText($path))
            try { $null = Open-OwnedChild $fact } catch [ArgumentException] { }
        } catch { $cleanupErrors.Add($_.ToString()) }
    }
    foreach ($process in $ownedProcesses) {
        try {
            if (-not $process.HasExited) { $process.Kill($true) }
            if (-not $process.WaitForExit(3000)) { throw "Owned process $($process.Id) is still alive after bounded cleanup." }
        } catch { $cleanupErrors.Add($_.ToString()) } finally { $process.Dispose() }
    }
    $failed = @($cases | Where-Object { -not $_.passed }).Count
    $summary = @{
        fixtureDirectory = $FixtureDirectory; run = $Run; cases = $cases.ToArray(); total = $cases.Count
        failed = $failed; passed = $cases.Count - $failed; cleanupErrors = $cleanupErrors.ToArray()
        parseResults = $parseResults.ToArray()
        powershell = $PSVersionTable.PSVersion.ToString(); dotnet = [Environment]::Version.ToString()
        launcher = $launcher; childExecutable = $pwsh
        testSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($self))).ToLowerInvariant()
        helperSha256 = $(if ([IO.File]::Exists($helper)) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($helper))).ToLowerInvariant() } else { $null })
        unityStarted = $false; playerStarted = $false; ideStarted = $false
        cancellationCoverage = 'See individual outcomes for CancellationToken + real PowerShell.BeginStop; interactive console CTRL+C not tested'
    }
    [IO.File]::WriteAllText((Join-Path $FixtureDirectory 'results.json'), (ConvertTo-Json -InputObject $summary -Depth 12), $utf8)
    "Results: $FixtureDirectory; passed=$($summary.passed)/$($summary.total); cleanupErrors=$($cleanupErrors.Count)"
}
if ($failed -ne 0 -or $cleanupErrors.Count -ne 0) { throw 'Owned-process regression tests failed; preserve results.json.' }
