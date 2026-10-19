[CmdletBinding()]
param(
    [string] $UnityData = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data',
    [string] $Run = 'current',
    [switch] $SkipProducts
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets/YLFramework/Runtime'))) { throw 'Not the B worktree.' }
if (-not $SkipProducts) {
    & (Join-Path $root 'Tools/Verify-UIPanel.ps1') -Mode Runtime -UnityData $UnityData
    & (Join-Path $root 'Tools/Verify-UIPanel.ps1') -Mode EditorTools -UnityData $UnityData
}
$output = Join-Path $root "Temp/Extensions.Data.Profiles/$Run"
$null = New-Item -ItemType Directory -Path $output -Force
$framework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.5'
$managed = Join-Path $UnityData 'Managed/UnityEngine'
$templates = Join-Path $UnityData 'Resources/PackageManager/ProjectTemplates/libcache/com.unity.template.2d-7.0.4/ScriptAssemblies'
$refs = @('mscorlib', 'System', 'System.Core', 'System.Xml', 'System.Xml.Linq', 'System.Net.Http', 'Microsoft.CSharp',
    'System.Runtime.Serialization', 'System.Security', 'System.IO.Compression', 'System.IO.Compression.FileSystem', 'System.Numerics') |
    ForEach-Object { Join-Path $framework ($_ + '.dll') }
$refs += Join-Path $framework 'Facades/netstandard.dll'
$refs += @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEngine*.dll' | ForEach-Object FullName)
$refs += @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEditor*.dll' | ForEach-Object FullName)
$refs += @('UnityEngine.UI.dll', 'UnityEngine.TestRunner.dll', 'UnityEditor.TestRunner.dll') | ForEach-Object { Join-Path $templates $_ }
$refs += @((Join-Path $UnityData 'Managed/Newtonsoft.Json.dll'),
    (Join-Path $env:USERPROFILE '.nuget/packages/nunit/3.14.0/lib/net45/nunit.framework.dll'),
    (Join-Path $root 'Temp/UI.Panel.Offline/Editor/YLFramework.Runtime.dll'),
    (Join-Path $root 'Temp/UI.Panel.Offline/EditorTools/YLFramework.Editor.dll'))
foreach ($reference in $refs) { if (-not (Test-Path -LiteralPath $reference)) { throw "Missing real reference: $reference" } }
foreach ($mode in @('EditMode', 'PlayMode')) {
    $folder = Join-Path $root "Assets/YLFramework/Tests/$mode/Extensions/Data"
    if (-not (Test-Path -LiteralPath $folder)) { continue }
    # The optional Excel assembly has its own probe and is not a default test dependency.
    $sources = @(Get-ChildItem -LiteralPath $folder -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '[/\\]Data[/\\]Excel[/\\]' } | ForEach-Object FullName)
    if ($mode -eq 'EditMode') { $sources += Join-Path $root 'Assets/YLFramework/Tests/EditMode/SerializedDictionarySaveTests.cs' }
    if ($sources.Count -eq 0) { continue }
    $arguments = @('/nologo', '/nostdlib+', '/langversion:9.0', '/target:library',
        "/out:$output/YLFramework.Tests.$mode.dll", '/define:UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_INCLUDE_TESTS;UNITY_STANDALONE_WIN;UNITY_2022_3_OR_NEWER')
    $arguments += $refs | ForEach-Object { "/reference:$_" }
    $arguments += $sources
    $rsp = Join-Path $output "$mode.rsp"
    [IO.File]::WriteAllLines($rsp, [string[]]@($arguments | ForEach-Object { '"' + $_ + '"' }), [Text.UTF8Encoding]::new($false))
    $diagnostics = @(& 'C:\Program Files\dotnet\dotnet.exe' 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore\csc.dll' /noconfig "@$rsp" 2>&1)
    $code = $LASTEXITCODE
    [IO.File]::WriteAllLines((Join-Path $output "$mode.log"), [string[]]$diagnostics, [Text.UTF8Encoding]::new($false))
    $diagnostics | Where-Object { $_ -match 'error CS' }
    "$mode test-source auxiliary compile exit=$code, files=$($sources.Count), warnings=$(@($diagnostics | Where-Object { $_ -match 'warning CS' }).Count)"
    if ($code -ne 0) { throw "$mode test-source compilation failed. Original diagnostics retained." }
}
'Auxiliary source/profile compilation only. Unity assets, GUI, PlayMode and Player tests were NOT executed.'
