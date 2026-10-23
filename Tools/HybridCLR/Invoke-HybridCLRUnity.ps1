#requires -Version 7.6
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Create','ImportBase','ImportOptional','SyncOptional','Resolve','Execute')][string]$Action,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run,
    [string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021',
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe',
    [ValidatePattern('^[A-Za-z0-9-]+\.unitypackage$')][string]$OptionalPackage = 'YLFramework-HybridCLR.unitypackage',
    [ValidatePattern('^YLFramework\.Tests\.HybridCLRReception\.Editor\.HybridCLRReceptionBootstrap\.[A-Za-z]+$')][string]$EntryPoint,
    [switch]$KeepOpenForCallback,
    [switch]$AuditOnly,
    [switch]$Graphics,
    [ValidatePattern('^[A-Za-z0-9-]+\.unitypackage$')][string]$BasePackage = 'YLFramework.unitypackage'
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$scope = & (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root
$Root = $scope.Root
$owner = $scope.Owner
$project = $scope.Project
if (-not (Test-Path -LiteralPath $Root) -or -not (Test-Path -LiteralPath $Unity)) { throw 'Missing explicit receiver parent or Unity executable.' }
$others = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -notmatch 'AssetImportWorker' -and $_.CommandLine.Contains($project) })
if ($others.Count) { throw 'This receiver already has a Unity instance; never launch a second one.' }
$log = Join-Path $Root ($Run + '.log')
if (-not $AuditOnly -and ([IO.File]::Exists($log) -or [IO.File]::Exists($log + '.process.json'))) { throw 'Preserve the previous invocation; choose a new run id.' }
if ($AuditOnly -and ($Action -notin @('ImportBase','ImportOptional') -or
    ([IO.File]::ReadAllText($log + '.exit.json') | ConvertFrom-Json).exitCode -ne 0)) { throw 'Audit-only requires an already exited successful product import.' }
