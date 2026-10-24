#requires -Version 7.6
[CmdletBinding()]
param(
    [ValidateSet('ServerV2','ServerBad','ServerMissing','ServerBadSignature')][string]$Publication = 'ServerV2',
    [string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021',
    [ValidatePattern('^[A-Za-z0-9-]*$')][string]$ReadySuffix = '',
    [switch]$CertificateOnly,
    [switch]$CompileOnly
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$owner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json
if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-independent-reception') { throw 'Unexpected acceptance fixture owner.' }
$credentials = Join-Path $Root 'TestCredentials'
$pfx = Join-Path $credentials 'local-only.pfx'
if (-not [IO.File]::Exists($pfx)) {
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
if ($CertificateOnly) { 'Created/reused an owned isolated fixture certificate; no system trust or secret output.'; exit 0 }
$repository = [IO.Path]::GetFullPath($owner.repository)
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
if ($CompileOnly) { 'Compiled the existing TLS fixture and acceptance host without starting a server.'; exit 0 }
[YLFramework.Tests.Resource.HybridCLRFixtureHost]::Run([IO.Path]::GetFullPath($Root), $Publication, $ReadySuffix)
