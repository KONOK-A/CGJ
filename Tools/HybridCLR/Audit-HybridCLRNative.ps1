#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021', [switch]$RequireRelease, [string]$Receiver)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
if ($Receiver -or [IO.Path]::GetFullPath($Root).StartsWith('G:\Unity\U63-20261027-c63a1', [StringComparison]::OrdinalIgnoreCase)) { throw 'Legacy-only native audit has fixed historical runs, not the new compatibility matrix.' }
$receptionOwner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json -AsHashtable
if ($receptionOwner.ContainsKey('schemaVersion')) { throw 'Legacy-only native audit rejects versioned compatibility owners.' }
function Read-Receipt([string]$Name) { [IO.File]::ReadAllText((Join-Path $Root ($Name + '.json'))) | ConvertFrom-Json }
$owner = Read-Receipt 'owner'
if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-independent-reception') { throw 'Wrong native acceptance owner.' }
$base = Read-Receipt $owner.baseReceiptStem
$patch = Read-Receipt 'patch-receipt'
if (-not $base.passed -or -not $patch.passed -or $base.PlayerFingerprint -ne $patch.PlayerFingerprint -or $base.NativeBaselineSha256 -ne $patch.BaselineSha256) { throw 'Patch/base frozen identity check failed.' }
$positive = @('native-v1d','native-signed-update','native-v2-restart','native-corrupt-payload','native-missing-dll','native-partial-metadata')
foreach ($run in $positive) {
    $result = Read-Receipt $run
    $exit = Read-Receipt ($run + '.log.exit')
    if (-not $result.passed -or $result.simulation -or $exit.exitCode -ne 0 -or -not $exit.unchanged -or $exit.before -ne $exit.after) { throw "Actual native gate failed: $run" }
}
$update = Read-Receipt 'native-signed-update'
$v2 = Read-Receipt 'native-v2-restart'
if (-not $update.checkAndPrepareWhileRunning -or -not $update.activateBusy -or -not $update.sameProcessReplacementRejected -or
    $update.activatedCatalogDigest -ne $patch.CatalogDigest -or $v2.catalogDigest -ne $patch.CatalogDigest -or $v2.codePackageId -ne $patch.CodePackageId -or
    $v2.message -notmatch 'Native: v2:.*async=v2-async.*prefab=v2-prefab') { throw 'Real same-Player v1→v2 state transition proof is incomplete.' }
$partial = Read-Receipt 'native-partial-metadata'
if (-not $partial.partialMetadataRestartRequired -or $partial.nativeErrorCode -eq 0 -or $partial.assembly -ne 'zzAbsentAot') { throw 'Native partial-load restart proof missing.' }
$rejection = Read-Receipt 'reject-new-native-requirement'
if (-not $rejection.passed -or -not $rejection.newRequirementRejected -or $rejection.unchangedPlayer -ne $base.PlayerFingerprint -or $rejection.unchangedBaseline -ne $base.NativeBaselineSha256) { throw 'New-native-requirement rejection modified its base.' }
$signature = Read-Receipt 'native-bad-signature'
$signatureExit = Read-Receipt 'native-bad-signature.log.exit'
if ($signature.passed -or $signatureExit.exitCode -ne 21 -or -not $signatureExit.unchanged -or $signature.error -notmatch 'Release signature rejected') { throw 'The actual negative signature case did not reject for the expected reason.' }
if ((Get-FileHash -LiteralPath (Join-Path $base.Baseline 'baseline.json') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $base.NativeBaselineSha256) { throw 'The actual immutable Development baseline changed after validation.' }
$releaseProof = $null
if ($RequireRelease) {
    $release = Read-Receipt 'base-release-receipt'
    $run = Read-Receipt 'native-release-v2'
    $exit = Read-Receipt 'native-release-v2.log.exit'
    if (-not $release.passed -or $release.Development -or -not $run.passed -or $run.simulation -or $exit.exitCode -ne 0 -or -not $exit.unchanged -or $run.message -notmatch 'Native: v2:.*async=v2-async.*prefab=v2-prefab') { throw 'Independent actual Release gate incomplete.' }
    $releaseProof = @{ catalogDigest=$release.CatalogDigest; codePackageId=$release.CodePackageId; baseline=$release.Baseline }
}
$name = if ($RequireRelease) { 'native-final-audit' } else { 'native-development-audit' }
$output = Join-Path $Root ($name + '.json')
if ([IO.File]::Exists($output)) { throw 'Preserve prior aggregate evidence.' }
[IO.File]::WriteAllText($output, (@{ passed=$true; simulation=$false; baseCatalog=$base.CatalogDigest; patchCatalog=$patch.CatalogDigest;
    baseCodePackageId=$base.CodePackageId; patchCodePackageId=$patch.CodePackageId; playerFingerprint=$base.PlayerFingerprint;
    baselineSha256=$base.NativeBaselineSha256; signedHttps=$true; activeBusy=$true; processRestart=$true; corruptAndMissingRejected=$true;
    signatureRejected=$true; realMetadataErrorCode=$partial.nativeErrorCode; newNativeRequirementRejected=$true; release=$releaseProof; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json -Depth 10))
"Actual native acceptance evidence audited; releaseRequired=$RequireRelease; receipt=$output"
