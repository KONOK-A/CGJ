#requires -Version 7.6
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Snapshot', 'Verify', 'Metas', 'Player', 'Xml', 'Archive', 'Wait', 'Binary')][string] $Mode,
    [Parameter(Mandatory)][string] $RunDirectory,
    [string] $Gate = '',
    [ValidateSet('summary.json', 'results.json', 'build-report.json')][string] $ResultFile = 'summary.json'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ($root -ne 'G:\Unity\Projects\CGJ2024' -or (Get-Location).Path -ne $root) { throw 'Run only in the main CGJ2024 workspace.' }
if ((git branch --show-current) -ne 'integration/xf-yl') { throw 'Wrong integration branch.' }
$candidate = 'ce1898e466d81cda187440293e89e2b596a84a18'
$baseline = 'c88500aef6651f5b13ac933dd1f209ecb314e92d'
$RunDirectory = [IO.Path]::GetFullPath($RunDirectory)
$parent = 'C:\Users\Kano\AppData\Local\Temp\kilo'
if (-not (Test-Path -LiteralPath $parent) -or $RunDirectory -notmatch '^C:\\Users\\Kano\\AppData\\Local\\Temp\\kilo\\BM26-[0-9TZ-]+$') { throw 'Use a new main-only BM26 evidence root.' }
function Write-Record([string] $Name, $Data) {
    [IO.File]::WriteAllText((Join-Path $RunDirectory $Name), (ConvertTo-Json -InputObject $Data -Depth 16), [Text.UTF8Encoding]::new($false))
}
function Read-Record([string] $Name) { return ConvertFrom-Json -InputObject ([IO.File]::ReadAllText((Join-Path $RunDirectory $Name))) }
function Hash([string] $Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
if ($Mode -eq 'Snapshot') {
    if (Test-Path -LiteralPath $RunDirectory) { throw 'Evidence root already exists.' }
    if (@(git diff --cached --name-only).Count) { throw 'Pre-existing staged content.' }
    if (Test-Path -LiteralPath '.git/MERGE_HEAD') { throw 'Pre-existing merge.' }
    $paths = @(git diff --name-only $baseline $candidate)
    $commits = @(git log --reverse --format='%H %s' "$baseline..$candidate")
    if ($paths.Count -ne 288 -or $commits.Count -ne 6) { throw 'Frozen candidate collection mismatch.' }
    $dirty = @(git diff --name-only)
    $untracked = @(git ls-files --others --exclude-standard)
    if (@($dirty + $untracked | Where-Object { $_ -in $paths }).Count) { throw 'Existing changes overlap candidate.' }
    $null = New-Item -ItemType Directory -Path $RunDirectory
    $records = @(foreach ($path in @(git ls-files)) {
        if (Test-Path -LiteralPath $path -PathType Leaf) { [ordered]@{ path = $path; sha256 = (Hash $path) } }
    })
    $metas = @(foreach ($path in $paths | Where-Object { $_.EndsWith('.meta') }) {
        $text = (git show "${candidate}:$path") -join "`n"
        [ordered]@{ path = $path; guid = [regex]::Match($text, '(?m)^guid: ([0-9a-f]{32})').Groups[1].Value; blob = (git rev-parse "${candidate}:$path") }
    })
    if ($metas.Count -ne 151 -or @($metas | Where-Object { -not $_.guid }).Count) { throw 'Frozen meta collection mismatch.' }
    $editors = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | ForEach-Object {
        $project = ''; if ($_.CommandLine -match '(?i)-projectpath\s+(?:"([^"]+)"|(\S+))') { $project = $Matches[1] + $Matches[2] }
        [ordered]@{ pid = $_.ProcessId; parentPid = $_.ParentProcessId; project = $project; batch = $_.CommandLine -match '(?i)-batchmode'; executable = $_.ExecutablePath }
    })
    Write-Record 'preflight.json' ([ordered]@{ utc = [DateTime]::UtcNow.ToString('o'); project = $root; branch = (git branch --show-current); head = (git rev-parse HEAD); candidate = $candidate; baseline = $baseline; commits = $commits; paths = $paths; dirty = $dirty; untracked = $untracked; status = @(git status --porcelain=v2 --branch --untracked-files=all); editors = $editors; files = $records; userMetaSha256 = (Hash 'Assets/YLFramework/Tests/Fixtures/WorkbenchUX1-20261017/AlternateContent.meta'); planDiff = @(git diff -- .kilo/plans/1792420261057-xf-ecosystem-integration.md) })
    Write-Record 'candidate-metas.json' $metas
    "Frozen $($commits.Count) commits, $($paths.Count) candidate paths, $($metas.Count) metas and $($records.Count) existing file hashes in $RunDirectory"
    exit 0
}
if (-not (Test-Path -LiteralPath (Join-Path $RunDirectory 'preflight.json'))) { throw 'Missing main-project preflight binding.' }
$preflight = Read-Record 'preflight.json'
if ($preflight.project -ne $root -or $preflight.candidate -ne $candidate) { throw 'Evidence identity mismatch.' }
if ($Mode -eq 'Verify') {
    $changed = @(foreach ($file in $preflight.files) {
        if (-not (Test-Path -LiteralPath $file.path -PathType Leaf) -or (Hash $file.path) -ne $file.sha256) { $file.path }
    })
    $protected = @($changed | Where-Object { $_ -ne '.kilo/plans/1792420261057-xf-ecosystem-integration.md' -and $_ -ne 'Assets/YLFramework/LICENSE.md' -and $_ -ne 'Assets/YLFramework/Runtime/THIRD-PARTY-NOTICES.md' })
    $userUnchanged = (Hash 'Assets/YLFramework/Tests/Fixtures/WorkbenchUX1-20261017/AlternateContent.meta') -eq $preflight.userMetaSha256
    Write-Record ('protection-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '.json') ([ordered]@{ changed = $changed; protectedChanges = $protected; userMetaUnchanged = $userUnchanged })
    "Existing changed paths: $($changed -join ', '); protected changes=$($protected.Count); user meta unchanged=$userUnchanged"
    if ($protected.Count -or -not $userUnchanged) { throw 'Protected pre-existing content changed.' }
}
if ($Mode -eq 'Metas') {
    $expected = @(Read-Record 'candidate-metas.json')
    $guids = [Collections.Generic.Dictionary[string, Collections.Generic.List[string]]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in [IO.Directory]::EnumerateFiles((Join-Path $root 'Assets'), '*.meta', [IO.SearchOption]::AllDirectories)) {
        $match = [regex]::Match([IO.File]::ReadAllText($path), '(?m)^guid:\s*([0-9a-f]{32})\s*$')
        if (-not $match.Success) { continue }
        $guid = $match.Groups[1].Value
        if (-not $guids.ContainsKey($guid)) { $guids.Add($guid, [Collections.Generic.List[string]]::new()) }
        $guids[$guid].Add($path.Substring($root.Length + 1).Replace('\', '/'))
    }
    $records = @(foreach ($meta in $expected) {
        $exists = Test-Path -LiteralPath $meta.path
        [ordered]@{ path = $meta.path; guid = $meta.guid; exists = $exists; assetExists = (Test-Path -LiteralPath $meta.path.Substring(0, $meta.path.Length - 5)); blobMatches = $exists -and (git hash-object -- $meta.path) -eq $meta.blob; rawPaths = @(if ($guids.ContainsKey($meta.guid)) { $guids[$meta.guid].ToArray() }); sha256 = $(if ($exists) { Hash $meta.path } else { '' }) }
    })
    $errors = @($records | Where-Object { -not $_.exists -or -not $_.assetExists -or -not $_.blobMatches -or @($_.rawPaths).Count -ne 1 -or $_.rawPaths[0] -ne $_.path })
    Write-Record ('meta-audit-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '.json') ([ordered]@{ utc = [DateTime]::UtcNow.ToString('o'); project = $root; rawGuidCount = $guids.Count; candidateMetaCount = $records.Count; errors = $errors; records = $records })
    "Candidate metas=$($records.Count); raw Assets GUIDs=$($guids.Count); errors=$($errors.Count)"
    if ($errors.Count) { throw 'Frozen A meta/GUID identity failed.' }
}
if ($Mode -eq 'Xml') {
    $records = @(foreach ($path in [IO.Directory]::EnumerateFiles($RunDirectory, 'results.xml', [IO.SearchOption]::AllDirectories)) {
        [xml] $xml = [IO.File]::ReadAllText($path)
        $cases = @($xml.SelectNodes('//test-case'))
        $failed = @($cases | Where-Object { $_.GetAttribute('result') -ne 'Passed' } | ForEach-Object { $failure = $_.SelectSingleNode('failure'); [ordered]@{ name = $_.GetAttribute('fullname'); result = $_.GetAttribute('result'); message = $(if ($null -ne $failure) { $failure.InnerText } else { $_.InnerText }) } })
        [ordered]@{ path = $path; sha256 = (Hash $path); total = $cases.Count; passed = @($cases | Where-Object { $_.GetAttribute('result') -eq 'Passed' }).Count; notPassed = $failed }
    })
    Write-Record ('xml-readback-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '.json') $records
    $records | ConvertTo-Json -Depth 8
}
if ($Mode -eq 'Binary') {
    Add-Type -AssemblyName System.Reflection.Metadata
    $build = Read-Record 'build-mono.json'
    $managed = Join-Path ([IO.Path]::GetDirectoryName($build.output)) 'BehaviorMainAcceptance_Data/Managed'
    if (-not $managed.StartsWith($RunDirectory + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unowned build path.' }
    $records = @(foreach ($name in @('YLFramework.Runtime', 'YLFramework.Behavior.Acceptance')) {
        $path = Join-Path $managed ($name + '.dll')
        $stream = [IO.File]::OpenRead($path)
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            $references = @(foreach ($handle in $reader.AssemblyReferences) { $reference = $reader.GetAssemblyReference($handle); [ordered]@{ name = $reader.GetString($reference.Name); version = $reference.Version.ToString() } })
            [ordered]@{ path = $path; sha256 = (Hash $path); name = $name; references = $references }
        } finally { $pe.Dispose(); $stream.Dispose() }
    })
    $forbidden = @($records | ForEach-Object { $_.references } | Where-Object { $_.name -match 'UnityEditor|^XF|EPPlus' })
    $editorAssemblies = @([IO.Directory]::EnumerateFiles($managed, '*Editor*.dll'))
    Write-Record 'player-assemblyref.json' ([ordered]@{ project = $root; candidate = $candidate; build = $build.output; records = $records; forbidden = $forbidden; editorAssemblies = $editorAssemblies })
    "Player binary AssemblyRef: $($records.Count) owned assemblies; forbidden=$($forbidden.Count); shipped Editor DLLs=$($editorAssemblies.Count)"
    if ($forbidden.Count -or $editorAssemblies.Count) { throw 'Player binary isolation failed.' }
}
if ($Mode -eq 'Wait') {
    if ($Gate -notmatch '^[a-z0-9-]+$') { throw 'Specify one existing gate.' }
    $directory = Join-Path $RunDirectory $Gate
    if (-not (Test-Path -LiteralPath $directory)) { throw 'Gate was not started.' }
    $watcher = [IO.FileSystemWatcher]::new($directory, $ResultFile)
    try {
        $watcher.EnableRaisingEvents = $true
        if (-not (Test-Path -LiteralPath (Join-Path $directory $ResultFile))) {
            $event = $watcher.WaitForChanged([IO.WatcherChangeTypes]::Created, 600000)
            if ($event.TimedOut) { throw 'Evidence wait timed out; main Editor remains untouched.' }
        }
        "Evidence ready: $directory\$ResultFile"
    } finally { $watcher.Dispose() }
}
if ($Mode -eq 'Archive') {
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
    $source = [IO.FileStream]::new('C:\Users\Kano\AppData\Local\Unity\Editor\Editor.log', [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $targetPath = Join-Path $RunDirectory "main-editor-$stamp.log"
    $target = [IO.FileStream]::new($targetPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try { $source.CopyTo($target) } finally { $target.Dispose(); $source.Dispose() }
    $paths = @(@(git diff --cached --name-only) + @(git diff --name-only) + @(git ls-files --others --exclude-standard) | Sort-Object -Unique)
    $files = @(foreach ($path in $paths) { if (Test-Path -LiteralPath $path -PathType Leaf) { [ordered]@{ path = $path; sha256 = (Hash $path); blob = (git hash-object -- $path) } } })
    Write-Record "file-binding-$stamp.json" ([ordered]@{ project = $root; beforeHead = $preflight.head; head = (git rev-parse HEAD); candidate = $candidate; utc = [DateTime]::UtcNow.ToString('o'); editorLog = $targetPath; editorLogSha256 = (Hash $targetPath); files = $files; status = @(git status --porcelain=v2 --branch --untracked-files=all) })
    "Archived main Editor log and $($files.Count) current file identities."
}
if ($Mode -eq 'Player') {
    $build = Read-Record 'build-mono.json'
    if ($build.actualBackend -ne 'Mono2x' -or $build.development -or $build.status -ne 'Succeeded') { throw 'Require successful Release Mono build.' }
    $exe = $build.output
    if (-not $exe.StartsWith($RunDirectory + '\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $exe)) { throw 'Wrong executable path.' }
    $log = Join-Path $RunDirectory 'player-graphics.log'
    if (Test-Path -LiteralPath $log) { throw 'Player evidence already exists.' }
    $start = [Diagnostics.ProcessStartInfo]::new($exe)
    $start.UseShellExecute = $false
    $start.ArgumentList.Add('-logFile'); $start.ArgumentList.Add($log)
    $start.ArgumentList.Add('-screen-fullscreen'); $start.ArgumentList.Add('0')
    $start.ArgumentList.Add('-screen-width'); $start.ArgumentList.Add('960')
    $start.ArgumentList.Add('-screen-height'); $start.ArgumentList.Add('540')
    $process = [Diagnostics.Process]::Start($start)
    $inputIdle = $process.WaitForInputIdle(10000)
    $process.Refresh()
    $window = $process.MainWindowHandle.ToInt64()
    Write-Record 'player-start.json' ([ordered]@{ pid = $process.Id; utc = [DateTime]::UtcNow.ToString('o'); exe = $exe; exeSha256 = (Hash $exe); arguments = @($start.ArgumentList); graphics = $true; inputIdle = $inputIdle; windowHandle = $window; windowTitle = $process.MainWindowTitle })
    $process.WaitForExit()
    $text = [IO.File]::ReadAllText($log)
    $passes = [regex]::Matches($text, '(?m)^YL_BEHAVIOR_ACCEPTANCE result=PASS[^\r\n]*')
    $valid = $process.ExitCode -eq 0 -and $window -ne 0 -and $passes.Count -eq 1 -and $passes[0].Value -match 'backend=Mono' -and $passes[0].Value -match 'development=False' -and $passes[0].Value -match 'graphics=Direct3D' -and $text -notmatch 'YL_BEHAVIOR_ACCEPTANCE result=FAIL|NullReferenceException|MissingMethodException|TypeLoadException'
    Write-Record 'player-result.json' ([ordered]@{ utc = [DateTime]::UtcNow.ToString('o'); pid = $process.Id; exitCode = $process.ExitCode; graphics = $true; exe = $exe; exeSha256 = (Hash $exe); log = $log; logSha256 = (Hash $log); passCount = $passes.Count; passLine = $(if ($passes.Count) { $passes[0].Value } else { '' }); passed = $valid })
    "Graphics Player PID=$($process.Id), exit=$($process.ExitCode), PASS count=$($passes.Count), valid=$valid"
    if (-not $valid) { throw 'Graphical Player failed. Preserve this run.' }
}
