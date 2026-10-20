[CmdletBinding()]
param(
    [string] $Run = 'green',
    [string] $UnityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Data',
    [switch] $ExpectRed
)
$ErrorActionPreference = 'Stop'
if ($Run -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Run must be a single safe path segment.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets/YLFramework'))) { throw 'Not a YLFramework worktree.' }
$output = Join-Path $root "Temp/Extensions.Data.Excel/$Run"
if (Test-Path -LiteralPath $output) { throw "Evidence already exists: $output. Use another Run name." }
$null = [IO.Directory]::CreateDirectory($output)
$compiler = 'C:/Program Files/dotnet/sdk/9.0.312/Roslyn/bincore/csc.dll'
$mono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$framework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.5'
$managed = Join-Path $UnityData 'Managed/UnityEngine'
$nunit = Join-Path $env:USERPROFILE '.nuget/packages/nunit/3.14.0/lib/net45/nunit.framework.dll'
$json = Join-Path $UnityData 'Managed/Newtonsoft.Json.dll'
$backend = Join-Path $root 'Assets/YLFramework/Integrations/Excel/Editor/Plugins/MiniExcel.dll'
$refs = @('mscorlib', 'System', 'System.Core', 'System.Data', 'System.Xml', 'System.Xml.Linq', 'System.Net.Http',
    'System.Runtime.Serialization', 'System.Security', 'System.Numerics', 'System.IO.Compression', 'System.IO.Compression.FileSystem', 'Microsoft.CSharp') |
    ForEach-Object { Join-Path $framework ($_ + '.dll') }
$refs += Join-Path $framework 'Facades/netstandard.dll'
$refs += @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEngine*.dll' | ForEach-Object FullName)
$refs += @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEditor*.dll' | ForEach-Object FullName)
$refs += Join-Path $UnityData 'Resources/PackageManager/ProjectTemplates/libcache/com.unity.template.2d-7.0.4/ScriptAssemblies/UnityEngine.UI.dll'
$refs += Join-Path $root 'Assets/YLFramework/Plugins/BouncyCastle/BouncyCastle.Cryptography.dll'
$refs += $json
$runtimeSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/YLFramework/Runtime') -Recurse -Filter '*.cs' | ForEach-Object FullName)
$editorSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/YLFramework/Editor') -Recurse -Filter '*.cs' | ForEach-Object FullName)
$product = Join-Path $root 'Assets/YLFramework/Integrations/Excel/Editor'
$productSources = @()
if (Test-Path -LiteralPath $product) { $productSources = @(Get-ChildItem -LiteralPath $product -Recurse -Filter '*.cs' | ForEach-Object FullName) }
$testSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/YLFramework/Tests/EditMode/Extensions/Data/Excel') -Recurse -Filter '*.cs' | ForEach-Object FullName)
$testSources += Join-Path $PSScriptRoot 'ExcelOfflineRunner.cs'
foreach ($file in @($compiler, $mono, $nunit, $backend) + $refs + $runtimeSources + $editorSources + $productSources + $testSources) { if (-not (Test-Path -LiteralPath $file)) { throw "Missing real input: $file" } }
function Invoke-Compilation([string] $Name, [string] $Target, [string[]] $Sources, [string[]] $References, [switch] $AllowUnsafe) {
    $arguments = @('/nologo', '/nostdlib+', '/langversion:9.0', "/target:$Target", '/define:UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_INCLUDE_TESTS;UNITY_STANDALONE_WIN;UNITY_STANDALONE;ENABLE_INPUT_SYSTEM;ENABLE_LEGACY_INPUT_MANAGER;UNITY_2022_3_OR_NEWER;UNITY_2021_3_OR_NEWER;UNITY_2020_1_OR_NEWER;UNITY_5_3_OR_NEWER', "/out:$output/$Name")
    $arguments += $References | ForEach-Object { "/reference:$_" }
    if ($AllowUnsafe) { $arguments += '/unsafe+' }
    $arguments += $Sources
    $rsp = Join-Path $output ($Name + '.rsp')
    [IO.File]::WriteAllLines($rsp, [string[]]@($arguments | ForEach-Object { '"' + $_ + '"' }), [Text.UTF8Encoding]::new($false))
    $compile = @(& dotnet $compiler /noconfig "@$rsp" 2>&1)
    $code = $LASTEXITCODE
    [IO.File]::WriteAllLines((Join-Path $output ($Name + '.compile.log')), [string[]]$compile)
    $compile
    if ($code -ne 0) { throw "Auxiliary $Name compile failed: $code" }
    "$Name auxiliary compile: $($Sources.Count) actual source files, $(@($compile | Where-Object { $_ -match 'warning CS' }).Count) warnings."
}
# The concrete optional DataAssets menu needs the actual full Runtime/Editor, never stubbed types.
Invoke-Compilation 'YLFramework.Runtime.dll' 'library' $runtimeSources $refs -AllowUnsafe
$runtime = Join-Path $output 'YLFramework.Runtime.dll'
Invoke-Compilation 'YLFramework.Editor.dll' 'library' $editorSources ($refs + $runtime)
$editor = Join-Path $output 'YLFramework.Editor.dll'
if ($productSources.Count -gt 0) { Invoke-Compilation 'YLFramework.Excel.Editor.dll' 'library' $productSources ($refs + @($runtime, $editor, $backend)) }
Invoke-Compilation 'ExcelOffline.exe' 'exe' $testSources ($refs + @($runtime, $editor, $nunit))
foreach ($file in $refs + @($nunit, $backend)) { Copy-Item -LiteralPath $file -Destination $output -Force }
$env:YL_EXCEL_TEST_OUTPUT = $output
$results = @(& $mono (Join-Path $output 'ExcelOffline.exe') 2>&1)
$code = $LASTEXITCODE
[IO.File]::WriteAllLines((Join-Path $output 'tests.log'), [string[]]$results)
$results
if ($ExpectRed) { if ($code -eq 0) { throw 'Expected RED, but all tests passed.' } }
elseif ($code -ne 0) { throw "Offline tests failed: $code" }
"Offline only; Unity GUI/AssetDatabase tests were compiled but NOT RUN. Evidence: $output"
