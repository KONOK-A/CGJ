[CmdletBinding()]
param(
    [ValidateSet('Pure', 'PureCore', 'Runtime', 'Editor', 'EditorTools', 'CorePlayMode', 'CoreEditMode', 'CoreEditorTests', 'Syntax')][string] $Mode = 'Pure',
    [string] $UnityData = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets\YLFramework\Runtime'))) { throw 'Not a YLFramework worktree.' }
$output = Join-Path $root "Temp\UI.Panel.Offline\$Mode"
$null = New-Item -ItemType Directory -Path $output -Force
if ($Mode -eq 'Syntax') {
    $roslyn = 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore'
    Add-Type -Path (Join-Path $roslyn 'Microsoft.CodeAnalysis.dll')
    Add-Type -Path (Join-Path $roslyn 'Microsoft.CodeAnalysis.CSharp.dll')
    $options = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9)
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets\YLFramework\Tests\Core\Acceptance') -Recurse -Filter '*.cs')
    $failures = @()
    foreach ($editor in @($false, $true)) {
        $symbols = @('UNITY_STANDALONE_WIN', 'ENABLE_INPUT_SYSTEM', 'ENABLE_LEGACY_INPUT_MANAGER')
        if ($editor) { $symbols += 'UNITY_EDITOR' }
        foreach ($source in $sources) {
            $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($source.FullName), $options.WithPreprocessorSymbols([string[]]$symbols), $source.FullName)
            $failures += @($tree.GetDiagnostics() | Where-Object { $_.Severity -eq [Microsoft.CodeAnalysis.DiagnosticSeverity]::Error } | ForEach-Object ToString)
        }
    }
    [IO.File]::WriteAllLines((Join-Path $output 'syntax.log'), [string[]]$failures)
    if ($failures.Count -ne 0) { $failures; throw 'Acceptance C#9 syntax validation failed.' }
    "Acceptance syntax only: $($sources.Count) files, Player and Editor symbols, 0 errors. This does not check bindings or execute Unity."
    return
}
$mono = Join-Path $UnityData 'MonoBleedingEdge\bin\mono.exe'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$compiler = 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore\csc.dll'
$ui = Join-Path $UnityData 'Resources\PackageManager\ProjectTemplates\libcache\com.unity.template.2d-7.0.4\ScriptAssemblies\UnityEngine.UI.dll'
$managed = Join-Path $UnityData 'Managed\UnityEngine'
$derived = $Mode -in @('EditorTools', 'CorePlayMode', 'CoreEditMode', 'CoreEditorTests')
if ($Mode -eq 'CoreEditorTests') { & $PSCommandPath -Mode EditorTools -UnityData $UnityData }
elseif ($derived) { & $PSCommandPath -Mode Editor -UnityData $UnityData }
if ($Mode -eq 'PureCore') { & $PSCommandPath -Mode Runtime -UnityData $UnityData }
$references = @((Join-Path $UnityData 'NetStandard\ref\2.1.0\netstandard.dll'))
$references += @(Get-ChildItem -LiteralPath (Join-Path $UnityData 'NetStandard\compat\2.1.0\shims\netstandard') -Filter '*.dll' | ForEach-Object FullName)
$references += @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEngine*.dll' | ForEach-Object FullName)
$arguments = @('/nologo', '/nostdlib+', '/langversion:9.0', '/unsafe+')
if ($Mode -in @('Pure', 'PureCore')) {
    $framework = Join-Path $UnityData $(if ($Mode -eq 'PureCore') { 'MonoBleedingEdge\lib\mono\4.5' } else { 'MonoBleedingEdge\lib\mono\4.8-api' })
    $references = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'Facades\netstandard.dll') | ForEach-Object { Join-Path $framework $_ }
    $references += Join-Path $managed 'UnityEngine.CoreModule.dll'
    $nunit = Join-Path $env:USERPROFILE '.nuget\packages\nunit\3.14.0\lib\net45\nunit.framework.dll'
    $references += $nunit
    $sources = @('PanelConfig.cs', 'PanelOverrides.cs', 'PanelRule.cs') | ForEach-Object { Join-Path $root "Assets\YLFramework\Runtime\Core\UI\Windows\$_" }
    $sources += Join-Path $root 'Assets\YLFramework\Tests\Core\EditMode\PanelConfigurationTests.cs'
    if ($Mode -eq 'PureCore') {
        $references += Join-Path $root 'Temp\UI.Panel.Offline\Runtime\YLFramework.Runtime.dll'
        $sources = @('CoreLoadViewArgsTests.cs', 'CoreTemporaryCollectionTests.cs') | ForEach-Object { Join-Path $root "Assets\YLFramework\Tests\Core\EditMode\$_" }
        $sources += Join-Path $root 'Assets\YLFramework\Tests\EditMode\CollectionPoolTests.cs'
        $sources += Join-Path $root 'Assets\YLFramework\Tests\EditMode\SingletonContractTests.cs'
    }
    $sources += Join-Path $PSScriptRoot 'UIPanelOfflineRunner.cs'
    $arguments += @('/target:exe', "/out:$output\PureTests.exe")
} else {
    $references += $ui
    $references += Join-Path $UnityData 'Managed\Newtonsoft.Json.dll'
    $references += Join-Path $root 'Assets\YLFramework\Plugins\BouncyCastle\BouncyCastle.Cryptography.dll'
    $symbols = 'UNITY_STANDALONE_WIN;UNITY_STANDALONE;ENABLE_INPUT_SYSTEM;ENABLE_LEGACY_INPUT_MANAGER;UNITY_2022_3_OR_NEWER;UNITY_2021_3_OR_NEWER;UNITY_2020_1_OR_NEWER;UNITY_5_3_OR_NEWER'
    if ($Mode -ne 'Runtime') {
        $symbols += ';UNITY_EDITOR;UNITY_EDITOR_WIN'
        $references += @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEditor*.dll' | ForEach-Object FullName)
    }
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets\YLFramework\Runtime') -Recurse -Filter '*.cs' | ForEach-Object FullName)
    $assemblyName = 'YLFramework.Runtime'
    if (-not $derived) { $arguments += '/analyzer:' + (Join-Path $root 'Assets\YLFramework\Runtime\Core\Analyzers\YLFramework.Analyzers.dll') }
    if ($derived) {
        $references += Join-Path $root 'Temp\UI.Panel.Offline\Editor\YLFramework.Runtime.dll'
        if ($Mode -in @('EditorTools', 'CoreEditorTests')) {
            $references = @($references | Where-Object { $_ -notmatch '[/\\]NetStandard[/\\]' })
            $editorFramework = Join-Path $UnityData 'MonoBleedingEdge\lib\mono\4.5'
            $references += @('mscorlib', 'System', 'System.Core', 'System.Xml', 'System.Xml.Linq', 'System.Net.Http', 'Microsoft.CSharp',
                'System.Runtime.Serialization', 'System.Security', 'System.IO.Compression', 'System.IO.Compression.FileSystem', 'System.Numerics') |
                ForEach-Object { Join-Path $editorFramework ($_ + '.dll') }
            $references += Join-Path $editorFramework 'Facades\netstandard.dll'
        }
        if ($Mode -eq 'EditorTools') {
            $assemblyName = 'YLFramework.Editor'
            $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets\YLFramework\Editor') -Recurse -Filter '*.cs' | ForEach-Object FullName)
        } else {
            $symbols += ';UNITY_INCLUDE_TESTS'
            $testFolder = switch ($Mode) { 'CorePlayMode' { 'PlayMode' } 'CoreEditMode' { 'EditMode' } 'CoreEditorTests' { 'Editor' } }
            $assemblyName = switch ($Mode) { 'CorePlayMode' { 'YLFramework.Core.Tests.PlayMode' } 'CoreEditMode' { 'YLFramework.Core.Tests.EditMode' } 'CoreEditorTests' { 'YLFramework.Core.Editor.Tests' } }
            $sources = @(Get-ChildItem -LiteralPath (Join-Path $root "Assets\YLFramework\Tests\Core\$testFolder") -Recurse -Filter '*.cs' | ForEach-Object FullName)
            $references += Join-Path $env:USERPROFILE '.nuget\packages\nunit\3.14.0\lib\netstandard2.0\nunit.framework.dll'
            $testAssemblies = Join-Path $UnityData 'Resources\PackageManager\ProjectTemplates\libcache\com.unity.template.2d-7.0.4\ScriptAssemblies'
            $references += @('UnityEngine.TestRunner.dll', 'UnityEditor.TestRunner.dll') | ForEach-Object { Join-Path $testAssemblies $_ }
            if ($Mode -eq 'CoreEditorTests') { $references += Join-Path $root 'Temp\UI.Panel.Offline\EditorTools\YLFramework.Editor.dll' }
        }
    }
    $arguments += @('/target:library', "/out:$output\$assemblyName.dll", "/define:$symbols")
}
foreach ($path in @($mono, $dotnet, $compiler) + $references + $sources) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing real reference/source: $path" }
}
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += $sources
$response = Join-Path $output 'compile.rsp'
[IO.File]::WriteAllLines($response, [string[]]@($arguments | ForEach-Object { '"' + $_ + '"' }), [Text.UTF8Encoding]::new($false))
$diagnostics = @(& $dotnet $compiler /noconfig "@$response" 2>&1)
$compileExit = $LASTEXITCODE
$log = Join-Path $output 'compile.log'
[IO.File]::WriteAllLines($log, [string[]]$diagnostics, [Text.UTF8Encoding]::new($false))
$diagnostics | Where-Object { $_ -match 'error CS' }
"Roslyn $Mode exit=$compileExit; warnings=$(@($diagnostics | Where-Object { $_ -match 'warning CS' }).Count); log=$log"
if ($compileExit -ne 0) { throw "Roslyn $Mode compilation failed: $compileExit" }
if ($Mode -in @('Pure', 'PureCore')) {
    Copy-Item -LiteralPath $nunit -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $managed 'UnityEngine.CoreModule.dll') -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $managed 'UnityEngine.SharedInternalsModule.dll') -Destination $output -Force
    if ($Mode -eq 'PureCore') { Copy-Item -LiteralPath (Join-Path $root 'Temp\UI.Panel.Offline\Runtime\YLFramework.Runtime.dll') -Destination $output -Force }
    $classes = if ($Mode -eq 'PureCore') { @('YLFramework.Core.Tests.CoreLoadViewArgsTests', 'YLFramework.Core.Tests.CoreTemporaryCollectionTests', 'YLFramework.Tests.CollectionPoolTests') } else { @() }
    & $mono (Join-Path $output 'PureTests.exe') @classes
    if ($LASTEXITCODE -ne 0) { throw "Pure regression failed: $LASTEXITCODE" }
}
"Offline $Mode finished. This is not a Unity compilation, PlayMode or Player result. Output: $output"
