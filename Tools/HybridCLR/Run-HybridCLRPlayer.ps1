#requires -Version 7.6
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('V1','Update','V2','BadPayload','BadMetadata','ProjectSubsets')][string]$Action,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run,
    [ValidateSet('Development','Release')][string]$Variant = 'Development',
    [string]$TrustReceipt,
    [string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021'
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$scope = & (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root
$Root = $scope.Root
$owner = $scope.Owner
if ($Action -eq 'ProjectSubsets') {
    if ($Variant -ne 'Development' -or $TrustReceipt -or $Run -in @('project-subsets-base','project-subsets-patch')) { throw 'ProjectSubsets requires its own new Development/local-snapshot run.' }
    $subsetBasePath = Join-Path $Root 'project-subsets-base.json'
    $subsetBase = [IO.File]::ReadAllText($subsetBasePath) | ConvertFrom-Json
    $subsetPatch = [IO.File]::ReadAllText((Join-Path $Root 'project-subsets-patch.json')) | ConvertFrom-Json
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
}
$player = Join-Path $playerRoot 'HybridCLR.exe'
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
            $relative + ':' + (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
        })
        return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($rows -join "`n")))).ToLowerInvariant()
    } finally { $sha.Dispose() }
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
    $before = Get-PlayerFingerprint
    $baseReceipt = Join-Path $Root $(if ($Variant -eq 'Development') { $owner.baseReceiptStem + '.json' } else { 'base-release-receipt.json' })
}
$arguments = @('-batchmode','-nographics','-logFile',$log,'-hclr-root',$Root,'-hclr-action',$Action,'-hclr-run',$Run,'-hclr-base-receipt',$baseReceipt)
if ($Action -in @('Update','BadPayload')) {
    $ready = if ($TrustReceipt) { [IO.Path]::GetFullPath($TrustReceipt) } else { Join-Path $Root $(if ($Action -eq 'Update') { 'ServerV2-ready.json' } else { 'ServerBad-ready.json' }) }
    if (-not (Test-Path -LiteralPath $ready)) { throw 'Start the tracked owned HTTPS fixture first.' }
    $arguments += @('-hclr-trust',$ready)
}
if ($Action -eq 'BadMetadata') { $arguments += @('-hclr-candidate',(Join-Path $Root 'bad-metadata-fixture.json')) }
$start = [Diagnostics.ProcessStartInfo]::new($player)
$start.UseShellExecute = $false
$start.WorkingDirectory = $Root
foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($start)
[IO.File]::WriteAllText($log + '.process.json', (@{ pid=$process.Id; action=$Action; playerSha256=(Get-FileHash -LiteralPath $player -Algorithm SHA256).Hash; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
if (-not $process.WaitForExit(600000)) { $process.Kill($true); throw 'Owned Player exceeded its bounded acceptance execution time.' }
$code = $process.ExitCode
$process.Dispose()
$after = if ($Action -eq 'ProjectSubsets') { Get-ProjectSubsetFingerprint $playerRoot } else { Get-PlayerFingerprint }
[IO.File]::WriteAllText($log + '.exit.json', (@{ exitCode=$code; before=$before; after=$after; unchanged=($before -eq $after); utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
if ($code -ne 0) { throw "Actual Player failed with exit $code; preserve $log and $receipt" }
$result = [IO.File]::ReadAllText($receipt) | ConvertFrom-Json
if (-not $result.passed -or $result.simulation -or $before -ne $after) { throw 'The native result, Simulation flag, or unchanged-Player check failed.' }
if ($Action -eq 'ProjectSubsets') {
    if ((Get-ProjectSubsetFingerprint $subsetBase.Baseline) -cne $subsetBaselineBefore -or
        -not $result.sharedSupportAssembly -or -not $result.barrierAReleased -or -not $result.barrierBReleased -or -not $result.stopped -or
        @($result.loads).Count -ne 2 -or @($result.loads | Where-Object { $_.mode -cne 'Native' -or $_.simulation }).Count) {
        throw 'The real two-project native load, shared Assembly, cleanup or unchanged-baseline check failed.'
    }
}
"Native $Action passed; executable and all Player files unchanged; receipt=$receipt"
