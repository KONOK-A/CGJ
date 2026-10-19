[CmdletBinding()]
param([string] $UnityData = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data', [string] $Run = 'current')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets/YLFramework/Runtime'))) { throw 'Not the B worktree.' }
$output = Join-Path $root "Temp/Extensions.Data.Localization/$Run"
$null = New-Item -ItemType Directory -Path $output -Force
$framework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.8-api'
$managed = Join-Path $UnityData 'Managed/UnityEngine'
$nunit = Join-Path $env:USERPROFILE '.nuget/packages/nunit/3.14.0/lib/net45/nunit.framework.dll'
$refs = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'Facades/netstandard.dll') | ForEach-Object { Join-Path $framework $_ }
$refs += @((Join-Path $managed 'UnityEngine.CoreModule.dll'), $nunit)
$sources = @((Join-Path $root 'Tools/UIPanelOfflineRunner.cs'))
$sources += @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/YLFramework/Tests/EditMode/Extensions/Data/Localization') -Filter 'LocalizationLogicTests.cs' | ForEach-Object FullName)
$sourceDir = Join-Path $root 'Assets/YLFramework/Runtime/Systems/Localization'
foreach ($name in @('LanguageInfo.cs', 'LocalizationFallback.cs', 'LocalizationEntryIndex.cs', 'LocalizationText.cs')) {
    $path = Join-Path $sourceDir $name
    if (Test-Path -LiteralPath $path) { $sources += $path }
}
$arguments = @('/nologo', '/nostdlib+', '/langversion:9.0', '/target:exe', "/out:$output/LocalizationTests.exe")
$arguments += $refs | ForEach-Object { "/reference:$_" }
$arguments += $sources
$rsp = Join-Path $output 'compile.rsp'
[IO.File]::WriteAllLines($rsp, [string[]]@($arguments | ForEach-Object { '"' + $_ + '"' }), [Text.UTF8Encoding]::new($false))
& 'C:\Program Files\dotnet\dotnet.exe' 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore\csc.dll' /noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) { throw 'Localization pure compilation failed.' }
Copy-Item -LiteralPath $nunit -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $managed 'UnityEngine.CoreModule.dll') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $managed 'UnityEngine.SharedInternalsModule.dll') -Destination $output -Force
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') (Join-Path $output 'LocalizationTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Localization pure tests failed. Original results retained.' }
'Pure localization behavior only; not a Unity compilation or asset/GUI/Player result.'
