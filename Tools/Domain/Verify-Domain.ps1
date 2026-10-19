[CmdletBinding()]
param(
    [ValidateSet('Pure', 'Runtime', 'Editor')][string] $Mode = 'Pure',
    [string[]] $Classes = @(),
    [string] $Run = 'current',
    [string] $UnityData = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets\YLFramework\Runtime'))) { throw 'Not a YLFramework worktree.' }
if ($Run -notmatch '^[A-Za-z0-9_-]+$') { throw 'Run must be a safe directory name.' }
if ($Mode -ne 'Pure') {
    $profile = if ($Mode -eq 'Editor') { 'EditorTools' } else { 'Runtime' }
    & (Join-Path $root 'Tools\Verify-UIPanel.ps1') -Mode $profile -UnityData $UnityData
    return
}
$output = Join-Path $root "Temp\Domain.Offline\$Run"
$null = New-Item -ItemType Directory -Path $output -Force
$framework = Join-Path $UnityData 'MonoBleedingEdge\lib\mono\4.5'
$runtime = Join-Path $root 'Temp\UI.Panel.Offline\Runtime\YLFramework.Runtime.dll'
$nunit = Join-Path $env:USERPROFILE '.nuget\packages\nunit\3.14.0\lib\net45\nunit.framework.dll'
$mono = Join-Path $UnityData 'MonoBleedingEdge\bin\mono.exe'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$compiler = 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore\csc.dll'
$references = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'Facades\netstandard.dll') | ForEach-Object { Join-Path $framework $_ }
$references += @($runtime, $nunit, (Join-Path $UnityData 'Managed\Newtonsoft.Json.dll'))
$references += @(Get-ChildItem -LiteralPath (Join-Path $UnityData 'Managed\UnityEngine') -Filter 'UnityEngine*.dll' | ForEach-Object FullName)
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets\YLFramework\Tests\EditMode\Extensions\Domain') -Recurse -Filter '*.cs' | ForEach-Object FullName)
$sources += Join-Path $root 'Tools\UIPanelOfflineRunner.cs'
foreach ($path in @($mono, $dotnet, $compiler) + $references + $sources) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing real input: $path" }
}
$arguments = @('/nologo', '/nostdlib+', '/langversion:9.0', '/target:exe', "/out:$output\DomainTests.exe")
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += $sources
$response = Join-Path $output 'compile.rsp'
[IO.File]::WriteAllLines($response, [string[]]@($arguments | ForEach-Object { '"' + $_ + '"' }), [Text.UTF8Encoding]::new($false))
& $dotnet $compiler /noconfig "@$response"
if ($LASTEXITCODE -ne 0) { throw 'Domain test source compilation failed.' }
foreach ($path in $references | Where-Object { $_ -notlike "$framework*" }) { Copy-Item -LiteralPath $path -Destination $output -Force }
& $mono (Join-Path $output 'DomainTests.exe') @Classes
$result = $LASTEXITCODE
"Domain managed tests exit=$result; evidence=$output\results.xml. Not a Unity/PlayMode/Player result."
if ($result -ne 0) { throw "Domain managed tests failed: $result" }
