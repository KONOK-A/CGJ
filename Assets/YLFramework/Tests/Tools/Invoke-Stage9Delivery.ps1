#requires -Version 7.6
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $DeliveryRoot,
    [Parameter(Mandatory)][ValidateSet('Initialize','PublishTools','AuditPackage','Create','Import','RecoverImport','InstallHarness','SyncHarness','InstallLogger','UpdateLogger','Editor','Player','RecoverPlayer','Protect','Finalize')][string] $Operation,
    [string] $Repository,
    [ValidatePattern('^Receiver[A-Z][0-9]*$')][string] $Receiver = 'ReceiverA',
    [ValidatePattern('^[A-Za-z0-9-]+$')][string] $Run = 'initial',
    [ValidateSet('Audit','Generate','VerifyGeneration','BuildMono','BuildIl2Cpp','DiagnoseReadDefaults')][string] $Method = 'Audit',
    [ValidateSet('Mono','Il2Cpp')][string] $Backend = 'Mono',
    [ValidatePattern('^[A-Za-z0-9-]+$')][string] $BuildRun = 'initial',
    [ValidateSet('smoke','update','probe','rollback','encrypted','missing-key','sample')][string] $Command = 'smoke',
    [ValidatePattern('^[a-z0-9-]+$')][string] $Store = 'main',
    [ValidateRange(1,2)][int] $ExpectedVersion = 1,
    [ValidatePattern('^[A-Za-z0-9-]+$')][string] $ProofRun = 'smoke4',
    [ValidatePattern('^[A-Za-z0-9-]+$')][string] $ImportRun = 'initial',
    [string] $Package = 'YLFramework-Stage9.unitypackage',
    [string] $Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($DeliveryRoot)
