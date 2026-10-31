#requires -Version 7.6
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Root,
    [string]$Receiver,
    [string]$Unity,
    [string]$ExpectedUnityVersion,
    [string]$ExpectedUnityRevision,
    [string]$UguiVersion,
    [string]$TestFrameworkVersion,
    [switch]$GuardSimulation
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
function Assert-NoLinks([string]$Path) {
    for ($cursor = [IO.Path]::GetFullPath($Path); $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        try { $attributes = [IO.File]::GetAttributes($cursor) }
        catch [IO.FileNotFoundException] { continue }
        catch [IO.DirectoryNotFoundException] { continue }
        if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked receiver paths are not supported: $cursor" }
    }
}
function Assert-HybridCLRSubsetNativeBinding($Owner, $Base, $Patch) {
    if (-not $Owner.ContainsKey('nativeSourceManifest') -and -not $Owner.ContainsKey('nativeSourceManifestSha256')) { return }
    if (-not $Owner.ContainsKey('nativeSourceManifest') -or -not $Owner.ContainsKey('nativeSourceManifestSha256') -or
        $Owner.nativeSourceManifestSha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'Subset native: explicit immutable source pair required.' }
    foreach ($record in @($Base,$Patch)) {
        $keys=if ($record -is [Collections.IDictionary]) { @($record.Keys) } else { @($record.PSObject.Properties.Name) }
        if ($keys -cnotcontains 'NativeVariant' -or $keys -cnotcontains 'NativeSourceManifestSha256' -or
            $record.NativeVariant -cne 'ParameterOwner' -or $record.NativeSourceManifestSha256 -cne $Owner.nativeSourceManifestSha256) {
            throw 'Subset native: both subset receipts must bind the exact maintained source and variant.'
        }
    }
}
function Assert-HybridCLRExplicitPath([string]$Path) {
    if ($Path -notmatch '^[A-Za-z]:[\\/]' -or -not [IO.Path]::IsPathFullyQualified($Path) -or $Path.Split([char[]]'\/') -contains '.' -or
        $Path.Split([char[]]'\/') -contains '..' -or $Path -match '[*?\[\]]|::' -or $Path.Substring(2).Contains(':')) {
        throw "Expected an exact filesystem path without aliases: $Path"
    }
    Assert-NoLinks $Path
}
function Test-HybridCLRNativeOwnerBinding($Owner, [string]$DeliveryRoot) {
    $count=@('nativeSourceManifest','nativeSourceManifestSha256' | Where-Object { $Owner.ContainsKey($_) }).Count
    if ($count -eq 0) { return $false }
    if ($count -ne 2 -or $Owner.expectedUnityVersion -cne '6000.3.27f1' -or $Owner.nativeSourceManifestSha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw 'Native owner: the exact engine and both immutable source bindings are required.'
    }
    $path=[string]$Owner.nativeSourceManifest
    $prefix=[IO.Path]::GetFullPath((Join-Path $DeliveryRoot 'B08Native'))+[IO.Path]::DirectorySeparatorChar
    if (-not [IO.Path]::IsPathFullyQualified($path) -or $path.Split([char[]]'\/') -contains '..' -or $path.Split([char[]]'\/') -contains '.' -or
        -not [IO.Path]::GetFullPath($path).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Native owner: source manifest must belong to the independent maintained-source root.' }
    return $true
}
function Assert-HybridCLRNewEvidence($Scope, [string]$Run, [string[]]$Extensions = @('.log','.json','.xml','.log.process.json','.log.exit.json','.receipt.json')) {
    if ($Run -cnotmatch '^[A-Za-z0-9-]+$') { throw 'Use one exact unused run name.' }
    foreach ($extension in $Extensions) {
        $path = Join-Path $Scope.Root ($Run + $extension)
        Assert-NoLinks $path
        if ([IO.Path]::Exists($path)) { throw "Preserve prior or unknown invocation output: $path" }
    }
}
function Assert-HybridCLRReady($Scope, [switch]$Creating, [switch]$Initializing) {
    if ($Scope.IsSimulation) { throw 'A pure guard simulation never authorizes engine or filesystem operations.' }
    Assert-NoLinks $Scope.Unity
    if (-not [IO.File]::Exists($Scope.Unity) -or
        [Diagnostics.FileVersionInfo]::GetVersionInfo($Scope.Unity).ProductVersion -cne ($Scope.ExpectedUnityVersion + '_' + $Scope.ExpectedUnityRevision)) {
        throw 'Unity executable version/revision differs from the explicit binding.'
    }
    if ($Scope.IsCompatibility -and -not $Scope.Bound -and -not $Initializing) { throw 'Initialize the new descriptor before using its receiver.' }
    if (-not $Creating) {
        $versionPath = Join-Path $Scope.Project 'ProjectSettings/ProjectVersion.txt'
        Assert-NoLinks $versionPath
        $version = [IO.File]::ReadAllText($versionPath)
        if ($version -cnotmatch ('(?m)^m_EditorVersion: ' + [regex]::Escape($Scope.ExpectedUnityVersion) + '\r?$') -or
            $version -cnotmatch ('(?m)^m_EditorVersionWithRevision: ' + [regex]::Escape($Scope.ExpectedUnityVersion + ' (' + $Scope.ExpectedUnityRevision + ')') + '\r?$')) {
            throw 'Receiver ProjectVersion does not match its engine/revision binding.'
        }
        if ($Scope.IsCompatibility) {
            $manifestPath = Join-Path $Scope.Project 'Packages/manifest.json'
            Assert-NoLinks $manifestPath
            $dependencies = ([IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -AsHashtable).dependencies
            if ($dependencies['com.unity.ugui'] -cne $Scope.UguiVersion -or
                ($dependencies.ContainsKey('com.unity.test-framework') -and $dependencies['com.unity.test-framework'] -cne $Scope.TestFrameworkVersion)) {
                throw 'Receiver UGUI/UTF differs from its fixed engine mapping.'
            }
        }
    }
}
function Assert-HybridCLRReceiptBinding($Scope, $Receipt) {
    if (-not $Scope.IsCompatibility) { return }
    if ($Receipt.UnityVersion -cne $Scope.ExpectedUnityVersion -or $Receipt.UnityRevision -cne $Scope.ExpectedUnityRevision -or
        $Receipt.Receiver -cne $Scope.Owner.receiver -or $Receipt.OwnerSha256 -cne $Scope.OwnerSha256) {
        throw 'Cross-engine or foreign-owner receipt is not a usable baseline.'
    }
}
function Assert-HybridCLRTestScene($Scope, $Receipt, [string]$Run) {
    Assert-HybridCLRReceiptBinding $Scope $Receipt
    if ($Run -cnotmatch '^[A-Za-z0-9-]+$') { throw 'An exact owned test run is required.' }
    $asset = 'Assets/HybridCLRTestsBootstrap-' + $Run + '.unity'
    $path = Join-Path $Scope.Project $asset
    $meta = $path + '.meta'
    Assert-NoLinks $path
    Assert-NoLinks $meta
    $guids = [regex]::Matches([IO.File]::ReadAllText($meta), '(?m)^guid: ([0-9a-f]{32})\r?$')
    if ($Receipt.Purpose -cne 'YLFramework-HybridCLR-owned-test-scene-v1' -or $Receipt.Project -cne $Scope.Project -or
        $Receipt.Run -cne $Run -or $Receipt.Scene -cne $asset -or $guids.Count -ne 1 -or
        $Receipt.SceneGuid -cne $guids[0].Groups[1].Value -or
        $Receipt.SceneSha256 -cne (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -or
        $Receipt.SceneMetaSha256 -cne (Get-FileHash -LiteralPath $meta -Algorithm SHA256).Hash.ToLowerInvariant()) {
        throw 'The saved runner host must retain its exact owner, run, scene bytes and actual meta identity.'
    }
}
function Assert-HybridCLRFixtureCredentials($Scope, $Receipt, [string]$CertificateSha256, [string]$PfxSha256) {
    Assert-HybridCLRReceiptBinding $Scope $Receipt
    if (($Receipt.SchemaVersion -isnot [int] -and $Receipt.SchemaVersion -isnot [long]) -or $Receipt.SchemaVersion -ne 1 -or
        $Receipt.Purpose -cne 'YLFramework-HybridCLR-owned-loopback-fixture-v1' -or $Receipt.Root -cne $Scope.Root -or
        $CertificateSha256 -cnotmatch '^[0-9a-f]{64}$' -or $PfxSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $Receipt.CertificateSha256 -cne $CertificateSha256 -or $Receipt.PfxSha256 -cne $PfxSha256) {
        throw 'The isolated TLS certificate and private-key file must match this exact receiver custody receipt.'
    }
}
function Assert-HybridCLRFixtureTrust($Scope, $Ready, $Custody, [string]$ReadyPath, [string]$CertificateSha256, [string]$PfxSha256, [string]$EnvelopeSha256) {
    Assert-HybridCLRFixtureCredentials $Scope $Custody $CertificateSha256 $PfxSha256
    [uri]$endpoint=$null
    if ($Custody.ReadyPath -cne $ReadyPath -or $Custody.Publication -cnotin @('ServerV2','ServerBad','ServerMissing','ServerBadSignature') -or
        $EnvelopeSha256 -cnotmatch '^[0-9a-f]{64}$' -or $Custody.EnvelopeSha256 -cne $EnvelopeSha256 -or
        -not [uri]::TryCreate([string]$Ready.endpoint,[UriKind]::Absolute,[ref]$endpoint) -or $endpoint.Scheme -cne 'https' -or
        $endpoint.Host -cne '127.0.0.1' -or $endpoint.AbsolutePath -cne '/' -or $endpoint.Query -or $endpoint.Fragment -or $endpoint.UserInfo) {
        throw 'TLS fixture trust must bind the exact owned ready path, signed envelope and HTTPS loopback endpoint.'
    }
    $raw=[Convert]::FromBase64String($Ready.certificate)
    if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($raw)).ToLowerInvariant() -cne $CertificateSha256) { throw 'Foreign fixture certificate.' }
    $certificate=[Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificate($raw)
    try {
        if ($certificate.NotBefore.ToUniversalTime() -gt [DateTime]::UtcNow -or $certificate.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow) { throw 'Expired fixture certificate.' }
        $rsa=[Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($certificate)
        if ($null -eq $rsa) { throw 'An exact RSA fixture key is required.' }
        try {
            $key=$rsa.ExportParameters($false)
            if ($Ready.modulus -cne [Convert]::ToBase64String($key.Modulus) -or $Ready.exponent -cne [Convert]::ToBase64String($key.Exponent)) {
                throw 'Fixture release trust key differs from the owned TLS certificate.'
            }
        } finally { $rsa.Dispose() }
    } finally { $certificate.Dispose() }
}
function Assert-HybridCLRMetadataFixture($Scope, $Candidate, $Base, $Proof, [string]$CandidatePath, [string]$CandidateSha256) {
    Assert-HybridCLRReceiptBinding $Scope $Proof
    Assert-HybridCLRExplicitPath $Candidate.DirectoryPath
    $directory=[IO.Path]::GetFullPath($Candidate.DirectoryPath)
    $publicationRoot=[IO.Path]::GetFullPath((Join-Path $Scope.Project 'AssetBundles'))+[IO.Path]::DirectorySeparatorChar
    if ($Proof.Purpose -cne 'YLFramework-HybridCLR-owned-negative-metadata-v1' -or $Proof.CandidatePath -cne $CandidatePath -or
        $CandidatePath -cne (Join-Path $Scope.Root 'bad-metadata-fixture.json') -or $CandidateSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $Proof.CandidateSha256 -cne $CandidateSha256 -or -not $Candidate.passed -or -not $Candidate.negativeFixtureOnly -or
        $Candidate.ProjectId -cne $Base.ProjectId -or $Candidate.CompatibilityId -cne $Base.CompatibilityId -or
        $Candidate.CatalogDigest -cnotmatch '^[0-9a-f]{64}$' -or [IO.Path]::GetFileName($directory) -cne $Candidate.CatalogDigest -or
        -not $directory.StartsWith($publicationRoot,[StringComparison]::OrdinalIgnoreCase)) {
        throw 'The negative metadata candidate must match its exact owner, producer bytes, base identity and owned publication.'
    }
}
function Assert-HybridCLRSignatureMutation($Before, $After) {
    foreach ($name in @('SchemaVersion','Algorithm','KeyId','Payload')) {
        if ($Before.$name -cne $After.$name) { throw 'A signature-only negative cannot alter signed payload, key or algorithm fields.' }
    }
    $original=[Convert]::FromBase64String($Before.Signature)
    $changed=[Convert]::FromBase64String($After.Signature)
    if ($original.Length -lt 256 -or $original.Length -gt 1024 -or $changed.Length -ne $original.Length) { throw 'Keep the exact bounded RSA signature length.' }
    $different=0
    for ($index=0; $index -lt $original.Length; $index++) {
        $xor=[int]$original[$index] -bxor [int]$changed[$index]
        if ($xor -ne 0) {
            if (($xor -band ($xor-1)) -ne 0) { throw 'The signature fixture must flip exactly one bit.' }
            $different++
        }
    }
    if ($different -ne 1) { throw 'The signature fixture must flip exactly one bit, not zero or several bytes.' }
}
function Assert-HybridCLRProductImports($Scope) {
    foreach ($name in @($Scope.BaseImportRun, $Scope.OptionalImportRun)) {
        $path = Join-Path $Scope.Root ($name + '.receipt.json')
        Assert-NoLinks $path
        $receipt = [IO.File]::ReadAllText($path) | ConvertFrom-Json
        if (-not $receipt.passed -or $receipt.testsPresent) { throw 'Product-only import must precede acceptance consumers.' }
        Assert-HybridCLRReceiptBinding $Scope $receipt
    }
}
function Convert-HybridCLRTemplate($Scope, [string]$Name) {
    if ($Name -notin @('HybridCLRReceptionBootstrap.cs.txt','HybridCLRReceptionRunner.cs.txt','HybridCLRProjectSubsetProbe.cs.txt','HybridCLROwnedRunnerSceneTests.cs.txt')) { throw 'Unknown reception template.' }
    $path = Join-Path $PSScriptRoot $Name
    Assert-NoLinks $path
    $text = [IO.File]::ReadAllText($path)
    $values = @{
        SOURCE_REPOSITORY=$Scope.Repository; EXPECTED_UNITY_VERSION=$Scope.ExpectedUnityVersion
        EXPECTED_UNITY_REVISION=$Scope.ExpectedUnityRevision; EXPECTED_ROOT=$Scope.Root
        OWNER_SHA256=$(if ($Scope.IsCompatibility) { $Scope.OwnerSha256 } else { '' })
        SUBSET_PLAYER=$Scope.SubsetPlayer; SUBSET_CACHE=$Scope.SubsetCache; SUBSET_COMPATIBILITY=$Scope.SubsetCompatibility
    }
    foreach ($key in $values.Keys) {
        $text = $text.Replace(('"__YL_HYBRIDCLR_' + $key + '__"'), (ConvertTo-Json -InputObject ([string]$values[$key]) -Compress))
    }
    if ($text.Contains('__YL_HYBRIDCLR_')) { throw 'Unbound reception template marker.' }
    return $text
}
function Assert-HybridCLRProcessResult([bool]$IsCompatibility, [int]$ExitCode, [bool]$ForcedCleanup, [bool]$KeepOpen, [bool]$ArtifactPassed) {
    if ($IsCompatibility) {
        if ($ExitCode -ne 0 -or $ForcedCleanup -or ($KeepOpen -and -not $ArtifactPassed)) {
            throw 'The new receiver requires normal Unity exit and every requested callback artifact; forced cleanup is never a pass.'
        }
        return
    }
    if ($ExitCode -ne 0 -and -not ($KeepOpen -and $ArtifactPassed -and $ForcedCleanup)) {
        throw "Legacy Unity process exited $ExitCode."
    }
    if ($ForcedCleanup -and -not ($KeepOpen -and $ArtifactPassed)) {
        throw 'Unity exceeded its bounded window; forced cleanup is not a pass.'
    }
}
function Assert-HybridCLRTestResult([int]$ExitCode, [xml]$Xml) {
    $summary = $Xml.'test-run'
    foreach ($name in @('total','passed','failed','skipped')) {
        if ([string]$summary.$name -cnotmatch '^(0|[1-9][0-9]*)$') { throw "Invalid actual NUnit counter: $name" }
    }
    if ($ExitCode -ne 0 -or $summary.result -cne 'Passed' -or [long]$summary.total -le 0 -or
        [long]$summary.failed -ne 0 -or [long]$summary.skipped -ne 0 -or [long]$summary.passed -ne [long]$summary.total) {
        throw 'Actual NUnit XML and normal process exit must both pass with no skipped tests.'
    }
}
function Assert-HybridCLRNormalProcessExit($Scope, [string]$Run) {
    if (-not $Scope.IsCompatibility -or $Run -cnotmatch '^[A-Za-z0-9-]+$') { throw 'Exact new receiver process evidence required.' }
    $path=Join-Path $Scope.Root ($Run+'.log.exit.json')
    Assert-NoLinks $path
    $fact=[IO.File]::ReadAllText($path) | ConvertFrom-Json -AsHashtable
    if ($fact.schemaVersion -ne 1 -or $fact.purpose -cne 'YLFramework-HybridCLR-owned-process-v1' -or
        $fact.run -cne $Run -or $fact.project -cne $Scope.Project -or $fact.ownerSha256 -cne $Scope.OwnerSha256 -or
        $fact.exe -ine $Scope.Unity -or $fact.log -ine (Join-Path $Scope.Root ($Run+'.log')) -or
        $fact.terminalStatus -cne 'Exited' -or $fact.observedExit -isnot [bool] -or -not $fact.observedExit -or
        ($fact.exitCode -isnot [int] -and $fact.exitCode -isnot [long]) -or $fact.exitCode -ne 0 -or
        $fact.editorForcedCleanup -isnot [bool] -or $fact.editorForcedCleanup -or
        $fact.timedOut -isnot [bool] -or $fact.timedOut -or $fact.alive -ne $false -or
        $null -ne $fact.observerError -or $null -ne $fact.cleanupError -or $fact.succeeded -isnot [bool] -or -not $fact.succeeded) {
        throw 'Only an observed normal exit0 without cleanup, cancellation or observer failure can authorize discovery consumption.'
    }
    return $fact
}
function Select-HybridCLRTests($Scope, $Discovery, [string]$TestPlatform, [string]$Filter) {
    if ($Discovery -isnot [Collections.IDictionary] -or $Discovery.Passed -isnot [bool] -or -not $Discovery.Passed -or
        $Discovery.Project -cne $Scope.Project) { throw 'Actual owned HybridCLR discovery required.' }
    Assert-HybridCLRReceiptBinding $Scope $Discovery
    foreach ($mode in @('EditMode','PlayMode')) {
        $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        if (-not $Discovery.Contains($mode) -or @($Discovery[$mode]).Count -eq 0) { throw 'Both actual HybridCLR test assemblies must be discovered.' }
        foreach ($row in $Discovery[$mode]) {
            if ([string]::IsNullOrWhiteSpace($row.Name) -or $row.Assembly -cne ('YLFramework.HybridCLR.Tests.'+$mode) -or
                -not $names.Add($row.Name)) { throw 'Invalid or duplicate discovered HybridCLR test identity.' }
        }
    }
    $selected=@($Discovery[$TestPlatform] | Where-Object { [regex]::IsMatch($_.Name,$Filter) })
    if ($selected.Count -eq 0 -or @($selected | Where-Object { $_.RunState -cne 'Runnable' }).Count) {
        throw 'A nonempty runnable discovered HybridCLR selection is required.'
    }
    $selected
}
function Assert-HybridCLRExecutedTests([object[]]$Expected, [xml]$Xml) {
    $cases=@($Xml.SelectNodes('//test-case'))
    $actual=@($cases | ForEach-Object { $_.GetAttribute('fullname') })
    if ($Expected.Count -eq 0 -or $cases.Count -ne $Expected.Count -or
        @($cases | Where-Object { $_.GetAttribute('result') -cne 'Passed' }).Count -or
        [long]$Xml.'test-run'.total -ne $cases.Count -or
        @(Compare-Object -CaseSensitive -ReferenceObject @($Expected.Name | Sort-Object -CaseSensitive) -DifferenceObject @($actual | Sort-Object -CaseSensitive)).Count) {
        throw 'Actual NUnit leaves must equal the complete selected discovery set, without duplicates or skips.'
    }
}
function Resolve-HybridCLRTestSceneProfile($Scope, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        if ($Scope.IsCompatibility) {throw 'Choose Common or Owned explicitly before any installation write.'}
        return 'Common'
    }
    if ($Value -ieq 'Common') {return 'Common'}
    if ($Value -ieq 'Owned' -and $Scope.IsCompatibility) {return 'Owned'}
    throw 'Only an explicit new compatibility receiver may select Owned scene fixtures.'
}
function Assert-HybridCLRTestInstallation($Scope, $Discovery=$null) {
    if (-not $Scope.IsCompatibility) { throw 'Declared receiver profiles require a new exact compatibility owner.' }
    $path=Join-Path $Scope.Root 'tests-installation.json'
    Assert-NoLinks $path
    $profile=[IO.File]::ReadAllText($path) | ConvertFrom-Json -AsHashtable
    Assert-HybridCLRReceiptBinding $Scope $profile
    if ($profile.Purpose -cne 'YLFramework-HybridCLR-receiver-tests-v1' -or $profile.Project -cne $Scope.Project -or
        $profile.SceneProfile -cnotin @('Common','Owned') -or $profile.CommonSceneCases -ne 5 -or
        $profile.OwnedSceneCases -ne $(if($profile.SceneProfile -ceq 'Owned'){8}else{0}) -or @($profile.Sources).Count -eq 0) {
        throw 'An explicit nonempty Common/Owned installation profile is required.'
    }
    $prefix=Join-Path $Scope.Project 'Assets/YLFramework/Tests/Integrations/HybridCLR/'
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($source in $profile.Sources) {
        Assert-NoLinks $source.Path
        Assert-NoLinks $source.SourcePath
        Assert-NoLinks ($source.Path+'.meta')
        $expected=Join-Path $Scope.Project ([IO.Path]::GetRelativePath($Scope.Repository,$source.SourcePath))
        if (-not $source.Path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or -not $seen.Add($source.Path) -or
            [IO.Path]::GetFullPath($source.Path) -ine [IO.Path]::GetFullPath($expected) -or
            $source.Sha256 -cne (Get-FileHash -LiteralPath $source.Path -Algorithm SHA256).Hash.ToLowerInvariant() -or
            $source.MetaSha256 -cne (Get-FileHash -LiteralPath ($source.Path+'.meta') -Algorithm SHA256).Hash.ToLowerInvariant()) {
            throw 'Installed universal test sources differ from the declared copy.'
        }
    }
    $owned=Join-Path $Scope.Project 'Assets/YLFramework/Tests/Integrations/HybridCLR/EditMode/HybridCLROwnedRunnerSceneTests.cs'
    Assert-NoLinks $owned
    Assert-NoLinks ($owned+'.meta')
    if ($profile.SceneProfile -ceq 'Common') {
        if ($null -ne $profile.Generated -or [IO.Path]::Exists($owned) -or [IO.Path]::Exists($owned+'.meta')) { throw 'Common profiles must not hide or acquire owned scene fixtures.' }
    } else {
        $template=Join-Path $PSScriptRoot 'HybridCLROwnedRunnerSceneTests.cs.txt'
        Assert-NoLinks $template
        if ($null -eq $profile.Generated -or $profile.Generated.Path -ine $owned -or $profile.Generated.TemplatePath -ine $template -or
            $profile.Generated.Sha256 -cne (Get-FileHash -LiteralPath $owned -Algorithm SHA256).Hash.ToLowerInvariant() -or
            $profile.Generated.Sha256 -cne (Get-FileHash -LiteralPath $template -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'The owned fixture must match its exact registered template.' }
    }
    $profile.ReceiptSha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($null -ne $Discovery) {
        Assert-HybridCLRReceiptBinding $Scope $Discovery
        $expectedSources=@($profile.Sources | ForEach-Object {[IO.Path]::GetFullPath($_.Path)})
        if ($profile.SceneProfile -ceq 'Owned') {$expectedSources += [IO.Path]::GetFullPath($profile.Generated.Path)}
        $actualSources=@($Discovery.Sources | ForEach-Object {[IO.Path]::GetFullPath($_.Path)})
        if ($actualSources.Count -ne $expectedSources.Count -or $actualSources.Count -eq 0 -or
            @(Compare-Object -CaseSensitive -ReferenceObject @($expectedSources | Sort-Object -CaseSensitive) -DifferenceObject @($actualSources | Sort-Object -CaseSensitive)).Count) { throw 'Actual compiled source files must close the declared copy and generated fixture.' }
        $common=@($Discovery.EditMode | Where-Object {$_.Name.StartsWith('YLFramework.Tests.Integrations.HybridCLR.HybridCLRRunnerSceneTests.',[StringComparison]::Ordinal)})
        $ownedCases=@($Discovery.EditMode | Where-Object {$_.Name.StartsWith('YLFramework.Tests.Integrations.HybridCLR.HybridCLROwnedRunnerSceneTests.',[StringComparison]::Ordinal)})
        if ($Discovery.SceneProfile -cne $profile.SceneProfile -or $Discovery.TestInstallationSha256 -cne $profile.ReceiptSha256 -or
            $common.Count -ne $profile.CommonSceneCases -or $ownedCases.Count -ne $profile.OwnedSceneCases) { throw 'Actual discovery must close the explicitly installed fixture sets.' }
    }
    return $profile
}
function Publish-HybridCLRFunctionalAcceptance($Scope, [string]$Run, $Decision) {
    Assert-HybridCLRReceiptBinding $Scope $Decision
    if ($Decision.Run -cne $Run -or $Decision.Accepted -ne $true -or $Decision.FunctionalPassed -ne $true) {throw 'Only a fully validated acceptance decision can be published.'}
    $path=Join-Path $Scope.Root ($Run+'.acceptance.json')
    Assert-NoLinks $path
    if ([IO.Path]::Exists($path)) {throw 'Preserve the first immutable acceptance.'}
    $snapshot=@{}
    foreach($property in $Decision.PSObject.Properties) {
        if ($property.Name -cnotin @('Artifact','ProcessFact')) {$snapshot[$property.Name]=$property.Value}
    }
    $pending=$path+'.pending-'+[Guid]::NewGuid().ToString('N')
    $bytes=[Text.Encoding]::UTF8.GetBytes(($snapshot | ConvertTo-Json -Depth 15))
    $stream=[IO.File]::Open($pending,'CreateNew','Write','None')
    try {$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true)} finally {$stream.Dispose()}
    [IO.File]::Move($pending,$path,$false)
}
function Assert-HybridCLRStoredAcceptance($Scope, [string]$Run, $Decision) {
    $path=Join-Path $Scope.Root ($Run+'.acceptance.json')
    Assert-NoLinks $path
    $stored=[IO.File]::ReadAllText($path) | ConvertFrom-Json -AsHashtable
    Assert-HybridCLRReceiptBinding $Scope $stored
    foreach($name in @('Run','Phase','Policy','Project','FunctionalPassed','ExitPassed','ProcessPassed','KnownIssueApplied','Accepted','AcceptanceStatus','ArtifactPath','ArtifactSha256','ProcessFactPath','ProcessFactSha256','CompletionSealPath','CompletionSealSha256','SelectionSha256')) {
        if (-not $stored.ContainsKey($name) -or $stored[$name] -cne $Decision.$name) {throw 'Acceptance cannot be rebaselined to different evidence or result flags.'}
    }
    foreach($pair in @(@('ArtifactPath','ArtifactSha256'),@('ProcessFactPath','ProcessFactSha256'),@('CompletionSealPath','CompletionSealSha256'))) {
        if ($null -eq $stored[$pair[0]]) {continue}
        Assert-NoLinks $stored[$pair[0]]
        if ((Get-FileHash -LiteralPath $stored[$pair[0]] -Algorithm SHA256).Hash.ToLowerInvariant() -cne $stored[$pair[1]]) {throw 'Previously accepted evidence bytes changed.'}
    }
    return $stored
}
function Resolve-HybridCLRExitPolicy($Scope, [string]$Policy='Strict') {
    if ($Policy -ieq 'Strict') {return 'Strict'}
    if ($Policy -ieq 'Known2022EditorExit' -and $Scope.IsCompatibility -and
        $Scope.ExpectedUnityVersion -ceq '2022.3.62f3c1' -and $Scope.ExpectedUnityRevision -ceq '1623fc0bbb97') {return 'Known2022EditorExit'}
    throw 'The approved exit exception is limited to the exact owned 2022 Editor discovery and tests.'
}
function Get-HybridCLRTestArguments($Scope, [ValidateSet('EditMode','PlayMode')][string]$TestPlatform, [string]$Filter, [string]$Result, [string]$Log, [bool]$OwnedSceneHost=$true, [string]$ExitPolicy='Strict') {
    if ([string]::IsNullOrWhiteSpace($Filter)) { throw 'An explicit test filter is required.' }
    $ExitPolicy=Resolve-HybridCLRExitPolicy $Scope $ExitPolicy
    $arguments = @('-batchmode','-projectPath',$Scope.Project,'-buildTarget','Win64','-runTests','-testPlatform',$TestPlatform,'-testFilter',$Filter,'-testResults',$Result,'-logFile',$Log)
    if ($Scope.IsCompatibility -and $TestPlatform -ceq 'EditMode' -and $OwnedSceneHost) {
        $arguments += @('-hclr-root',$Scope.Root,'-hclr-run',[IO.Path]::GetFileNameWithoutExtension($Result),'-hclr-owner-sha256',$Scope.OwnerSha256)
    }
    if ($ExitPolicy -ceq 'Known2022EditorExit') {
        $arguments+=@('-hclr-acceptance-policy',$ExitPolicy,'-hclr-completion-root',$Scope.Root,
            '-hclr-completion-run',[IO.Path]::GetFileNameWithoutExtension($Result),'-hclr-completion-owner-sha256',$Scope.OwnerSha256)
    }
    return $arguments
}
function Get-HybridCLRPlayerArguments($Scope, [string]$Log, [string]$Action, [string]$Run, [string]$BaseReceipt) {
    if (-not $Scope.IsCompatibility) { '-batchmode'; '-nographics' }
    @('-logFile',$Log,'-hclr-root',$Scope.Root,'-hclr-action',$Action,'-hclr-run',$Run,'-hclr-base-receipt',$BaseReceipt)
}
Assert-HybridCLRExplicitPath $Root
$Root = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]'\/')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Assert-NoLinks $PSCommandPath
Assert-NoLinks $repository
if ($Receiver) {
    $productionRoot = 'G:\Unity\U63-20261027-c63a1'
    $simulationParent = 'C:\Users\Kano\AppData\Local\Temp\kilo\U63-20261027-c63a1\'
    if (($GuardSimulation -and -not $Root.StartsWith($simulationParent, [StringComparison]::OrdinalIgnoreCase)) -or
        (-not $GuardSimulation -and $Root -ne $productionRoot)) { throw 'This receiver is outside the exact owned delivery root.' }
    if ($Receiver -cnotmatch '^[a-z][a-z0-9]{0,7}$' -or $Receiver -match '^(?:[nvw].*|r|h26|hc26|h26s|hc26s|a|d|t|u|p|j|q|r6|x|y|z)$') { throw 'Use an exact new short receiver name, not a historical receiver or wildcard.' }
    $deliveryOwnerPath = Join-Path $Root 'owner.json'
    Assert-NoLinks $deliveryOwnerPath
    $deliveryOwner = [IO.File]::ReadAllText($deliveryOwnerPath) | ConvertFrom-Json -AsHashtable
    $purpose = if ($GuardSimulation) { 'YLFramework-HybridCLR-guard-simulation' } else { 'YLFramework-Stage9-independent-delivery' }
    if ($deliveryOwner.purpose -cne $purpose -or $deliveryOwner.root -ine $Root -or $deliveryOwner.repository -ine $repository -or
        $deliveryOwner.runId -cne 'U63-20261027-c63a1' -or $deliveryOwner.plan -cne '.kilo/plans/1793047106303-unity-63-compatibility.md') { throw 'Delivery owner does not bind this root, repository and run.' }
    $descriptorPath = Join-Path $Root ($Receiver + '.hybridclr-owner.json')
    Assert-NoLinks $descriptorPath
    $ownerJson = [IO.File]::ReadAllText($descriptorPath)
    $owner = $ownerJson | ConvertFrom-Json -AsHashtable
    $pathKeys = @('project','evidenceRoot','baseline','releaseBaseline','playerRoot','releasePlayerRoot','runtimeCacheRoot','subsetBaseline','subsetPlayerRoot','subsetCacheRoot')
    $keys = @('schemaVersion','purpose','simulation','deliveryRoot','repository','runId','receiver','unity','expectedUnityVersion','expectedUnityRevision','uguiVersion','testFrameworkVersion','compatibilityId','releaseCompatibilityId','subsetCompatibilityId','baseReceiptStem','baseImportRun','optionalImportRun','basePackage','optionalPackage') + $pathKeys
    $nativeOwner=Test-HybridCLRNativeOwnerBinding $owner $Root
    if ($nativeOwner) { $keys+=@('nativeSourceManifest','nativeSourceManifestSha256') }
    if (@($owner.Keys | Where-Object { $_ -cnotin $keys }).Count -or @($keys | Where-Object { -not $owner.ContainsKey($_) }).Count -or
        $owner.schemaVersion -cne 2 -or $owner.purpose -cne 'YLFramework-HybridCLR-9.0.3-independent-reception' -or
        $owner.simulation -isnot [bool] -or $owner.simulation -ne [bool]$GuardSimulation -or
        $owner.deliveryRoot -ine $Root -or $owner.repository -ine $repository -or $owner.runId -cne $deliveryOwner.runId -or $owner.receiver -cne $Receiver) { throw 'Unknown or mismatched exact receiver owner schema.' }
    $mapping = switch -CaseSensitive ($owner.expectedUnityVersion) {
        '2022.3.62f3c1' { @('1623fc0bbb97','1.0.0','1.3.9') }
        '6000.3.27f1' { @('672d562a42a7','2.0.0','1.6.0') }
        default { throw 'Only the two exact approved Unity versions are allowed.' }
    }
    if ($owner.expectedUnityRevision -cne $mapping[0] -or $owner.uguiVersion -cne $mapping[1] -or $owner.testFrameworkVersion -cne $mapping[2]) { throw 'Wrong Unity/revision/UGUI/UTF combination.' }
    if ($nativeOwner) {
        Assert-HybridCLRExplicitPath $owner.nativeSourceManifest
        if ((Get-FileHash -LiteralPath $owner.nativeSourceManifest -Algorithm SHA256).Hash.ToLowerInvariant() -cne $owner.nativeSourceManifestSha256) { throw 'Native owner: source receipt changed.' }
        $verifiedSource=& (Join-Path $PSScriptRoot 'Prepare-HybridCLRNativeSource.ps1') -Operation Verify -SourceManifest $owner.nativeSourceManifest
        if ($verifiedSource.Verified -ne $true -or $verifiedSource.ActualPreimagePostimageVerified -ne $true -or
            $verifiedSource.SourceManifestSha256 -cne $owner.nativeSourceManifestSha256) { throw 'Native owner: actual source variant/delta did not verify.' }
    }
    foreach ($name in @('Unity','ExpectedUnityVersion','ExpectedUnityRevision','UguiVersion','TestFrameworkVersion')) {
        if ($PSBoundParameters.ContainsKey($name) -and $PSBoundParameters[$name] -cne $owner[$name.Substring(0,1).ToLowerInvariant() + $name.Substring(1)]) { throw "Explicit binding differs from owner: $name" }
    }
    Assert-HybridCLRExplicitPath $owner.unity
    $identity = 'u63-c63a1-' + $owner.expectedUnityVersion.Replace('.','-') + '-' + $Receiver
    if ($owner.compatibilityId -cne ($identity + '-v1') -or $owner.releaseCompatibilityId -cne ($identity + '-release-v1') -or
        $owner.subsetCompatibilityId -cne ($identity + '-subsets-v1')) { throw 'Compatibility identities must be unique to this engine and receiver.' }
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $pathKeys) {
        $path = [string]$owner[$name]
        Assert-HybridCLRExplicitPath $path
        $path = [IO.Path]::GetFullPath($path).TrimEnd([char[]]'\/')
        $leaf = [IO.Path]::GetFileName($path)
        if ([IO.Path]::GetDirectoryName($path) -ine $Root -or $leaf -cnotmatch '^[a-z][a-z0-9]{0,7}$' -or
            $leaf -match '^(?:[nvw].*|r|h26|hc26|h26s|hc26s|a|d|t|u|p|j|q|r6|x|y|z)$' -or -not $paths.Add($path)) { throw "Unknown, overlapping or historical output: $name" }
        # Reserve room for generated descendants; the product's exact path preflight remains authoritative.
        if ((Join-Path $productionRoot $leaf).Length + $(if ($name -eq 'project') { 228 } else { 180 }) -ge 260) { throw "Output exceeds the 260-character path budget: $name" }
    }
    if ($owner.project -ine (Join-Path $Root $Receiver)) { throw 'Project must match the exact receiver leaf.' }
    foreach ($otherPath in [IO.Directory]::GetFiles($Root, '*.hybridclr-owner.json')) {
        if ($otherPath -ieq $descriptorPath) { continue }
        Assert-NoLinks $otherPath
        $otherOwner = [IO.File]::ReadAllText($otherPath) | ConvertFrom-Json -AsHashtable
        foreach ($name in $pathKeys) {
            if (-not $otherOwner.ContainsKey($name)) { throw 'Unknown neighboring receiver descriptor; do not guess its output reservations.' }
            Assert-HybridCLRExplicitPath $otherOwner[$name]
            if ($paths.Contains([IO.Path]::GetFullPath($otherOwner[$name]).TrimEnd([char[]]'\/'))) { throw 'Receiver descriptors must not share any project, evidence, baseline, Player or cache path.' }
        }
    }
    foreach ($name in @('baseReceiptStem','baseImportRun','optionalImportRun')) {
        if ($owner[$name] -cnotmatch '^[A-Za-z0-9-]+$') { throw 'Use explicit receipt stems without paths.' }
    }
    if ($owner.baseImportRun -ceq $owner.optionalImportRun -or $owner.baseReceiptStem -in @($owner.baseImportRun,$owner.optionalImportRun)) { throw 'Import and baseline receipts must not overlap.' }
    if ($owner.baseReceiptStem -in @('owner','product-selection','patch-receipt','base-release-receipt','project-subsets-base','project-subsets-patch','bad-metadata-fixture','bad-signature-fixture','ServerV2-ready','ServerBad-ready','ServerMissing-ready','ServerBadSignature-ready')) { throw 'The baseline receipt must not collide with a fixed consumer or owner output.' }
    foreach ($name in @('basePackage','optionalPackage')) {
        if ($owner[$name] -cnotmatch '^[A-Za-z0-9-]+\.unitypackage$') { throw 'Use exact newly exported package names, not paths.' }
    }
    if ($owner.basePackage -ieq $owner.optionalPackage) { throw 'Default and optional exports require distinct package paths.' }
    $snapshot = Join-Path $owner.evidenceRoot 'owner.json'
    Assert-NoLinks $snapshot
    $bound = [IO.File]::Exists($snapshot)
    if ($bound) {
        if ([IO.File]::ReadAllText($snapshot) -cne $ownerJson) { throw 'An initialized owner is immutable; never rebind an existing receiver.' }
    } else {
        foreach ($path in $paths) {
            if ([IO.Path]::Exists($path) -or [IO.Path]::Exists($path + '.meta')) { throw "Never adopt an occupied or unknown receiver/output: $path" }
        }
    }
    return [pscustomobject]@{
        Root=$owner.evidenceRoot; DeliveryRoot=$Root; Owner=$owner; OwnerJson=$ownerJson; OwnerPath=$descriptorPath
        OwnerSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($ownerJson))).ToLowerInvariant()
        Repository=$repository; Project=$owner.project; IsCompatibility=$true; IsSimulation=[bool]$GuardSimulation; Bound=$bound
        IsStyle=$false; IsStructure=$false; PersistentProject=$owner.project; CacheRoot=$owner.runtimeCacheRoot
        SubsetPlayer=$owner.subsetPlayerRoot; SubsetCache=$owner.subsetCacheRoot; SubsetBaseline=$owner.subsetBaseline; SubsetCompatibility=$owner.subsetCompatibilityId
        Unity=$owner.unity; ExpectedUnityVersion=$owner.expectedUnityVersion; ExpectedUnityRevision=$owner.expectedUnityRevision
        UguiVersion=$owner.uguiVersion; TestFrameworkVersion=$owner.testFrameworkVersion
        BaseImportRun=$owner.baseImportRun; OptionalImportRun=$owner.optionalImportRun
    }
}
if ($GuardSimulation) { throw 'Guard simulation requires an explicit receiver.' }
if (($ExpectedUnityVersion -and $ExpectedUnityVersion -cne '2022.3.62f3c1') -or
    ($ExpectedUnityRevision -and $ExpectedUnityRevision -cne '1623fc0bbb97') -or
    ($UguiVersion -and $UguiVersion -cne '1.0.0') -or ($TestFrameworkVersion -and $TestFrameworkVersion -cne '1.1.33')) {
    throw 'Legacy owners cannot change engine or dependency identity; use a new explicit receiver.'
}
$approved = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Temp/kilo')) + [IO.Path]::DirectorySeparatorChar
$durable = $Root -eq [IO.Path]::GetFullPath('G:/Unity/YLStyleAcceptance/YS28')
if (-not $Root.StartsWith($approved, [StringComparison]::OrdinalIgnoreCase) -and -not $durable) { throw 'Expected an explicitly approved independent receiver root.' }
Assert-NoLinks $PSCommandPath
$ownerPath = Join-Path $Root 'owner.json'
Assert-NoLinks $ownerPath
$ownerJson = [IO.File]::ReadAllText($ownerPath)
$owner = $ownerJson | ConvertFrom-Json -AsHashtable
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Assert-NoLinks $repository
if ($owner['purpose'] -cne 'YLFramework-HybridCLR-9.0.3-independent-reception' -or
    -not [IO.Path]::IsPathFullyQualified([string]$owner['repository']) -or
    ([string]$owner['repository']).Split([char[]]'\/') -contains '.' -or
    ([string]$owner['repository']).Split([char[]]'\/') -contains '..' -or
    [IO.Path]::GetFullPath([string]$owner['repository']) -ne $repository) { throw 'Receiver purpose or source repository mismatch.' }
