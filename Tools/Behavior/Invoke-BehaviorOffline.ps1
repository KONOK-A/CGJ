[CmdletBinding()]
param(
    [ValidateSet('Pure', 'Legacy', 'Compile', 'Static', 'All')][string] $Mode = 'All',
    [string] $UnityData = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data',
    [string] $Evidence = 'current'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets\YLFramework\Runtime'))) { throw 'Not a YLFramework worktree.' }
if ($Evidence -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Evidence must be a simple directory name.' }
if (-not (Test-Path -LiteralPath $root)) { throw 'Missing output parent.' }
$output = Join-Path $root "Temp\Behavior.Offline\$Evidence"
$null = New-Item -ItemType Directory -Path $output -Force
$env:YL_BEHAVIOR_ROOT = $root
if ($Mode -in @('Compile', 'All')) {
    & (Join-Path $root 'Tools\Verify-UIPanel.ps1') -Mode Runtime -UnityData $UnityData
    & (Join-Path $root 'Tools\Verify-UIPanel.ps1') -Mode EditorTools -UnityData $UnityData
    $compiler = 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore\csc.dll'
    $baseResponse = Join-Path $root 'Temp\UI.Panel.Offline\EditorTools\compile.rsp'
    $common = @([IO.File]::ReadAllLines($baseResponse) | Where-Object { $_ -match '^"/(reference|nologo|nostdlib|langversion|unsafe)' })
    $acceptancePath = Join-Path $root 'Assets\YLFramework\Tests\Fixtures\Extensions\Behavior'
    if (Test-Path -LiteralPath (Join-Path $acceptancePath 'Player')) {
        $runtimeResponse = Join-Path $root 'Temp\UI.Panel.Offline\Runtime\compile.rsp'
        $playerArguments = @([IO.File]::ReadAllLines($runtimeResponse) | Where-Object { $_ -match '^"/(reference|nologo|nostdlib|langversion|unsafe)' -and $_ -notmatch 'UnityEditor' })
        $playerArguments += '"/reference:' + (Join-Path $root 'Temp\UI.Panel.Offline\Runtime\YLFramework.Runtime.dll') + '"'
        $playerArguments += @('"/target:library"', ('"/out:' + (Join-Path $output 'YLFramework.Behavior.Acceptance.dll') + '"'))
        $playerArguments += @(Get-ChildItem -LiteralPath (Join-Path $acceptancePath 'Player') -Filter '*.cs' -Recurse | ForEach-Object { '"' + $_.FullName + '"' })
        $response = Join-Path $output 'acceptance-runtime.rsp'
        [IO.File]::WriteAllLines($response, [string[]]$playerArguments, [Text.UTF8Encoding]::new($false))
        & dotnet $compiler /noconfig "@$response"
        if ($LASTEXITCODE -ne 0) { throw 'Prepared Player consumer source compilation failed.' }
        $editorArguments = $common + @('"/target:library"', '"/define:UNITY_EDITOR"', ('"/reference:' + (Join-Path $output 'YLFramework.Behavior.Acceptance.dll') + '"'), ('"/out:' + (Join-Path $output 'YLFramework.Behavior.Acceptance.Editor.dll') + '"'))
        $editorArguments += @(Get-ChildItem -LiteralPath (Join-Path $acceptancePath 'Editor') -Filter '*.cs' -Recurse | ForEach-Object { '"' + $_.FullName + '"' })
        $response = Join-Path $output 'acceptance-editor.rsp'
        [IO.File]::WriteAllLines($response, [string[]]$editorArguments, [Text.UTF8Encoding]::new($false))
        & dotnet $compiler /noconfig "@$response"
        if ($LASTEXITCODE -ne 0) { throw 'Prepared acceptance builder source compilation failed.' }
        'Prepared standalone consumer and owned-scene builder compiled, NOT Unity/Player executed.'
    }
    $common += '"/reference:' + (Join-Path $root 'Temp\UI.Panel.Offline\EditorTools\YLFramework.Editor.dll') + '"'
    $common += '"/reference:' + (Join-Path $env:USERPROFILE '.nuget\packages\nunit\3.14.0\lib\net45\nunit.framework.dll') + '"'
    $testReferences = Join-Path $UnityData 'Resources\PackageManager\ProjectTemplates\libcache\com.unity.template.2d-7.0.4\ScriptAssemblies'
    foreach ($name in @('UnityEngine.TestRunner.dll', 'UnityEditor.TestRunner.dll')) {
        $reference = Join-Path $testReferences $name
        if (-not (Test-Path -LiteralPath $reference)) { throw "Missing real test reference: $reference" }
        $common += '"/reference:' + $reference + '"'
    }
    foreach ($phase in @('EditMode', 'PlayMode')) {
        $testPath = Join-Path $root "Assets\YLFramework\Tests\$phase\Extensions\Behavior"
        if (-not (Test-Path -LiteralPath $testPath)) { continue }
        $arguments = $common + @('"/target:library"', '"/define:UNITY_EDITOR;UNITY_INCLUDE_TESTS"', ('"/out:' + (Join-Path $output "YLFramework.Tests.$phase.dll") + '"'))
        $arguments += @(Get-ChildItem -LiteralPath $testPath -Filter '*.cs' -Recurse | ForEach-Object { '"' + $_.FullName + '"' })
        $response = Join-Path $output "$phase.rsp"
        [IO.File]::WriteAllLines($response, [string[]]$arguments, [Text.UTF8Encoding]::new($false))
        & dotnet $compiler /noconfig "@$response"
        if ($LASTEXITCODE -ne 0) { throw "Prepared $phase test source compilation failed." }
        "Prepared $phase fixtures compiled, NOT executed."
    }
}
if ($Mode -in @('Pure', 'Static', 'All')) {
    $mono = Join-Path $UnityData 'MonoBleedingEdge\bin\mono.exe'
    $compiler = 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore\csc.dll'
    $framework = Join-Path $UnityData 'MonoBleedingEdge\lib\mono\4.8-api'
    $managed = Join-Path $UnityData 'Managed\UnityEngine'
    $nunit = Join-Path $env:USERPROFILE '.nuget\packages\nunit\3.14.0\lib\net45\nunit.framework.dll'
    $references = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'Facades\netstandard.dll') | ForEach-Object { Join-Path $framework $_ }
    $references += $nunit
    $references += Join-Path $managed 'UnityEngine.CoreModule.dll'
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets\YLFramework\Tests\EditMode\Extensions\Behavior') -Filter '*OfflineTests.cs' -Recurse | ForEach-Object FullName)
    $sources += Join-Path $root 'Tools\UIPanelOfflineRunner.cs'
    # Only genuinely managed product logic belongs here. No Unity substitutes or test doubles.
    $pureSources = Join-Path $PSScriptRoot 'PureSources.txt'
    if (Test-Path -LiteralPath $pureSources) {
        $sources += @([IO.File]::ReadAllLines($pureSources) | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { Join-Path $root $_ })
    }
    foreach ($path in @($mono, $compiler) + $references + $sources) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing real input: $path" }
    }
    $arguments = @('/nologo', '/nostdlib+', '/langversion:9.0', '/target:exe', "/out:$output\BehaviorTests.exe")
    $arguments += $references | ForEach-Object { "/reference:$_" }
    $arguments += $sources
    $response = Join-Path $output 'pure.rsp'
    [IO.File]::WriteAllLines($response, [string[]]@($arguments | ForEach-Object { '"' + $_ + '"' }), [Text.UTF8Encoding]::new($false))
    & dotnet $compiler /noconfig "@$response"
    if ($LASTEXITCODE -ne 0) { throw 'Offline test compilation failed.' }
    foreach ($path in @($nunit, (Join-Path $managed 'UnityEngine.CoreModule.dll'), (Join-Path $managed 'UnityEngine.SharedInternalsModule.dll'))) {
        Copy-Item -LiteralPath $path -Destination $output -Force
    }
    & $mono (Join-Path $output 'BehaviorTests.exe')
    $exitCode = $LASTEXITCODE
    Copy-Item -LiteralPath (Join-Path $output 'results.xml') -Destination (Join-Path $output 'pure-results.xml') -Force
    if ($exitCode -ne 0) { throw "Offline tests failed. Evidence: $output" }
}
if ($Mode -in @('Legacy', 'All')) {
    if ($Mode -eq 'Legacy') { & (Join-Path $root 'Tools\Verify-UIPanel.ps1') -Mode Runtime -UnityData $UnityData }
    $mono = Join-Path $UnityData 'MonoBleedingEdge\bin\mono.exe'
    $compiler = 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore\csc.dll'
    $framework = Join-Path $UnityData 'MonoBleedingEdge\lib\mono\4.8-api'
    $managed = Join-Path $UnityData 'Managed\UnityEngine'
    $runtime = Join-Path $root 'Temp\UI.Panel.Offline\Runtime\YLFramework.Runtime.dll'
    $references = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'Facades\netstandard.dll') | ForEach-Object { Join-Path $framework $_ }
    $references += @($runtime, (Join-Path $managed 'UnityEngine.CoreModule.dll'), (Join-Path $env:USERPROFILE '.nuget\packages\nunit\3.14.0\lib\net45\nunit.framework.dll'))
    $arguments = @('/nologo', '/nostdlib+', '/langversion:9.0', '/target:exe', "/out:$output\LegacyTests.exe")
    $arguments += $references | ForEach-Object { "/reference:$_" }
    $arguments += @((Join-Path $root 'Tools\UIPanelOfflineRunner.cs'), (Join-Path $root 'Assets\YLFramework\Tests\EditMode\Extensions\Behavior\DualFSMLegacyTests.cs'))
    $response = Join-Path $output 'legacy.rsp'
    [IO.File]::WriteAllLines($response, [string[]]@($arguments | ForEach-Object { '"' + $_ + '"' }), [Text.UTF8Encoding]::new($false))
    & dotnet $compiler /noconfig "@$response"
    if ($LASTEXITCODE -ne 0) { throw 'Read-only lightweight regression compilation failed.' }
    foreach ($path in @($runtime, (Join-Path $managed 'UnityEngine.CoreModule.dll'), (Join-Path $managed 'UnityEngine.SharedInternalsModule.dll'), (Join-Path $env:USERPROFILE '.nuget\packages\nunit\3.14.0\lib\net45\nunit.framework.dll'))) { Copy-Item -LiteralPath $path -Destination $output -Force }
    $runLog = @(& $mono (Join-Path $output 'LegacyTests.exe') 2>&1)
    $exitCode = $LASTEXITCODE
    Copy-Item -LiteralPath (Join-Path $output 'results.xml') -Destination (Join-Path $output 'legacy-results.xml') -Force
    [IO.File]::WriteAllLines((Join-Path $output 'legacy.log'), [string[]]$runLog, [Text.UTF8Encoding]::new($false))
    $runLog
    if ($exitCode -ne 0 -or $runLog -match 'cant resolve internal call|out of sync') { throw 'Read-only lightweight regression failed or touched an unavailable Unity native entry.' }
}
"Behavior offline $Mode finished. NOT Unity/EditMode/PlayMode/Player acceptance. Evidence: $output"
