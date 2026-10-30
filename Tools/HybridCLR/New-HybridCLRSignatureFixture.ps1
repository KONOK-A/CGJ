#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021', [string]$Receiver,
    [ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
if ($Receiver -or [IO.Path]::GetFullPath($Root).StartsWith('G:\Unity\U63-20261027-c63a1',[StringComparison]::OrdinalIgnoreCase)) {
    $scope=. (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root -Receiver $Receiver
    if (-not $scope.IsCompatibility -or -not $Run) { throw 'Select a new exact owned signature fixture run.' }
    Assert-HybridCLRReady $scope
    Assert-HybridCLRProductImports $scope
    Assert-HybridCLRNewEvidence $scope $Run
    $Root=$scope.Root
    $source=Join-Path $Root 'ServerV2/release.json'
    $patchPath=Join-Path $Root 'patch-receipt.json'
    $basePath=Join-Path $Root ($scope.Owner.baseReceiptStem+'.json')
    $custodyPath=Join-Path $Root 'TestCredentials/owner.json'
    $pfxPath=Join-Path $Root 'TestCredentials/local-only.pfx'
    $destination=Join-Path $Root 'ServerBadSignature'
    $stage=Join-Path $Root ($Run+'-signature.stage')
    foreach ($path in @($source,$patchPath,$basePath,$custodyPath,$pfxPath,$destination,$destination+'.meta',$stage)) { Assert-NoLinks $path }
    foreach ($path in @($destination,$destination+'.meta',$stage)) { if ([IO.Path]::Exists($path)) { throw 'Preserve all prior signature fixture targets.' } }
    $base=[IO.File]::ReadAllText($basePath) | ConvertFrom-Json
    $patch=[IO.File]::ReadAllText($patchPath) | ConvertFrom-Json
    Assert-HybridCLRReceiptBinding $scope $base
    Assert-HybridCLRReceiptBinding $scope $patch
    if (-not $base.passed -or -not $base.Development -or -not $patch.passed -or $patch.PlayerFingerprint -cne $base.PlayerFingerprint -or
        $patch.BaselineSha256 -cne $base.NativeBaselineSha256) { throw 'Require this exact successful Development base and patch.' }
    $originalBytes=[IO.File]::ReadAllBytes($source)
    $sourceSha=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    $envelope=[Text.Encoding]::UTF8.GetString($originalBytes) | ConvertFrom-Json -AsHashtable
    if ($envelope.Count -ne 5 -or $envelope.SchemaVersion -ne 1 -or $envelope.Algorithm -cne 'RSA-SHA256-PKCS1' -or $envelope.KeyId -cne 'hclr-test') { throw 'Exact bounded canonical signed envelope required.' }
    $canonical=[ordered]@{ Algorithm=$envelope.Algorithm; KeyId=$envelope.KeyId; Payload=$envelope.Payload; SchemaVersion=1; Signature=$envelope.Signature } | ConvertTo-Json -Compress
    if ($canonical -cne [Text.Encoding]::UTF8.GetString($originalBytes)) { throw 'The signature control must be the original canonical bytes.' }
    $payload=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($envelope.Payload)) | ConvertFrom-Json
    if ($payload.ProjectId -cne $base.ProjectId -or $payload.AppContentCompatibilityId -cne $base.CompatibilityId -or $payload.CatalogDigest -cne $patch.CatalogDigest) { throw 'Foreign signed payload control.' }
    $custody=[IO.File]::ReadAllText($custodyPath) | ConvertFrom-Json -AsHashtable
    $certificate=[Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12FromFile($pfxPath,'hclr-local-fixture',[Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    try {
        $certSha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificate.RawData)).ToLowerInvariant()
        Assert-HybridCLRFixtureCredentials $scope $custody $certSha ((Get-FileHash -LiteralPath $pfxPath -Algorithm SHA256).Hash.ToLowerInvariant())
        $rsa=[Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($certificate)
        if ($null -eq $rsa) { throw 'The owned source signing certificate must be RSA.' }
        try {
            $unsigned=[ordered]@{ Algorithm=$envelope.Algorithm; KeyId=$envelope.KeyId; Payload=$envelope.Payload; SchemaVersion=1 } | ConvertTo-Json -Compress
            if (-not $rsa.VerifyData([Text.Encoding]::UTF8.GetBytes($unsigned),[Convert]::FromBase64String($envelope.Signature),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)) { throw 'The unmodified positive signature control must verify first.' }
        } finally { $rsa.Dispose() }
    } finally { $certificate.Dispose() }
    $changed=$envelope.Clone()
    $signature=[Convert]::FromBase64String($changed.Signature); $signature[0]=$signature[0] -bxor 1
    $changed.Signature=[Convert]::ToBase64String($signature)
    Assert-HybridCLRSignatureMutation $envelope $changed
    $bad=[ordered]@{ Algorithm=$changed.Algorithm; KeyId=$changed.KeyId; Payload=$changed.Payload; SchemaVersion=1; Signature=$changed.Signature } | ConvertTo-Json -Compress
    $null=[IO.Directory]::CreateDirectory($stage)
    [IO.File]::WriteAllText((Join-Path $stage 'release.json'),$bad,[Text.UTF8Encoding]::new($false))
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -cne $sourceSha) { throw 'The original signed control changed.' }
    [IO.Directory]::Move($stage,$destination)
    [IO.File]::WriteAllText((Join-Path $Root ($Run+'.json')),(@{ Prepared=$true; NegativeFixtureOnly=$true; PayloadUnchanged=$true; SignatureBitFlips=1
        UnityVersion=$scope.ExpectedUnityVersion; UnityRevision=$scope.ExpectedUnityRevision; Receiver=$scope.Owner.receiver; OwnerSha256=$scope.OwnerSha256
        SourceSha256=$sourceSha; CorruptSha256=(Get-FileHash -LiteralPath (Join-Path $destination 'release.json') -Algorithm SHA256).Hash.ToLowerInvariant()
        CatalogDigest=$patch.CatalogDigest; ProjectId=$base.ProjectId; CompatibilityId=$base.CompatibilityId } | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
    'Prepared exact one-bit signature-only negative; positive control and signed payload unchanged.'
    exit 0
}
$receptionOwner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json -AsHashtable
if ($receptionOwner.ContainsKey('schemaVersion')) { throw 'Legacy-only signature fixture rejects versioned compatibility owners.' }
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
