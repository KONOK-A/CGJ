#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021')
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$owner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json
if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-independent-reception') { throw 'Unexpected receiver owner.' }
$project = [IO.Path]::GetFullPath($owner.project)
$active = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($project) })
if ($active.Count) { throw 'Never overwrite test consumers in a running receiver.' }
$destination = Join-Path $project 'Assets/HCLRReceptionHarness'
if (-not (Test-Path -LiteralPath (Join-Path $destination 'Editor')) -or -not (Test-Path -LiteralPath (Join-Path $destination 'Runtime'))) { throw 'Install the real consumer after product proof first.' }
[IO.File]::Copy((Join-Path $PSScriptRoot 'HybridCLRReceptionBootstrap.cs.txt'),(Join-Path $destination 'Editor/HybridCLRReceptionBootstrap.cs'),$true)
[IO.File]::Copy((Join-Path $PSScriptRoot 'HybridCLRReceptionRunner.cs.txt'),(Join-Path $destination 'Runtime/HybridCLRReceptionRunner.cs'),$true)
'Synchronized only the owned test consumer scripts; existing meta and product sources were preserved.'
