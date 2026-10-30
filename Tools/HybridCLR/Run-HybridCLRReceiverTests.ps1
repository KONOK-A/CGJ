#requires -Version 7.6
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run,
    [string]$Filter = 'YLFramework.Tests.Integrations.HybridCLR.HybridCLRBuildPipelineTests',
    [string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021',
    [string]$Receiver,
    [string]$Unity,
    [string]$ExpectedUnityVersion,
    [string]$ExpectedUnityRevision,
    [string]$UguiVersion,
    [string]$TestFrameworkVersion,
    [ValidateSet('EditMode','PlayMode')][string]$TestPlatform = 'EditMode',
    [ValidatePattern('^[A-Za-z0-9-]+$')][string]$DiscoveryRun,
    [ValidateSet('Strict','Known2022EditorExit')][string]$ExitPolicy='Strict'
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$binding = @{}
foreach ($name in @('Unity','ExpectedUnityVersion','ExpectedUnityRevision','UguiVersion','TestFrameworkVersion')) {
    if ($PSBoundParameters.ContainsKey($name)) { $binding[$name]=$PSBoundParameters[$name] }
}
$scope = . (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root -Receiver $Receiver @binding
$ExitPolicy=Resolve-HybridCLRExitPolicy $scope $ExitPolicy
if ($scope.IsCompatibility -and (-not $PSBoundParameters.ContainsKey('TestPlatform') -or -not $PSBoundParameters.ContainsKey('Filter') -or -not $DiscoveryRun)) { throw 'New receivers require an explicit EditMode/PlayMode, exact filter and actual discovery; Prepare is a separate explicit engine invocation.' }
$Root = $scope.Root
$project = $scope.Project
Assert-HybridCLRReady $scope
Assert-HybridCLRProductImports $scope
Assert-HybridCLRNewEvidence $scope $Run
if ($scope.IsCompatibility) { Assert-HybridCLRNewEvidence $scope $Run @('.selection.json','.scene.json','.completion.json','.acceptance.json') }
$active = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($project) })
if ($active.Count) { throw 'Only one Editor per receiver is allowed.' }
$log = Join-Path $Root ($Run + '.log')
$result = Join-Path $Root ($Run + '.xml')
if ([IO.File]::Exists($log) -or [IO.File]::Exists($result)) { throw 'Preserve prior test evidence.' }
$unity = $scope.Unity
if (-not (Test-Path -LiteralPath $Root) -or -not (Test-Path -LiteralPath $unity)) { throw 'Explicit execution parents must exist.' }
$nativeFilter=$Filter
$ownedSceneHost=$scope.IsCompatibility
if ($scope.IsCompatibility) {
    if ($ExitPolicy -ceq 'Strict') {$null=Assert-HybridCLRNormalProcessExit $scope $DiscoveryRun}
    else {
        . (Join-Path $PSScriptRoot 'Get-HybridCLRFunctionalAcceptance.ps1')
        $discoveryAcceptance=Resolve-HybridCLRFunctionalAcceptance -Scope $scope -Run $DiscoveryRun -Phase Discovery -Policy $ExitPolicy
        $null=Assert-HybridCLRStoredAcceptance $scope $DiscoveryRun $discoveryAcceptance
    }
    $discoveryPath=Join-Path $Root ($DiscoveryRun+'.json')
    Assert-NoLinks $discoveryPath
    $discovery=[IO.File]::ReadAllText($discoveryPath) | ConvertFrom-Json -AsHashtable
    $installation=Assert-HybridCLRTestInstallation $scope $discovery
    $ownedSceneHost=$installation.SceneProfile -ceq 'Owned'
    $selected=@(Select-HybridCLRTests $scope $discovery $TestPlatform $Filter)
    if (@($discovery.Sources).Count -eq 0) { throw 'Actual discovered test source fingerprints are required.' }
    foreach ($source in $discovery.Sources) {
        Assert-HybridCLRExplicitPath $source.Path
        if (-not $source.Path.StartsWith((Join-Path $project 'Assets/YLFramework/Tests/Integrations/HybridCLR/').Replace('/','\'),[StringComparison]::OrdinalIgnoreCase) -or
            (Get-FileHash -LiteralPath $source.Path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $source.Sha256) { throw 'Discovered test sources changed; discover the actual current graph again.' }
        Assert-NoLinks ($source.Path+'.meta')
        if ($source.MetaSha256 -cne (Get-FileHash -LiteralPath ($source.Path+'.meta') -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Discovered source meta identity changed.' }
    }
    $nativeFilter='^(?:'+(($selected | ForEach-Object { [regex]::Escape($_.Name) }) -join '|')+')$'
    if ($nativeFilter.Length -gt 28000) { throw 'Select a narrower HybridCLR fixture within native command-line limits.' }
    $selectionPath=Join-Path $Root ($Run+'.selection.json')
    Assert-NoLinks $selectionPath
    if ([IO.Path]::Exists($selectionPath)) { throw 'Preserve prior HybridCLR selections.' }
    [IO.File]::WriteAllText($selectionPath,(@{ Project=$project; UnityVersion=$scope.ExpectedUnityVersion; UnityRevision=$scope.ExpectedUnityRevision
        Receiver=$scope.Owner.receiver; OwnerSha256=$scope.OwnerSha256; TestPlatform=$TestPlatform; Expected=$selected
        DiscoverySha256=(Get-FileHash -LiteralPath $discoveryPath -Algorithm SHA256).Hash.ToLowerInvariant() } | ConvertTo-Json -Depth 12))
}
$start = [Diagnostics.ProcessStartInfo]::new($unity)
$start.UseShellExecute = $false; $start.WorkingDirectory = $Root
foreach ($argument in (Get-HybridCLRTestArguments $scope $TestPlatform $nativeFilter $result $log $ownedSceneHost $ExitPolicy)) { $start.ArgumentList.Add($argument) }
if ($scope.IsCompatibility) { $start.ArgumentList.Add('-assemblyNames'); $start.ArgumentList.Add('YLFramework.HybridCLR.Tests.'+$TestPlatform) }
if ($scope.IsCompatibility) {
    . (Join-Path $PSScriptRoot 'Invoke-HybridCLROwnedProcess.ps1')
    $processParameters=@{StartInfo=$start;EvidenceRoot=$Root;Log=$log;Project=$project;Run=$Run;OwnerSha256=$scope.OwnerSha256;TimeoutMilliseconds=1800000}
    if ($ExitPolicy -cne 'Strict') {$processParameters.CompletionSealPath=Join-Path $Root ($Run+'.completion.json');$processParameters.ExitGraceMilliseconds=120000}
    try {$observation=Invoke-HybridCLROwnedProcess @processParameters}
    catch {
        $fact=$_.Exception.Data['HybridCLRProcessResult']
        if ($ExitPolicy -ceq 'Strict' -or $null -eq $fact -or $fact.terminalStatus -cne 'PostCompletionExitStalled') {throw}
        $observation=[pscustomobject]$fact
    }
    $code=$observation.exitCode
} else {
$process = [Diagnostics.Process]::Start($start)
[IO.File]::WriteAllText($log + '.process.json', (@{ pid=$process.Id; project=$project; filter=$Filter; testPlatform=$TestPlatform; unityVersion=$scope.ExpectedUnityVersion; unityRevision=$scope.ExpectedUnityRevision; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
$timedOut = -not $process.WaitForExit(1800000)
if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
$code = $process.ExitCode; $process.Dispose()
[IO.File]::WriteAllText($log + '.exit.json', (@{ exitCode=$code; timedOut=$timedOut; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
if ($timedOut) { throw 'The actual Unity test run exceeded its bounded execution window; forced cleanup is not a pass.' }
}
if (-not [IO.File]::Exists($result)) { throw "No actual NUnit XML was produced; Unity exit=$code" }
$xml = [xml][IO.File]::ReadAllText($result)
$summary = $xml.'test-run'
if ($ExitPolicy -ceq 'Strict') {Assert-HybridCLRTestResult -ExitCode $code -Xml $xml}
else {
    # 正常退出无需使用已知问题豁免，沿用 Strict 的实际进程与完整 NUnit 证据标准。
    $resultPolicy=if($code -eq 0 -and $observation.succeeded){'Strict'}else{$ExitPolicy}
    $acceptance=Resolve-HybridCLRFunctionalAcceptance -Scope $scope -Run $Run -Phase Tests -Policy $resultPolicy -Expected $selected
}
if ($scope.IsCompatibility) {
    Assert-HybridCLRExecutedTests $selected $xml
    if ($TestPlatform -ceq 'EditMode' -and $ownedSceneHost) {
        $scenePath = Join-Path $scope.Root ($Run + '.scene.json')
        Assert-NoLinks $scenePath
        $scene = [IO.File]::ReadAllText($scenePath) | ConvertFrom-Json
        Assert-HybridCLRTestScene $scope $scene $Run
    }
}
if ($ExitPolicy -cne 'Strict') {
    $null=Assert-HybridCLRTestInstallation $scope $discovery
    foreach($source in $discovery.Sources) {
        Assert-NoLinks ($source.Path+'.meta')
        if ($source.Sha256 -cne (Get-FileHash -LiteralPath $source.Path).Hash.ToLowerInvariant() -or
            $source.MetaSha256 -cne (Get-FileHash -LiteralPath ($source.Path+'.meta')).Hash.ToLowerInvariant()) {throw 'Source or meta changed during functional tests.'}
    }
    Publish-HybridCLRFunctionalAcceptance $scope $Run $acceptance
}
"Unity actual NUnit result=$($summary.result); passed=$($summary.passed); failed=$($summary.failed); skipped=$($summary.skipped); XML=$result"
if ($ExitPolicy -cne 'Strict') {"Acceptance=$($acceptance.AcceptanceStatus); FunctionalPassed=$($acceptance.FunctionalPassed); ExitPassed=$($acceptance.ExitPassed); actualExit=$code; KnownIssueApplied=$($acceptance.KnownIssueApplied)"}
