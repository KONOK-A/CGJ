[CmdletBinding()]
param(
    [string] $Sdk = 'C:\Program Files\dotnet\sdk\10.0.102',
    [string] $ReferenceRoot = 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\10.0.2\ref\net10.0',
    [string] $Newtonsoft = 'G:\Unity\Projects\CGJ2024\Library\PackageCache\com.unity.nuget.newtonsoft-json@3.2.1\Runtime\Newtonsoft.Json.dll',
    [string] $NUnit = "$env:USERPROFILE\.nuget\packages\nunit\3.14.0\lib\netstandard2.0\nunit.framework.dll",
    [string[]] $Filter = @(),
    [switch] $WithFacade,
    [switch] $ReferenceOnly,
    [string] $UnityData = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
if (-not (Test-Path -LiteralPath (Join-Path $root 'Temp'))) { throw 'Missing worktree Temp parent.' }
$profile = if ($ReferenceOnly) { 'unity-references' } elseif ($WithFacade) { 'facade' } else { 'offline' }
$output = Join-Path $root "Temp/Extensions.Data.Save/$profile"
$null = New-Item -ItemType Directory -Path $output -Force
$compiler = Join-Path $Sdk 'Roslyn/bincore/csc.dll'
foreach ($path in @($compiler, $ReferenceRoot, $Newtonsoft, $NUnit)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing real SDK/reference: $path" }
}
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/YLFramework/Runtime/Systems/Save') -Filter '*.cs' |
    Where-Object Name -ne 'SaveManager.cs' | ForEach-Object FullName)
$sources += Join-Path $root 'Assets/YLFramework/Tests/EditMode/Systems/Save/SaveStoreSafetyTests.cs'
$sources += Join-Path $PSScriptRoot 'SaveSafetyRunner.cs'
$references = @(Get-ChildItem -LiteralPath $ReferenceRoot -Filter '*.dll' | ForEach-Object FullName) + @($Newtonsoft, $NUnit)
$unityReferences = @()
if ($WithFacade -or $ReferenceOnly) {
    $unityReferences = @('UnityEngine.CoreModule.dll', 'UnityEngine.SharedInternalsModule.dll', 'UnityEngine.JSONSerializeModule.dll') |
        ForEach-Object { Join-Path $UnityData "Managed/UnityEngine/$_" }
    $references += $unityReferences
    $sources += Join-Path $root 'Assets/YLFramework/Runtime/Systems/Save/SaveManager.cs'
    $sources += @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/YLFramework/Runtime/Systems/Save/Serialization') -Filter '*.cs' | ForEach-Object FullName)
    $sources += Join-Path $root 'Assets/YLFramework/Runtime/Foundation/Serialization/SerializedDictionary.cs'
    $sources += Join-Path $root 'Assets/YLFramework/Tests/EditMode/Systems/Save/SaveManagerFacadeTests.cs'
    $sources += Join-Path $root 'Assets/YLFramework/Tests/EditMode/Systems/Save/SerializedDictionarySaveTests.cs'
    $sources += Join-Path $root 'Assets/YLFramework/Tests/EditMode/Foundation/Serialization/SerializedDictionaryJsonTests.cs'
    $sources += Join-Path $root 'Assets/YLFramework/Tests/EditMode/Foundation/SerializedDictionaryTestAsset.cs'
}
if ($ReferenceOnly) {
    $references = @((Join-Path $UnityData 'NetStandard/ref/2.1.0/netstandard.dll'))
    $references += @(Get-ChildItem -LiteralPath (Join-Path $UnityData 'NetStandard/compat/2.1.0/shims/netstandard') -Filter '*.dll' | ForEach-Object FullName)
    $references += @($Newtonsoft, $NUnit) + $unityReferences
}
$target = if ($ReferenceOnly) { '/target:library' } else { '/target:exe' }
$arguments = @('/nologo', '/nostdlib+', '/langversion:9.0', $target, '/define:UNITY_2020_1_OR_NEWER', "/out:$output/SaveSafety.dll")
foreach ($path in $references + $sources) { if (-not (Test-Path -LiteralPath $path)) { throw "Missing actual input: $path" } }
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += $sources
$response = Join-Path $output 'compile.rsp'
[IO.File]::WriteAllLines($response, [string[]]@($arguments | ForEach-Object { '"' + $_ + '"' }), [Text.UTF8Encoding]::new($false))
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$diagnostics = @(& dotnet $compiler /noconfig "@$response" 2>&1)
$compileExit = $LASTEXITCODE
[IO.File]::WriteAllLines((Join-Path $output "compile-$stamp.log"), [string[]]$diagnostics)
$diagnostics
if ($compileExit -ne 0) { throw "Save safety compilation failed: $compileExit" }
if ($ReferenceOnly) { 'Unity 2022.3 .NET Standard 2.1 reference compilation passed; not Unity compilation or execution.'; return }
Copy-Item -LiteralPath $Newtonsoft -Destination $output -Force
Copy-Item -LiteralPath $NUnit -Destination $output -Force
foreach ($reference in $unityReferences) { Copy-Item -LiteralPath $reference -Destination $output -Force }
$runtime = @{ runtimeOptions = @{ tfm = 'net10.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = '10.0.2' } } }
[IO.File]::WriteAllText((Join-Path $output 'SaveSafety.runtimeconfig.json'), ($runtime | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
$ErrorActionPreference = 'Continue'
$result = @(& dotnet (Join-Path $output 'SaveSafety.dll') @Filter 2>&1)
$testExit = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
[IO.File]::WriteAllLines((Join-Path $output "tests-$stamp.log"), [string[]]$result)
$result
if ($testExit -ne 0) { throw "Save safety tests failed: $testExit" }
'BCL file tests only; not Unity, PlayMode or Player verification.'
