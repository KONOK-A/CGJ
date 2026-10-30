#requires -Version 7.6
[CmdletBinding()]
param(
    [ValidateSet('ServerV2','ServerBad','ServerMissing','ServerBadSignature')][string]$Publication = 'ServerV2',
    [string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021',
    [ValidatePattern('^[A-Za-z0-9-]*$')][string]$ReadySuffix = '',
    [switch]$CertificateOnly,
    [switch]$CompileOnly,
    [string]$Receiver,
    [ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$scope=$null
if ($Receiver -or [IO.Path]::GetFullPath($Root).StartsWith('G:\Unity\U63-20261027-c63a1',[StringComparison]::OrdinalIgnoreCase)) {
    $scope=. (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root -Receiver $Receiver
    if (-not $scope.IsCompatibility -or -not $Run -or ($CertificateOnly -and $CompileOnly) -or ($ReadySuffix -and $ReadySuffix -cne $Run)) { throw 'Use an exact new receiver/run and one explicit fixture operation.' }
    Assert-HybridCLRReady $scope
    Assert-HybridCLRProductImports $scope
    Assert-HybridCLRNewEvidence $scope $Run
    $Root=$scope.Root; $owner=$scope.Owner; $ReadySuffix=$Run
} else {
    $receptionOwner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json -AsHashtable
    if ($receptionOwner.ContainsKey('schemaVersion')) { throw 'Legacy-only TLS fixture rejects versioned compatibility owners.' }
    $owner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json
    if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-independent-reception') { throw 'Unexpected acceptance fixture owner.' }
}
$credentials = Join-Path $Root 'TestCredentials'
$pfx = Join-Path $credentials 'local-only.pfx'
if ($scope -and -not $CompileOnly) {
    $custodyPath=Join-Path $credentials 'owner.json'
    foreach ($path in @($credentials,$credentials+'.meta',$pfx,$custodyPath)) { Assert-NoLinks $path }
    if ([IO.Path]::Exists($credentials) -and -not [IO.Directory]::Exists($credentials)) { throw 'Do not replace an unknown credentials path.' }
    if (-not [IO.Directory]::Exists($credentials)) {
        if (-not $CertificateOnly -or [IO.Path]::Exists($credentials+'.meta')) { throw 'Prepare a new explicitly owned certificate before serving signed candidates.' }
        $stage=Join-Path $Root ($Run+'-credentials.stage')
        Assert-NoLinks $stage
        if ([IO.Path]::Exists($stage)) { throw 'Preserve prior credential creation attempts.' }
        $null=[IO.Directory]::CreateDirectory($stage)
        $rsa=[Security.Cryptography.RSA]::Create(2048)
        try {
            $request=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=YL-HybridCLR-Loopback-Fixture',$rsa,
                [Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $names=[Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
            $names.AddIpAddress([Net.IPAddress]::Loopback)
            $request.CertificateExtensions.Add($names.Build())
            $certificate=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-10),[DateTimeOffset]::UtcNow.AddDays(2))
            try {
                $stagePfx=Join-Path $stage 'local-only.pfx'
                [IO.File]::WriteAllBytes($stagePfx,$certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx,'hclr-local-fixture'))
                $custody=@{ SchemaVersion=1; Purpose='YLFramework-HybridCLR-owned-loopback-fixture-v1'; Root=$Root
                    UnityVersion=$scope.ExpectedUnityVersion; UnityRevision=$scope.ExpectedUnityRevision; Receiver=$owner.receiver; OwnerSha256=$scope.OwnerSha256
                    CertificateSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificate.RawData)).ToLowerInvariant()
                    PfxSha256=(Get-FileHash -LiteralPath $stagePfx -Algorithm SHA256).Hash.ToLowerInvariant()
                    CreatedUtc=[DateTime]::UtcNow.ToString('o'); Run=$Run }
                [IO.File]::WriteAllText((Join-Path $stage 'owner.json'),($custody | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
            } finally { $certificate.Dispose() }
        } finally { $rsa.Dispose() }
        [IO.Directory]::Move($stage,$credentials)
    }
    if (-not [IO.File]::Exists($pfx) -or -not [IO.File]::Exists($custodyPath) -or [IO.Directory]::GetFileSystemEntries($credentials).Count -ne 2) {
        throw 'Refuse unknown, incomplete or foreign credential contents.'
    }
    $custody=[IO.File]::ReadAllText($custodyPath) | ConvertFrom-Json -AsHashtable
    $certificate=[Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12FromFile($pfx,'hclr-local-fixture',[Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    try {
        $certificateSha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificate.RawData)).ToLowerInvariant()
        $publicCertificate=[Convert]::ToBase64String($certificate.RawData)
        $pfxSha=(Get-FileHash -LiteralPath $pfx -Algorithm SHA256).Hash.ToLowerInvariant()
        Assert-HybridCLRFixtureCredentials $scope $custody $certificateSha $pfxSha
        if (-not $certificate.HasPrivateKey -or $certificate.NotBefore.ToUniversalTime() -gt [DateTime]::UtcNow -or $certificate.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow.AddMinutes(5)) {
            throw 'The exact isolated certificate must still be valid and own its private key.'
        }
    } finally { $certificate.Dispose() }
    if ($CertificateOnly) {
        [IO.File]::WriteAllText((Join-Path $Root ($Run+'.json')),(@{ Prepared=$true; CertificateOnly=$true; SystemTrustModified=$false
            UnityVersion=$scope.ExpectedUnityVersion; UnityRevision=$scope.ExpectedUnityRevision; Receiver=$owner.receiver; OwnerSha256=$scope.OwnerSha256
            CertificateSha256=$certificateSha; PfxSha256=$pfxSha } | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
        'Prepared exact receiver-owned loopback credentials; no system trust or secret output.'
        exit 0
    }
} elseif (-not $scope -and -not [IO.File]::Exists($pfx)) {
    if (-not (Test-Path -LiteralPath $Root)) { throw 'Missing explicit fixture parent.' }
    [IO.Directory]::CreateDirectory($credentials) | Out-Null
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=YL-HybridCLR-Loopback-Fixture', $rsa,
            [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $names = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        $names.AddIpAddress([Net.IPAddress]::Loopback)
        $request.CertificateExtensions.Add($names.Build())
        $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-10), [DateTimeOffset]::UtcNow.AddDays(2))
        try { [IO.File]::WriteAllBytes($pfx, $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, 'hclr-local-fixture')) }
        finally { $certificate.Dispose() }
    } finally { $rsa.Dispose() }
}
if (-not $scope -and $CertificateOnly) { 'Created/reused an owned isolated fixture certificate; no system trust or secret output.'; exit 0 }
$repository = [IO.Path]::GetFullPath($owner.repository)
if ($scope) {
    $node=(Get-Command node.exe -CommandType Application -ErrorAction Stop).Source
    $hostPath=Join-Path $PSScriptRoot 'HybridCLRFixtureHost.cjs'
    Assert-NoLinks $hostPath
    & $node --check $hostPath
    if ($LASTEXITCODE -ne 0) { throw 'The exact loopback Node host did not parse.' }
    if ($CompileOnly) {
        [IO.File]::WriteAllText((Join-Path $Root ($Run+'.json')),(@{ Compiled=$true; ServerStarted=$false; CertificateCreated=$false; Transport='Node-OpenSSL'
            UnityVersion=$scope.ExpectedUnityVersion; UnityRevision=$scope.ExpectedUnityRevision; Receiver=$owner.receiver; OwnerSha256=$scope.OwnerSha256
            HostSha256=(Get-FileHash -LiteralPath $hostPath -Algorithm SHA256).Hash.ToLowerInvariant() } | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
        'Parsed the Node/OpenSSL fixture without opening sockets or creating credentials.'
        exit 0
    }
    $publicationRoot=Join-Path $Root $Publication
    $ready=Join-Path $Root ($Publication+'-'+$ReadySuffix+'-ready.json')
    foreach ($path in @($publicationRoot,$ready,$ready+'.owner.json',$ready+'.stop',$ready+'.requests.jsonl',$ready+'.exit.json')) { Assert-NoLinks $path }
    foreach ($path in @($ready,$ready+'.owner.json',$ready+'.stop',$ready+'.requests.jsonl',$ready+'.exit.json')) {
        if ([IO.Path]::Exists($path)) { throw 'Preserve all prior ready, custody, request and stop evidence.' }
    }
    $envelope=Join-Path $publicationRoot 'release.json'
    Assert-NoLinks $envelope
    if (-not [IO.File]::Exists($envelope)) { throw 'Publish and sign this owned candidate before serving it.' }
    $binding=$custody.Clone()
    $binding.Publication=$Publication; $binding.ReadyPath=$ready; $binding.PreparedRun=$Run; $binding.Certificate=$publicCertificate
    $binding.Transport='Node-OpenSSL'; $binding.HostSha256=(Get-FileHash -LiteralPath $hostPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $binding.EnvelopeSha256=(Get-FileHash -LiteralPath $envelope -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText(($ready+'.owner.json'),($binding | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
    & $node $hostPath $Root $Publication $ReadySuffix $scope.OwnerSha256
    if ($LASTEXITCODE -ne 0) { throw 'Owned Node/OpenSSL fixture failed; preserve its invocation evidence.' }
    exit 0
}
$source = [IO.File]::ReadAllText((Join-Path $repository 'Assets/YLFramework/Tests/EditMode/Systems/Resource/ResourceTlsServer.cs')).Replace("`r`n", "`n")
$errorStatement = @'
        private void Record(Exception exception)
        {
            lock (_sync)
            {
                _errors.Add(exception);
            }
        }
'@
if (-not $source.Contains($errorStatement)) { throw 'The existing TLS fixture error surface changed; do not silently adapt it.' }
$loggedErrorStatement = @'
        private void Record(Exception exception)
        {
            lock (_sync)
            {
                _errors.Add(exception);
            }
            System.Console.Error.WriteLine("TLS fixture: " + exception);
        }
'@
$source = $source.Replace($errorStatement, $loggedErrorStatement)
$hostCode = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'HybridCLRFixtureHost.cs.txt'))
Add-Type -TypeDefinition ($source + [Environment]::NewLine + $hostCode) -Language CSharp
if ($CompileOnly) {
    'Compiled the existing legacy TLS fixture and acceptance host without starting a server.'
    exit 0
}
[YLFramework.Tests.Resource.HybridCLRFixtureHost]::Run([IO.Path]::GetFullPath($Root), $Publication, $ReadySuffix)
