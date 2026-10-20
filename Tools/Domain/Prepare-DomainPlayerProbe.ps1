[CmdletBinding()]
param([string] $Run = 'receiver')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if ($Run -notmatch '^[A-Za-z0-9_-]+$') { throw 'Unsafe Run.' }
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets\YLFramework\Tests\Fixtures\Extensions\Domain'))) { throw 'Missing fixture source.' }
$output = Join-Path $root "Temp\Domain.PlayerProbe\$Run\Assets\DomainFixture"
if (Test-Path -LiteralPath $output) { throw 'Refusing to overwrite an existing receiver fixture.' }
$null = New-Item -ItemType Directory -Path (Join-Path $output 'Editor') -Force
$source = Join-Path $root 'Assets\YLFramework\Tests\Fixtures\Extensions\Domain'
Copy-Item -LiteralPath (Join-Path $source 'DomainPlayerProbe.cs.txt') -Destination (Join-Path $output 'DomainPlayerProbe.cs')
Copy-Item -LiteralPath (Join-Path $source 'DomainFixtureAuthoring.cs.txt') -Destination (Join-Path $output 'Editor\DomainFixtureAuthoring.cs')
Copy-Item -LiteralPath (Join-Path $source 'link.xml.txt') -Destination (Join-Path $output 'link.xml')
"Prepared receiver-owned fixture at $output. No Unity process or receiver project was opened. Enable YL_DOMAIN_PLAYER_PROBE in a separate acceptance project; create its own assets with the fixture menu."
