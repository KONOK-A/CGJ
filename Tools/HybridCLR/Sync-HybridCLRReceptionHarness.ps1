#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021')
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$scope = . (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root
$Root = $scope.Root
$project = $scope.Project
Assert-NoLinks $PSCommandPath
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
$bootstrap = [IO.File]::ReadAllText($template)
$marker = '"__YL_HYBRIDCLR_SOURCE_REPOSITORY__"'
if ([regex]::Matches($bootstrap, [regex]::Escape($marker)).Count -ne 1) { throw 'Expected one unbound SourceRepository literal in the receiver template.' }
$bootstrap = $bootstrap.Replace($marker, (ConvertTo-Json -InputObject $scope.Repository -Compress))
foreach ($name in @('HybridCLRReceptionRunner.cs.txt','HybridCLRProjectSubsetProbe.cs.txt')) { Assert-NoLinks (Join-Path $PSScriptRoot $name) }
foreach ($relative in @('Editor/HybridCLRReceptionBootstrap.cs','Runtime/HybridCLRReceptionRunner.cs','Runtime/HybridCLRProjectSubsetProbe.cs')) {
    Assert-NoLinks (Join-Path $destination $relative)
}
[IO.File]::WriteAllText((Join-Path $destination 'Editor/HybridCLRReceptionBootstrap.cs'), $bootstrap, [Text.UTF8Encoding]::new($false))
[IO.File]::Copy((Join-Path $PSScriptRoot 'HybridCLRReceptionRunner.cs.txt'),(Join-Path $destination 'Runtime/HybridCLRReceptionRunner.cs'),$true)
[IO.File]::Copy((Join-Path $PSScriptRoot 'HybridCLRProjectSubsetProbe.cs.txt'),(Join-Path $destination 'Runtime/HybridCLRProjectSubsetProbe.cs'),$true)
'Synchronized only the owned test consumer scripts; existing meta and product sources were preserved.'
