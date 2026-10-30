#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021', [string]$Receiver)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$scope = . (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root -Receiver $Receiver
$rootPath = $scope.Root
Assert-NoLinks $PSCommandPath
Assert-HybridCLRReady $scope
Assert-HybridCLRProductImports $scope
$project = $scope.Project
if (-not $scope.IsCompatibility -and $project -ne (Join-Path $rootPath 'R')) { throw 'Receiver path mismatch.' }
$active = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($project) })
if ($active.Count) { throw 'Do not install consumers into a running receiver.' }
$destination = Join-Path $project 'Assets/HCLRReceptionHarness'
Assert-NoLinks $destination
if ([IO.Path]::Exists($destination) -or [IO.Path]::Exists($destination + '.meta')) { throw 'Never overwrite an existing acceptance consumer.' }
if ($scope.IsCompatibility) {
    foreach ($relative in @('Host/YLFramework.HybridCLR.Sample.Host.asmdef','Editor/YLFramework.HybridCLR.Sample.Editor.asmdef')) {
        $samplePath=Join-Path $project ('Assets/YLFramework/Integrations/HybridCLR/Samples/' + $relative)
        Assert-NoLinks $samplePath
        if (-not [IO.File]::Exists($samplePath)) { throw 'The native harness requires the formal optional export with includeSample:true.' }
    }
}
$template = Join-Path $PSScriptRoot 'HybridCLRReceptionBootstrap.cs.txt'
Assert-NoLinks $template
$bootstrap = Convert-HybridCLRTemplate $scope 'HybridCLRReceptionBootstrap.cs.txt'
$runner = Convert-HybridCLRTemplate $scope 'HybridCLRReceptionRunner.cs.txt'
$subset = Convert-HybridCLRTemplate $scope 'HybridCLRProjectSubsetProbe.cs.txt'
foreach ($name in @('HybridCLRReceptionRunner.cs.txt','HybridCLRProjectSubsetProbe.cs.txt')) { Assert-NoLinks (Join-Path $PSScriptRoot $name) }
[IO.Directory]::CreateDirectory((Join-Path $destination 'Editor')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $destination 'Runtime')) | Out-Null
foreach ($relative in @('Editor/HybridCLRReceptionBootstrap.cs','Runtime/HybridCLRReceptionRunner.cs','Runtime/HybridCLRProjectSubsetProbe.cs','Runtime/YLFramework.Tests.PlayMode.asmdef','Editor/YLFramework.HybridCLR.Receiver.Editor.asmdef')) {
    Assert-NoLinks (Join-Path $destination $relative)
}
$bootstrapPath = Join-Path $destination 'Editor/HybridCLRReceptionBootstrap.cs'
[IO.File]::WriteAllText($bootstrapPath, $bootstrap, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $destination 'Runtime/HybridCLRReceptionRunner.cs'), $runner, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $destination 'Runtime/HybridCLRProjectSubsetProbe.cs'), $subset, [Text.UTF8Encoding]::new($false))
$runtime = @{ name='YLFramework.Tests.PlayMode'; references=@('YLFramework.Runtime','YLFramework.HybridCLR','YLFramework.HybridCLR.Sample.Host'); overrideReferences=$true; precompiledReferences=@('Newtonsoft.Json.dll'); autoReferenced=$false; noEngineReferences=$false }
$editor = @{ name='YLFramework.HybridCLR.Receiver.Editor'; references=@('YLFramework.Runtime','YLFramework.Editor','YLFramework.HybridCLR','YLFramework.HybridCLR.Editor','YLFramework.HybridCLR.Sample.Editor','YLFramework.Tests.PlayMode','HybridCLR.Runtime','HybridCLR.Editor'); includePlatforms=@('Editor'); overrideReferences=$true; precompiledReferences=@('Newtonsoft.Json.dll','dnlib.dll'); autoReferenced=$false; noEngineReferences=$false }
[IO.File]::WriteAllText((Join-Path $destination 'Runtime/YLFramework.Tests.PlayMode.asmdef'), ($runtime | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $destination 'Editor/YLFramework.HybridCLR.Receiver.Editor.asmdef'), ($editor | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
'Installed the real receiver-only acceptance consumer after product import; no Core API or dependency stub was added.'
