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
foreach ($module in $modules) {
    $manifest = Join-Path $PSScriptRoot "$module.source.json"
    if (Test-Path -LiteralPath $manifest) {
        $record = [IO.File]::ReadAllText($manifest) | ConvertFrom-Json
        foreach ($file in $record.files) {
            $path = Join-Path $record.sourceRoot $file.path
            if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) { throw "Source identity changed: $path" }
        }
        $identities += $record.package
    }
}
$protected = @('Assets/YLFramework/Runtime/Foundation', 'Assets/YLFramework/Runtime/Core', 'Assets/YLFramework/Runtime/Systems/Resource', 'Assets/YLFramework/Runtime/Utilities/FSM', 'Assets/YLFramework/Editor/Systems/Resource', 'Packages', 'ProjectSettings', 'Assets/Games', 'Assets/Scenes', 'Assets/StreamingAssets', '.kilo/plans/1792420261057-xf-ecosystem-integration.md')
$changes = @(& git -C $root diff --name-only c88500aef6651f5b13ac933dd1f209ecb314e92d -- @protected)
if ($LASTEXITCODE -ne 0 -or $changes.Count -ne 0) { throw "Protected changes: $changes" }
$hashes = @($files | Sort-Object -Unique | ForEach-Object { [ordered]@{ path = $_.Substring($root.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() } })
[IO.File]::WriteAllText((Join-Path $output 'source-boundary.json'), ([ordered]@{ sourceFiles = $sources.Count; inputs = $identities; protectedDiff = $changes; metaPending = $metaPending; files = $hashes; unityExecuted = $false } | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
"Boundary: $($sources.Count) C# inputs; $($identities.Count) upstream identities verified; protected diff empty; $($metaPending.Count) meta pending main Unity."
