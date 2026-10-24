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
    $root = Join-Path $fixture 'S88'
    [IO.Directory]::CreateDirectory($root) | Out-Null
    $owner = @{
        purpose = 'YLFramework-HybridCLR-9.0.3-independent-reception'
        project = Join-Path $root 'R'
        repository = $repository
        work = 'yl-structure-and-namespaces-20261023'
        nativePrefix = 'S88'
        playerRoot = Join-Path $nativeParent 'S88D4'
        releasePlayerRoot = Join-Path $nativeParent 'S88R'
        runtimeCacheRoot = Join-Path $nativeParent 'S88C'
        subsetPlayerRoot = Join-Path $nativeParent 'S88S'
        subsetCacheRoot = Join-Path $nativeParent 'S88CS'
        baseline = Join-Path $nativeParent 'S88B'
        subsetBaseline = Join-Path $nativeParent 'S88BS'
        compatibilityId = 'yl-structure-native-20261023-s88-v1'
        releaseCompatibilityId = 'yl-structure-native-20261023-s88-release-v1'
        subsetCompatibilityId = 'yl-structure-project-subsets-20261023-s88-v1'
    }
    Write-Owner
    $scope = & $validator -Root $root
    if (-not $scope.IsStructure -or $scope.IsStyle -or $scope.PersistentProject -ne (Join-Path $nativeParent 'S88') -or
        $scope.SubsetBaseline -ne $owner.subsetBaseline) { throw 'The exact structure scope was not derived.' }
    $owner.project = $scope.PersistentProject
    Write-Owner
    if ((& $validator -Root $root).Project -ne $owner.project) { throw 'The new owned physical short receiver must be accepted.' }
    foreach ($case in @(
        @('nativePrefix', 'H26'),
        @('nativePrefix', '../S88'),
        @('nativePrefix', 'S87'),
        @('nativePrefix', 'YS88'),
        @('project', (Join-Path $nativeParent 'H26')),
        @('playerRoot', (Join-Path $nativeParent 'H26D4')),
        @('runtimeCacheRoot', (Join-Path $nativeParent 'HC26')),
        @('baseline', (Join-Path $root 'Baselines/Native')),
        @('subsetBaseline', (Join-Path $repository 'Build/HybridCLR/Reception-20261022/ProjectSubsets')),
        @('compatibilityId', 'hybridclr-base-development'),
        @('releaseCompatibilityId', 'hybridclr-base-release'),
        @('subsetCompatibilityId', 'hybridclr-project-subsets-v1'),
        @('work', 'yl-code-style-naming-cleanup-20261022'),
        @('repository', $parent)
    )) {
        $previous = $owner[$case[0]]
        $owner[$case[0]] = $case[1]
        Write-Owner
        Reject-Owner ('structure ' + ($case -join '='))
        $owner[$case[0]] = $previous
    }
    $owner.Remove('nativePrefix')
    Write-Owner
    Reject-Owner 'structure work without an explicit isolated prefix'
    $owner.nativePrefix = 'S88'
    $owner.project = Join-Path $root 'R'
    $owner.repository = Join-Path $fixture 'Other/Projects/Repository'
    [IO.Directory]::CreateDirectory($owner.repository) | Out-Null
    $otherParent = [IO.DirectoryInfo]::new($owner.repository).Parent.Parent.FullName
    foreach ($pair in @(@('playerRoot','S88D4'),@('releasePlayerRoot','S88R'),@('runtimeCacheRoot','S88C'),@('subsetPlayerRoot','S88S'),@('subsetCacheRoot','S88CS'),@('baseline','S88B'),@('subsetBaseline','S88BS'))) {
        $owner[$pair[0]] = Join-Path $otherParent $pair[1]
    }
    Write-Owner
    Reject-Owner 'Root/R with a different existing repository and self-consistent derived paths'
    'Receiver scope: style and structure exact owners accepted; original 28 unsafe/adopted scopes plus a self-consistent foreign repository rejected; legacy descriptor preserved; no Unity or native output started.'
} finally {
    if ([IO.Directory]::Exists($fixture)) { [IO.Directory]::Delete($fixture, $true) }
}