$project = Join-Path $root $Receiver
function Reject-Link([string] $Path) {
    for ($cursor = $Path; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ([IO.Path]::Exists($cursor) -and ([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked path rejected: $cursor" }
    }
}
function Hash([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Read-SharedText([string] $Path) {
    $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $reader=[IO.StreamReader]::new($stream)
    try { $reader.ReadToEnd() } finally { $reader.Dispose(); $stream.Dispose() }
}
function Read-ExportRoots([string] $Source) {
    $declarations = [regex]::Matches($Source, '(?s)\b_exportRoots\s*=\s*(?:new\s*(?:string)?\s*\[\s*\]\s*)?\{(.*?)\};')
    if ($declarations.Count -ne 1) { throw 'The current formal exporter must declare exactly one _exportRoots array.' }
    $roots = @([regex]::Matches($declarations[0].Groups[1].Value, '"(Assets/[^"\r\n]+)"') | ForEach-Object { $_.Groups[1].Value })
    $required = @('Assets/YLFramework/Editor','Assets/YLFramework/Plugins/BouncyCastle','Assets/YLFramework/Prefabs',
        'Assets/YLFramework/Runtime','Assets/YLFramework/Samples','Assets/YLFramework/Documentation',
        'Assets/YLFramework/LICENSE.md','Assets/YLFramework/CodingStyle.md','Assets/YLFramework/AGENTS.md')
    if ($roots.Count -ne $required.Count -or @($roots | Sort-Object -Unique).Count -ne $required.Count -or
        @($required | Where-Object { $roots -cnotcontains $_ }).Count -ne 0) {
        throw 'The formal export closure must contain the exact six product directories and three root files.'
    }
    $roots
}
function Json([string] $Path, $Value) {
    if (Test-Path -LiteralPath $Path) { throw "Preserve existing evidence: $Path" }
    [IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $Value -Depth 50), [Text.UTF8Encoding]::new($false))
}
Reject-Link $root
$ownerPath = Join-Path $root 'owner.json'
$owner = Get-Content -LiteralPath $ownerPath -Raw | ConvertFrom-Json
$repo = [IO.Path]::GetFullPath($(if ($Repository) { $Repository } else { $owner.repository }))
if ($root.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Delivery must be outside the repository and all worktrees.' }
if ($owner.purpose -cne 'YLFramework-Stage9-independent-delivery' -or [IO.Path]::GetFullPath($owner.root) -cne $root -or
    [IO.Path]::GetFullPath($owner.repository) -cne $repo) { throw 'Explicit owned delivery identity required.' }
$worktrees = @(& git -C $repo worktree list --porcelain | Where-Object { $_.StartsWith('worktree ') } | ForEach-Object { [IO.Path]::GetFullPath($_.Substring(9)) })
foreach ($tree in $worktrees) { if ($root -eq $tree -or $root.StartsWith($tree + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Delivery lies inside a worktree.' } }
function Protected-Files {
    $relativeRoots = @('Assets/Games','Assets/Scenes','Assets/StreamingAssets','Packages','ProjectSettings','AGENTS.md','.gitignore',
        'Assets/YLFramework/Plugins/Editor Enhancers Bundle','Assets/YLFramework/Tests/Acceptance/Core',
        'Assets/YLFramework/Tests/Fixtures/WorkbenchUX1-20261017/AlternateContent.meta')
    foreach ($relative in $relativeRoots) {
        $path = Join-Path $repo $relative
        if (-not [IO.Path]::Exists($path)) { continue }
        $files = if ([IO.File]::Exists($path)) { @($path) } else { [IO.Directory]::GetFiles($path,'*',[IO.SearchOption]::AllDirectories) }
        foreach ($file in $files) { [ordered]@{ Path=$file; Sha256=Hash $file } }
    }
    foreach ($tree in $worktrees | Where-Object { $_ -like '*fusion-ui-panel-redesign*' }) {
        foreach ($relative in @('Assets/YLFramework','Packages','ProjectSettings')) {
            $path = Join-Path $tree $relative
            if ([IO.Directory]::Exists($path)) {
                foreach ($file in [IO.Directory]::GetFiles($path,'*',[IO.SearchOption]::AllDirectories)) { [ordered]@{ Path=$file; Sha256=Hash $file } }
            }
        }
    }
}
function Verify-Project {
    Reject-Link $project
    $identity = Get-Content -LiteralPath (Join-Path $project 'stage9-owner.json') -Raw | ConvertFrom-Json
    if ($identity.Purpose -cne 'Stage9-independent-receiver' -or $identity.Project -cne $project -or $identity.Delivery -cne $root) { throw 'Receiver identity mismatch.' }
    foreach ($tree in $worktrees) { if ($project -eq $tree -or $project.StartsWith($tree + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Receiver is a worktree.' } }
}
function Receiver-PackageHash {
    $identity=Get-Content -LiteralPath (Join-Path $project 'stage9-owner.json') -Raw | ConvertFrom-Json
    if ($Package -cne $identity.Package) { throw 'Cannot rebind an existing receiver to another package.' }
    $sha=Hash (Join-Path $root $identity.Package)
    $audit=Get-Content -LiteralPath (Join-Path $root ($identity.Package + '.receipt.json')) -Raw | ConvertFrom-Json
    if (-not $audit.Passed -or $audit.Package -cne $identity.Package -or $sha -cne $identity.PackageSha256 -or $sha -cne $audit.Sha256) { throw 'Receiver/package/audited identity mismatch.' }
    $sha
}
function Run-Unity([string[]] $Arguments, [string] $Log, [switch] $Creating) {
    $processes = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")
    if (@($processes | Where-Object { $_.CommandLine -like ('*' + $project + '*') -and $_.CommandLine -notlike '*AssetImportWorker*' }).Count) { throw 'Receiver already running; recover the same result, never launch twice.' }
    if (-not $Creating) { Verify-Project }
    if (Test-Path -LiteralPath $Log) { throw 'Never overwrite a prior Unity log.' }
    $start = [Diagnostics.ProcessStartInfo]::new($Unity)
    $start.UseShellExecute = $false
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $packageSha=Hash (Join-Path $root $Package)
    $process = [Diagnostics.Process]::Start($start)
    Json ($Log + '.process.json') @{ Pid=$process.Id; Project=$project; Executable=$Unity; Arguments=@($start.ArgumentList); Package=$Package; PackageSha256=$packageSha; MainEditors=@($processes | Where-Object { $_.CommandLine -notlike '*AssetImportWorker*' } | Select-Object ProcessId,CommandLine); StartedUtc=[DateTime]::UtcNow.ToString('O') }
    try { $process.WaitForExit(); $code=$process.ExitCode; Json ($Log + '.exit.json') @{ Pid=$process.Id; Project=$project; ExitCode=$code; EndedUtc=[DateTime]::UtcNow.ToString('O') }; if ($code -ne 0) { throw "Unity exited $code; preserve $Log" } }
    finally { $process.Dispose() }
}
if ($Operation -eq 'Initialize') {
    Json (Join-Path $root 'protected-before.json') @(Protected-Files)
    Json (Join-Path $root 'source-baseline.json') @{ Head=(& git -C $repo rev-parse HEAD); Branch=(& git -C $repo branch --show-current); Status=@(& git -C $repo status --short --untracked-files=all); Worktrees=$worktrees; Utc=[DateTime]::UtcNow.ToString('O') }
    foreach ($directory in @('Runs','Stores','Tools')) { $null=[IO.Directory]::CreateDirectory((Join-Path $root $directory)) }
    foreach ($file in @('Invoke-Stage9Delivery.ps1','Stage9DeliveryEditor.cs.txt','Stage9DeliveryProbe.cs.txt','Stage9DeliveryPanel.cs.txt','Test-Stage9OptionalLogger.ps1','Run-Stage9Representative.ps1')) {
        if ([IO.File]::Exists((Join-Path $PSScriptRoot $file))) { [IO.File]::Copy((Join-Path $PSScriptRoot $file),(Join-Path $root ('Tools/' + $file))) }
    }
    'Initialized independent delivery protection snapshot.'
    return
}
if ($Operation -eq 'PublishTools') {
    $history=Join-Path $root ('ToolsHistory/DeliveryTools-' + $Run)
    if (Test-Path -LiteralPath $history) { throw 'Preserve delivered tool revisions.' }
    $null=[IO.Directory]::CreateDirectory($history); $null=[IO.Directory]::CreateDirectory((Join-Path $root 'Tools'))
    $files=@(foreach ($name in @('Invoke-Stage9Delivery.ps1','Stage9DeliveryEditor.cs.txt','Stage9DeliveryProbe.cs.txt','Stage9DeliveryPanel.cs.txt','Test-Stage9OptionalLogger.ps1','Run-Stage9Representative.ps1')) {
        $source=Join-Path $PSScriptRoot $name; $destination=Join-Path $root ('Tools/' + $name)
        if ([IO.File]::Exists($destination)) { [IO.File]::Copy($destination,(Join-Path $history $name)) }
        if ($source -cne $destination) { [IO.File]::Copy($source,$destination,$true) }
        @{ Name=$name; Sha256=Hash $destination }
    })
    Json (Join-Path $history 'receipt.json') @{ Files=$files; Repository=$repo; Root=$root; Utc=[DateTime]::UtcNow.ToString('O') }
    'Published current repeatable tools, preserving the prior snapshot.'
    return
}
if ($Operation -eq 'Protect') {
    $before = Get-Content -LiteralPath (Join-Path $root 'protected-before.json') -Raw | ConvertFrom-Json
    $after = @(Protected-Files); $old=@{}; foreach ($row in $before) { $old[$row.Path]=$row.Sha256 }
    $changes=@(foreach ($row in $after) { if (-not $old.ContainsKey($row.Path) -or $old[$row.Path] -cne $row.Sha256) { $row.Path }; $old.Remove($row.Path) }; $old.Keys)
    $authorized=@(); $unapproved=@($changes)
    if ($Run -eq 'final') {
        $logger=Get-Content -LiteralPath (Join-Path $root 'Optional/logger-final-receipt.json') -Raw | ConvertFrom-Json
        $authorized=@(foreach ($relative in @('CHANGELOG.md','Runtime/LoggerManager.cs')) {
            $path=Join-Path $repo ('Packages/com.xfkj.xfgameframework.loggersystem/'+$relative)
            $entry=@($logger.Files | Where-Object Path -CEQ $relative)
            if ($entry.Count -ne 1 -or (Hash $path) -cne $entry[0].Sha256) { throw 'Authorized Logger repair differs from the tested final artifact.' }
            if ($changes -ccontains $path) { @{ Path=$path; Sha256=Hash $path; Basis='User stage9 direct-delivery defect repair; exact separately audited artifact bytes' } }
        })
        $settings=Join-Path $repo 'ProjectSettings/ProjectSettings.asset'
        & git -C $repo diff --quiet HEAD -- 'ProjectSettings/ProjectSettings.asset'
        if ($LASTEXITCODE -ne 0) { throw 'The user-authorized two-default cleanup did not restore the exact baseline representation.' }
        if ($changes -ccontains $settings) { $authorized+=@{ Path=$settings; Sha256=Hash $settings; Basis='User explicitly authorized removing only two Standalone default records; git diff confirms exact clean baseline' } }
        $allowed=@($authorized | ForEach-Object Path)
        $unapproved=@($changes | Where-Object { $allowed -cnotcontains $_ })
    }
    Json (Join-Path $root ('protection-' + $Run + '.json')) @{ Passed=$unapproved.Count -eq 0; Before=$before.Count; After=$after.Count;
        AllDifferences=$changes; AuthorizedDifferences=$authorized; UnapprovedDifferences=$unapproved }
    if ($unapproved.Count) { throw 'Unapproved protected inputs changed; preserve and investigate without reverting.' }
    'Protected files unchanged apart from exact authorized delivery repairs and the explicitly approved default cleanup.'
    return
}
if ($Operation -eq 'Finalize') {
    $first=Get-Content -LiteralPath (Join-Path $root ($Receiver+'-first-import-initial.json')) -Raw | ConvertFrom-Json
    $audit=Get-Content -LiteralPath (Join-Path $root ($Receiver+'-audit-final.json')) -Raw | ConvertFrom-Json
    $protection=Get-Content -LiteralPath (Join-Path $root 'protection-final.json') -Raw | ConvertFrom-Json
    $packageReceipt=Get-Content -LiteralPath (Join-Path $root ($Package+'.receipt.json')) -Raw | ConvertFrom-Json
    $finalFiles=Get-Content -LiteralPath (Join-Path $root ($Package+'.files.json')) -Raw | ConvertFrom-Json
    $testedFiles=Get-Content -LiteralPath (Join-Path $root 'YLFramework-Stage9.unitypackage.files.json') -Raw | ConvertFrom-Json
    $logger=Get-Content -LiteralPath (Join-Path $root 'Optional/logger-final-receipt.json') -Raw | ConvertFrom-Json
    if (-not $first.Passed -or $first.HarnessPresent -or -not $audit.Passed -or -not $protection.Passed -or -not $packageReceipt.Passed -or
        $first.PackageSha256 -cne $packageReceipt.Sha256 -or $packageReceipt.Sha256 -cne (Hash (Join-Path $root $Package)) -or
        $logger.Sha256 -cne (Hash $logger.Artifact)) { throw 'Final import/audit/protection/artifact gates are not all established.' }
    $tested=@{}; foreach ($file in $testedFiles) { $tested[$file.Path]=$file }
    $postExportJournal=[Collections.Generic.List[object]]::new()
    $documentationChanges=@(foreach ($file in $finalFiles) {
        $source=Join-Path $repo $file.Path
        if ((Hash ($source+'.meta')) -cne $file.MetaSha256) { throw 'Current formal source meta differs from the frozen final package.' }
        if (-not $file.Folder -and (Hash $source) -cne $file.Sha256) {
            if ($file.Path -cne 'Assets/YLFramework/Documentation/IntegrationPlan.md') { throw 'Current formal source differs from the final exported payload.' }
            $postExportJournal.Add(@{ Path=$file.Path; PackageSha256=$file.Sha256; CurrentSha256=Hash $source;
                Reason='Post-export acceptance journal only; final package is the exact previously compiled documentation snapshot' })
        }
        if (-not $tested.ContainsKey($file.Path)) { throw 'Final package adds an untested product path.' }
        $prior=$tested[$file.Path]
        if ($file.Guid -cne $prior.Guid -or $file.MetaSha256 -cne $prior.MetaSha256) { throw 'Final product identity/meta changed after Player verification.' }
        if ($file.Sha256 -cne $prior.Sha256) {
            if ($file.Path -notlike 'Assets/YLFramework/Documentation/*' -and $file.Path -cne 'Assets/YLFramework/LICENSE.md' -and
                $file.Path -cne 'Assets/YLFramework/Plugins/BouncyCastle/SOURCE.txt') {
                throw "Final product code/binary/resource changed after Player tests: $($file.Path)"
            }
            @{ Path=$file.Path; TestedSha256=$prior.Sha256; FinalSha256=$file.Sha256; Kind='Documentation/license-only finalization; no executable payload changes' }
        }
        $tested.Remove($file.Path)
    })
    if ($tested.Count) { throw 'Final product closure removed a tested path.' }
    $runs=@(
        @{ Backend='Mono'; Run='smoke4' }, @{ Backend='Mono'; Run='mono-update1' }, @{ Backend='Mono'; Run='mono-restart2' },
        @{ Backend='Mono'; Run='mono-rollback1' }, @{ Backend='Mono'; Run='mono-restart1' }, @{ Backend='Mono'; Run='mono-encrypted' },
        @{ Backend='Mono'; Run='mono-missing-key' }, @{ Backend='Mono'; Run='mono-sample' }, @{ Backend='Mono'; Run='mono-final-logger' },
        @{ Backend='Il2Cpp'; Run='il2cpp-final-smoke' }, @{ Backend='Il2Cpp'; Run='il2cpp-final-update1' }, @{ Backend='Il2Cpp'; Run='il2cpp-final-restart2' },
        @{ Backend='Il2Cpp'; Run='il2cpp-final-rollback1' }, @{ Backend='Il2Cpp'; Run='il2cpp-final-restart1' }, @{ Backend='Il2Cpp'; Run='il2cpp-final-encrypted' },
        @{ Backend='Il2Cpp'; Run='il2cpp-final-missing-key' }, @{ Backend='Il2Cpp'; Run='il2cpp-final-sample' }
    )
    $verdicts=@(foreach ($case in $runs) {
        $folder=Join-Path $root ('Runs/ReceiverB-'+$case.Backend+'-'+$case.Run)
        $verdict=Get-Content -LiteralPath (Join-Path $folder 'verdict.json') -Raw | ConvertFrom-Json
        if (-not $verdict.Passed -or -not $verdict.IdentityMatched -or $verdict.ProcessExitCode -ne 0 -or $verdict.Development -or
            $verdict.ResultSha256 -cne (Hash (Join-Path $folder 'result.json')) -or $verdict.PlayerLogSha256 -cne (Hash (Join-Path $folder 'Player.log'))) { throw 'A required same-process Player verdict is missing or changed.' }
        @{ Backend=$case.Backend; Run=$case.Run; Verdict=$verdict; VerdictSha256=Hash (Join-Path $folder 'verdict.json') }
    })
    $receiverB=Join-Path $root 'ReceiverB'
    $reports=@(foreach ($name in @('m-ugui1','m-mf','i-l2')) {
        $build=Join-Path $receiverB ('P/'+$name); $path=Join-Path $build 'BuildReport.json'
        $report=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($report.Result -cne 'Succeeded' -or $report.Errors -ne 0 -or $report.Development -or $report.Options -cne 'None') { throw 'Required non-Development build missing.' }
        foreach ($file in $report.PlayerFiles) { if ((Hash (Join-Path $build $file.Path)) -cne $file.Sha256) { throw 'A required build artifact changed.' } }
        if ($name -eq 'i-l2' -and ($report.Stripping -cne 'Low' -or $report.Backend -cne 'IL2CPP')) { throw 'Incorrect IL2CPP support configuration.' }
        $optional=if ($report.PSObject.Properties['OptionalLogger']) { $report.OptionalLogger } else { $null }
        @{ Directory=$build; ReportSha256=Hash $path; BuildGuid=$report.BuildGuid; Backend=$report.Backend; Stripping=$report.Stripping; Errors=$report.Errors; Warnings=$report.Warnings; OptionalLogger=$optional }
    })
    $nativeRoot=Join-Path $receiverB 'P/i-l2'
    $backup=Join-Path $nativeRoot 'Stage9Delivery_BackUpThisFolder_ButDontShipItWithYourGame'
    $managed=@(foreach ($dll in [IO.Directory]::GetFiles((Join-Path $backup 'Managed'),'*.dll',[IO.SearchOption]::AllDirectories)) {
        $stream=[IO.File]::OpenRead($dll); $pe=[System.Reflection.PortableExecutable.PEReader]::new($stream)
        try { $metadata=[System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            $references=@($metadata.AssemblyReferences | ForEach-Object { $metadata.GetString($metadata.GetAssemblyReference($_).Name) })
        } finally { $pe.Dispose(); $stream.Dispose() }
        @{ Name=[IO.Path]::GetFileName($dll); Sha256=Hash $dll; References=$references }
    })
    $runtime=@($managed | Where-Object Name -CEQ 'YLFramework.Runtime.dll')
    if ($runtime.Count -ne 1 -or @($runtime[0].References | Where-Object { $_ -match 'UnityEditor|Assembly-CSharp|Tests|XF|Tink' }).Count) { throw 'Actual IL2CPP managed input runtime dependency boundary failed.' }
    if (@($managed | Where-Object Name -CEQ 'XFGameFramework.LoggerSystem.dll').Count -ne 1) { throw 'Fixed optional Logger absent from actual IL2CPP managed backup.' }
    $generated=@(foreach ($file in [IO.Directory]::GetFiles((Join-Path $backup 'il2cppOutput'),'*',[IO.SearchOption]::AllDirectories)) {
        @{ Path=[IO.Path]::GetRelativePath($backup,$file).Replace('\','/'); Length=([IO.FileInfo]$file).Length; Sha256=Hash $file }
    })
    if (-not @($generated | Where-Object Path -Like '*YLFramework.Runtime*.cpp').Count -or -not @($generated | Where-Object Path -Like '*XFGameFramework.LoggerSystem*.cpp').Count) { throw 'Actual generated C++ evidence missing.' }
    $native=@(foreach ($relative in @('GameAssembly.dll','UnityPlayer.dll','Stage9Delivery.exe','Stage9Delivery_Data/il2cpp_data/Metadata/global-metadata.dat')) {
        $file=Join-Path $nativeRoot $relative; @{ Path=$relative; Length=([IO.FileInfo]$file).Length; Sha256=Hash $file }
    })
    $currentEditors=@(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -notlike '*AssetImportWorker*' })
    if (@($currentEditors | Where-Object { $_.CommandLine -like ('*'+$root+'*') -or $_.CommandLine -like '*YLFramework-Stage9-20261019*' }).Count) { throw 'Owned receiver Editor still running; settle it before final protection.' }
    $main=Get-Content -LiteralPath (Join-Path $root 'main-final-state.json') -Raw | ConvertFrom-Json
    if ($main.Project -cne $repo -or $main.Pid -ne 32168 -or $main.IsPlaying -or $main.IsCompiling -or $main.CompilationFailed -or
        $main.InputHandling -ne 2 -or @($main.Scenes | Where-Object Dirty).Count -or $main.Backend -cne 'Mono2x' -or $main.Stripping -cne 'Disabled') { throw 'Main Editor protection final-state gate failed.' }
    $head=& git -C $repo rev-parse HEAD
    if ($head -cne '586eebedaf900d926d2e30301b20ed3129decb27' -or @(& git -C $repo diff --cached --name-only).Count) { throw 'HEAD or staging changed; do not finalize as an uncommitted delivery.' }
    if ($Run -eq 'preflight') {
        'All required final import, executable identity, Player, IL2CPP native, license and protection gates verified; ready to freeze the final delivery binding.'
        return
    }
    $sourceFiles=@(foreach ($path in @(& git -C $repo diff --name-only) + @(& git -C $repo ls-files --others --exclude-standard)) {
        $absolute=Join-Path $repo $path
        if ([IO.File]::Exists($absolute)) { @{ Path=$path; Sha256=Hash $absolute } }
    })
    $patch=& git -C $repo diff --binary
    [IO.File]::WriteAllText((Join-Path $root 'final-source.patch'),($patch -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
    $value=@{ Passed=$true; Statement='阶段9独立工程交付技术验收完成，待用户验收。'; Baseline=$head; Branch=(& git -C $repo branch --show-current);
        CommitCreated=$false; Staged=$false; Pushed=$false; Repository=$repo; FinalPackage=$packageReceipt; OptionalLogger=$logger;
        FinalReceiver=$first; FinalAudit=$audit; PlayerReceiver=$receiverB; ExecutablePayloadsEqualToPlayerTestedPackage=$true;
        FinalDocumentationDifferences=$documentationChanges; PostExportAcceptanceJournal=$postExportJournal; Builds=$reports; PlayerVerdicts=$verdicts; NativeArtifacts=$native;
        Il2CppManagedInputs=$managed; GeneratedCpp=$generated; MainEditor=$main; Protection=$protection; CurrentSourceFiles=$sourceFiles;
        GitStatus=@(& git -C $repo status --short --untracked-files=all); SourcePatchSha256=Hash (Join-Path $root 'final-source.patch');
        Limitations=@('IL2CPP Low only; prior UI Medium result remains its original scope, not High or other platforms',
        'Current modal input uses actual Canvas raycast and public UGUI handlers, not OS physical input',
        'Audio resource/native playback/release verified; zero output sample is not evidence of audible sound',
        'Historical Scene risk, positive ordinary-CA networking untested, and no zero-native-leak telemetry claim remain',
        'Some owned batch Editors hung/crashed during native exit; preserved and stopped by exact PID, not recorded as normal Editor exit',
        'Optional Logger Analyzer exposes existing documentation warnings; no diagnostic suppression'); Utc=[DateTime]::UtcNow.ToString('O') }
    Json (Join-Path $root 'delivery-binding.json') $value
    [IO.File]::WriteAllText((Join-Path $root 'delivery-binding.sha256'),(Hash (Join-Path $root 'delivery-binding.json'))+"  delivery-binding.json`n",[Text.UTF8Encoding]::new($false))
    $sums=@(
        ((Hash (Join-Path $root $Package))+'  '+$Package)
        ((Hash $logger.Artifact)+'  Optional/Final/com.xfkj.xfgameframework.loggersystem-1.0.1.tgz')
        ((Hash (Join-Path $root 'delivery-binding.json'))+'  delivery-binding.json')
    )
    [IO.File]::WriteAllText((Join-Path $root 'SHA256SUMS.txt'),($sums -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
    'Final independent delivery binding verified and frozen; no Git write performed.'
    return
}
if ($Operation -eq 'AuditPackage') {
    if ([IO.Path]::GetFileName($Package) -cne $Package) { throw 'Package must be a local delivery filename.' }
    Add-Type -AssemblyName System.Formats.Tar
    $path=Join-Path $root $Package
    $exporter=[IO.File]::ReadAllText((Join-Path $repo 'Assets/YLFramework/Editor/Distribution/ExportUnityPackage.cs'))
    $roots=@(Read-ExportRoots $exporter)
    $expected=@{}; foreach ($sourceRoot in $roots) {
        $absolute=Join-Path $repo $sourceRoot
        $expected[$sourceRoot]=Hash ($absolute + '.meta')
        if ([IO.Directory]::Exists($absolute)) { foreach ($meta in [IO.Directory]::GetFiles($absolute,'*.meta',[IO.SearchOption]::AllDirectories)) { $relative=[IO.Path]::GetRelativePath($repo,$meta).Replace('\','/'); $expected[$relative.Substring(0,$relative.Length-5)]=Hash $meta } }
    }
    $records=@{}; $stream=[IO.File]::OpenRead($path); $gzip=[IO.Compression.GZipStream]::new($stream,[IO.Compression.CompressionMode]::Decompress); $tar=[System.Formats.Tar.TarReader]::new($gzip,$true)
    try {
        while ($null -ne ($entry=$tar.GetNextEntry())) {
            if ($entry.EntryType -eq [System.Formats.Tar.TarEntryType]::Directory) { continue }
            $name=$entry.Name.TrimStart('./')
            if ($name -notmatch '^([0-9a-f]{32})/(asset|asset\.meta|pathname|preview\.png)$' -or $entry.Length -gt 80MB) { throw "Unexpected archive member: $name" }
            $guid=$Matches[1]; $member=$Matches[2]
            if (-not $records.ContainsKey($guid)) { $records[$guid]=@{} }
            if ($records[$guid].ContainsKey($member)) { throw 'Duplicate archive member.' }
            $buffer=[IO.MemoryStream]::new()
            try { if ($entry.DataStream) { $entry.DataStream.CopyTo($buffer) }; $records[$guid][$member]=$buffer.ToArray() } finally { $buffer.Dispose() }
        }
    } finally { $tar.Dispose(); $gzip.Dispose(); $stream.Dispose() }
    $rows=@(foreach ($pair in $records.GetEnumerator()) {
        $record=$pair.Value
        if (-not $record.ContainsKey('pathname') -or -not $record.ContainsKey('asset.meta')) { throw 'Missing package identity.' }
        $relative=[Text.Encoding]::UTF8.GetString($record['pathname']).TrimEnd([char]0,"`r","`n")
        if (-not $expected.ContainsKey($relative) -or $relative -match '/Tests/|Editor Enhancers|AcceptanceBuild|^Packages/|/StreamingAssets/|XFAB|\.(pfx|p12|pem|key)$|/(bcrypt|ncrypt|crypt32|Newtonsoft\.Json)\.dll$') { throw "Excluded/unexpected package path: $relative" }
        $meta=[Text.Encoding]::UTF8.GetString($record['asset.meta'])
        if ($meta -notmatch '(?m)^guid:\s*([0-9a-f]{32})\s*\r?$' -or $Matches[1] -cne $pair.Key) { throw "GUID mismatch: $relative" }
        $metaSha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$record['asset.meta'])).ToLowerInvariant()
        if ($metaSha -cne $expected[$relative]) { throw "Meta changed: $relative" }
        $source=Join-Path $repo $relative; $file=[IO.File]::Exists($source); $sha=$null; $length=0
        if ($file) {
            if (-not $record.ContainsKey('asset')) { throw "Missing payload: $relative" }
            $sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$record['asset'])).ToLowerInvariant(); $length=$record['asset'].Length
            if ($sha -cne (Hash $source)) { throw "Payload changed: $relative" }
        }
        $expected.Remove($relative)
        [ordered]@{ Path=$relative; Guid=$pair.Key; Folder=(-not $file); Length=$length; Sha256=$sha; MetaSha256=$metaSha }
    })
    if ($expected.Count) { throw 'The formal export closure is incomplete.' }
    Json (Join-Path $root ($Package + '.files.json')) @($rows | Sort-Object Path -CaseSensitive)
    Json (Join-Path $root ($Package + '.receipt.json')) @{ Passed=$true; Package=$Package; Sha256=Hash $path; Bytes=([IO.FileInfo]$path).Length; Records=$rows.Count; Roots=$roots; Head=(& git -C $repo rev-parse HEAD); Status=@(& git -C $repo status --short --untracked-files=all); Utc=[DateTime]::UtcNow.ToString('O') }
    "Exact package/meta/GUID/payload audit passed: $($rows.Count) records, $(Hash $path)"
    return
}
if ($Operation -eq 'Create') {
    if (Test-Path -LiteralPath $project) { throw 'Never adopt an existing receiver.' }
    $log=Join-Path $root ($Receiver + '-create.log')
    Run-Unity @('-batchmode','-quit','-createProject',$project,'-logFile',$log) $log -Creating
    Json (Join-Path $project 'stage9-owner.json') @{ Purpose='Stage9-independent-receiver'; Project=$project; Delivery=$root; Package=$Package; PackageSha256=Hash (Join-Path $root $Package) }
    # 只声明实际必需的独立依赖，保留新工程正常创建的内建模块；不复制主manifest。
    $manifestPath=Join-Path $project 'Packages/manifest.json'
    $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
    foreach ($key in @($manifest.dependencies.Keys)) { if (-not $key.StartsWith('com.unity.modules.')) { $manifest.dependencies.Remove($key) } }
    $manifest.dependencies['com.unity.ugui']='1.0.0'; $manifest.dependencies['com.unity.nuget.newtonsoft-json']='3.2.1'
    [IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
    'Created receiver using the actual Unity createProject command; minimal dependencies declared.'
    return
}
Verify-Project
if ($Operation -in @('Import','RecoverImport')) {
    $log=Join-Path $root ($Receiver + '-import-' + $Run + '.log')
    $packagePath=Join-Path $root $Package
    $frozenPackageSha=Receiver-PackageHash
    if (Test-Path -LiteralPath (Join-Path $project 'Assets/Stage9')) { throw 'First import cannot use a test harness.' }
    if ($Operation -eq 'Import') {
        Run-Unity @('-batchmode','-quit','-projectPath',$project,'-buildTarget','Win64','-importPackage',$packagePath,'-logFile',$log) $log
    } else {
        $exit=Get-Content -LiteralPath ($log + '.exit.json') -Raw | ConvertFrom-Json
        $invocation=Get-Content -LiteralPath ($log + '.process.json') -Raw | ConvertFrom-Json
        if ($exit.Project -cne $project -or $exit.ExitCode -ne 0 -or (Get-Process -Id $exit.Pid -ErrorAction SilentlyContinue)) { throw 'Recover only the same completed import, never a running or failed one.' }
        if ($invocation.Pid -ne $exit.Pid -or $invocation.Package -cne $Package -or $invocation.PackageSha256 -cne $frozenPackageSha -or
            $invocation.Arguments -cnotcontains $packagePath -or $invocation.Project -cne $project) { throw 'Recovered import must retain its original process/package identity.' }
    }
    if ((Receiver-PackageHash) -cne $frozenPackageSha) { throw 'Package changed during the same import operation.' }
    $errors=@([regex]::Matches((Read-SharedText $log),'(?im)^.*(?:error CS\d+|Scripts have compiler errors|Aborting batchmode due to failure|Multiple precompiled assemblies).*$') | ForEach-Object Value)
    $assemblies=@(foreach ($name in @('YLFramework.Runtime','YLFramework.Editor','YLFramework.ResourceStartup.Sample','YLFramework.ResourceStartup.Sample.Editor')) {
        $assembly=Join-Path $project ('Library/ScriptAssemblies/' + $name + '.dll')
        if (-not [IO.File]::Exists($assembly)) { throw "Actual first-import Unity assembly missing: $name" }
        @{ Name=$name; Sha256=Hash $assembly; Length=([IO.FileInfo]$assembly).Length }
    })
    $passed=$errors.Count -eq 0
    $receiptPath=Join-Path $root ($Receiver + '-first-import-' + $Run + '.json')
    if ($Operation -eq 'RecoverImport' -and (Test-Path -LiteralPath $receiptPath)) {
        $existing=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        if ($existing.Passed -ne $passed -or $existing.Project -cne $project -or $existing.Package -cne $Package -or
            $existing.PackageSha256 -cne $frozenPackageSha -or $existing.Log -cne $log -or $existing.HarnessPresent -or
            @($existing.Errors).Count -ne $errors.Count -or @($existing.Assemblies).Count -ne $assemblies.Count) { throw 'Previously collected import identity changed; preserve it.' }
        foreach ($assembly in $assemblies) {
            $prior=@($existing.Assemblies | Where-Object Name -CEQ $assembly.Name)
            if ($prior.Count -ne 1 -or $prior[0].Sha256 -cne $assembly.Sha256) { throw 'Previously collected first-import assemblies changed; never rebind them.' }
        }
    } else {
        Json $receiptPath @{ Passed=$passed; Project=$project; Package=$Package; PackageSha256=$frozenPackageSha; HarnessPresent=$false; RecoveredSameResult=$Operation -eq 'RecoverImport'; Log=$log; Errors=$errors; Assemblies=$assemblies; Manifest=(Get-Content -LiteralPath (Join-Path $project 'Packages/manifest.json') -Raw | ConvertFrom-Json); Lock=(Get-Content -LiteralPath (Join-Path $project 'Packages/packages-lock.json') -Raw | ConvertFrom-Json) }
    }
    if (-not $passed) { throw 'Raw product import compilation failed.' }
    'Raw product import compiled in the real independent Unity project before any test facilities.'
    return
}
if ($Operation -in @('InstallHarness','SyncHarness')) {
    $packageSha=Receiver-PackageHash
    $firstPath=Join-Path $root ($Receiver + '-first-import-' + $ImportRun + '.json')
    $first=Get-Content -LiteralPath $firstPath -Raw | ConvertFrom-Json
    if (-not $first.Passed -or $first.HarnessPresent -or $first.Project -cne $project -or $first.PackageSha256 -cne $packageSha -or $first.Package -cne $Package) { throw 'First prove this exact product package alone compiles.' }
    $destination=Join-Path $project 'Assets/Stage9'
    if ($Operation -eq 'InstallHarness' -and (Test-Path -LiteralPath $destination)) { throw 'Never adopt an existing harness.' }
    $history=Join-Path $root ('ToolsHistory/' + $Receiver + '-' + $Run)
    if (Test-Path -LiteralPath $history) { throw 'Preserve harness revision history.' }
    $null=[IO.Directory]::CreateDirectory($history)
    $null=[IO.Directory]::CreateDirectory((Join-Path $destination 'Editor'))
    $changes=@(foreach ($file in @('Stage9DeliveryEditor','Stage9DeliveryProbe','Stage9DeliveryPanel')) {
        $target=if ($file.EndsWith('Editor')) { Join-Path $destination ('Editor/' + $file + '.cs') } else { Join-Path $destination ($file + '.cs') }
        $before=$null
        if ([IO.File]::Exists($target)) { $before=Hash $target; [IO.File]::Copy($target,(Join-Path $history ($file + '.cs.before.txt'))) }
        [IO.File]::Copy((Join-Path $PSScriptRoot ($file + '.cs.txt')),$target,$Operation -eq 'SyncHarness')
        @{ Path=$target; Before=$before; After=Hash $target }
    })
    Json (Join-Path $history 'revision.json') @{ Project=$project; Operation=$Operation; ProductFirstProof=$firstPath; Changes=$changes }
    'Installed test-only consumers after the product-first compile gate.'
    return
}
if ($Operation -eq 'Editor') {
    $log=Join-Path $root ($Receiver + '-' + $Method + '-' + $Run + '.log')
    Run-Unity @('-batchmode','-quit','-nographics','-projectPath',$project,'-buildTarget','Win64','-executeMethod',('Stage9DeliveryEditor.' + $Method),'-s9-run',$Run,'-logFile',$log) $log
    "Completed explicit receiver entry: $Method / $Run"
    return
}
if ($Operation -in @('InstallLogger','UpdateLogger')) {
    $null=Receiver-PackageHash
    $proof=Get-Content -LiteralPath (Join-Path $root ('Runs/'+$Receiver+'-Mono-'+$ProofRun+'/verdict.json')) -Raw | ConvertFrom-Json
    if (-not $proof.Passed -or -not $proof.IdentityMatched -or $proof.ProcessExitCode -ne 0) { throw 'First prove a real product-only independent Player passes without Logger.' }
    $artifact=Get-Content -LiteralPath (Join-Path $root 'Optional/logger-final-receipt.json') -Raw | ConvertFrom-Json
    if (-not $artifact.Passed -or $artifact.RequiredByYLFramework -or $artifact.Sha256 -cne (Hash $artifact.Artifact)) { throw 'The explicit optional licensed artifact must match its audit.' }
    $path=Join-Path $project 'Packages/manifest.json'
    $manifest=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    if ($Operation -eq 'InstallLogger') {
        if ($manifest.dependencies.ContainsKey('com.xfkj.xfgameframework.loggersystem')) { throw 'Never overwrite an existing optional dependency.' }
        Json (Join-Path $root ($Receiver+'-manifest-before-optional.json')) $manifest
    } else {
        $prior=Get-Content -LiteralPath (Join-Path $root ($Receiver+'-optional-install-request.json')) -Raw | ConvertFrom-Json
        if ($manifest.dependencies['com.xfkj.xfgameframework.loggersystem'] -cne ('file:'+$prior.Artifact.Replace('\','/'))) { throw 'Only replace the exact owned failed predecessor artifact.' }
        Json (Join-Path $root ($Receiver+'-manifest-before-final-logger.json')) $manifest
    }
    $manifest.dependencies['com.xfkj.xfgameframework.loggersystem']='file:'+$artifact.Artifact.Replace('\','/')
    [IO.File]::WriteAllText($path,($manifest | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
    Json (Join-Path $root ($Receiver+$(if ($Operation -eq 'UpdateLogger') { '-optional-final-install-request.json' } else { '-optional-install-request.json' }))) @{ ProductOnlyPlayerProof=$ProofRun; Artifact=$artifact.Artifact; Sha256=$artifact.Sha256; RequiredByProduct=$false }
    'Declared only the separately audited optional standalone Logger; not used to repair product dependencies.'
    return
}
if ($Operation -in @('Player','RecoverPlayer')) {
    $build=Join-Path $project ('P/' + $(if ($Backend -eq 'Mono') { 'm-' } else { 'i-' }) + $BuildRun)
    $report=Get-Content -LiteralPath (Join-Path $build 'BuildReport.json') -Raw | ConvertFrom-Json
    if ($report.Result -cne 'Succeeded' -or $report.Errors -ne 0 -or $report.Development) { throw 'A successful non-Development build is required.' }
    foreach ($file in $report.PlayerFiles) {
        if ((Hash (Join-Path $build $file.Path)) -cne $file.Sha256) { throw "Player artifact differs from the real build: $($file.Path)" }
    }
    $runRoot=Join-Path $root ('Runs/' + $Receiver + '-' + $Backend + '-' + $Run)
    $buildReportSha=Hash (Join-Path $build 'BuildReport.json')
    if ($Operation -eq 'Player') {
        if (Test-Path -LiteralPath $runRoot) { throw 'Use RecoverPlayer to collect this same run without restarting it.' }
        $null=[IO.Directory]::CreateDirectory($runRoot)
        $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $build 'Stage9Delivery.exe')); $start.UseShellExecute=$false; $start.WorkingDirectory=$build
        foreach ($arg in @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',(Join-Path $runRoot 'Player.log'),
            '--s9-out',(Join-Path $runRoot 'result.json'),'--s9-command',$Command,'--s9-store',(Join-Path $root ('Stores/' + $Store)),'--s9-expect',$ExpectedVersion.ToString())) { $start.ArgumentList.Add($arg) }
        $process=[Diagnostics.Process]::Start($start)
        Json (Join-Path $runRoot 'process.json') @{ Pid=$process.Id; Executable=$start.FileName; Project=$project; Arguments=@($start.ArgumentList); Command=$Command;
            Store=$Store; ExpectedVersion=$ExpectedVersion; BuildReportSha256=$buildReportSha; StartedUtc=[DateTime]::UtcNow.ToString('O') }
        try { $process.WaitForExit(); Json (Join-Path $runRoot 'exit.json') @{ Pid=$process.Id; ExitCode=$process.ExitCode; EndedUtc=[DateTime]::UtcNow.ToString('O') } }
        finally { $process.Dispose() }
    }
    $invocation=Get-Content -LiteralPath (Join-Path $runRoot 'process.json') -Raw | ConvertFrom-Json
    $exit=Get-Content -LiteralPath (Join-Path $runRoot 'exit.json') -Raw | ConvertFrom-Json
    if ($invocation.Project -cne $project -or $invocation.Executable -cne (Join-Path $build 'Stage9Delivery.exe') -or $invocation.Pid -ne $exit.Pid -or
        $invocation.BuildReportSha256 -cne $buildReportSha -or $invocation.Command -cne $Command -or $invocation.Store -cne $Store -or
        $invocation.ExpectedVersion -ne $ExpectedVersion -or (Get-Process -Id $exit.Pid -ErrorAction SilentlyContinue)) { throw 'Collect only the same completed Player/build identity.' }
    $code=$exit.ExitCode
    $result=Get-Content -LiteralPath (Join-Path $runRoot 'result.json') -Raw | ConvertFrom-Json
    $log=Read-SharedText (Join-Path $runRoot 'Player.log')
    $latePath=Join-Path $runRoot 'result.json.late-errors.log'
    $lateErrors=if (Test-Path -LiteralPath $latePath) { Read-SharedText $latePath } else { '' }
    $exitErrors=@([regex]::Matches($log,'(?im)^.*(?:\b[A-Za-z][A-Za-z0-9_.]*Exception\s*:|Assertion failed|Fatal Error|Crash!!!|UnityEngine\.Debug:LogError|UnityEngine\.Debug:LogException).*$') | ForEach-Object Value)
    $expectedBackend=if ($Backend -eq 'Mono') { 'Mono2x' } else { 'IL2CPP' }
    $identity=$result.Pid -eq $exit.Pid -and $result.Backend -ceq $expectedBackend -and -not $result.Development -and
        $result.BuildGuid -ceq $report.BuildGuid -and $result.Graphics -cne 'Null' -and $result.Platform -ceq 'WindowsPlayer' -and
        $result.LegacyInput -and -not $result.NewInput -and $result.Command -ceq $Command -and $result.Store -ceq (Join-Path $root ('Stores/' + $Store))
    $passed=$code -eq 0 -and $result.BusinessPassed -and $identity -and $exitErrors.Count -eq 0 -and $lateErrors.Length -eq 0 -and $log.Contains('STAGE9_PLAYER_PASS')
    $verdict=@{ Passed=$passed; BusinessPassed=$result.BusinessPassed; ProcessExitCode=$code; IdentityMatched=$identity;
        Pid=$exit.Pid; BuildGuid=$result.BuildGuid; Backend=$result.Backend; Development=$result.Development; ExitErrors=$exitErrors; LateErrors=$lateErrors;
        ResultSha256=Hash (Join-Path $runRoot 'result.json'); PlayerLogSha256=Hash (Join-Path $runRoot 'Player.log'); BuildReportSha256=$buildReportSha }
    $verdictPath=Join-Path $runRoot 'verdict.json'
    if (Test-Path -LiteralPath $verdictPath) {
        $existing=Get-Content -LiteralPath $verdictPath -Raw | ConvertFrom-Json
        if ($existing.Passed -ne $passed -or $existing.ResultSha256 -cne $verdict.ResultSha256 -or $existing.PlayerLogSha256 -cne $verdict.PlayerLogSha256 -or
            $existing.BuildReportSha256 -cne $buildReportSha -or $existing.Pid -ne $exit.Pid) { throw 'Previously collected evidence changed; never overwrite it.' }
    } else { Json $verdictPath $verdict }
    if (-not $passed) { throw 'Independent Player process/identity/late-exit gate failed; preserve this same run.' }
    "Independent graphical Player passed: $Backend / $Command / exit $code (same-run collection supported)"
}
