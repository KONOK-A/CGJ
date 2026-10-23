#requires -Version 7.6
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1'
if (-not (Test-Path -LiteralPath $validator)) { throw 'The receiver ownership scope validator is missing.' }
$parent = Join-Path $env:LOCALAPPDATA 'Temp/kilo'
if (-not (Test-Path -LiteralPath $parent)) { throw 'Missing approved fixture parent.' }
$fixture = Join-Path $parent ('ReceptionScope-' + [Guid]::NewGuid().ToString('N'))
$root = Join-Path $fixture 'YS88'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$nativeParent = [IO.DirectoryInfo]::new($repository).Parent.Parent.FullName
$owner = @{
    purpose = 'YLFramework-HybridCLR-9.0.3-independent-reception'
    project = Join-Path $root 'R'
    repository = $repository
    work = 'yl-code-style-naming-cleanup-20261022'
    nativePrefix = 'YS88'
    playerRoot = Join-Path $nativeParent 'YS88D4'
    releasePlayerRoot = Join-Path $nativeParent 'YS88R'
    runtimeCacheRoot = Join-Path $nativeParent 'YS88C'
    subsetPlayerRoot = Join-Path $nativeParent 'YS88S'
    subsetCacheRoot = Join-Path $nativeParent 'YS88CS'
    baseline = Join-Path $root 'Baselines/Native'
    subsetBaseline = Join-Path $root 'Baselines/ProjectSubsets'
    compatibilityId = 'yl-style-native-20261022-ys88-v1'
    releaseCompatibilityId = 'yl-style-native-20261022-ys88-release-v1'
    subsetCompatibilityId = 'yl-style-project-subsets-20261022-ys88-v1'
}
function Write-Owner { [IO.File]::WriteAllText((Join-Path $root 'owner.json'), ($owner | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false)) }
function Reject-Owner([string]$Name) {
    $rejected = $false
    try { & $validator -Root $root | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "Unsafe receiver ownership was accepted: $Name" }
}
try {
    [IO.Directory]::CreateDirectory($root) | Out-Null
    Write-Owner
    $scope = & $validator -Root $root
    if (-not $scope.IsStyle -or $scope.PersistentProject -ne (Join-Path $nativeParent 'YS88') -or $scope.SubsetCache -ne $owner.subsetCacheRoot) { throw 'The exact new scope was not derived.' }
    foreach ($case in @(
        @('nativePrefix', 'H26'),
        @('nativePrefix', '../H26'),
        @('nativePrefix', 'YS87'),
        @('project', (Join-Path $nativeParent 'H26')),
        @('playerRoot', (Join-Path $nativeParent 'H26D4')),
        @('runtimeCacheRoot', (Join-Path $nativeParent 'HC26')),
        @('subsetPlayerRoot', (Join-Path $nativeParent 'H26S')),
        @('subsetBaseline', (Join-Path $repository 'Build/HybridCLR/Reception-20261022/ProjectSubsets')),
        @('compatibilityId', 'hybridclr-base-development'),
        @('subsetCompatibilityId', 'hybridclr-project-subsets-v1'),
        @('work', 'another-owner'),
        @('repository', $parent)
    )) {
        $previous = $owner[$case[0]]
        $owner[$case[0]] = $case[1]
        Write-Owner
        Reject-Owner ($case -join '=')
        $owner[$case[0]] = $previous
    }
    $prefix = $owner.nativePrefix
    $owner.Remove('nativePrefix')
    Write-Owner
    Reject-Owner 'style work without an explicit isolated prefix'
    $owner.Remove('work')
    Write-Owner
    $legacy = & $validator -Root $root
    if ($legacy.IsStyle -or $legacy.PersistentProject -ne (Join-Path $nativeParent 'H26')) { throw 'The existing legacy descriptor contract changed.' }
    $owner.nativePrefix = $prefix
    'Receiver scope: new exact owner accepted; 13 unsafe/adopted scopes rejected; legacy descriptor preserved; no Unity or native output started.'
} finally {
    if ([IO.Directory]::Exists($fixture)) { [IO.Directory]::Delete($fixture, $true) }
}
