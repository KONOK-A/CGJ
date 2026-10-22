#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021')
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$rootPath = [IO.Path]::GetFullPath($Root)
$owner = [IO.File]::ReadAllText((Join-Path $rootPath 'owner.json')) | ConvertFrom-Json
if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-independent-reception') { throw 'Wrong receiver owner.' }
foreach ($name in @('base-no-package-import','optional-no-package-import')) {
    $receipt = [IO.File]::ReadAllText((Join-Path $rootPath ($name + '.receipt.json'))) | ConvertFrom-Json
    if (-not $receipt.passed -or $receipt.testsPresent) { throw 'Product-only import must succeed before acceptance consumer installation.' }
}
$project = [IO.Path]::GetFullPath($owner.project)
if ($project -ne (Join-Path $rootPath 'R')) { throw 'Receiver path mismatch.' }
$destination = Join-Path $project 'Assets/HCLRReceptionHarness'
if ([IO.Path]::Exists($destination)) { throw 'Never overwrite an existing acceptance consumer.' }
[IO.Directory]::CreateDirectory((Join-Path $destination 'Editor')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $destination 'Runtime')) | Out-Null
[IO.File]::Copy((Join-Path $PSScriptRoot 'HybridCLRReceptionBootstrap.cs.txt'), (Join-Path $destination 'Editor/HybridCLRReceptionBootstrap.cs'))
[IO.File]::Copy((Join-Path $PSScriptRoot 'HybridCLRReceptionRunner.cs.txt'), (Join-Path $destination 'Runtime/HybridCLRReceptionRunner.cs'))
$runtime = @{ name='YLFramework.Tests.PlayMode'; references=@('YLFramework.Runtime','YLFramework.HybridCLR','YLFramework.HybridCLR.Sample.Host'); overrideReferences=$true; precompiledReferences=@('Newtonsoft.Json.dll'); autoReferenced=$false; noEngineReferences=$false }
$editor = @{ name='YLFramework.HybridCLR.Receiver.Editor'; references=@('YLFramework.Runtime','YLFramework.Editor','YLFramework.HybridCLR','YLFramework.HybridCLR.Editor','YLFramework.HybridCLR.Sample.Editor','YLFramework.Tests.PlayMode','HybridCLR.Runtime','HybridCLR.Editor'); includePlatforms=@('Editor'); overrideReferences=$true; precompiledReferences=@('Newtonsoft.Json.dll'); autoReferenced=$false; noEngineReferences=$false }
[IO.File]::WriteAllText((Join-Path $destination 'Runtime/YLFramework.Tests.PlayMode.asmdef'), ($runtime | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $destination 'Editor/YLFramework.HybridCLR.Receiver.Editor.asmdef'), ($editor | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
'Installed the real receiver-only acceptance consumer after product import; no Core API or dependency stub was added.'
