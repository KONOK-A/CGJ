#requires -Version 7.6
[CmdletBinding()]
param([Parameter(Mandatory)][string] $EvidenceRoot, [string] $CarrierRoot, [switch] $Sync)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$root = [IO.Path]::GetFullPath($EvidenceRoot)
$carrier = if ($Sync) { (Get-Content -LiteralPath (Join-Path $root 'carrier-location.json') -Raw | ConvertFrom-Json).Carrier } elseif ($CarrierRoot) { [IO.Path]::GetFullPath($CarrierRoot) } else { Join-Path $root 'Carrier' }
$approvedTemp = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'kilo'))
if (-not $root.StartsWith($approvedTemp + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a private child of the approved kilo temporary root.' }
function Reject-Link([string] $Path) {
    for ($cursor = $Path; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ([IO.Path]::Exists($cursor) -and ([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked path rejected: $cursor" }
    }
}
function Hash([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Json([string] $Path, $Value) { [IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $Value -Depth 40), [Text.UTF8Encoding]::new($false)) }
Reject-Link $root
Reject-Link $carrier
if (-not $carrier.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
    -not $carrier.StartsWith((Join-Path $repo 'Temp') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Carrier must be this evidence child or a new repository Temp child.' }
if (-not (Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($root)) -PathType Container)) { throw 'Evidence parent must already exist.' }
if (-not (Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($carrier)) -PathType Container) -and $CarrierRoot) { throw 'Explicit carrier parent must already exist.' }
if ($Sync) {
    $owner = Get-Content -LiteralPath (Join-Path $carrier 'r810-owner.json') -Raw | ConvertFrom-Json
    if ($owner.Carrier -cne $carrier -or $owner.Repository -cne $repo -or $owner.Purpose -cne 'Resource-8.10-owned-carrier') { throw 'Carrier owner mismatch.' }
    foreach ($row in (Get-Content -LiteralPath (Join-Path $carrier 'source-identity.json') -Raw | ConvertFrom-Json)) {
        if ((Hash (Join-Path $carrier $row.Path)) -cne $row.Sha256) { throw "Carrier source changed outside this synchronization: $($row.Path)" }
    }
} else {
    if ((Test-Path -LiteralPath $root) -or (Test-Path -LiteralPath $carrier)) { throw 'Never adopt or overwrite existing evidence/carrier.' }
    $null = [IO.Directory]::CreateDirectory($root)
    $null = [IO.Directory]::CreateDirectory($carrier)
    $null = [IO.Directory]::CreateDirectory((Join-Path $root 'Runs'))
    Json (Join-Path $carrier 'r810-owner.json') ([ordered]@{ Purpose='Resource-8.10-owned-carrier'; Repository=$repo; Carrier=$carrier; Evidence=$root; CreatedUtc=[DateTime]::UtcNow.ToString('O') })
    Json (Join-Path $root 'carrier-location.json') @{ Carrier=$carrier }
}
$formalRoots = @('Runtime','Editor','Plugins','Prefabs','Samples','Documentation')
$sourcePaths = [Collections.Generic.List[string]]::new()
foreach ($leaf in $formalRoots) {
    $relative = 'Assets/YLFramework/' + $leaf
    $sourcePaths.Add($relative + '.meta')
    foreach ($item in Get-ChildItem -LiteralPath (Join-Path $repo $relative) -Recurse -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked formal source rejected.' }
        if (-not $item.PSIsContainer) { $sourcePaths.Add([IO.Path]::GetRelativePath($repo, $item.FullName).Replace('\','/')) }
    }
}
foreach ($path in @('Assets/YLFramework.meta','Assets/YLFramework/LICENSE.md','Assets/YLFramework/LICENSE.md.meta')) { $sourcePaths.Add($path) }
$identity = [Collections.Generic.List[object]]::new()
foreach ($relative in ($sourcePaths | Sort-Object -CaseSensitive)) {
    $source = Join-Path $repo $relative
    $before = Hash $source
    $destination = Join-Path $carrier $relative
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
    [IO.File]::Copy($source, $destination, $Sync.IsPresent)
    if ((Hash $destination) -cne $before -or (Hash $source) -cne $before) { throw 'Concurrent formal source edit; retain incomplete evidence and do not build.' }
    $identity.Add([ordered]@{ Path=$relative; Length=([IO.FileInfo]$source).Length; Sha256=$before })
}
foreach ($template in @('ResourcePlayerAcceptance','ResourcePlayerAcceptanceBuild')) {
    $relative = if ($template.EndsWith('Build')) { "Assets/R810/Editor/$template.cs" } else { "Assets/R810/$template.cs" }
    $destination = Join-Path $carrier $relative
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
    $source = Join-Path $PSScriptRoot "$template.cs.txt"
    [IO.File]::Copy($source, $destination, $Sync.IsPresent)
    $identity.Add([ordered]@{ Path=$relative; Length=([IO.FileInfo]$source).Length; Sha256=(Hash $source) })
}
$bc = $identity | Where-Object Path -CEQ 'Assets/YLFramework/Plugins/BouncyCastle/BouncyCastle.Cryptography.dll'
if ($bc.Sha256 -cne 'd61c1f2ba929a230a58e101ccd850e21f2675fa6b9814ec279633e8a089c3495') { throw 'Approved BC binary identity mismatch.' }
Json (Join-Path $carrier 'source-identity.json') $identity.ToArray()
if (-not $Sync) {
    $null = [IO.Directory]::CreateDirectory((Join-Path $carrier 'Packages'))
    $null = [IO.Directory]::CreateDirectory((Join-Path $carrier 'ProjectSettings'))
    [IO.File]::Copy((Join-Path $repo 'ProjectSettings/ProjectVersion.txt'), (Join-Path $carrier 'ProjectSettings/ProjectVersion.txt'))
    $dependencies = [ordered]@{ 'com.unity.ugui'='1.0.0'; 'com.unity.nuget.newtonsoft-json'='3.2.1' }
    foreach ($module in @('ai','animation','assetbundle','audio','cloth','director','imageconversion','imgui','jsonserialize','particlesystem','physics','physics2d','screencapture','subsystems','terrain','terrainphysics','tilemap','ui','uielements','umbra','unityanalytics','unitywebrequest','unitywebrequestassetbundle','unitywebrequestaudio','unitywebrequesttexture','unitywebrequestwww','vehicles','video','vr','wind','xr')) { $dependencies['com.unity.modules.'+$module]='1.0.0' }
    Json (Join-Path $carrier 'Packages/manifest.json') @{ dependencies=$dependencies }
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $san = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        $san.AddDnsName('localhost'); $san.AddIpAddress([Net.IPAddress]::Loopback)
        $request.CertificateExtensions.Add($san.Build())
        $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(3))
        try {
            [IO.File]::WriteAllBytes((Join-Path $root 'loopback.pfx'), $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx,'r810-loopback-only'))
            $public = $rsa.ExportParameters($false)
            Json (Join-Path $root 'public-config.json') @{ Endpoint='https://localhost:18443/'; Certificate=[Convert]::ToBase64String($certificate.RawData); SigningKey=@{ Modulus=[Convert]::ToBase64String($public.Modulus); Exponent=[Convert]::ToBase64String($public.Exponent) } }
        } finally { $certificate.Dispose() }
    } finally { $rsa.Dispose() }
    $protected = @('Assets/Games','Assets/Scenes','Assets/StreamingAssets','Packages','ProjectSettings','.gitignore','.kilo/kilo.jsonc','AGENTS.md','.kilo/plans/1791960935353-ui-panel-redesign.md','Assets/YLFramework/Tests/Acceptance/Core','Assets/YLFramework/Tests/Fixtures/WorkbenchUX1-20261017')
    $rows = @(foreach ($relative in $protected) {
        $path = Join-Path $repo $relative
        if (-not (Test-Path -LiteralPath $path)) { continue }
        $files = if ([IO.File]::Exists($path)) { @($path) } else { @(Get-ChildItem -LiteralPath $path -File -Recurse -Force | ForEach-Object FullName) }
        foreach ($file in $files) { [ordered]@{ Path=[IO.Path]::GetRelativePath($repo,$file); Sha256=(Hash $file) } }
    })
    Json (Join-Path $root 'protected-before.json') $rows
}
Json (Join-Path $root ('source-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '.json')) $identity.ToArray()
[pscustomobject]@{ Carrier=$carrier; Evidence=$root; FormalFiles=$sourcePaths.Count; Sync=$Sync.IsPresent }
