#requires -Version 7.6
[CmdletBinding()]
param([Parameter(Mandatory)][string] $RunDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$RunDirectory = [IO.Path]::GetFullPath($RunDirectory)
if (-not $RunDirectory.StartsWith('C:\Users\Kano\AppData\Local\Temp\kilo\BA26\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $RunDirectory)) { throw 'Use an existing dedicated A evidence directory.' }
$modules = @('Overlap', 'CharacterController2D', 'MoveablePlatform', 'StateMachine')
function Test-Owned([string] $Path) {
    foreach ($module in $modules) {
        foreach ($side in @('Runtime', 'Editor')) {
            $prefix = "Assets/YLFramework/$side/Systems/$module"
            if ($Path.StartsWith($prefix + '/', [StringComparison]::Ordinal) -or $Path -eq ($prefix + '.meta')) { return $true }
        }
    }
    return $Path -match '^Assets/YLFramework/Tests/(EditMode|PlayMode|Fixtures)/Extensions/Behavior(?:/|\.meta$)'
}
$all = [Collections.Generic.Dictionary[string, Collections.Generic.List[string]]]::new([StringComparer]::OrdinalIgnoreCase)
$owned = @()
foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets') -Filter '*.meta' -File -Recurse)) {
    $path = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
    $text = [IO.File]::ReadAllText($file.FullName)
    $match = [regex]::Match($text, '(?m)^guid:\s*([0-9a-f]{32})\s*$')
    if (-not $match.Success) { if (Test-Owned $path) { throw "Invalid owned GUID: $path" }; continue }
    $guid = $match.Groups[1].Value
    if (-not $all.ContainsKey($guid)) { $all[$guid] = [Collections.Generic.List[string]]::new() }
    $all[$guid].Add($path)
    if (Test-Owned $path) { $owned += [ordered]@{ path = $path; guid = $guid; sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); assetExists = (Test-Path -LiteralPath $file.FullName.Substring(0, $file.FullName.Length - 5)) } }
}
$expectedPath = Join-Path $RunDirectory 'expected-meta-146.txt'
if (-not (Test-Path -LiteralPath $expectedPath)) { throw 'Missing frozen pre-import expectation; no completeness claim is permitted.' }
$expected = [IO.File]::ReadAllLines($expectedPath)
$missing = @($expected | Where-Object { -not (Test-Path -LiteralPath (Join-Path $root $_)) })
$extras = @($owned | Where-Object { $_.path -notin $expected } | ForEach-Object path)
$duplicates = @($owned | Where-Object { $all[$_.guid].Count -ne 1 } | ForEach-Object { [ordered]@{ guid = $_.guid; paths = $all[$_.guid].ToArray() } })
$orphan = @($owned | Where-Object { -not $_.assetExists } | ForEach-Object path)
$baseline = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'LightweightFSM.baseline.json')) | ConvertFrom-Json
$lightweightChanged = @($baseline.files | Where-Object { (Get-FileHash -LiteralPath (Join-Path $root $_.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $_.sha256 } | ForEach-Object path)
$record = [ordered]@{ utc = [DateTime]::UtcNow.ToString('o'); project = $root; expectedCount = $expected.Count; ownedMetaCount = $owned.Count; rawGuidCount = $all.Count; missing = $missing; additional = $extras; duplicateOwnedGuids = $duplicates; orphanOwnedMetas = $orphan; lightweightChanged = $lightweightChanged; owned = $owned }
[IO.File]::WriteAllText((Join-Path $RunDirectory 'meta-audit.json'), ($record | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
"Raw meta audit: owned=$($owned.Count), expected=$($expected.Count), additional=$($extras.Count), missing=$($missing.Count), duplicates=$($duplicates.Count), orphan=$($orphan.Count), lightweight changes=$($lightweightChanged.Count)."
if ($missing.Count -or $duplicates.Count -or $orphan.Count -or $lightweightChanged.Count) { throw 'A meta/GUID audit failed. Preserve the report.' }
