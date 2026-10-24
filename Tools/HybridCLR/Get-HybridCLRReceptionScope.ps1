#requires -Version 7.6
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Root)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]'\/')
$approved = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Temp/kilo')) + [IO.Path]::DirectorySeparatorChar
$durable = $Root -eq [IO.Path]::GetFullPath('G:/Unity/YLStyleAcceptance/YS28')
if (-not $Root.StartsWith($approved, [StringComparison]::OrdinalIgnoreCase) -and -not $durable) { throw 'Expected an explicitly approved independent receiver root.' }
function Assert-NoLinks([string]$Path) {
    for ($cursor = [IO.Path]::GetFullPath($Path); $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        try { $attributes = [IO.File]::GetAttributes($cursor) }
        catch [IO.FileNotFoundException] { continue }
        catch [IO.DirectoryNotFoundException] { continue }
        if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked receiver paths are not supported: $cursor" }
    }
}
Assert-NoLinks $PSCommandPath
$ownerPath = Join-Path $Root 'owner.json'
Assert-NoLinks $ownerPath
$ownerJson = [IO.File]::ReadAllText($ownerPath)
$owner = $ownerJson | ConvertFrom-Json -AsHashtable
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Assert-NoLinks $repository
if ($owner['purpose'] -cne 'YLFramework-HybridCLR-9.0.3-independent-reception' -or
    -not [IO.Path]::IsPathFullyQualified([string]$owner['repository']) -or
    ([string]$owner['repository']).Split([char[]]'\/') -contains '.' -or
    ([string]$owner['repository']).Split([char[]]'\/') -contains '..' -or
    [IO.Path]::GetFullPath([string]$owner['repository']) -ne $repository) { throw 'Receiver purpose or source repository mismatch.' }
$parent = [IO.DirectoryInfo]::new($repository).Parent.Parent.FullName
if (-not [IO.Path]::IsPathFullyQualified([string]$owner['project']) -or
    ([string]$owner['project']).Split([char[]]'\/') -contains '.' -or
    ([string]$owner['project']).Split([char[]]'\/') -contains '..') { throw 'Expected an explicit receiver project path without dot segments.' }
$project = [IO.Path]::GetFullPath([string]$owner['project'])
$isolated = $owner.ContainsKey('nativePrefix')
$structure = $owner['work'] -ceq 'yl-structure-and-namespaces-20261023'
if ($durable -and -not $isolated) { throw 'The durable receiver requires an exact isolated owner.' }
$persistent = Join-Path $parent 'H26'
$cache = Join-Path $parent 'HC26'
$subsetPlayer = Join-Path $parent 'H26S'
$subsetCache = Join-Path $parent 'HC26S'
$subsetBaseline = Join-Path $repository 'Build/HybridCLR/Reception-20261022/ProjectSubsets'
$subsetCompatibility = 'hybridclr-project-subsets-v1'
$allowed = @((Join-Path $Root 'R'), (Join-Path $repository 'Temp/H26'), $persistent)
if ($isolated) {
    $prefix = [string]$owner['nativePrefix']
    $pattern = if ($structure) { '^S[0-9]{2}$' } else { '^YS[0-9]{2}$' }
    $identity = if ($structure) { 'yl-structure-native-20261023-' } else { 'yl-style-native-20261022-' }
    if ($prefix -cnotmatch $pattern -or [IO.Path]::GetFileName($Root) -cne $prefix -or
        (-not $structure -and $owner['work'] -cne 'yl-code-style-naming-cleanup-20261022')) { throw 'The isolated owner prefix does not match this exact root and work.' }
    $persistent = Join-Path $parent $prefix
    $cache = Join-Path $parent ($prefix + 'C')
    $subsetPlayer = Join-Path $parent ($prefix + 'S')
    $subsetCache = Join-Path $parent ($prefix + 'CS')
    $subsetBaseline = if ($structure) { Join-Path $parent ($prefix + 'BS') } else { Join-Path $Root 'Baselines/ProjectSubsets' }
    $subsetCompatibility = $(if ($structure) { 'yl-structure-project-subsets-20261023-' } else { 'yl-style-project-subsets-20261022-' }) + $prefix.ToLowerInvariant() + '-v1'
    $paths = @{
        playerRoot = Join-Path $parent ($prefix + 'D4')
        releasePlayerRoot = Join-Path $parent ($prefix + 'R')
        runtimeCacheRoot = $cache
        subsetPlayerRoot = $subsetPlayer
        subsetCacheRoot = $subsetCache
        baseline = $(if ($structure) { Join-Path $parent ($prefix + 'B') } else { Join-Path $Root 'Baselines/Native' })
        subsetBaseline = $subsetBaseline
    }
    foreach ($name in $paths.Keys) {
        $path = [string]$owner[$name]
        if (-not [IO.Path]::IsPathFullyQualified($path) -or $path.Split([char[]]'\/') -contains '..' -or
            $path.Split([char[]]'\/') -contains '.' -or [IO.Path]::GetFullPath($path).TrimEnd([char[]]'\/') -ne $paths[$name]) { throw "Unowned isolated receiver path: $name" }
        Assert-NoLinks $path
    }
    if ($owner['compatibilityId'] -cne ($identity + $prefix.ToLowerInvariant() + '-v1') -or
        $owner['releaseCompatibilityId'] -cne ($identity + $prefix.ToLowerInvariant() + '-release-v1') -or
        $owner['subsetCompatibilityId'] -cne $subsetCompatibility) { throw 'The new receiver must use its own exact compatibility identities.' }
    $allowed = @((Join-Path $Root 'R'), $persistent)
} elseif ($structure -or $owner['work'] -ceq 'yl-code-style-naming-cleanup-20261022') {
    throw 'This acceptance cannot fall back to historical native outputs; an explicit isolated prefix is required.'
}
if ($project -notin $allowed) { throw 'Unexpected receiver project path.' }
Assert-NoLinks $project
[pscustomobject]@{
    Root = $Root
    Owner = $owner
    OwnerJson = $ownerJson
    Repository = $repository
    Project = $project
    IsStyle = $isolated -and -not $structure
    IsStructure = $structure
    PersistentProject = $persistent
    CacheRoot = $cache
    SubsetPlayer = $subsetPlayer
    SubsetCache = $subsetCache
    SubsetBaseline = $subsetBaseline
    SubsetCompatibility = $subsetCompatibility
}
