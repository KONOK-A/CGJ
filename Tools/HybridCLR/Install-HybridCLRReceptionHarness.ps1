#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021')
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$scope = . (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root
$rootPath = $scope.Root
Assert-NoLinks $PSCommandPath
foreach ($name in @('base-no-package-import','optional-no-package-import')) {
    $receiptPath = Join-Path $rootPath ($name + '.receipt.json')
    Assert-NoLinks $receiptPath
    $receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json
    if (-not $receipt.passed -or $receipt.testsPresent) { throw 'Product-only import must succeed before acceptance consumer installation.' }
}
$project = $scope.Project
if ($project -ne (Join-Path $rootPath 'R')) { throw 'Receiver path mismatch.' }
$destination = Join-Path $project 'Assets/HCLRReceptionHarness'
Assert-NoLinks $destination
if ([IO.Path]::Exists($destination)) { throw 'Never overwrite an existing acceptance consumer.' }
$template = Join-Path $PSScriptRoot 'HybridCLRReceptionBootstrap.cs.txt'
Assert-NoLinks $template
$bootstrap = [IO.File]::ReadAllText($template)
$marker = '"__YL_HYBRIDCLR_SOURCE_REPOSITORY__"'
if ([regex]::Matches($bootstrap, [regex]::Escape($marker)).Count -ne 1) { throw 'Expected one unbound SourceRepository literal in the receiver template.' }
$bootstrap = $bootstrap.Replace($marker, (ConvertTo-Json -InputObject $scope.Repository -Compress))
foreach ($name in @('HybridCLRReceptionRunner.cs.txt','HybridCLRProjectSubsetProbe.cs.txt')) { Assert-NoLinks (Join-Path $PSScriptRoot $name) }
[IO.Directory]::CreateDirectory((Join-Path $destination 'Editor')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $destination 'Runtime')) | Out-Null
foreach ($relative in @('Editor/HybridCLRReceptionBootstrap.cs','Runtime/HybridCLRReceptionRunner.cs','Runtime/HybridCLRProjectSubsetProbe.cs','Runtime/YLFramework.Tests.PlayMode.asmdef','Editor/YLFramework.HybridCLR.Receiver.Editor.asmdef')) {
    Assert-NoLinks (Join-Path $destination $relative)
}
$bootstrapPath = Join-Path $destination 'Editor/HybridCLRReceptionBootstrap.cs'
[IO.File]::WriteAllText($bootstrapPath, $bootstrap, [Text.UTF8Encoding]::new($false))
[IO.File]::Copy((Join-Path $PSScriptRoot 'HybridCLRReceptionRunner.cs.txt'), (Join-Path $destination 'Runtime/HybridCLRReceptionRunner.cs'))
[IO.File]::Copy((Join-Path $PSScriptRoot 'HybridCLRProjectSubsetProbe.cs.txt'), (Join-Path $destination 'Runtime/HybridCLRProjectSubsetProbe.cs'))
$runtime = @{ name='YLFramework.Tests.PlayMode'; references=@('YLFramework.Runtime','YLFramework.HybridCLR','YLFramework.HybridCLR.Sample.Host'); overrideReferences=$true; precompiledReferences=@('Newtonsoft.Json.dll'); autoReferenced=$false; noEngineReferences=$false }
$editor = @{ name='YLFramework.HybridCLR.Receiver.Editor'; references=@('YLFramework.Runtime','YLFramework.Editor','YLFramework.HybridCLR','YLFramework.HybridCLR.Editor','YLFramework.HybridCLR.Sample.Editor','YLFramework.Tests.PlayMode','HybridCLR.Runtime','HybridCLR.Editor'); includePlatforms=@('Editor'); overrideReferences=$true; precompiledReferences=@('Newtonsoft.Json.dll'); autoReferenced=$false; noEngineReferences=$false }
[IO.File]::WriteAllText((Join-Path $destination 'Runtime/YLFramework.Tests.PlayMode.asmdef'), ($runtime | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $destination 'Editor/YLFramework.HybridCLR.Receiver.Editor.asmdef'), ($editor | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
'Installed the real receiver-only acceptance consumer after product import; no Core API or dependency stub was added.'
