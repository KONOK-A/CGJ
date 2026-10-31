#requires -Version 7.6
[CmdletBinding()]
param(
    [string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021',
    [string]$Delivery = 'G:\Unity\H26Delivery',
    [string]$Receiver
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
if ($Receiver -or [IO.Path]::GetFullPath($Root).StartsWith('G:\Unity\U63-20261027-c63a1', [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFullPath($Delivery).StartsWith('G:\Unity\U63-20261027-c63a1', [StringComparison]::OrdinalIgnoreCase)) { throw 'Legacy-only freeze has fixed runs and Plan; it cannot freeze Unity 6.3 compatibility evidence.' }
$receptionOwner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json -AsHashtable
if ($receptionOwner.ContainsKey('schemaVersion')) { throw 'Legacy-only freeze rejects versioned compatibility owners.' }
$owner = [IO.File]::ReadAllText((Join-Path $Delivery 'owner.json')) | ConvertFrom-Json
if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-final-delivery' -or -not (Test-Path -LiteralPath $Delivery)) { throw 'Explicit persistent delivery ownership required.' }
$native = [IO.File]::ReadAllText((Join-Path $Root 'native-final-audit.json')) | ConvertFrom-Json
if (-not $native.passed -or $null -eq $native.release) { throw 'Audit all real native gates before freezing handoff evidence.' }
$destination = Join-Path $Delivery 'Evidence'
if ([IO.Path]::Exists($destination) -or [IO.Path]::Exists((Join-Path $Delivery 'Tools'))) { throw 'Never overwrite a frozen handoff.' }
[IO.Directory]::CreateDirectory($destination) | Out-Null
$records = [Collections.Generic.List[object]]::new()
function Copy-Recorded([string]$Source, [string]$Relative) {
    if (-not [IO.File]::Exists($Source) -or ([IO.File]::GetAttributes($Source) -band [IO.FileAttributes]::ReparsePoint)) { throw "Missing or linked explicit evidence: $Source" }
    if ([IO.Path]::GetExtension($Source) -in @('.pfx','.pem','.key')) { throw 'Private credential material never belongs to handoff evidence.' }
    $target = Join-Path $Delivery $Relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    [IO.File]::Copy($Source,$target,$false)
    $hash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Frozen evidence bytes changed during copy.' }
    $records.Add(@{ path=$Relative; length=[IO.FileInfo]::new($target).Length; sha256=$hash })
}
foreach ($name in @('owner','base-v1d-receipt','base-release-receipt','patch-receipt','native-final-audit','native-development-audit',
    'reject-new-native-requirement','bad-signature-fixture','publish-bad-payload-fixture','publish-missing-dll-fixture','bad-metadata-fixture',
    'compile-external-v2','restore-external-v2-release','compile-new-native-requirement','sync-optional-r8-mvid.receipt')) {
    Copy-Recorded (Join-Path $Root ($name + '.json')) ('Evidence/' + $name + '.json')
}
foreach ($run in @('native-v1d','native-signed-update','native-v2-restart','native-corrupt-payload','native-missing-dll','native-partial-metadata','native-bad-signature','native-release-v2',
    'build-native-dev-pe-mvid','build-native-release','publish-real-v2-patch')) {
    foreach ($extension in @('.json','.log','.log.exit.json')) { Copy-Recorded (Join-Path $Root ($run + $extension)) ('Evidence/' + $run + $extension) }
}
foreach ($name in @('final-base-product-only.receipt.json','final-optional-product-only.receipt.json','final-supported-import.json','final-resolve-official.log')) {
    Copy-Recorded (Join-Path $Root ('FinalVerified/' + $name)) ('Evidence/FinalImport/' + $name)
}
foreach ($file in [IO.Directory]::GetFiles($PSScriptRoot)) { Copy-Recorded $file ('Tools/HybridCLR/' + [IO.Path]::GetFileName($file)) }
Copy-Recorded (Join-Path $owner.repository '.kilo/plans/1792591409867-xf-hybridclr-migration.md') 'Evidence/1792591409867-xf-hybridclr-migration.md'
$packages = @('YLFramework.unitypackage','YLFramework-HybridCLR.unitypackage') | ForEach-Object {
    $path = Join-Path $Delivery $_
    if (-not [IO.File]::Exists($path)) { throw 'Export both formal products before freezing.' }
    @{ path=$_; length=[IO.FileInfo]::new($path).Length; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$finalImport = [IO.File]::ReadAllText((Join-Path $Root 'FinalVerified/final-supported-import.json')) | ConvertFrom-Json
if (-not $finalImport.passed -or $packages[0].sha256 -ne $finalImport.defaultPackageSha256.ToLowerInvariant() -or $packages[1].sha256 -ne $finalImport.optionalPackageSha256.ToLowerInvariant()) { throw 'Persistent packages differ from actual final imported artifacts.' }
$manifest = @{ passed=$true; packages=$packages; evidence=$records; native=$native; privateKeysIncluded=$false; noCommitOrPush=$true; utc=[DateTime]::UtcNow.ToString('o') }
[IO.File]::WriteAllText((Join-Path $Delivery 'delivery.json'), ($manifest | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
"Frozen persistent handoff: packages=$($packages.Count), explicit evidence/tool files=$($records.Count); no private key copied; $Delivery"
