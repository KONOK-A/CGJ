[CmdletBinding()]
param(
    [ValidateSet('Pure', 'Compile')][string] $Mode = 'Pure',
    [string] $UnityData = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string] $Run = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets/YLFramework/Runtime'))) { throw 'Not a YLFramework worktree.' }
$output = Join-Path $root "Temp/Extensions.Data.DataAssets/$Run"
if (-not (Test-Path -LiteralPath $root)) { throw 'Missing output parent.' }
if (Test-Path -LiteralPath $output) { throw 'Evidence already exists. Choose a fresh Run name.' }
$null = New-Item -ItemType Directory -Path $output -Force
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$compiler = 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore\csc.dll'
$mono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$framework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.5'
$managed = Join-Path $UnityData 'Managed/UnityEngine'
$nunit = Join-Path $env:USERPROFILE '.nuget/packages/nunit/3.14.0/lib/net45/nunit.framework.dll'
function Compile-Sources([string] $name, [string[]] $sources, [string[]] $references, [string] $symbols, [switch] $Executable) {
    foreach ($path in @($dotnet, $compiler) + $sources + $references) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing real reference/source: $path" }
    }
    $arguments = @('/nologo', '/nostdlib+', '/langversion:9.0', '/unsafe+', "/out:$output/$name")
    $arguments += if ($Executable) { '/target:exe' } else { '/target:library' }
    if ($symbols) { $arguments += "/define:$symbols" }
    $arguments += $references | ForEach-Object { "/reference:$_" }
    $arguments += $sources
    $response = Join-Path $output ($name + '.rsp')
    [IO.File]::WriteAllLines($response, [string[]]@($arguments | ForEach-Object { '"' + $_ + '"' }), [Text.UTF8Encoding]::new($false))
    $diagnostics = @(& $dotnet $compiler /noconfig "@$response" 2>&1)
    $code = $LASTEXITCODE
    [IO.File]::WriteAllLines((Join-Path $output ($name + '.log')), [string[]]$diagnostics, [Text.UTF8Encoding]::new($false))
    $diagnostics | Where-Object { $_ -match 'error CS' }
    "$name exit=$code warnings=$(@($diagnostics | Where-Object { $_ -match 'warning CS' }).Count)"
    if ($code -ne 0) { throw "Offline compilation failed: $name" }
}
$bcl = @('mscorlib', 'System', 'System.Core', 'System.Xml', 'System.Xml.Linq', 'System.Net.Http', 'Microsoft.CSharp',
    'System.Runtime.Serialization', 'System.Security', 'System.IO.Compression', 'System.IO.Compression.FileSystem', 'System.Numerics') |
    ForEach-Object { Join-Path $framework ($_ + '.dll') }
$bcl += Join-Path $framework 'Facades/netstandard.dll'
if ($Mode -eq 'Pure') {
    $internalPath = Join-Path $root 'Assets/YLFramework/Runtime/Systems/DataAssets/Internal'
    $sources = @()
    if (Test-Path -LiteralPath $internalPath) {
        $sources += @(Get-ChildItem -LiteralPath $internalPath -Filter '*.cs' |
            Where-Object { $_.Name -ne 'DataAssetLifetime.cs' } | ForEach-Object FullName)
    }
    $sources += Join-Path $root 'Assets/YLFramework/Tests/EditMode/Extensions/Data/DataAssets/DataAssetIdentityTests.cs'
    $sources += Join-Path $root 'Tools/UIPanelOfflineRunner.cs'
    Compile-Sources 'DataAssets.Pure.exe' $sources ($bcl + $nunit) '' -Executable
    Copy-Item -LiteralPath $nunit -Destination $output -Force
    & $mono (Join-Path $output 'DataAssets.Pure.exe') 'YLFramework.Tests.DataAssets.DataAssetIdentityTests'
    if ($LASTEXITCODE -ne 0) { throw 'Pure DataAssets regressions failed.' }
} else {
    $engine = @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEngine*.dll' | ForEach-Object FullName)
    $editor = @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEditor*.dll' | ForEach-Object FullName)
    $ui = Join-Path $UnityData 'Resources/PackageManager/ProjectTemplates/libcache/com.unity.template.2d-7.0.4/ScriptAssemblies/UnityEngine.UI.dll'
    $refs = $bcl + $engine + @($ui, (Join-Path $UnityData 'Managed/Newtonsoft.Json.dll'),
        (Join-Path $root 'Assets/YLFramework/Plugins/BouncyCastle/BouncyCastle.Cryptography.dll'))
    $symbols = 'UNITY_STANDALONE_WIN;UNITY_STANDALONE;ENABLE_INPUT_SYSTEM;ENABLE_LEGACY_INPUT_MANAGER;UNITY_2022_3_OR_NEWER;UNITY_2021_3_OR_NEWER;UNITY_2020_1_OR_NEWER;UNITY_5_3_OR_NEWER'
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/YLFramework/Runtime') -Recurse -Filter '*.cs' | ForEach-Object FullName)
    Compile-Sources 'YLFramework.Runtime.dll' $sources $refs $symbols
    $editorSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/YLFramework/Editor') -Recurse -Filter '*.cs' | ForEach-Object FullName)
    # Preserve the Runtime assembly identity required by its existing friends.
    $profileDirectory = Join-Path $output 'EditorProfile'
    $null = New-Item -ItemType Directory -Path $profileDirectory -Force
    # This is an offline output, not a generated Unity project file.
    Compile-Sources 'EditorProfile/YLFramework.Runtime.dll' $sources ($refs + $editor) ($symbols + ';UNITY_EDITOR;UNITY_EDITOR_WIN')
    $editorRefs = $refs + $editor + (Join-Path $profileDirectory 'YLFramework.Runtime.dll')
    Compile-Sources 'YLFramework.Editor.dll' $editorSources $editorRefs ($symbols + ';UNITY_EDITOR;UNITY_EDITOR_WIN')
    $testRunner = Join-Path $UnityData 'Resources/PackageManager/ProjectTemplates/libcache/com.unity.template.2d-7.0.4/ScriptAssemblies'
    $testRefs = $editorRefs + @($nunit, (Join-Path $output 'YLFramework.Editor.dll'),
        (Join-Path $testRunner 'UnityEngine.TestRunner.dll'), (Join-Path $testRunner 'UnityEditor.TestRunner.dll'))
    foreach ($kind in @('EditMode', 'PlayMode')) {
        $tests = @(Get-ChildItem -LiteralPath (Join-Path $root "Assets/YLFramework/Tests/$kind/Extensions/Data/DataAssets") -Recurse -Filter '*.cs' | ForEach-Object FullName)
        Compile-Sources "YLFramework.Tests.$kind.dll" $tests $testRefs ($symbols + ';UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_INCLUDE_TESTS')
    }
}
"Offline $Mode finished: $output. Not a Unity compile, EditMode/PlayMode execution or Player result."