$arguments = @('-batchmode', '-logFile', $log)
if (-not $Graphics) { $arguments += '-nographics' }
if (-not $KeepOpenForCallback) { $arguments += '-quit' }
if ($Action -eq 'Create') {
    if ([IO.Path]::Exists($project)) { throw 'Never adopt or replace an existing project.' }
    $arguments += @('-createProject', $project)
} else {
    if (-not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) { throw 'Receiver project has not been created.' }
    $arguments += @('-projectPath', $project, '-buildTarget', 'Win64')
    if ($Action -in @('ImportBase','ImportOptional','SyncOptional')) {
        $package = Join-Path $Root $(if ($Action -eq 'ImportBase') { $BasePackage } else { $OptionalPackage })
        if (-not (Test-Path -LiteralPath $package)) { throw 'Missing formal exported product artifact.' }
        $arguments += @('-importPackage', $package)
    }
    if ($Action -eq 'Resolve') {
        $manifestPath = Join-Path $project 'Packages/manifest.json'
        $manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -AsHashtable
        $manifest.dependencies['com.code-philosophy.hybridclr'] = 'https://gitee.com/focus-creative-games/hybridclr_unity.git#v9.0.3'
        [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 15), [Text.UTF8Encoding]::new($false))
    }
    if ($Action -eq 'Execute') {
        if (-not $EntryPoint) { throw 'Execute needs one explicit receiver-only entry point.' }
        $arguments += @('-executeMethod', $EntryPoint, '-hclr-run', $Run, '-hclr-root', $Root)
    }
}
$start = [Diagnostics.ProcessStartInfo]::new($Unity)
$start.UseShellExecute = $false
$start.WorkingDirectory = $Root
foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
$code = 0
$artifactPassed = $false
$forcedCleanup = $false
if (-not $AuditOnly) {
    $watcher = $null
    if ($KeepOpenForCallback) {
        $watcher = [IO.FileSystemWatcher]::new($Root, ($Run + '.json'))
        $watcher.EnableRaisingEvents = $true
    }
    $process = [Diagnostics.Process]::Start($start)
    [IO.File]::WriteAllText($log + '.process.json', (@{ pid=$process.Id; project=$project; arguments=$arguments; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json -Depth 5))
    try {
        if ($KeepOpenForCallback) {
            $resultPath = Join-Path $Root ($Run + '.json')
            while (-not [IO.File]::Exists($resultPath) -and -not $process.HasExited) {
                $changed = $watcher.WaitForChanged(([IO.WatcherChangeTypes]::Created -bor [IO.WatcherChangeTypes]::Renamed), 900000)
                if ($changed.TimedOut) { throw 'The external compiler did not publish its atomic completion receipt.' }
            }
            if ([IO.File]::Exists($resultPath)) {
                $result = [IO.File]::ReadAllText($resultPath) | ConvertFrom-Json
                $dll = Join-Path $Root 'ExternalHotUpdate/StandaloneWindows64/YLFramework.HybridCLR.Sample.Support.dll'
                $artifactPassed = $result.passed -and $result.dllSha256 -eq (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()
            }
            if (-not $process.WaitForExit(15000)) { $process.Kill($true); $process.WaitForExit(); $forcedCleanup = $true }
        } else { $process.WaitForExit() }
    } finally { if ($null -ne $watcher) { $watcher.Dispose() } }
    $code = $process.ExitCode
    $process.Dispose()
    [IO.File]::WriteAllText($log + '.exit.json', (@{ exitCode=$code; artifactPassed=$artifactPassed; editorForcedCleanup=$forcedCleanup; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
}
if ($code -ne 0 -and -not ($KeepOpenForCallback -and $artifactPassed -and $forcedCleanup)) { throw "Unity exited $code; preserve $log" }
if ($Action -eq 'Create') {
    $manifestPath = Join-Path $project 'Packages/manifest.json'
    $manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -AsHashtable
    foreach ($key in @($manifest.dependencies.Keys)) { if (-not $key.StartsWith('com.unity.modules.')) { $manifest.dependencies.Remove($key) } }
    $manifest.dependencies['com.unity.ugui'] = '1.0.0'
    $manifest.dependencies['com.unity.nuget.newtonsoft-json'] = '3.2.1'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 15), [Text.UTF8Encoding]::new($false))
}
if ($Action -in @('ImportBase','ImportOptional','SyncOptional')) {
    $stream = [IO.File]::Open($log, 'Open', 'Read', ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $reader = [IO.StreamReader]::new($stream)
    try { $text = $reader.ReadToEnd() } finally { $reader.Dispose(); $stream.Dispose() }
    $errors = @([regex]::Matches($text, '(?im)^.*(?:error CS\d+|Scripts have compiler errors|Aborting batchmode due to failure|Multiple precompiled assemblies).*$') | ForEach-Object Value)
    if ($errors.Count) { throw ($errors -join [Environment]::NewLine) }
    $assemblies = @('YLFramework.Runtime','YLFramework.Editor') | ForEach-Object {
        $path = Join-Path $project ('Library/ScriptAssemblies/' + $_ + '.dll')
        if (-not [IO.File]::Exists($path)) { throw "Missing actual receiver assembly: $_" }
        @{ name=$_; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    }
    $lock = [IO.File]::ReadAllText((Join-Path $project 'Packages/packages-lock.json')) | ConvertFrom-Json -AsHashtable
    if ($Action -ne 'SyncOptional' -and ($lock.dependencies.ContainsKey('com.code-philosophy.hybridclr') -or @($lock.dependencies.Keys | Where-Object { $_ -match 'xfkj|xfab' }).Count)) { throw 'No-package import must precede installation of HybridCLR/XF/test facilities.' }
    $receipt = @{ action=$Action; passed=$true; assemblies=$assemblies; packageSha256=(Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash; testsPresent=[IO.Directory]::Exists((Join-Path $project 'Assets/YLFramework/Tests')); utc=[DateTime]::UtcNow.ToString('o') }
    [IO.File]::WriteAllText((Join-Path $Root ($Run + '.receipt.json')), ($receipt | ConvertTo-Json -Depth 10))
    if ($Action -eq 'SyncOptional') {
        [IO.File]::WriteAllText((Join-Path $Root 'product-selection.json'), (@{ packagePath=$package; packageSha256=$receipt.packageSha256; importReceipt=($Run + '.receipt.json') } | ConvertTo-Json))
    }
}
"Completed $Action; exit=$code; artifactPassed=$artifactPassed; editorForcedCleanup=$forcedCleanup; log=$log"
