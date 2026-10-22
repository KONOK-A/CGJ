#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021')
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$owner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json
if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-independent-reception' -or -not (Test-Path -LiteralPath $Root)) { throw 'Expected the owned synthetic test root.' }
$destination = Join-Path $Root 'ServerBadSignature'
if ([IO.Path]::Exists($destination)) { throw 'Preserve prior bad-signature fixture.' }
$valid = Join-Path $Root 'ServerV2/release.json'
$envelope = [IO.File]::ReadAllText($valid) | ConvertFrom-Json -AsHashtable
$signature = [Convert]::FromBase64String($envelope.Signature)
if ($signature.Length -lt 256) { throw 'Expected the genuine signed RSA fixture.' }
$signature[0] = $signature[0] -bxor 1
$envelope.Signature = [Convert]::ToBase64String($signature)
[IO.Directory]::CreateDirectory($destination) | Out-Null
[IO.File]::WriteAllText((Join-Path $destination 'release.json'), ($envelope | ConvertTo-Json -Compress -Depth 8), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $Root 'bad-signature-fixture.json'), (@{ negativeFixtureOnly=$true; payloadUnchanged=$true;
    originalEnvelopeSha256=(Get-FileHash -LiteralPath $valid -Algorithm SHA256).Hash;
    invalidEnvelopeSha256=(Get-FileHash -LiteralPath (Join-Path $destination 'release.json') -Algorithm SHA256).Hash } | ConvertTo-Json))
'Created an isolated one-bit signature corruption; payload, valid release and private key were not modified.'
