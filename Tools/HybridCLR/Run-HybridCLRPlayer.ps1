#requires -Version 7.6
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('V1','Update','LocalUpdate','V2','BadPayload','BadMetadata','BadSignature','ProjectSubsets','Parameters')][string]$Action,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run,
    [ValidateSet('Development','Release')][string]$Variant = 'Development',
    [string]$TrustReceipt,
    [string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021',
    [string]$Receiver,
    [ValidatePattern('^[A-Za-z0-9-]+$')][string]$CandidateRun
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$scope = . (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root -Receiver $Receiver
$Root = $scope.Root
$owner = $scope.Owner
if ($scope.IsCompatibility) {
    Assert-HybridCLRReady $scope
    Assert-HybridCLRNewEvidence $scope $Run
}
if ($Action -eq 'ProjectSubsets') {
    if ($Variant -ne 'Development' -or $TrustReceipt -or $Run -in @('project-subsets-base','project-subsets-patch')) { throw 'ProjectSubsets requires its own new Development/local-snapshot run.' }
    $subsetBasePath = Join-Path $Root 'project-subsets-base.json'
    $subsetBase = [IO.File]::ReadAllText($subsetBasePath) | ConvertFrom-Json
    $subsetPatch = [IO.File]::ReadAllText((Join-Path $Root 'project-subsets-patch.json')) | ConvertFrom-Json
    $subsetConflict = [IO.File]::ReadAllText((Join-Path $Root 'project-subsets-conflict.json')) | ConvertFrom-Json
    Assert-HybridCLRReceiptBinding $scope $subsetBase
    Assert-HybridCLRReceiptBinding $scope $subsetPatch
    Assert-HybridCLRReceiptBinding $scope $subsetConflict
    if ($subsetBase.SubsetConsumerVersion -ne 2 -or $subsetPatch.SubsetConsumerVersion -ne 2 -or $subsetConflict.SubsetConsumerVersion -ne 2 -or
        -not $subsetConflict.passed -or -not $subsetConflict.FixtureOnly -or $subsetConflict.PlayerFingerprint -cne $subsetBase.PlayerFingerprint -or
        $subsetConflict.BaselineFingerprint -cne $subsetBase.BaselineFingerprint -or $subsetConflict.CompatibilityId -cne $subsetBase.CompatibilityId) { throw 'A fresh conflict-capable subset consumer and producer are required.' }
    if (-not $subsetBase.passed -or -not $subsetPatch.passed -or -not $subsetBase.Development -or -not $subsetPatch.Development -or
        $subsetBase.CompatibilityId -ne $scope.SubsetCompatibility -or $subsetPatch.CompatibilityId -ne $subsetBase.CompatibilityId -or
        -not [IO.Path]::IsPathFullyQualified($subsetBase.PlayerDirectory)) { throw 'The dedicated successful subset base/patch receipts are required.' }
    $playerRoot = [IO.Path]::GetFullPath($subsetBase.PlayerDirectory)
    if ($playerRoot -ne $scope.SubsetPlayer -or
        [IO.Path]::GetFullPath($subsetBase.RuntimeCacheDirectory) -ne $scope.SubsetCache -or
        [IO.Path]::GetFullPath($subsetBase.Baseline) -ne $scope.SubsetBaseline) {
        throw 'Refuse any legacy or unowned Player, cache or baseline path.'
    }
    if ([IO.Path]::Exists($subsetBase.RuntimeCacheDirectory) -or [IO.Path]::Exists($subsetBase.RuntimeCacheDirectory + '.meta')) { throw 'Never adopt an existing project-subset runtime cache.' }
} else {
    $playerRoot = [IO.Path]::GetFullPath($(if ($Variant -eq 'Development') { $owner.playerRoot } else { $owner.releasePlayerRoot }))
    if ($scope.IsCompatibility) {
        $basePath = Join-Path $Root $(if ($Variant -eq 'Development') { $owner.baseReceiptStem + '.json' } else { 'base-release-receipt.json' })
        Assert-NoLinks $basePath
        $baseBinding = [IO.File]::ReadAllText($basePath) | ConvertFrom-Json
        Assert-HybridCLRReceiptBinding $scope $baseBinding
        if (-not $baseBinding.passed -or $baseBinding.Development -ne ($Variant -eq 'Development') -or
            [IO.Path]::GetFullPath($baseBinding.PlayerDirectory) -ine $playerRoot -or
            [IO.Path]::GetFullPath($baseBinding.Baseline) -ine $owner[$(if ($Variant -eq 'Development') { 'baseline' } else { 'releaseBaseline' })]) { throw 'Wrong owned Player/baseline path or Development identity.' }
    }
}
$player = Join-Path $playerRoot 'HybridCLR.exe'
if ($Action -eq 'BadSignature' -and (-not $scope.IsCompatibility -or $Variant -cne 'Development' -or
    $null -eq $baseBinding.PSObject.Properties['NegativeConsumerVersion'] -or $baseBinding.NegativeConsumerVersion -ne 1)) {
    throw 'BadSignature requires a fresh signature-capable owned Development base; legacy or already-frozen older consumers cannot be reinterpreted.'
}
Assert-NoLinks $player
if ($owner.ContainsKey('nativeSourceManifest')) {
    if ($Action -ceq 'ProjectSubsets') {
        Assert-HybridCLRSubsetNativeBinding $owner $subsetBase $subsetPatch
        Assert-HybridCLRSubsetNativeBinding $owner $subsetBase $subsetConflict
    } elseif ($baseBinding.NativeSourceManifestSha256 -cne $owner.nativeSourceManifestSha256 -or
        $baseBinding.NativeVariant -cnotin @('ParameterOwner','UnmodifiedControl')) { throw 'The exact custom native source variant must remain part of this Player base.' }
}
if ($Action -ceq 'Parameters' -and ($Variant -cne 'Release' -or -not $owner.ContainsKey('nativeSourceManifest'))) { throw 'The native parameter experiment requires an explicitly bound ordinary Release base.' }
if (-not (Test-Path -LiteralPath $Root) -or -not (Test-Path -LiteralPath $player)) { throw 'A real successful base Player is required.' }
$log = Join-Path $Root ($Run + '.log')
$receipt = Join-Path $Root ($Run + '.json')
if ([IO.File]::Exists($log) -or [IO.File]::Exists($receipt)) { throw 'Do not overwrite prior Player evidence.' }
if ($Action -eq 'ProjectSubsets') {
    foreach ($path in @($log, $receipt, ($log + '.process.json'), ($log + '.exit.json'))) {
        if ([IO.Path]::Exists($path)) { throw 'Do not overwrite any prior project-subset invocation evidence.' }
    }
    $active = @(Get-CimInstance Win32_Process -Filter "Name = 'HybridCLR.exe'" | Where-Object { $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -eq $player })
    if ($active.Count) { throw 'This owned subset Player is already running.' }
}
function Get-PlayerFingerprint {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $rows = @([IO.Directory]::GetFiles($playerRoot, '*', [IO.SearchOption]::AllDirectories) | Sort-Object -CaseSensitive | ForEach-Object {
            $relative = $_.Substring($playerRoot.Length + 1).Replace('\','/')
            Assert-NoLinks $_
            $relative + ':' + (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
        })
        return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($rows -join "`n")))).ToLowerInvariant()
    } finally { $sha.Dispose() }
}
function Test-HybridCLRPlayerGraphics($Result, [string]$Action, [string]$LogText) {
    $keys=if ($Result -is [Collections.IDictionary]) { @($Result.Keys) } else { @($Result.PSObject.Properties.Name) }
    if ($keys -ccontains 'graphicsDevice') { return -not [string]::IsNullOrWhiteSpace($Result.graphicsDevice) -and $Result.graphicsDevice -cne 'Null' }
    return $Action -ceq 'ProjectSubsets' -and $LogText -notmatch 'NullGfxDevice' -and
        $LogText -match '(?m)^Direct3D:\r?$' -and $LogText -match '(?m)^[ \t]+Version:[ \t]+Direct3D (?:11|12)\b' -and
        $LogText -match '(?m)^[ \t]+Renderer:[ \t]+\S[^\r\n]*'
}
function Get-ProjectSubsetFingerprint([string]$Directory) {
    $directoryRoot = [IO.Path]::GetFullPath($Directory).TrimEnd([char[]]'\/')
    if (-not [IO.Directory]::Exists($directoryRoot)) { throw 'Missing dedicated acceptance output.' }
    for ($cursor = $directoryRoot; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked acceptance paths are not supported.' }
    }
    $files = [Collections.Generic.List[string]]::new()
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($directoryRoot)
    while ($pending.Count) {
        foreach ($path in [IO.Directory]::GetFileSystemEntries($pending.Pop())) {
            $attributes = [IO.File]::GetAttributes($path)
            if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked acceptance content is not supported.' }
            if ($attributes -band [IO.FileAttributes]::Directory) { $pending.Push($path) } else { $files.Add($path) }
        }
    }
    if (-not $files.Count) { throw 'Cannot fingerprint an empty acceptance output.' }
    [string[]]$ordered = $files.ToArray()
    [Array]::Sort($ordered, [StringComparer]::Ordinal)
    $rows = @(foreach ($path in $ordered) {
        $path.Substring($directoryRoot.Length + 1).Replace('\','/') + ':' + (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    })
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($rows -join "`n")))).ToLowerInvariant() }
    finally { $sha.Dispose() }
}
if ($Action -eq 'ProjectSubsets') {
    $before = Get-ProjectSubsetFingerprint $playerRoot
    $subsetBaselineBefore = Get-ProjectSubsetFingerprint $subsetBase.Baseline
    $subsetBaselineSha = (Get-FileHash -LiteralPath (Join-Path $subsetBase.Baseline 'baseline.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($before -cne $subsetBase.PlayerFingerprint -or $before -cne $subsetPatch.PlayerFingerprint -or
        $subsetBaselineBefore -cne $subsetBase.BaselineFingerprint -or $subsetBaselineBefore -cne $subsetPatch.BaselineFingerprint -or
        $subsetBaselineSha -cne $subsetBase.BaselineSha256 -or $subsetBaselineSha -cne $subsetPatch.BaselineSha256) { throw 'The frozen subset Player/baseline no longer matches both receipts.' }
    $baseReceipt = $subsetBasePath
} else {
    $before = if ($scope.IsCompatibility) { Get-ProjectSubsetFingerprint $playerRoot } else { Get-PlayerFingerprint }
    if ($scope.IsCompatibility -and $before -cne $baseBinding.PlayerFingerprint) { throw 'The owned Player bytes no longer match the successful frozen base receipt.' }
    $baseReceipt = Join-Path $Root $(if ($Variant -eq 'Development') { $owner.baseReceiptStem + '.json' } else { 'base-release-receipt.json' })
}
$arguments = @(Get-HybridCLRPlayerArguments $scope $log $Action $Run $baseReceipt)
if ($Action -ceq 'LocalUpdate') {
    if ($Variant -cne 'Release' -or -not $owner.ContainsKey('nativeSourceManifest') -or -not $CandidateRun) { throw 'An explicit ordinary Release native base and producer run are required.' }
    $candidatePath=Join-Path $Root 'release-patch-receipt.json'
    Assert-NoLinks $candidatePath
    $candidate=[IO.File]::ReadAllText($candidatePath) | ConvertFrom-Json
    Assert-HybridCLRReceiptBinding $scope $candidate
    if (-not $candidate.passed -or $candidate.Development -or $candidate.ProducerRun -cne $CandidateRun -or $candidate.PlayerFingerprint -cne $before -or
        $candidate.NativeSourceManifestSha256 -cne $owner.nativeSourceManifestSha256 -or $candidate.BaselineSha256 -cne $baseBinding.NativeBaselineSha256) { throw 'Local candidate identity/producer/base differs.' }
    $arguments+=@('-hclr-candidate',$candidatePath)
}
if ($Action -in @('Update','BadPayload','BadSignature')) {
    $ready = if ($TrustReceipt) { [IO.Path]::GetFullPath($TrustReceipt) } else { Join-Path $Root $(if ($Action -eq 'Update') { 'ServerV2-ready.json' } elseif ($Action -eq 'BadSignature') { 'ServerBadSignature-ready.json' } else { 'ServerBad-ready.json' }) }
    Assert-NoLinks $ready
    if ($scope.IsCompatibility -and [IO.Path]::GetDirectoryName($ready) -ine $Root) { throw 'Trust receipt must belong to this exact evidence root.' }
    if (-not (Test-Path -LiteralPath $ready)) { throw 'Start the tracked owned HTTPS fixture first.' }
    if ($scope.IsCompatibility) {
        $custodyPath=Join-Path $Root 'TestCredentials/owner.json'
        $pfxPath=Join-Path $Root 'TestCredentials/local-only.pfx'
        foreach ($path in @($custodyPath,$pfxPath,$ready+'.owner.json')) { Assert-NoLinks $path }
        $credentials=[IO.File]::ReadAllText($custodyPath) | ConvertFrom-Json -AsHashtable
        $custody=[IO.File]::ReadAllText($ready+'.owner.json') | ConvertFrom-Json -AsHashtable
        $trust=[IO.File]::ReadAllText($ready) | ConvertFrom-Json -AsHashtable
        $pfxSha=(Get-FileHash -LiteralPath $pfxPath -Algorithm SHA256).Hash.ToLowerInvariant()
        Assert-HybridCLRFixtureCredentials $scope $credentials $credentials.CertificateSha256 $pfxSha
        if ($custody.Publication -cnotin @('ServerV2','ServerBad','ServerMissing','ServerBadSignature')) { throw 'Unknown owned publication.' }
        if ($Action -ceq 'BadSignature' -and $custody.Publication -cne 'ServerBadSignature') { throw 'A signature-only corrupted candidate is required for this negative.' }
        $envelope=Join-Path $Root ($custody.Publication+'/release.json')
        Assert-NoLinks $envelope
        Assert-HybridCLRFixtureTrust $scope $trust $custody $ready $credentials.CertificateSha256 $pfxSha ((Get-FileHash -LiteralPath $envelope -Algorithm SHA256).Hash.ToLowerInvariant())
    }
    $arguments += @('-hclr-trust',$ready)
}
if ($Action -eq 'BadMetadata') {
    $candidatePath=Join-Path $Root 'bad-metadata-fixture.json'
    Assert-NoLinks $candidatePath
    if ($scope.IsCompatibility) {
        if (-not $CandidateRun) { throw 'Select the exact successful owned metadata producer run.' }
        $proofPath=Join-Path $Root ($CandidateRun+'.receipt.json')
        Assert-NoLinks $proofPath
        $candidate=[IO.File]::ReadAllText($candidatePath) | ConvertFrom-Json
        $proof=[IO.File]::ReadAllText($proofPath) | ConvertFrom-Json
        if ($proof.ProducerRun -cne $CandidateRun) { throw 'Wrong negative metadata producer run.' }
        $candidateSha=(Get-FileHash -LiteralPath $candidatePath -Algorithm SHA256).Hash.ToLowerInvariant()
        Assert-HybridCLRMetadataFixture $scope $candidate $baseBinding $proof $candidatePath $candidateSha
    }
    $arguments += @('-hclr-candidate',$candidatePath)
}
$start = [Diagnostics.ProcessStartInfo]::new($player)
$start.UseShellExecute = $false
$start.WorkingDirectory = $Root
foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($start)
[IO.File]::WriteAllText($log + '.process.json', (@{ pid=$process.Id; action=$Action; playerSha256=(Get-FileHash -LiteralPath $player -Algorithm SHA256).Hash; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
if (-not $process.WaitForExit(600000)) { $process.Kill($true); throw 'Owned Player exceeded its bounded acceptance execution time.' }
$code = $process.ExitCode
$process.Dispose()
$after = if ($Action -eq 'ProjectSubsets' -or $scope.IsCompatibility) { Get-ProjectSubsetFingerprint $playerRoot } else { Get-PlayerFingerprint }
[IO.File]::WriteAllText($log + '.exit.json', (@{ exitCode=$code; before=$before; after=$after; unchanged=($before -eq $after); utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
if ($code -ne 0) { throw "Actual Player failed with exit $code; preserve $log and $receipt" }
$result = [IO.File]::ReadAllText($receipt) | ConvertFrom-Json
if (-not $result.passed -or $result.simulation -or $before -ne $after) { throw 'The native result, Simulation flag, or unchanged-Player check failed.' }
if ($scope.IsCompatibility -and -not (Test-HybridCLRPlayerGraphics $result $Action ([IO.File]::ReadAllText($log)))) {
    throw 'A real graphics device is required for this new Player evidence.'
}
if ($Action -eq 'ProjectSubsets') {
    if ((Get-ProjectSubsetFingerprint $subsetBase.Baseline) -cne $subsetBaselineBefore -or
        -not $result.sharedSupportAssembly -or -not $result.barrierAReleased -or -not $result.barrierBReleased -or -not $result.stopped -or
        @($result.loads).Count -ne 2 -or @($result.loads | Where-Object { $_.mode -cne 'Native' -or $_.simulation }).Count) {
        throw 'The real two-project native load, shared Assembly, cleanup or unchanged-baseline check failed.'
    }
}
"Native $Action passed; executable and all Player files unchanged; receipt=$receipt"
