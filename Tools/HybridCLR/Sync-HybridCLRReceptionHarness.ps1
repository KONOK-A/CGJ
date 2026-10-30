#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021', [string]$Receiver)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$scope = . (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root -Receiver $Receiver
$Root = $scope.Root
$project = $scope.Project
Assert-NoLinks $PSCommandPath
if ($scope.IsCompatibility) { throw 'New compatibility consumers are immutable; install into a new receiver instead of synchronizing an existing one.' }
foreach ($name in @('base-no-package-import','optional-no-package-import')) {
    $receiptPath = Join-Path $Root ($name + '.receipt.json')
    Assert-NoLinks $receiptPath
    $receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json
    if (-not $receipt.passed -or $receipt.testsPresent) { throw 'Preserve the product-only import proof before consumer synchronization.' }
}
$active = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($project) })
if ($active.Count) { throw 'Never overwrite test consumers in a running receiver.' }
$destination = Join-Path $project 'Assets/HCLRReceptionHarness'
if (-not (Test-Path -LiteralPath (Join-Path $destination 'Editor')) -or -not (Test-Path -LiteralPath (Join-Path $destination 'Runtime'))) { throw 'Install the real consumer after product proof first.' }
$template = Join-Path $PSScriptRoot 'HybridCLRReceptionBootstrap.cs.txt'
Assert-NoLinks $template
$bootstrap = Convert-HybridCLRTemplate $scope 'HybridCLRReceptionBootstrap.cs.txt'
foreach ($name in @('HybridCLRReceptionRunner.cs.txt','HybridCLRProjectSubsetProbe.cs.txt')) { Assert-NoLinks (Join-Path $PSScriptRoot $name) }
foreach ($relative in @('Editor/HybridCLRReceptionBootstrap.cs','Runtime/HybridCLRReceptionRunner.cs','Runtime/HybridCLRProjectSubsetProbe.cs')) {
    Assert-NoLinks (Join-Path $destination $relative)
}
[IO.File]::WriteAllText((Join-Path $destination 'Editor/HybridCLRReceptionBootstrap.cs'), $bootstrap, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $destination 'Runtime/HybridCLRReceptionRunner.cs'), (Convert-HybridCLRTemplate $scope 'HybridCLRReceptionRunner.cs.txt'), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $destination 'Runtime/HybridCLRProjectSubsetProbe.cs'), (Convert-HybridCLRTemplate $scope 'HybridCLRProjectSubsetProbe.cs.txt'), [Text.UTF8Encoding]::new($false))
'Synchronized only the owned test consumer scripts; existing meta and product sources were preserved.'
