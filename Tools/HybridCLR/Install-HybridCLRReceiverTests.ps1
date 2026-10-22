#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021')
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$owner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json
if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-independent-reception') { throw 'Unexpected receiver owner.' }
$project = [IO.Path]::GetFullPath($owner.project)
foreach ($name in @('base-no-package-import','optional-no-package-import')) {
    $receipt = [IO.File]::ReadAllText((Join-Path $Root ($name + '.receipt.json'))) | ConvertFrom-Json
    if (-not $receipt.passed -or $receipt.testsPresent) { throw 'Product-only proof must precede real test facilities.' }
}
$active = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($project) })
if ($active.Count) { throw 'Do not alter a receiver while its Editor is running.' }
$source = Join-Path $owner.repository 'Assets/YLFramework/Tests/Integrations/HybridCLR'
$destination = Join-Path $project 'Assets/YLFramework/Tests/Integrations/HybridCLR'
if ([IO.Path]::Exists($destination)) { throw 'Never adopt or overwrite another test fixture tree.' }
$parent = Join-Path $project 'Assets/YLFramework'
if (-not (Test-Path -LiteralPath $parent)) { throw 'Import the real default framework first.' }
[IO.Directory]::CreateDirectory((Join-Path $project 'Assets/YLFramework/Tests/Fixtures')) | Out-Null
$pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($source)
$count = 0
while ($pending.Count) {
    $directory = $pending.Pop()
    if ([IO.File]::GetAttributes($directory) -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked test input refused.' }
    $relative = $directory.Substring($source.Length).TrimStart([char]92,[char]47)
    $target = if ($relative) { Join-Path $destination $relative } else { $destination }
    [IO.Directory]::CreateDirectory($target) | Out-Null
    foreach ($child in [IO.Directory]::GetDirectories($directory)) { $pending.Push($child) }
    foreach ($file in [IO.Directory]::GetFiles($directory)) {
        if ([IO.File]::GetAttributes($file) -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked test file refused.' }
        [IO.File]::Copy($file,(Join-Path $target ([IO.Path]::GetFileName($file))),$false); $count++
    }
}
$manifestPath = Join-Path $project 'Packages/manifest.json'
$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -AsHashtable
$manifest.dependencies['com.unity.test-framework'] = '1.1.33'
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 15), [Text.UTF8Encoding]::new($false))
"Installed $count real test files after product-only import; Unity must resolve exact Test Framework 1.1.33. No dependency stub was created."
