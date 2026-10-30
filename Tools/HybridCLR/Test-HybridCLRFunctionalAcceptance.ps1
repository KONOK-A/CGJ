#requires -Version 7.6
#requires -PSEdition Core
[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run = 'suite')

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -ne 7 -or $PSVersionTable.PSVersion.Minor -ne 6) {
    throw 'These pure evidence tests require Windows PowerShell Core 7.6.x.'
}
$parent = 'C:\Users\Kano\AppData\Local\Temp\kilo\U63-20261027-c63a1'
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'Missing approved test parent.' }
$fixture = Join-Path $parent ('b01-acceptance-' + $Run + '-' + [Guid]::NewGuid().ToString('N'))
for ($cursor = $parent; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
    if ([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked test parent.' }
}
if ([IO.Path]::Exists($fixture)) { throw 'Never adopt an existing fixture.' }
$null = [IO.Directory]::CreateDirectory($fixture)
$helper = Join-Path $PSScriptRoot 'Get-HybridCLRFunctionalAcceptance.ps1'
$utf8 = [Text.UTF8Encoding]::new($false)
$cases = [Collections.Generic.List[object]]::new()
$parseResults = [Collections.Generic.List[object]]::new()
$nextFixture = 0

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Save-Text([string]$Path, [string]$Text) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($fixture + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Test output escaped its exclusive fixture.' }
    for ($cursor = $full; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if (-not [IO.Path]::Exists($cursor)) { continue }
        if ([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) { throw 'Test writer refuses links.' }
    }
    if (-not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($full))) { throw 'Missing fixture output parent.' }
    [IO.File]::WriteAllText($full, $Text, $utf8)
}
function Save-Json([string]$Path, $Data) {
    Save-Text $Path (ConvertTo-Json -InputObject $Data -Depth 24)
}
function Hash-File([string]$Path) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($Path))).ToLowerInvariant()
}
function Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        $cases.Add(@{ name = $Name; passed = $true; error = $null })
        "PASS $Name"
    } catch {
        $cases.Add(@{ name = $Name; passed = $false; error = $_.Exception.Message })
        "FAIL ${Name}: $($_.Exception.Message)"
    }
}
function Save-Exit($F) { Save-Json $F.ExitPath $F.Fact }
function Save-Seal($F) {
    Save-Json $F.SealPath $F.Seal
    $F.Fact.completionSealSha256 = Hash-File $F.SealPath
    $F.Completion.completionSealSha256 = $F.Fact.completionSealSha256
    $F.Kill.completionSealSha256 = $F.Fact.completionSealSha256
    Save-Json $F.CompletionPath $F.Completion
    Save-Json $F.KillPath $F.Kill
    Save-Exit $F
}
function New-Evidence([string]$Phase = 'Tests', [string]$Platform = 'EditMode', [string]$Version = '2022.3.62f3c1', [string]$Revision = '') {
    $script:nextFixture++
    $delivery = Join-Path $fixture ('case-' + $script:nextFixture)
    $root = Join-Path $delivery 'h1e'
    $project = Join-Path $delivery 'h1'
    $editor = Join-Path $delivery 'editor'
    foreach ($path in @($delivery, $root, $project, $editor)) { $null = [IO.Directory]::CreateDirectory($path) }
    if (-not $Revision) { $Revision = $(if ($Version -ceq '6000.3.27f1') { '672d562a42a7' } else { '1623fc0bbb97' }) }
    $identityPrefix = 'u63-c63a1-' + $Version.Replace('.', '-') + '-h1'
    $owner = @{
        schemaVersion = 2; purpose = 'YLFramework-HybridCLR-9.0.3-independent-reception'; simulation = $false
        deliveryRoot = $delivery; repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
        runId = 'U63-20261027-c63a1'; receiver = 'h1'; project = $project; evidenceRoot = $root
        unity = Join-Path $editor 'Unity.exe'; expectedUnityVersion = $Version; expectedUnityRevision = $Revision
        uguiVersion = $(if ($Version -ceq '6000.3.27f1') { '2.0.0' } else { '1.0.0' })
        testFrameworkVersion = $(if ($Version -ceq '6000.3.27f1') { '1.6.0' } else { '1.3.9' })
        compatibilityId = $identityPrefix + '-v1'; releaseCompatibilityId = $identityPrefix + '-release-v1'
        subsetCompatibilityId = $identityPrefix + '-subsets-v1'
        baseReceiptStem = 'base'; baseImportRun = 'import-base'; optionalImportRun = 'import-optional'
        basePackage = 'base.unitypackage'; optionalPackage = 'optional.unitypackage'
        baseline = Join-Path $delivery 'h1b'; releaseBaseline = Join-Path $delivery 'h1br'
        playerRoot = Join-Path $delivery 'h1p'; releasePlayerRoot = Join-Path $delivery 'h1pr'
        runtimeCacheRoot = Join-Path $delivery 'h1c'; subsetBaseline = Join-Path $delivery 'h1bs'
        subsetPlayerRoot = Join-Path $delivery 'h1s'; subsetCacheRoot = Join-Path $delivery 'h1cs'
    }
    $ownerPath = Join-Path $delivery 'h1.hybridclr-owner.json'
    Save-Json $ownerPath $owner
    Save-Text (Join-Path $root 'owner.json') ([IO.File]::ReadAllText($ownerPath))
    Save-Text $owner.unity 'Fake evidence only. Never executed.'
    $scope = [pscustomobject]@{
        Root = $root; DeliveryRoot = $delivery; Owner = $owner; OwnerPath = $ownerPath; OwnerJson = [IO.File]::ReadAllText($ownerPath)
        OwnerSha256 = Hash-File $ownerPath; Repository = $owner.repository; Project = $project
        IsCompatibility = $true; IsSimulation = $false; Bound = $true; Unity = $owner.unity
        ExpectedUnityVersion = $owner.expectedUnityVersion; ExpectedUnityRevision = $owner.expectedUnityRevision
    }
    $name = 'owned-run'
    $log = Join-Path $root ($name + '.log')
    $selectionPath = Join-Path $root ($name + '.selection.json')
    $xmlPath = Join-Path $root ($name + '.xml')
    $outputPath = Join-Path $root ($name + '.json')
    $sealPath = Join-Path $root ($name + '.completion.json')
    $expected = @(
        @{ Name = "HybridCLR.$Platform.CaseA"; Assembly = "YLFramework.HybridCLR.Tests.$Platform"; RunState = 'Runnable' },
        @{ Name = "HybridCLR.$Platform.CaseB"; Assembly = "YLFramework.HybridCLR.Tests.$Platform"; RunState = 'Runnable' }
    )
    $binding = @{ Project = $project; UnityVersion = $scope.ExpectedUnityVersion; UnityRevision = $scope.ExpectedUnityRevision
        Receiver = 'h1'; OwnerSha256 = $scope.OwnerSha256 }
    $discovery = $binding.Clone()
    $discovery.Passed = $true
    $discovery.EditMode = @(@{ Name = 'HybridCLR.EditMode.CaseA'; Assembly = 'YLFramework.HybridCLR.Tests.EditMode'; RunState = 'Runnable' })
    $discovery.PlayMode = @(@{ Name = 'HybridCLR.PlayMode.CaseA'; Assembly = 'YLFramework.HybridCLR.Tests.PlayMode'; RunState = 'Runnable' })
    $discovery.Sources = @(@{ Path = Join-Path $project 'Assets/Fake.cs'; Sha256 = 'a' * 64; MetaSha256 = 'b' * 64 })
    $discovery.SceneProfile = 'Common'; $discovery.TestInstallationSha256 = 'c' * 64
    Save-Json $outputPath $discovery
    $selection = $binding.Clone()
    $selection.TestPlatform = $Platform; $selection.Expected = $expected; $selection.DiscoverySha256 = Hash-File $outputPath
    Save-Json $selectionPath $selection
    $xml = [xml]("<test-run result='Passed' total='2' passed='2' failed='0' skipped='0' inconclusive='0'><test-suite type='TestFixture' result='Passed' total='2' passed='2' failed='0' skipped='0' inconclusive='0'><test-case fullname='HybridCLR.$Platform.CaseA' result='Passed'/><test-case fullname='HybridCLR.$Platform.CaseB' result='Passed'/></test-suite></test-run>")
    Save-Text $xmlPath $xml.OuterXml
    $seal = $binding.Clone()
    $seal.SchemaVersion = 1; $seal.Purpose = 'YLFramework-HybridCLR-functional-completion-v1'
    $seal.Policy = 'Known2022EditorExit'; $seal.KnownIssueId = 'U63-KI-2022-EDITOR-EXIT-001'; $seal.Phase = $Phase
    $seal.Run = $name; $seal.Completed = $true; $seal.LifecycleChecksCompleted = $true; $seal.Passed = $true
    if ($Phase -ceq 'Discovery') {
        $seal.OutputSha256 = Hash-File $outputPath; $seal.CleanupProof = 'DiscoveryApiDisposed'
    } else {
        $seal.SelectionSha256 = Hash-File $selectionPath; $seal.CleanupProof = 'NUnitRunFinishedIncludesFixtureTearDown'
        $seal.Total = 2; $seal.PassedCount = 2; $seal.FailedCount = 0; $seal.SkippedCount = 0; $seal.InconclusiveCount = 0
        $seal.Leaves = @(@{ Name = "HybridCLR.$Platform.CaseB"; Result = 'Passed' }, @{ Name = "HybridCLR.$Platform.CaseA"; Result = 'Passed' })
    }
    Save-Json $sealPath $seal
    $arguments = @('-batchmode', '-projectPath', $project, '-logFile', $log, '-buildTarget', 'Win64')
    if ($Phase -ceq 'Discovery') {
        $arguments += @('-executeMethod', 'YLFramework.Tests.Integrations.HybridCLR.HybridCLRTestFixtures.Discover', '-hclr-run', $name, '-hclr-root', $root)
    } else {
        $arguments += @('-runTests', '-testPlatform', $Platform, '-testResults', $xmlPath, '-testFilter', '^HybridCLR', '-assemblyNames', "YLFramework.HybridCLR.Tests.$Platform")
    }
    $fact = @{
        schemaVersion = 1; purpose = 'YLFramework-HybridCLR-owned-process-v1'; invocationId = [Guid]::NewGuid().ToString('N')
        wrapperPid = 100; wrapperStartUtc = '2026-10-28T00:00:00Z'; wrapperExe = 'C:\fake-observer.exe'
        requestedUtc = '2026-10-28T00:00:01Z'; startUtc = '2026-10-28T00:00:02Z'; pid = 101; exe = $scope.Unity
        project = $project; evidenceRoot = $root; log = $log; run = $name; ownerSha256 = $scope.OwnerSha256
        arguments = $arguments; timeoutMilliseconds = 1800000; terminalStatus = 'PostCompletionExitStalled'
        observedExit = $true; exitCode = -1; observerError = $null; cancellationReason = $null; timedOut = $false
        editorForcedCleanup = $true; killRequestedUtc = '2026-10-28T00:02:10Z'; killRequestPersisted = $true; killInvoked = $true
        cleanupError = $null; alive = $false; childState = 'Exited'; handleRetained = $true
        validationRequested = $false; artifactPassed = $false; succeeded = $false; utc = '2026-10-28T00:02:11Z'
        completionObserved = $true; completionSealPath = $sealPath; completionSealSha256 = Hash-File $sealPath
        exitGraceMilliseconds = 120000; completionObservedUtc = '2026-10-28T00:00:10Z'
        completionObservedElapsedMilliseconds = 8000; completionReceiptPath = $log + '.completion.json'
    }
    $process = $fact.Clone(); $process.terminalStatus = 'InProgress'; $process.observedExit = $false; $process.exitCode = $null
    $process.editorForcedCleanup = $false; $process.killRequestPersisted = $false; $process.killInvoked = $false
    $process.alive = $true; $process.childState = 'Running'; $process.completionObserved = $false; $process.completionSealSha256 = $null
    Save-Json ($log + '.process.json') $process
    $identity = $process.Clone(); $identity.pid = $null; $identity.startUtc = $null; $identity.childState = 'NotStarted'
    Save-Json ($log + '.identity.json') $identity
    $kill = $fact.Clone(); $kill.terminalStatus = 'KillRequested'; $kill.observedExit = $false; $kill.exitCode = $null
    $kill.alive = $true; $kill.childState = 'Running'; $kill.killInvoked = $false
    Save-Json ($log + '.kill.json') $kill
    $completion = $fact.Clone(); $completion.terminalStatus = 'InProgress'; $completion.observedExit = $false; $completion.exitCode = $null
    $completion.editorForcedCleanup = $false; $completion.killRequestPersisted = $false; $completion.killInvoked = $false
    $completion.alive = $true; $completion.childState = 'Running'
    Save-Json ($log + '.completion.json') $completion
    Save-Json ($log + '.exit.json') $fact
    Save-Text $log "Expected LogAssert: error before completion is not an exit failure.`nHYBRIDCLR_FUNCTIONAL_COMPLETED $name`nInput System module state changed to: Shutdown`n"
    return [pscustomobject]@{
        Scope = $scope; Run = $name; Phase = $Phase; Expected = $expected; Fact = $fact; Process = $process
        Completion = $completion; CompletionPath = $log + '.completion.json'; Kill = $kill; KillPath = $log + '.kill.json'
        Seal = $seal; Discovery = $discovery; Selection = $selection; Xml = $xml; Log = $log
        ExitPath = $log + '.exit.json'; SealPath = $sealPath; OutputPath = $outputPath; SelectionPath = $selectionPath; XmlPath = $xmlPath
    }
}
function Set-NormalExit($F) {
    $F.Fact.terminalStatus = 'Exited'; $F.Fact.exitCode = 0; $F.Fact.succeeded = $true
    $F.Fact.editorForcedCleanup = $false; $F.Fact.killRequestPersisted = $false; $F.Fact.killInvoked = $false
    $F.Fact.killRequestedUtc = $null
    Save-Exit $F
}
function Resolve-Fixture($F, [string]$Policy = 'Known2022EditorExit') {
    Resolve-HybridCLRFunctionalAcceptance -Scope $F.Scope -Run $F.Run -Phase $F.Phase -Policy $Policy -Expected $F.Expected
}
function Reject-Fixture($F, [string]$Policy = 'Known2022EditorExit') {
    $caught = $null; $output = @()
    try { $output = @(Resolve-Fixture $F $Policy) } catch { $caught = $_ }
    Assert-True ($null -ne $caught) 'Unsafe evidence was accepted.'
    Assert-True ($output.Count -eq 0) 'Rejected evidence leaked successful output.'
    Assert-True ($caught.Exception.Message.StartsWith('HybridCLR acceptance:', [StringComparison]::Ordinal)) "Not an intentional evidence rejection: $($caught.Exception.Message)"
}
function Assert-Accepted($Result, [bool]$Known) {
    Assert-True ($Result.FunctionalPassed -ceq $true -and $Result.Accepted -ceq $true) 'Functional result was not accepted.'
    Assert-True ($Result.ExitPassed -ceq (-not $Known) -and $Result.ProcessPassed -ceq (-not $Known)) 'Process failure was relabeled passed.'
    Assert-True ($Result.KnownIssueApplied -ceq $Known) 'Wrong known-issue classification.'
    Assert-True ($Result.AcceptanceStatus -ceq $(if ($Known) { 'PassedWithKnownIssue' } else { 'Passed' })) 'Wrong acceptance status.'
    Assert-True ($Result.ExitCode -eq $(if ($Known) { -1 } else { 0 }) -and $Result.EditorForcedCleanup -ceq $Known) 'Actual OS facts were rewritten.'
    Assert-True ($Result.ProcessFact.exitCode -eq $Result.ExitCode -and $Result.ProcessFact.succeeded -ceq (-not $Known)) 'Raw process fact was altered.'
}

