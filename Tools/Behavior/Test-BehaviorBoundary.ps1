[CmdletBinding()]
param([string] $Evidence = 'current')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if ($Evidence -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid evidence name.' }
if (-not (Test-Path -LiteralPath $root)) { throw 'Missing output parent.' }
$output = Join-Path $root "Temp\Behavior.Offline\$Evidence"
$null = New-Item -ItemType Directory -Path $output -Force
$modules = @('Overlap', 'CharacterController2D', 'MoveablePlatform', 'StateMachine')
$files = @()
$directories = @()
foreach ($side in @('Runtime\Systems', 'Editor\Systems', 'Tests\EditMode\Extensions\Behavior', 'Tests\PlayMode\Extensions\Behavior', 'Tests\Fixtures\Extensions\Behavior')) {
    $parent = Join-Path $root "Assets\YLFramework\$side"
    $targets = if ($side.StartsWith('Tests')) { @($parent) } else { @($modules | ForEach-Object { Join-Path $parent $_ }) }
    foreach ($target in $targets) {
        if (-not (Test-Path -LiteralPath $target)) { continue }
        $directories += $target
        $ancestor = [IO.Path]::GetDirectoryName($target)
        while ($ancestor.StartsWith((Join-Path $root 'Assets\YLFramework'), [StringComparison]::OrdinalIgnoreCase) -and -not (Test-Path -LiteralPath ($ancestor + '.meta'))) {
            $directories += $ancestor
            $ancestor = [IO.Path]::GetDirectoryName($ancestor)
        }
        $directories += @(Get-ChildItem -LiteralPath $target -Directory -Recurse | ForEach-Object FullName)
        $files += @(Get-ChildItem -LiteralPath $target -File -Recurse | Where-Object { $_.Extension -ne '.meta' -and -not $_.Name.StartsWith('.') } | ForEach-Object FullName)
    }
}
$metaPending = @(@($files + $directories | Sort-Object -Unique) | Where-Object { -not (Test-Path -LiteralPath ($_ + '.meta')) } | ForEach-Object { $_.Substring($root.Length + 1).Replace('\', '/') + '.meta' })
[IO.File]::WriteAllLines((Join-Path $output 'pending-meta.txt'), [string[]]$metaPending, [Text.UTF8Encoding]::new($false))
$sources = @($files | Where-Object { $_.EndsWith('.cs') })
foreach ($file in $sources) {
    $text = [IO.File]::ReadAllText($file)
    if ($file.Contains('\Runtime\') -and $text -match '(?m)^\s*using (UnityEditor|XFGameFramework|XFFSM|XFABManager)\b') { throw "Foreign Runtime dependency: $file" }
}
$identities = @()
$originalGuids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($module in $modules) {
    $manifest = Join-Path $PSScriptRoot "$module.source.json"
    if (Test-Path -LiteralPath $manifest) {
        $record = [IO.File]::ReadAllText($manifest) | ConvertFrom-Json
        foreach ($file in $record.files) {
            $path = Join-Path $record.sourceRoot $file.path
            if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) { throw "Source identity changed: $path" }
            if (Test-Path -LiteralPath ($path + '.meta')) {
                $match = [regex]::Match([IO.File]::ReadAllText($path + '.meta'), '(?m)^guid:\s*([0-9a-f]{32})')
                if ($match.Success) { $null = $originalGuids.Add($match.Groups[1].Value) }
            }
        }
        $identities += $record.package
    }
}
foreach ($file in $files) {
    foreach ($match in [regex]::Matches([IO.File]::ReadAllText($file), '\b[0-9a-fA-F]{32}\b')) {
        if ($originalGuids.Contains($match.Value)) { throw "Copied original package GUID in $file" }
    }
}
$protected = @('Assets/YLFramework/Runtime/Foundation', 'Assets/YLFramework/Runtime/Core', 'Assets/YLFramework/Runtime/Systems/Resource', 'Assets/YLFramework/Runtime/Utilities/FSM', 'Assets/YLFramework/Editor/Systems/Resource', 'Assets/YLFramework/Runtime/YLFramework.Runtime.asmdef', 'Assets/YLFramework/Editor/YLFramework.Editor.asmdef', 'Assets/YLFramework/Tests/EditMode/YLFramework.Tests.EditMode.asmdef', 'Assets/YLFramework/Tests/PlayMode/YLFramework.Tests.PlayMode.asmdef', 'Tools/Verify-UIPanel.ps1', 'Tools/UIPanelOfflineRunner.cs', 'Packages', 'ProjectSettings', 'Assets/Games', 'Assets/Scenes', 'Assets/StreamingAssets', '.kilo/plans/1792420261057-xf-ecosystem-integration.md')
$changes = @(& git -C $root diff --name-only c88500aef6651f5b13ac933dd1f209ecb314e92d -- @protected)
if ($LASTEXITCODE -ne 0 -or $changes.Count -ne 0) { throw "Protected changes: $changes" }
$changed = @(& git -C $root diff --name-only c88500aef6651f5b13ac933dd1f209ecb314e92d) + @(& git -C $root ls-files --others --exclude-standard)
foreach ($change in $changed) {
    $allowed = $change -eq '.kilo/handovers/xf-ext-behavior.md' -or $change.StartsWith('Tools/Behavior/') -or $change.StartsWith('Assets/YLFramework/Tests/EditMode/Extensions/Behavior/') -or $change.StartsWith('Assets/YLFramework/Tests/PlayMode/Extensions/Behavior/') -or $change.StartsWith('Assets/YLFramework/Tests/Fixtures/Extensions/Behavior/')
    foreach ($module in $modules) {
        foreach ($side in @('Runtime', 'Editor')) {
            $prefix = "Assets/YLFramework/$side/Systems/$module"
            $allowed = $allowed -or $change.StartsWith($prefix + '/') -or $change -eq ($prefix + '.meta')
        }
    }
    if (-not $allowed) { throw "Change outside the authorized Behavior group: $change" }
}
$baseline = Join-Path $PSScriptRoot 'LightweightFSM.baseline.json'
if (Test-Path -LiteralPath $baseline) {
    foreach ($file in ([IO.File]::ReadAllText($baseline) | ConvertFrom-Json).files) {
        if ((Get-FileHash -LiteralPath (Join-Path $root $file.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) { throw "Lightweight FSM identity changed: $($file.path)" }
    }
}
$hashes = @($files | Sort-Object -Unique | ForEach-Object { [ordered]@{ path = $_.Substring($root.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() } })
[IO.File]::WriteAllText((Join-Path $output 'source-boundary.json'), ([ordered]@{ sourceFiles = $sources.Count; inputs = $identities; protectedDiff = $changes; metaPending = $metaPending; files = $hashes; unityExecuted = $false } | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
$referenceRecords = @()
$seenReferences = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($response in @(Get-ChildItem -LiteralPath $output -Filter '*.rsp')) {
    foreach ($line in [IO.File]::ReadAllLines($response.FullName)) {
        if ($line -notmatch '^"/reference:(.+)"$') { continue }
        $reference = $Matches[1]
        if (-not $seenReferences.Add($reference) -or -not (Test-Path -LiteralPath $reference)) { continue }
        $referenceRecords += [ordered]@{ path = $reference; sha256 = (Get-FileHash -LiteralPath $reference -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
}
[IO.File]::WriteAllText((Join-Path $output 'reference-inputs.json'), ($referenceRecords | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
"Boundary: $($sources.Count) C# inputs; $($identities.Count) upstream identities verified; protected diff empty; $($metaPending.Count) meta pending main Unity."