$parent = [IO.DirectoryInfo]::new($repository).Parent.Parent.FullName
if (-not [IO.Path]::IsPathFullyQualified([string]$owner['project']) -or
    ([string]$owner['project']).Split([char[]]'\/') -contains '.' -or
    ([string]$owner['project']).Split([char[]]'\/') -contains '..') { throw 'Expected an explicit receiver project path without dot segments.' }
$project = [IO.Path]::GetFullPath([string]$owner['project'])
$isolated = $owner.ContainsKey('nativePrefix')
$structure = $owner['work'] -ceq 'yl-structure-and-namespaces-20261023'
if ($durable -and -not $isolated) { throw 'The durable receiver requires an exact isolated owner.' }
$persistent = Join-Path $parent 'H26'
$cache = Join-Path $parent 'HC26'
$subsetPlayer = Join-Path $parent 'H26S'
$subsetCache = Join-Path $parent 'HC26S'
$subsetBaseline = Join-Path $repository 'Build/HybridCLR/Reception-20261022/ProjectSubsets'
$subsetCompatibility = 'hybridclr-project-subsets-v1'
$allowed = @((Join-Path $Root 'R'), (Join-Path $repository 'Temp/H26'), $persistent)
if ($isolated) {
    $prefix = [string]$owner['nativePrefix']
    $pattern = if ($structure) { '^S[0-9]{2}$' } else { '^YS[0-9]{2}$' }
    $identity = if ($structure) { 'yl-structure-native-20261023-' } else { 'yl-style-native-20261022-' }
    if ($prefix -cnotmatch $pattern -or [IO.Path]::GetFileName($Root) -cne $prefix -or
        (-not $structure -and $owner['work'] -cne 'yl-code-style-naming-cleanup-20261022')) { throw 'The isolated owner prefix does not match this exact root and work.' }
    $persistent = Join-Path $parent $prefix
    $cache = Join-Path $parent ($prefix + 'C')
    $subsetPlayer = Join-Path $parent ($prefix + 'S')
    $subsetCache = Join-Path $parent ($prefix + 'CS')
    $subsetBaseline = if ($structure) { Join-Path $parent ($prefix + 'BS') } else { Join-Path $Root 'Baselines/ProjectSubsets' }
    $subsetCompatibility = $(if ($structure) { 'yl-structure-project-subsets-20261023-' } else { 'yl-style-project-subsets-20261022-' }) + $prefix.ToLowerInvariant() + '-v1'
    $paths = @{
        playerRoot = Join-Path $parent ($prefix + 'D4')
        releasePlayerRoot = Join-Path $parent ($prefix + 'R')
        runtimeCacheRoot = $cache
        subsetPlayerRoot = $subsetPlayer
        subsetCacheRoot = $subsetCache
        baseline = $(if ($structure) { Join-Path $parent ($prefix + 'B') } else { Join-Path $Root 'Baselines/Native' })
        subsetBaseline = $subsetBaseline
    }
    foreach ($name in $paths.Keys) {
        $path = [string]$owner[$name]
        if (-not [IO.Path]::IsPathFullyQualified($path) -or $path.Split([char[]]'\/') -contains '..' -or
            $path.Split([char[]]'\/') -contains '.' -or [IO.Path]::GetFullPath($path).TrimEnd([char[]]'\/') -ne $paths[$name]) { throw "Unowned isolated receiver path: $name" }
        Assert-NoLinks $path
    }
    if ($owner['compatibilityId'] -cne ($identity + $prefix.ToLowerInvariant() + '-v1') -or
        $owner['releaseCompatibilityId'] -cne ($identity + $prefix.ToLowerInvariant() + '-release-v1') -or
        $owner['subsetCompatibilityId'] -cne $subsetCompatibility) { throw 'The new receiver must use its own exact compatibility identities.' }
    $allowed = @((Join-Path $Root 'R'), $persistent)
} elseif ($structure -or $owner['work'] -ceq 'yl-code-style-naming-cleanup-20261022') {
    throw 'This acceptance cannot fall back to historical native outputs; an explicit isolated prefix is required.'
}
if ($project -notin $allowed) { throw 'Unexpected receiver project path.' }
Assert-NoLinks $project
[pscustomobject]@{
    Root = $Root
    Owner = $owner
    OwnerJson = $ownerJson
    Repository = $repository
    Project = $project
    IsStyle = $isolated -and -not $structure
    IsStructure = $structure
    PersistentProject = $persistent
    CacheRoot = $cache
    SubsetPlayer = $subsetPlayer
    SubsetCache = $subsetCache
    SubsetBaseline = $subsetBaseline
    SubsetCompatibility = $subsetCompatibility
    IsCompatibility = $false
    IsSimulation = $false
    Bound = $true
    Unity = $(if ($Unity) { $Unity } else { 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe' })
    ExpectedUnityVersion = '2022.3.62f3c1'
    ExpectedUnityRevision = '1623fc0bbb97'
    UguiVersion = '1.0.0'
    TestFrameworkVersion = '1.1.33'
    BaseImportRun = 'base-no-package-import'
    OptionalImportRun = 'optional-no-package-import'
}