try {
    Case 'helper-load-is-side-effect-free' {
        Assert-True ([IO.File]::Exists($helper)) 'Missing functional-acceptance helper.'
        foreach ($path in @($helper, $PSCommandPath)) {
            $tokens = $null; $errors = $null
            $null = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
            $parseResults.Add(@{ path = $path; errors = $errors.Count })
            Assert-True ($errors.Count -eq 0) 'PowerShell parse error.'
        }
        $before = @([IO.Directory]::GetFiles($fixture, '*', [IO.SearchOption]::AllDirectories))
        $preference = $ErrorActionPreference
        $output = @(. $helper)
        Assert-True ($output.Count -eq 0 -and $ErrorActionPreference -ceq $preference) 'Loading emitted output or changed caller preferences.'
        Assert-True (@([IO.Directory]::GetFiles($fixture, '*', [IO.SearchOption]::AllDirectories)).Count -eq $before.Count) 'Loading wrote files.'
    }
    if ([IO.File]::Exists($helper)) {
        . $helper
        Case 'strict-default-normal-discovery-needs-no-new-seal' {
            $f = New-Evidence Discovery; Set-NormalExit $f; [IO.File]::Delete($f.SealPath)
            Assert-Accepted (Resolve-HybridCLRFunctionalAcceptance $f.Scope $f.Run Discovery) $false
        }
        Case 'strict-normal-official-tests' {
            foreach ($version in @('2022.3.62f3c1', '6000.3.27f1')) {
                $f = New-Evidence Tests EditMode $version; Set-NormalExit $f; [IO.File]::Delete($f.SealPath)
                Assert-Accepted (Resolve-Fixture $f Strict) $false
            }
        }
        Case 'known-discovery-retains-negative-exit-and-pre-completion-errors' {
            $f = New-Evidence Discovery
            Assert-Accepted (Resolve-Fixture $f) $true
        }
        Case 'known-official-tests-in-both-editor-platforms' {
            foreach ($platform in @('EditMode', 'PlayMode')) {
                $f = New-Evidence Tests $platform
                Assert-Accepted (Resolve-Fixture $f) $true
            }
            foreach ($phase in @('discovery', 'tests')) {
                $f = New-Evidence $(if ($phase -eq 'discovery') { 'Discovery' } else { 'Tests' })
                Assert-Accepted (Resolve-HybridCLRFunctionalAcceptance -Scope $f.Scope -Run $f.Run -Phase $phase -Policy known2022editorexit -Expected $f.Expected) $true
            }
        }
        Case 'known-policy-with-real-exit-zero-does-not-apply-issue' {
            $f = New-Evidence; Set-NormalExit $f
            Assert-Accepted (Resolve-Fixture $f) $false
        }
        Case 'strict-rejects-post-completion-stall' { Reject-Fixture (New-Evidence) Strict }

        $specs = @(
            @{ Name = 'known-rejects-6000'; Version = '6000.3.27f1'; Change = { param($f) } },
            @{ Name = 'known-rejects-wrong-revision'; Revision = 'wrong'; Change = { param($f) } },
            @{ Name = 'legacy-or-unbound-is-not-a-new-owner'; Variants = @('legacy', 'unbound', 'simulation'); Change = { param($f, $v) switch ($v) { 'legacy' { $f.Scope.IsCompatibility = $false } 'unbound' { $f.Scope.Bound = $false } 'simulation' { $f.Scope.IsSimulation = $true } } } },
            @{ Name = 'immutable-owner-snapshot-drift'; Change = { param($f) Save-Text (Join-Path $f.Scope.Root 'owner.json') '{}' } },
            @{ Name = 'missing-producer-seal'; Change = { param($f) [IO.File]::Delete($f.SealPath) } },
            @{ Name = 'seal-schema-policy-phase-and-cleanup-are-exact'; Variants = @('SchemaVersion', 'Purpose', 'Policy', 'KnownIssueId', 'Phase', 'CleanupProof'); Change = { param($f, $v) $f.Seal[$v] = 'invalid'; Save-Seal $f } },
            @{ Name = 'seal-project-owner-version-run-and-receiver-bind'; Variants = @('Project', 'OwnerSha256', 'UnityVersion', 'UnityRevision', 'Run', 'Receiver'); Change = { param($f, $v) $f.Seal[$v] = 'foreign'; Save-Seal $f } },
            @{ Name = 'completion-lifecycle-and-pass-require-real-true'; Variants = @('Completed', 'LifecycleChecksCompleted', 'Passed'); Change = { param($f, $v) $f.Seal[$v] = 'true'; Save-Seal $f } },
            @{ Name = 'discovery-output-byte-drift'; Phase = 'Discovery'; Change = { param($f) Save-Text $f.OutputPath ([IO.File]::ReadAllText($f.OutputPath) + ' ') } },
            @{ Name = 'discovery-identity-is-not-callback-completion'; Phase = 'Discovery'; Variants = @('Passed', 'Project', 'OwnerSha256'); Change = { param($f, $v) $f.Discovery[$v] = 'foreign'; Save-Json $f.OutputPath $f.Discovery; $f.Seal.OutputSha256 = Hash-File $f.OutputPath; Save-Seal $f } },
            @{ Name = 'discovery-both-sets-nonempty-and-unique'; Phase = 'Discovery'; Variants = @('empty-edit', 'empty-play', 'duplicate', 'cross-duplicate'); Change = { param($f, $v) switch ($v) { 'empty-edit' { $f.Discovery.EditMode = @() } 'empty-play' { $f.Discovery.PlayMode = @() } 'duplicate' { $f.Discovery.EditMode += $f.Discovery.EditMode[0] } 'cross-duplicate' { $f.Discovery.PlayMode[0].Name = $f.Discovery.EditMode[0].Name } }; Save-Json $f.OutputPath $f.Discovery; $f.Seal.OutputSha256 = Hash-File $f.OutputPath; Save-Seal $f } },
            @{ Name = 'official-xml-is-mandatory'; Change = { param($f) [IO.File]::Delete($f.XmlPath) } },
            @{ Name = 'fixture-teardown-failure-is-not-leaf-success'; Change = { param($f) $f.Xml.'test-run'.SetAttribute('result', 'Failed'); $f.Xml.'test-run'.'test-suite'.SetAttribute('result', 'Failed'); Save-Text $f.XmlPath $f.Xml.OuterXml } },
            @{ Name = 'xml-root-counters-must-close'; Variants = @('total', 'passed', 'failed', 'skipped', 'inconclusive'); Change = { param($f, $v) $f.Xml.'test-run'.SetAttribute($v, '1'); Save-Text $f.XmlPath $f.Xml.OuterXml } },
            @{ Name = 'expected-names-are-nonempty-and-unique'; Variants = @('empty', 'duplicate'); Change = { param($f, $v) $f.Expected = $(if ($v -ceq 'empty') { @() } else { @($f.Expected[0], $f.Expected[0]) }) } },
            @{ Name = 'xml-missing-selected-leaf'; Change = { param($f) $node = $f.Xml.SelectSingleNode('//test-case'); $null = $node.ParentNode.RemoveChild($node); Save-Text $f.XmlPath $f.Xml.OuterXml } },
            @{ Name = 'xml-extra-leaf'; Change = { param($f) $node = $f.Xml.SelectSingleNode('//test-case'); $extra = $node.CloneNode($true); $extra.SetAttribute('fullname', 'HybridCLR.EditMode.Foreign'); $null = $node.ParentNode.AppendChild($extra); Save-Text $f.XmlPath $f.Xml.OuterXml } },
            @{ Name = 'xml-duplicate-leaf'; Change = { param($f) $node = $f.Xml.SelectSingleNode('//test-case'); $null = $node.ParentNode.AppendChild($node.CloneNode($true)); Save-Text $f.XmlPath $f.Xml.OuterXml } },
            @{ Name = 'xml-skips-inconclusives-and-assertion-failures'; Variants = @('Skipped', 'Inconclusive', 'Failed'); Change = { param($f, $v) $f.Xml.SelectSingleNode('//test-case').SetAttribute('result', $v); Save-Text $f.XmlPath $f.Xml.OuterXml } },
            @{ Name = 'seal-missing-leaf'; Change = { param($f) $f.Seal.Leaves = @($f.Seal.Leaves[0]); Save-Seal $f } },
            @{ Name = 'seal-extra-leaf'; Change = { param($f) $f.Seal.Leaves += @{ Name = 'HybridCLR.EditMode.Foreign'; Result = 'Passed' }; Save-Seal $f } },
            @{ Name = 'seal-duplicate-leaf'; Change = { param($f) $f.Seal.Leaves = @($f.Seal.Leaves[0], $f.Seal.Leaves[0]); Save-Seal $f } },
            @{ Name = 'seal-real-run-finished-counters-and-results'; Variants = @('FailedCount', 'SkippedCount', 'InconclusiveCount', 'Total', 'PassedCount', 'leaf'); Change = { param($f, $v) if ($v -ceq 'leaf') { $f.Seal.Leaves[0].Result = 'Failed' } else { $f.Seal[$v] = 1 }; Save-Seal $f } },
            @{ Name = 'selection-identity-platform-and-selected-set'; Variants = @('OwnerSha256', 'Project', 'TestPlatform', 'missing', 'extra', 'duplicate'); Change = { param($f, $v) switch ($v) { 'missing' { $f.Selection.Expected = @($f.Expected[0]) } 'extra' { $f.Selection.Expected += @{ Name = 'HybridCLR.EditMode.Other' } } 'duplicate' { $f.Selection.Expected = @($f.Expected[0], $f.Expected[0]) } default { $f.Selection[$v] = 'foreign' } }; Save-Json $f.SelectionPath $f.Selection; $f.Seal.SelectionSha256 = Hash-File $f.SelectionPath; Save-Seal $f } },
            @{ Name = 'selection-byte-hash-drift'; Change = { param($f) Save-Text $f.SelectionPath ([IO.File]::ReadAllText($f.SelectionPath) + ' ') } },
            @{ Name = 'execution-timeout-is-not-exit-stall'; Change = { param($f) $f.Fact.terminalStatus = 'TimedOut'; $f.Fact.timedOut = $true; Save-Exit $f } },
            @{ Name = 'nonzero-exit-is-not-exit-stall'; Change = { param($f) $f.Fact.terminalStatus = 'NonZeroExit'; $f.Fact.exitCode = 2; Save-Exit $f } },
            @{ Name = 'observer-lost-and-unknown-exit'; Variants = @('status', 'null-exit', 'text-exit'); Change = { param($f, $v) switch ($v) { 'status' { $f.Fact.terminalStatus = 'ObserverLost' } 'null-exit' { $f.Fact.observedExit = $false; $f.Fact.exitCode = $null } 'text-exit' { $f.Fact.exitCode = '-1' } }; Save-Exit $f } },
            @{ Name = 'cancellation-is-never-waived'; Change = { param($f) $f.Fact.cancellationReason = 'cancelled'; Save-Exit $f } },
            @{ Name = 'observer-error-is-never-waived'; Change = { param($f) $f.Fact.observerError = 'lost observation'; Save-Exit $f } },
            @{ Name = 'cleanup-error-is-never-waived'; Change = { param($f) $f.Fact.cleanupError = 'cleanup failed'; Save-Exit $f } },
            @{ Name = 'forced-cleanup-facts-must-all-agree'; Variants = @('editorForcedCleanup', 'killRequestPersisted', 'killInvoked', 'alive', 'childState', 'exitCode', 'timedOut', 'succeeded'); Change = { param($f, $v) switch ($v) { 'alive' { $f.Fact.alive = $true } 'childState' { $f.Fact.childState = 'Alive' } 'exitCode' { $f.Fact.exitCode = 0 } 'timedOut' { $f.Fact.timedOut = $true } 'succeeded' { $f.Fact.succeeded = $true } default { $f.Fact[$v] = $false } }; Save-Exit $f } },
            @{ Name = 'completion-observation-and-sidecar-are-mandatory'; Variants = @('not-observed', 'missing', 'foreign', 'terminal-sidecar', 'empty-time'); Change = { param($f, $v) switch ($v) { 'not-observed' { $f.Fact.completionObserved = $false; Save-Exit $f } 'missing' { [IO.File]::Delete($f.CompletionPath) } 'foreign' { $f.Completion.invocationId = '0' * 32; Save-Json $f.CompletionPath $f.Completion } 'terminal-sidecar' { $f.Completion.terminalStatus = 'Exited'; $f.Completion.succeeded = $true; $f.Completion.exitCode = 0; Save-Json $f.CompletionPath $f.Completion } 'empty-time' { $f.Fact.completionObservedUtc = ''; $f.Completion.completionObservedUtc = ''; $f.Kill.completionObservedUtc = ''; Save-Exit $f; Save-Json $f.CompletionPath $f.Completion; Save-Json $f.KillPath $f.Kill } } } },
            @{ Name = 'observer-seal-sha-must-match-current-bytes'; Change = { param($f) $f.Fact.completionSealSha256 = '0' * 64; Save-Exit $f } },
            @{ Name = 'observer-seal-path-is-exact-run-child'; Change = { param($f) $f.Fact.completionSealPath = Join-Path $f.Scope.Root 'other.completion.json'; Save-Exit $f } },
            @{ Name = 'exit-grace-must-be-an-integer-at-least-one-second'; Variants = @(999, '120000'); Change = { param($f, $v) $f.Fact.exitGraceMilliseconds = $v; Save-Exit $f } },
            @{ Name = 'known-shutdown-must-follow-exact-completion-marker'; Variants = @('missing', 'before', 'wrong-run'); Change = { param($f, $v) $text = switch ($v) { 'missing' { "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nStill executing" } 'before' { "Input System module state changed to: Shutdown`nHYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)" } 'wrong-run' { "HYBRIDCLR_FUNCTIONAL_COMPLETED other`nExiting batchmode successfully now" } }; Save-Text $f.Log $text } },
            @{ Name = 'post-completion-errors-are-not-expected-logasserts'; Variants = @('fatal error', 'Crash!!!', 'Unhandled Exception', 'error CS1002', 'NullReferenceException', 'AssertionException', 'ordinary error', 'Exception: failed', 'Segmentation fault', 'Access violation'); Change = { param($f, $v) Save-Text $f.Log ("HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`n$v`nInput System module state changed to: Shutdown") } },
            @{ Name = 'unsafe-run-and-scope-paths'; Variants = @('run', 'relative', 'dotdot', 'ads', 'trailing-dot'); Change = { param($f, $v) switch ($v) { 'run' { $f.Run = '../owned-run' } 'relative' { $f.Scope.Root = 'h1e' } 'dotdot' { $f.Scope.Root += '\..\h1e' } 'ads' { $f.Scope.Root += ':stream' } 'trailing-dot' { $f.Scope.Root += '.' } } } },
            @{ Name = 'linked-root-seal-and-artifact-are-rejected'; Variants = @('root', 'seal', 'xml', 'log', 'selection'); Change = { param($f, $v) $target = Join-Path ([IO.Path]::GetDirectoryName($f.Scope.Root)) 'link-target'; $null = [IO.Directory]::CreateDirectory($target); if ($v -ceq 'root') { $link = Join-Path ([IO.Path]::GetDirectoryName($f.Scope.Root)) 'linked-evidence'; $f.Scope.Root = $link } else { $link = switch ($v) { 'seal' { $f.SealPath } 'xml' { $f.XmlPath } 'log' { $f.Log } 'selection' { $f.SelectionPath } }; [IO.File]::Delete($link) }; $null = New-Item -ItemType Junction -Path $link -Target $target -ErrorAction Stop } },
            @{ Name = 'producer-seal-byte-drift-after-observation'; Change = { param($f) Save-Text $f.SealPath ([IO.File]::ReadAllText($f.SealPath) + ' ') } },
            @{ Name = 'player-build-and-foreign-process-evidence'; Variants = @('player', 'build', 'owner', 'run', 'build-switch', 'mixed-case-command'); Change = { param($f, $v) switch ($v) { 'player' { $f.Fact.exe = Join-Path $f.Scope.Project 'Player.exe' } 'build' { $f.Fact.arguments += @('-executeMethod', 'Build.Player') } 'owner' { $f.Fact.ownerSha256 = '0' * 64 } 'run' { $f.Fact.run = 'other' } 'build-switch' { $f.Fact.arguments += @('-buildWindows64Player', 'out.exe'); $f.Process.arguments = $f.Fact.arguments; Save-Json ($f.Log + '.process.json') $f.Process } 'mixed-case-command' { $f.Fact.arguments += @('-ExecuteMethod', 'Build.Player'); $f.Process.arguments = $f.Fact.arguments; Save-Json ($f.Log + '.process.json') $f.Process } }; Save-Exit $f } }
        )
        foreach ($spec in $specs) {
            Case $spec.Name {
                $variants = $(if ($spec.ContainsKey('Variants')) { $spec.Variants } else { @('one') })
                foreach ($variant in $variants) {
                    $phase = $(if ($spec.ContainsKey('Phase')) { $spec.Phase } else { 'Tests' })
                    $version = $(if ($spec.ContainsKey('Version')) { $spec.Version } else { '2022.3.62f3c1' })
                    $revision = $(if ($spec.ContainsKey('Revision')) { $spec.Revision } else { '' })
                    $f = New-Evidence $phase EditMode $version $revision
                    & $spec.Change $f $variant
                    Reject-Fixture $f
                }
            }
        }
        Case 'resolver-is-read-only-and-leaf-order-independent' {
            $f = New-Evidence
            $paths = @([IO.Directory]::GetFiles($f.Scope.Root))
            $before = @($paths | ForEach-Object { Hash-File $_ })
            Assert-Accepted (Resolve-Fixture $f) $true
            Assert-True (@([IO.Directory]::GetFiles($f.Scope.Root)).Count -eq $paths.Count) 'Resolver created evidence.'
            for ($index = 0; $index -lt $paths.Count; $index++) { Assert-True ((Hash-File $paths[$index]) -ceq $before[$index]) 'Resolver modified original evidence.' }
        }
        Case 'exact-curl42-after-completion-and-shutdown-is-retained' {
            foreach ($phase in @('Discovery', 'Tests')) {
                foreach ($shutdown in @('Input System module state changed to: Shutdown', 'Exiting batchmode successfully now')) {
                    $f = New-Evidence $phase
                    Save-Text $f.Log "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`n$shutdown`nCurl error 42: Callback aborted`n"
                    $before = Hash-File $f.ExitPath
                    $result = Resolve-Fixture $f
                    Assert-Accepted $result $true
                    Assert-True ($result.KnownShutdownDiagnostics -is [string[]] -and $result.KnownShutdownDiagnostics.Count -eq 1 -and
                        $result.KnownShutdownDiagnostics[0] -ceq 'Curl error 42: Callback aborted') 'The exact shutdown diagnostic was not retained.'
                    Assert-True ((Hash-File $f.ExitPath) -ceq $before) 'Shutdown classification rewrote original exit facts.'
                }
            }
        }
        Case 'curl42-before-shutdown-is-not-retrospectively-waived' {
            $f = New-Evidence Discovery
            Save-Text $f.Log "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nCurl error 42: Callback aborted`nInput System module state changed to: Shutdown`n"
            Reject-Fixture $f
        }
        Case 'curl42-before-completion-is-not-a-shutdown-diagnostic' {
            $f = New-Evidence Discovery
            Save-Text $f.Log "Curl error 42: Callback aborted`nHYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nInput System module state changed to: Shutdown`n"
            Reject-Fixture $f
        }
        Case 'shutdown-before-marker-cannot-authorize-curl42-after-marker' {
            $f = New-Evidence Discovery
            Save-Text $f.Log "Input System module state changed to: Shutdown`nHYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nCurl error 42: Callback aborted`nInput System module state changed to: Shutdown`n"
            Reject-Fixture $f
        }
        Case 'shutdown-curl-whitelist-is-exact-case-sensitive-and-whole-line' {
            foreach ($text in @('Curl error 43: Callback aborted', 'Curl error 28: Callback aborted', 'Curl error 42: Transfer aborted',
                'curl error 42: Callback aborted', 'Curl error 42: Callback aborted ', ' Curl error 42: Callback aborted',
                'prefix Curl error 42: Callback aborted', 'Curl error 42: Callback aborted additional text',
                'Curl error 42: Callback aborted error CS1002', 'Curl error 42: Callback aborted Exception: failed')) {
                $f = New-Evidence
                Save-Text $f.Log "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nInput System module state changed to: Shutdown`n$text`n"
                Reject-Fixture $f
            }
        }
        Case 'retained-curl42-does-not-hide-other-shutdown-errors' {
            foreach ($text in @('ordinary error', 'Unhandled Exception', 'fatal error', 'Crash!!!')) {
                $f = New-Evidence
                Save-Text $f.Log "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nInput System module state changed to: Shutdown`nCurl error 42: Callback aborted`n$text`n"
                Reject-Fixture $f
            }
        }
        Case 'curl42-does-not-waive-strict-process-gate' {
            $f = New-Evidence Discovery
            Save-Text $f.Log "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nInput System module state changed to: Shutdown`nCurl error 42: Callback aborted`n"
            Reject-Fixture $f Strict
        }
        Case 'accepted-results-without-curl42-have-empty-diagnostics-array' {
            foreach ($policy in @('Strict', 'Known2022EditorExit')) {
                $f = New-Evidence
                if ($policy -ceq 'Strict') { Set-NormalExit $f }
                $result = Resolve-Fixture $f $policy
                Assert-Accepted $result ($policy -ceq 'Known2022EditorExit')
                Assert-True ($result.KnownShutdownDiagnostics -is [string[]] -and $result.KnownShutdownDiagnostics.Count -eq 0) 'Absent diagnostics must be an empty string array.'
            }
        }
        Case 'exit-intent-then-curl42-then-shutdown-is-retained' {
            foreach ($phase in @('Discovery', 'Tests')) {
                $f = New-Evidence $phase
                Save-Text $f.Log "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nHYBRIDCLR_EDITOR_EXIT_REQUESTED $($f.Run)`nCurl error 42: Callback aborted`nInput System module state changed to: Shutdown`n"
                $result = Resolve-Fixture $f
                Assert-Accepted $result $true
                Assert-True ($result.KnownShutdownDiagnostics.Count -eq 1 -and
                    $result.KnownShutdownDiagnostics[0] -ceq 'Curl error 42: Callback aborted') 'Exit-intent diagnostic was not retained.'
            }
        }
        Case 'curl42-before-exit-intent-is-not-retrospectively-waived' {
            $f = New-Evidence Discovery
            Save-Text $f.Log "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nCurl error 42: Callback aborted`nHYBRIDCLR_EDITOR_EXIT_REQUESTED $($f.Run)`nInput System module state changed to: Shutdown`n"
            Reject-Fixture $f
        }
        Case 'foreign-or-inexact-exit-intent-does-not-waive-curl42' {
            foreach ($intent in @('HYBRIDCLR_EDITOR_EXIT_REQUESTED other', 'HYBRIDCLR_EDITOR_EXIT_REQUESTED owned-run-extra',
                'hybridclr_editor_exit_requested owned-run', 'HYBRIDCLR_EDITOR_EXIT_REQUESTED owned-run ',
                ' HYBRIDCLR_EDITOR_EXIT_REQUESTED owned-run', 'HYBRIDCLR_EDITOR_EXIT_REQUESTED owned-run extra')) {
                $f = New-Evidence Discovery
                Save-Text $f.Log "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`n$intent`nCurl error 42: Callback aborted`nInput System module state changed to: Shutdown`n"
                Reject-Fixture $f
            }
        }
        Case 'exit-intent-before-completion-does-not-authorize-later-curl42' {
            $f = New-Evidence Discovery
            Save-Text $f.Log "HYBRIDCLR_EDITOR_EXIT_REQUESTED $($f.Run)`nHYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nCurl error 42: Callback aborted`nInput System module state changed to: Shutdown`n"
            Reject-Fixture $f
        }
        Case 'exit-intent-without-native-or-official-shutdown-is-incomplete' {
            $f = New-Evidence Discovery
            Save-Text $f.Log "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nHYBRIDCLR_EDITOR_EXIT_REQUESTED $($f.Run)`nCurl error 42: Callback aborted`n"
            Reject-Fixture $f
        }
        Case 'exit-intent-does-not-waive-other-errors-or-curl-suffixes' {
            foreach ($text in @('Curl error 43: Callback aborted', 'Curl error 42: Callback aborted extra',
                "Curl error 42: Callback aborted`nUnhandled Exception", "fatal error`nCurl error 42: Callback aborted")) {
                $f = New-Evidence
                Save-Text $f.Log "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nHYBRIDCLR_EDITOR_EXIT_REQUESTED $($f.Run)`n$text`nInput System module state changed to: Shutdown`n"
                Reject-Fixture $f
            }
        }
        Case 'non-utf8-preamble-does-not-decode-the-completed-tail-leniently' {
            foreach ($phase in @('Discovery', 'Tests')) {
                foreach ($newline in @("`n", "`r`n")) {
                    $f = New-Evidence $phase
                    $tail = "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)$newline" +
                        "HYBRIDCLR_EDITOR_EXIT_REQUESTED $($f.Run)$newline" +
                        "Curl error 42: Callback aborted${newline}Input System module state changed to: Shutdown$newline"
                    [IO.File]::WriteAllBytes($f.Log, [byte[]]($utf8.GetBytes('native warning: ') + @(0xd6, 0xd0) + $utf8.GetBytes($newline + $tail)))
                    Assert-Accepted (Resolve-Fixture $f) $true
                }
            }
        }
        Case 'non-utf8-completed-tail-is-rejected-with-sanitized-decoder-type' {
            foreach ($location in @('before-intent', 'after-shutdown', 'truncated-eof')) {
                $f = New-Evidence
                $marker = "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`n"
                $shutdown = "HYBRIDCLR_EDITOR_EXIT_REQUESTED $($f.Run)`nInput System module state changed to: Shutdown`n"
                $bytes = switch ($location) {
                    'before-intent' { [byte[]]($utf8.GetBytes($marker) + @(0xd6, 0xd0, 10) + $utf8.GetBytes($shutdown)) }
                    'after-shutdown' { [byte[]]($utf8.GetBytes($marker + $shutdown) + @(0xff, 10)) }
                    'truncated-eof' { [byte[]]($utf8.GetBytes($marker + $shutdown) + @(0xc3)) }
                }
                [IO.File]::WriteAllBytes($f.Log, $bytes)
                $caught = $null
                try { $null = Resolve-Fixture $f } catch { $caught = $_ }
                Assert-True ($null -ne $caught -and $caught.Exception.Message.Contains('System.Text.DecoderFallbackException', [StringComparison]::Ordinal)) 'Strict tail decoding must reject with only the internal exception type.'
            }
        }
        Case 'raw-preamble-cannot-invent-a-missing-foreign-or-cross-line-marker' {
            foreach ($marker in @('unrelated', 'HYBRIDCLR_FUNCTIONAL_COMPLETED other',
                "HYBRIDCLR_FUNCTIONAL_COMPLETED`nowned-run", 'HYBRIDCLR_FUNCTIONAL_COMPLETED owned-run-extra')) {
                $f = New-Evidence Discovery
                [IO.File]::WriteAllBytes($f.Log, [byte[]](@(0xff, 10) + $utf8.GetBytes("$marker`nInput System module state changed to: Shutdown`n")))
                Reject-Fixture $f
            }
        }
        Case 'non-utf8-preamble-does-not-hide-duplicate-completion-markers' {
            $f = New-Evidence Discovery
            $tail = "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nHYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nInput System module state changed to: Shutdown`n"
            [IO.File]::WriteAllBytes($f.Log, [byte[]](@(0xff, 10) + $utf8.GetBytes($tail)))
            Reject-Fixture $f
        }
        Case 'non-utf8-preamble-does-not-hide-post-completion-errors' {
            foreach ($errorLine in @('error CS1002', 'Unhandled Exception', 'fatal error')) {
                $f = New-Evidence
                $tail = "HYBRIDCLR_FUNCTIONAL_COMPLETED $($f.Run)`nHYBRIDCLR_EDITOR_EXIT_REQUESTED $($f.Run)`n$errorLine`nInput System module state changed to: Shutdown`n"
                [IO.File]::WriteAllBytes($f.Log, [byte[]](@(0xff, 10) + $utf8.GetBytes($tail)))
                Reject-Fixture $f
            }
        }
    }
} finally {
    $failed = @($cases | Where-Object { -not $_.passed }).Count
    $summary = @{
        fixtureDirectory = $fixture; run = $Run; cases = $cases.ToArray(); total = $cases.Count
        passed = $cases.Count - $failed; failed = $failed; fixtureCount = $nextFixture; parseResults = $parseResults.ToArray()
        powershell = $PSVersionTable.PSVersion.ToString(); fakeEvidenceOnly = $true
        unityStarted = $false; playerStarted = $false; ideStarted = $false
        testSha256 = Hash-File $PSCommandPath; helperSha256 = $(if ([IO.File]::Exists($helper)) { Hash-File $helper } else { $null })
    }
    Save-Json (Join-Path $fixture 'results.json') $summary
    "Results: $fixture; passed=$($summary.passed)/$($summary.total); fakeEvidenceOnly=true"
}
if ($failed -ne 0) { throw 'Functional-acceptance regression tests failed; preserve results.json.' }
