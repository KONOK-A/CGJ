#requires -Version 7.6
#requires -PSEdition Core

function Resolve-HybridCLRFunctionalAcceptance {
    <#
    .SYNOPSIS
    Validates existing Editor evidence without running Unity or changing any original facts.
    .DESCRIPTION
    Dot-source this file, then pass a bound compatibility Scope from Get-HybridCLRReceptionScope.
    Strict requires a real normal exit and complete functional artifacts; it needs no completion seal.
    Known2022EditorExit requires the exact producer seal, owner and artifact bindings. Only the
    PostCompletionExitStalled outcome may be accepted with a known issue, never ProcessPassed=true.
    Expected contains the parent-selected Name rows (or strings), not a set derived from the XML.
    The parent must still validate installation, actual source/meta hashes and discovery selection.
    Tests seals bind selection bytes and the complete run-finished result, not official XML bytes
    (which Unity writes later). ArtifactSha256 reports the XML bytes read by this invocation.
    KnownShutdownDiagnostics retains only exact Curl callback-abort lines after this run's marker
    and an exact exit request or recognized shutdown under Known2022EditorExit. An exit request
    never replaces the final native/official shutdown proof or changes any process facts.
    Preamble lines are byte-preserving until the ASCII completion marker; that line and the
    entire completed tail must decode as strict UTF-8, without replacement or encoding fallback.
    Reparse points and ambiguous paths are rejected at inspection time; this is not a hostile
    concurrent filesystem or authenticated producer boundary. No log text is returned in errors.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Scope,
        [Parameter(Mandatory)][string]$Run,
        [Parameter(Mandatory)][ValidateSet('Discovery', 'Tests')][string]$Phase,
        [ValidateSet('Strict', 'Known2022EditorExit')][string]$Policy = 'Strict',
        [object[]]$Expected = @()
    )
    Set-StrictMode -Version 3.0
    $ErrorActionPreference = 'Stop'
    $Phase = $(if ($Phase -ieq 'Discovery') { 'Discovery' } else { 'Tests' })
    $Policy = $(if ($Policy -ieq 'Strict') { 'Strict' } else { 'Known2022EditorExit' })

    function Require-Fields($Data, [string[]]$Names) {
        if ($null -eq $Data) { throw 'HybridCLR acceptance: missing evidence object.' }
        foreach ($name in $Names) {
            $present = $(if ($Data -is [Collections.IDictionary]) { $Data.Contains($name) } else { $null -ne $Data.PSObject.Properties[$name] })
            if (-not $present) { throw "HybridCLR acceptance: missing required field $name." }
        }
    }
    function Is-Integer($Value) { return ($Value -is [int] -or $Value -is [long]) }
    function Get-ExactPath([string]$Path) {
        if ($Path -cnotmatch '^[A-Za-z]:[\\/]' -or -not [IO.Path]::IsPathFullyQualified($Path) -or
            $Path.Substring(2).Contains(':') -or $Path -match '[<>"|?*\[\]\x00-\x1f]' -or
            $Path.Split([char[]]'\/') -contains '.' -or $Path.Split([char[]]'\/') -contains '..') {
            throw 'HybridCLR acceptance: an explicit unambiguous local path is required.'
        }
        foreach ($part in $Path.Split([char[]]'\/')) {
            if ($part.EndsWith('.') -or $part.EndsWith(' ') -or $part -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)') {
                throw 'HybridCLR acceptance: ambiguous Windows path segment.'
            }
        }
        $full = [IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/')
        for ($cursor = $full; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
            try { $attributes = [IO.File]::GetAttributes($cursor) }
            catch [IO.FileNotFoundException] { continue }
            catch [IO.DirectoryNotFoundException] { continue }
            if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'HybridCLR acceptance: linked evidence paths are forbidden.' }
        }
        return $full
    }
    $files = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
    function Read-OwnedFile([string]$Path) {
        $path = Get-ExactPath $Path
        if (-not $files.ContainsKey($path)) {
            try { $bytes = [IO.File]::ReadAllBytes($path) }
            catch { throw 'HybridCLR acceptance: required evidence file is absent or unreadable.' }
            $files.Add($path, [pscustomobject]@{
                Path = $path; Bytes = $bytes
                Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
            })
        }
        return $files[$path]
    }
    function Read-OwnedJson($File) {
        try {
            $text = [Text.UTF8Encoding]::new($false, $true).GetString($File.Bytes).TrimStart([char]0xfeff)
            $document = [Text.Json.JsonDocument]::Parse($text)
            try {
                if ($document.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object) { throw 'Object required.' }
                $pending = [Collections.Generic.Stack[Text.Json.JsonElement]]::new()
                $pending.Push($document.RootElement)
                while ($pending.Count) {
                    $node = $pending.Pop()
                    if ($node.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
                        $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
                        foreach ($property in $node.EnumerateObject()) {
                            if (-not $keys.Add($property.Name)) { throw 'Duplicate JSON key.' }
                            $pending.Push($property.Value)
                        }
                    } elseif ($node.ValueKind -eq [Text.Json.JsonValueKind]::Array) {
                        foreach ($child in $node.EnumerateArray()) { $pending.Push($child) }
                    }
                }
            } finally { $document.Dispose() }
            return ConvertFrom-Json -AsHashtable -DateKind String -Depth 100 -InputObject $text
        } catch { throw 'HybridCLR acceptance: invalid, ambiguous or non-object JSON evidence.' }
    }
    function Assert-Binding($Data) {
        Require-Fields $Data @('Project', 'UnityVersion', 'UnityRevision', 'Receiver', 'OwnerSha256')
        if ($Data.Project -cne $Scope.Project -or $Data.UnityVersion -cne $Scope.ExpectedUnityVersion -or
            $Data.UnityRevision -cne $Scope.ExpectedUnityRevision -or $Data.Receiver -cne $Scope.Owner.receiver -or
            $Data.OwnerSha256 -cne $Scope.OwnerSha256) {
            throw 'HybridCLR acceptance: foreign project, engine, revision, receiver or owner.'
        }
    }
    function Get-Names([object[]]$Rows, [switch]$PassedLeaves) {
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        if ($null -eq $Rows -or $Rows.Count -eq 0) { throw 'HybridCLR acceptance: a nonempty complete name set is required.' }
        foreach ($row in $Rows) {
            if ($row -is [string] -and -not $PassedLeaves) { $name = $row }
            else {
                Require-Fields $row @('Name')
                $name = $row.Name
                if ($PassedLeaves) {
                    Require-Fields $row @('Result')
                    if ($row.Result -cne 'Passed') { throw 'HybridCLR acceptance: a run-finished leaf did not pass.' }
                }
            }
            if ($name -isnot [string] -or [string]::IsNullOrWhiteSpace($name) -or -not $names.Add($name)) {
                throw 'HybridCLR acceptance: empty or duplicate test name.'
            }
        }
        return ,$names
    }
    function Get-SingleArgument([object[]]$Arguments, [string]$Name) {
        $indices = @(for ($index = 0; $index -lt $Arguments.Count; $index++) { if ($Arguments[$index] -ieq $Name) { $index } })
        if ($indices.Count -ne 1 -or $indices[0] + 1 -ge $Arguments.Count) { throw 'HybridCLR acceptance: missing or duplicate Editor argument.' }
        return $Arguments[$indices[0] + 1]
    }
    function Assert-ProcessBinding($Data) {
        Require-Fields $Data @('schemaVersion', 'purpose', 'invocationId', 'pid', 'startUtc', 'project', 'evidenceRoot', 'log', 'run', 'ownerSha256', 'exe', 'arguments')
        if (-not (Is-Integer $Data.schemaVersion) -or $Data.schemaVersion -ne 1 -or
            $Data.purpose -cne 'YLFramework-HybridCLR-owned-process-v1' -or $Data.run -cne $Run -or
            $Data.project -cne $Scope.Project -or $Data.ownerSha256 -cne $Scope.OwnerSha256 -or
            $Data.invocationId -cnotmatch '^[0-9a-f]{32}$' -or -not (Is-Integer $Data.pid) -or $Data.pid -le 0 -or
            $Data.startUtc -isnot [string] -or [string]::IsNullOrWhiteSpace($Data.startUtc) -or
            (Get-ExactPath $Data.evidenceRoot) -ine $root -or (Get-ExactPath $Data.log) -ine $logPath -or
            (Get-ExactPath $Data.exe) -ine $unity -or [IO.Path]::GetFileName($Data.exe) -ine 'Unity.exe') {
            throw 'HybridCLR acceptance: process facts are not this exact owned Editor invocation.'
        }
        if ($Data.arguments -isnot [array] -or @($Data.arguments | Where-Object { $_ -isnot [string] }).Count -ne 0 -or
            @($Data.arguments | Where-Object { $_ -ieq '-batchmode' }).Count -ne 1 -or
            (Get-SingleArgument $Data.arguments '-projectPath') -cne $Scope.Project -or
            (Get-SingleArgument $Data.arguments '-logFile') -cne $logPath -or
            $Data.arguments -contains '-createProject' -or
            @($Data.arguments | Where-Object { $_ -imatch '^-build' -and $_ -ine '-buildTarget' }).Count -ne 0) {
            throw 'HybridCLR acceptance: not an owned batch Editor discovery/test command.'
        }
        if ($Phase -ceq 'Discovery') {
            if ($Data.arguments -contains '-runTests' -or
                (Get-SingleArgument $Data.arguments '-executeMethod') -cne 'YLFramework.Tests.Integrations.HybridCLR.HybridCLRTestFixtures.Discover' -or
                (Get-SingleArgument $Data.arguments '-hclr-run') -cne $Run -or
                (Get-SingleArgument $Data.arguments '-hclr-root') -cne $Scope.Root) {
                throw 'HybridCLR acceptance: only actual Editor test discovery is eligible.'
            }
        } elseif (@($Data.arguments | Where-Object { $_ -ieq '-runTests' }).Count -ne 1 -or
            $Data.arguments -contains '-executeMethod' -or
            (Get-SingleArgument $Data.arguments '-testPlatform') -cnotin @('EditMode', 'PlayMode') -or
            (Get-SingleArgument $Data.arguments '-testResults') -cne (Join-Path $root ($Run + '.xml'))) {
            throw 'HybridCLR acceptance: only official Editor runTests results are eligible.'
        }
    }

    if ($Run -cnotmatch '^[A-Za-z0-9-]+$') { throw 'HybridCLR acceptance: use one exact run name without a path.' }
    Require-Fields $Scope @('Root', 'Project', 'Unity', 'Owner', 'OwnerSha256', 'OwnerPath', 'DeliveryRoot', 'Repository', 'ExpectedUnityVersion', 'ExpectedUnityRevision', 'IsCompatibility', 'IsSimulation', 'Bound')
    Require-Fields $Scope.Owner @('receiver')
    if ($Scope.IsCompatibility -isnot [bool] -or -not $Scope.IsCompatibility -or $Scope.Bound -isnot [bool] -or -not $Scope.Bound -or
        $Scope.IsSimulation -isnot [bool] -or $Scope.IsSimulation -or
        $Scope.OwnerSha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'HybridCLR acceptance: a bound new compatibility Scope is required; legacy evidence is not adopted.' }
    $root = Get-ExactPath $Scope.Root
    $project = Get-ExactPath $Scope.Project
    $unity = Get-ExactPath $Scope.Unity
    $delivery = Get-ExactPath $Scope.DeliveryRoot
    $null = Get-ExactPath $Scope.Repository
    $ownerFile = Read-OwnedFile $Scope.OwnerPath
    $snapshot = Read-OwnedFile (Join-Path $root 'owner.json')
    $owner = Read-OwnedJson $ownerFile
    Require-Fields $owner @('schemaVersion', 'purpose', 'simulation', 'receiver', 'project', 'evidenceRoot', 'deliveryRoot', 'repository', 'unity', 'expectedUnityVersion', 'expectedUnityRevision')
    if (-not (Is-Integer $owner.schemaVersion) -or $owner.schemaVersion -ne 2 -or
        $owner.purpose -cne 'YLFramework-HybridCLR-9.0.3-independent-reception' -or
        $owner.simulation -isnot [bool] -or $owner.simulation -or
        $owner.receiver -cnotmatch '^[a-z][a-z0-9]{0,7}$' -or $owner.receiver -cmatch '^(?:[nvw].*|r|h26|hc26|h26s|hc26s|a|d|t|u|p|j|q|r6|x|y|z)$' -or
        $owner.receiver -cne $Scope.Owner.receiver -or $owner.project -cne $Scope.Project -or
        $owner.evidenceRoot -cne $Scope.Root -or $owner.deliveryRoot -cne $Scope.DeliveryRoot -or
        $owner.repository -cne $Scope.Repository -or $owner.unity -cne $Scope.Unity -or
        $owner.expectedUnityVersion -cne $Scope.ExpectedUnityVersion -or $owner.expectedUnityRevision -cne $Scope.ExpectedUnityRevision -or
        $ownerFile.Path -ine (Join-Path $delivery ($owner.receiver + '.hybridclr-owner.json')) -or
        [IO.Path]::GetDirectoryName($root) -ine $delivery -or [IO.Path]::GetDirectoryName($project) -ine $delivery -or
        $project -ine (Join-Path $delivery $owner.receiver) -or $root -ieq $project -or
        $ownerFile.Sha256 -cne $Scope.OwnerSha256 -or $snapshot.Sha256 -cne $Scope.OwnerSha256) {
        throw 'HybridCLR acceptance: immutable new-owner descriptor and evidence snapshot must match Scope.'
    }
    if ($Policy -ceq 'Known2022EditorExit' -and
        ($Scope.ExpectedUnityVersion -cne '2022.3.62f3c1' -or $Scope.ExpectedUnityRevision -cne '1623fc0bbb97')) {
        throw 'HybridCLR acceptance: the known issue is limited to 2022.3.62f3c1 revision 1623fc0bbb97.'
    }

    $logPath = Get-ExactPath (Join-Path $root ($Run + '.log'))
    if (-not [IO.File]::Exists($logPath)) { throw 'HybridCLR acceptance: missing exact run log.' }
    $exitFile = Read-OwnedFile ($logPath + '.exit.json')
    $fact = Read-OwnedJson $exitFile
    Assert-ProcessBinding $fact
    $process = Read-OwnedJson (Read-OwnedFile ($logPath + '.process.json'))
    Assert-ProcessBinding $process
    if ($process.invocationId -cne $fact.invocationId -or $process.pid -ne $fact.pid -or $process.startUtc -cne $fact.startUtc -or
        (ConvertTo-Json -InputObject $process.arguments -Compress) -cne (ConvertTo-Json -InputObject $fact.arguments -Compress)) {
        throw 'HybridCLR acceptance: process-start and terminal facts belong to different invocations.'
    }
    Require-Fields $fact @('terminalStatus', 'observedExit', 'exitCode', 'editorForcedCleanup', 'timedOut', 'alive', 'childState', 'observerError', 'cleanupError', 'cancellationReason', 'succeeded', 'killRequestPersisted', 'killInvoked')
    foreach ($name in @('observedExit', 'editorForcedCleanup', 'timedOut', 'alive', 'succeeded', 'killRequestPersisted', 'killInvoked')) {
        if ($fact[$name] -isnot [bool]) { throw 'HybridCLR acceptance: process flags must be actual JSON booleans.' }
    }
    $clean = ($fact.observedExit -and (Is-Integer $fact.exitCode) -and -not $fact.alive -and $fact.childState -ceq 'Exited' -and
        -not $fact.timedOut -and $null -eq $fact.observerError -and $null -eq $fact.cleanupError -and $null -eq $fact.cancellationReason)
    $exitPassed = ($clean -and $fact.terminalStatus -ceq 'Exited' -and $fact.exitCode -eq 0 -and $fact.succeeded -and
        -not $fact.editorForcedCleanup -and -not $fact.killRequestPersisted -and -not $fact.killInvoked)
    $knownApplied = ($Policy -ceq 'Known2022EditorExit' -and $clean -and $fact.terminalStatus -ceq 'PostCompletionExitStalled' -and
        -not $fact.succeeded -and $fact.exitCode -eq -1 -and $fact.editorForcedCleanup -and $fact.killRequestPersisted -and $fact.killInvoked)
    if (-not $exitPassed -and -not $knownApplied) { throw 'HybridCLR acceptance: exit is neither normal success nor the exact approved post-completion stall.' }

    $seal = $null; $sealFile = $null; $selectionFile = $null
    if ($Policy -ceq 'Known2022EditorExit') {
        $sealFile = Read-OwnedFile (Join-Path $root ($Run + '.completion.json'))
        $seal = Read-OwnedJson $sealFile
        Assert-Binding $seal
        Require-Fields $seal @('SchemaVersion', 'Purpose', 'Policy', 'KnownIssueId', 'Phase', 'Run', 'Completed', 'LifecycleChecksCompleted', 'Passed', 'CleanupProof')
        if (-not (Is-Integer $seal.SchemaVersion) -or $seal.SchemaVersion -ne 1 -or
            $seal.Purpose -cne 'YLFramework-HybridCLR-functional-completion-v1' -or $seal.Policy -cne $Policy -or
            $seal.KnownIssueId -cne 'U63-KI-2022-EDITOR-EXIT-001' -or $seal.Phase -cne $Phase -or $seal.Run -cne $Run -or
            $seal.Completed -isnot [bool] -or -not $seal.Completed -or $seal.LifecycleChecksCompleted -isnot [bool] -or
            -not $seal.LifecycleChecksCompleted -or $seal.Passed -isnot [bool] -or -not $seal.Passed) {
            throw 'HybridCLR acceptance: complete successful producer lifecycle seal required.'
        }
    }

    $selectedNames = $null
    if ($Phase -ceq 'Discovery') {
        $artifactFile = Read-OwnedFile (Join-Path $root ($Run + '.json'))
        $artifact = Read-OwnedJson $artifactFile
        Assert-Binding $artifact
        Require-Fields $artifact @('Passed', 'EditMode', 'PlayMode')
        if ($artifact.Passed -isnot [bool] -or -not $artifact.Passed) { throw 'HybridCLR acceptance: actual discovery did not pass.' }
        $allNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($mode in @('EditMode', 'PlayMode')) {
            $names = Get-Names $artifact[$mode]
            foreach ($row in $artifact[$mode]) {
                Require-Fields $row @('Assembly', 'RunState')
                if ($row.Assembly -cne ('YLFramework.HybridCLR.Tests.' + $mode) -or -not $allNames.Add($row.Name)) {
                    throw 'HybridCLR acceptance: discovered assemblies or names are not distinct actual HybridCLR sets.'
                }
            }
        }
        if ($null -ne $seal) {
            Require-Fields $seal @('OutputSha256')
            if ($seal.OutputSha256 -cne $artifactFile.Sha256 -or $seal.CleanupProof -cne 'DiscoveryApiDisposed') {
                throw 'HybridCLR acceptance: discovery bytes or API disposal proof differ from the producer seal.'
            }
        }
    } else {
        $selectedNames = Get-Names $Expected
        $selectionFile = Read-OwnedFile (Join-Path $root ($Run + '.selection.json'))
        $selection = Read-OwnedJson $selectionFile
        Assert-Binding $selection
        Require-Fields $selection @('TestPlatform', 'Expected', 'DiscoverySha256')
        $platform = Get-SingleArgument $fact.arguments '-testPlatform'
        if ($selection.TestPlatform -cne $platform -or $selection.DiscoverySha256 -cnotmatch '^[0-9a-f]{64}$' -or
            -not $selectedNames.SetEquals((Get-Names $selection.Expected))) {
            throw 'HybridCLR acceptance: selection must bind the parent Expected set and actual Editor platform.'
        }
        $artifactFile = Read-OwnedFile (Join-Path $root ($Run + '.xml'))
        $settings = [Xml.XmlReaderSettings]::new(); $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit; $settings.XmlResolver = $null
        $stream = [IO.MemoryStream]::new($artifactFile.Bytes, $false)
        $reader = $null
        try {
            $reader = [Xml.XmlReader]::Create($stream, $settings)
            $artifact = [Xml.XmlDocument]::new(); $artifact.XmlResolver = $null; $artifact.Load($reader)
        } catch { throw 'HybridCLR acceptance: invalid or unsafe official NUnit XML.' }
        finally { if ($null -ne $reader) { $reader.Dispose() }; $stream.Dispose() }
        $summary = $artifact.DocumentElement
        if ($null -eq $summary -or $summary.Name -cne 'test-run' -or $summary.NamespaceURI.Length -ne 0 -or
            $summary.GetAttribute('result') -cne 'Passed' -or $artifact.SelectNodes('//test-run').Count -ne 1) {
            throw 'HybridCLR acceptance: official NUnit root must report Passed.'
        }
        foreach ($counter in @('total', 'passed', 'failed', 'skipped', 'inconclusive')) {
            $value = $summary.GetAttribute($counter)
            if ($value -cnotmatch '^(0|[1-9][0-9]*)$' -or $value.Length -gt 9 -or
                [long]$value -ne $(if ($counter -cin @('total', 'passed')) { $selectedNames.Count } else { 0 })) {
                throw 'HybridCLR acceptance: NUnit root counters do not close the complete all-passed selection.'
            }
        }
        $xmlNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($leaf in $artifact.SelectNodes('//test-case')) {
            $name = $leaf.GetAttribute('fullname')
            if ([string]::IsNullOrWhiteSpace($name) -or -not $xmlNames.Add($name) -or $leaf.GetAttribute('result') -cne 'Passed') {
                throw 'HybridCLR acceptance: duplicate, missing-name or non-passed NUnit leaf.'
            }
        }
        if (-not $selectedNames.SetEquals($xmlNames) -or $artifact.SelectNodes('//failure').Count -ne 0 -or
            @($artifact.SelectNodes('//test-suite') | Where-Object { $_.GetAttribute('result') -cne 'Passed' }).Count -ne 0) {
            throw 'HybridCLR acceptance: complete NUnit leaves and fixture teardown must pass, with no extra or missing tests.'
        }
        if ($null -ne $seal) {
            Require-Fields $seal @('SelectionSha256', 'Total', 'PassedCount', 'FailedCount', 'SkippedCount', 'InconclusiveCount', 'Leaves')
            if ($seal.SelectionSha256 -cne $selectionFile.Sha256 -or $seal.CleanupProof -cne 'NUnitRunFinishedIncludesFixtureTearDown') {
                throw 'HybridCLR acceptance: selection bytes and completed NUnit teardown proof must match the seal.'
            }
            foreach ($counter in @('Total', 'PassedCount', 'FailedCount', 'SkippedCount', 'InconclusiveCount')) {
                if (-not (Is-Integer $seal[$counter]) -or $seal[$counter] -ne $(if ($counter -cin @('Total', 'PassedCount')) { $selectedNames.Count } else { 0 })) {
                    throw 'HybridCLR acceptance: run-finished counters do not close Expected and official XML.'
                }
            }
            if (-not $selectedNames.SetEquals((Get-Names $seal.Leaves -PassedLeaves))) {
                throw 'HybridCLR acceptance: the producer run-finished leaves must equal Expected and official XML.'
            }
        }
    }

    if ($knownApplied) {
        Require-Fields $fact @('completionObserved', 'completionSealPath', 'completionSealSha256', 'completionReceiptPath', 'completionObservedUtc', 'completionObservedElapsedMilliseconds', 'timeoutMilliseconds', 'exitGraceMilliseconds')
        if ($fact.completionObserved -isnot [bool] -or -not $fact.completionObserved -or
            $fact.completionObservedUtc -isnot [string] -or [string]::IsNullOrWhiteSpace($fact.completionObservedUtc) -or
            (Get-ExactPath $fact.completionSealPath) -ine $sealFile.Path -or $fact.completionSealSha256 -cne $sealFile.Sha256 -or
            (Get-ExactPath $fact.completionReceiptPath) -ine ($logPath + '.completion.json') -or
            -not (Is-Integer $fact.exitGraceMilliseconds) -or $fact.exitGraceMilliseconds -lt 1000 -or
            -not (Is-Integer $fact.completionObservedElapsedMilliseconds) -or $fact.completionObservedElapsedMilliseconds -lt 0 -or
            -not (Is-Integer $fact.timeoutMilliseconds) -or $fact.timeoutMilliseconds -le $fact.completionObservedElapsedMilliseconds) {
            throw 'HybridCLR acceptance: observed producer seal and bounded post-completion grace must match exact bytes and paths.'
        }
        foreach ($suffix in @('.completion.json', '.kill.json')) {
            $observation = Read-OwnedJson (Read-OwnedFile ($logPath + $suffix))
            Assert-ProcessBinding $observation
            Require-Fields $observation @('completionObserved', 'completionSealPath', 'completionSealSha256', 'completionReceiptPath', 'completionObservedUtc', 'completionObservedElapsedMilliseconds', 'exitGraceMilliseconds',
                'terminalStatus', 'observedExit', 'exitCode', 'succeeded', 'alive', 'childState', 'timedOut', 'editorForcedCleanup', 'killRequestPersisted', 'killInvoked', 'observerError', 'cleanupError', 'cancellationReason')
            foreach ($flag in @('completionObserved', 'observedExit', 'succeeded', 'alive', 'timedOut', 'editorForcedCleanup', 'killRequestPersisted', 'killInvoked')) {
                if ($observation[$flag] -isnot [bool]) { throw 'HybridCLR acceptance: observation flags must be real JSON booleans.' }
            }
            $isKill = $suffix -ceq '.kill.json'
            if ($observation.invocationId -cne $fact.invocationId -or $observation.pid -ne $fact.pid -or $observation.startUtc -cne $fact.startUtc -or
                $observation.terminalStatus -cne $(if ($isKill) { 'KillRequested' } else { 'InProgress' }) -or
                $observation.observedExit -or $null -ne $observation.exitCode -or $observation.succeeded -or
                -not $observation.alive -or $observation.childState -cne 'Running' -or $observation.timedOut -or
                $observation.editorForcedCleanup -ne $isKill -or $observation.killRequestPersisted -ne $isKill -or $observation.killInvoked -or
                $null -ne $observation.observerError -or $null -ne $observation.cleanupError -or $null -ne $observation.cancellationReason -or
                -not $observation.completionObserved -or
                $observation.completionSealPath -cne $fact.completionSealPath -or $observation.completionSealSha256 -cne $sealFile.Sha256 -or
                $observation.completionReceiptPath -cne $fact.completionReceiptPath -or
                $observation.completionObservedUtc -cne $fact.completionObservedUtc -or
                -not (Is-Integer $observation.completionObservedElapsedMilliseconds) -or $observation.completionObservedElapsedMilliseconds -ne $fact.completionObservedElapsedMilliseconds -or
                -not (Is-Integer $observation.exitGraceMilliseconds) -or $observation.exitGraceMilliseconds -ne $fact.exitGraceMilliseconds) {
                throw 'HybridCLR acceptance: completion and durable kill observations must belong to the same sealed invocation.'
            }
        }
    }
    $knownShutdownDiagnostics = [Collections.Generic.List[string]]::new()
    if ($Policy -ceq 'Known2022EditorExit') {
        $seen = $false; $shutdown = $false; $exitRequested = $false; $markerCount = 0
        $marker = '(?:^|\s)HYBRIDCLR_FUNCTIONAL_COMPLETED ' + [regex]::Escape($Run) + '\s*$'
        $exitMarker = 'HYBRIDCLR_EDITOR_EXIT_REQUESTED ' + $Run
        $byteEncoding = [Text.Encoding]::Latin1
        $logUtf8 = [Text.UTF8Encoding]::new($false, $true)
        $utf8Preamble = $byteEncoding.GetString([byte[]]@(0xef, 0xbb, 0xbf))
        $firstLine = $true
        $logReader = $null
        try {
            # Latin1 frames raw byte lines losslessly; only the completed tail is UTF-8 text.
            $logReader = [IO.StreamReader]::new($logPath, $byteEncoding, $false)
            while ($null -ne ($line = $logReader.ReadLine())) {
                if ($firstLine -and $line.StartsWith($utf8Preamble, [StringComparison]::Ordinal)) { $line = $line.Substring(3) }
                $firstLine = $false
                if ($seen -or $line -cmatch $marker) { $line = $logUtf8.GetString($byteEncoding.GetBytes($line)) }
                if ($line -cmatch $marker) { $seen = $true; $markerCount++; continue }
                if ($seen -and $line.Equals($exitMarker, [StringComparison]::Ordinal)) { $exitRequested = $true; continue }
                if ($line.Equals('Curl error 42: Callback aborted', [StringComparison]::Ordinal)) {
                    if (-not $seen -or (-not $exitRequested -and -not $shutdown)) { throw 'HybridCLR acceptance: Curl callback abort is not within the completed Editor exit stage.' }
                    $knownShutdownDiagnostics.Add($line)
                    continue
                }
                if (-not $seen) { continue }
                if ($line -match '(?i)\b(?:fatal|crash(?:ed|ing)?|unhandled|errors?|assertion|(?:[A-Za-z_][A-Za-z0-9_.]*)?Exception|SIG(?:SEGV|ABRT|BUS|ILL|FPE)|segmentation fault|access violation|aborting batchmode)\b') {
                    throw 'HybridCLR acceptance: an error occurred after functional completion; no known-issue waiver applies.'
                }
                if ($line.Contains('Input System module state changed to: Shutdown', [StringComparison]::Ordinal) -or
                    $line.Contains('Exiting batchmode successfully now', [StringComparison]::Ordinal)) { $shutdown = $true }
            }
        } catch {
            if ($_.Exception.Message.StartsWith('HybridCLR acceptance:', [StringComparison]::Ordinal)) { throw }
            $cause = $_.Exception
            while ($null -ne $cause.InnerException) { $cause = $cause.InnerException }
            throw ('HybridCLR acceptance: exact run log is unreadable (' + $cause.GetType().FullName + ').')
        } finally { if ($null -ne $logReader) { $logReader.Dispose() } }
        if (-not $shutdown -or $markerCount -ne 1) { throw 'HybridCLR acceptance: one exact completion marker followed by a known shutdown path is required.' }
    }
    foreach ($file in $files.Values) {
        $null = Get-ExactPath $file.Path
        try { $currentSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($file.Path))).ToLowerInvariant() }
        catch { throw 'HybridCLR acceptance: evidence disappeared during validation.' }
        if ($currentSha -cne $file.Sha256) { throw 'HybridCLR acceptance: evidence bytes changed during validation.' }
    }
    return [pscustomobject]@{
        SchemaVersion = 1; Run = $Run; Phase = $Phase; Policy = $Policy; Project = $Scope.Project
        UnityVersion = $Scope.ExpectedUnityVersion; UnityRevision = $Scope.ExpectedUnityRevision
        Receiver = $Scope.Owner.receiver; OwnerSha256 = $Scope.OwnerSha256
        FunctionalPassed = $true; ExitPassed = [bool]$exitPassed; ProcessPassed = [bool]$exitPassed
        KnownIssueApplied = [bool]$knownApplied; KnownIssueId = $(if ($knownApplied) { 'U63-KI-2022-EDITOR-EXIT-001' } else { $null })
        KnownShutdownDiagnostics = $knownShutdownDiagnostics.ToArray()
        Accepted = $true; AcceptanceStatus = $(if ($knownApplied) { 'PassedWithKnownIssue' } else { 'Passed' })
        ExitCode = $fact.exitCode; EditorForcedCleanup = $fact.editorForcedCleanup
        ProcessFact = $fact; ProcessFactPath = $exitFile.Path; ProcessFactSha256 = $exitFile.Sha256
        CompletionSealPath = $(if ($null -ne $sealFile) { $sealFile.Path } else { $null })
        CompletionSealSha256 = $(if ($null -ne $sealFile) { $sealFile.Sha256 } else { $null })
        ArtifactPath = $artifactFile.Path; ArtifactSha256 = $artifactFile.Sha256; Artifact = $artifact
        SelectionSha256 = $(if ($null -ne $selectionFile) { $selectionFile.Sha256 } else { $null })
    }
}
