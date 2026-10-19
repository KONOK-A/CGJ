#requires -Version 7.6
[CmdletBinding()]
param([Parameter(Mandatory)][string] $DeliveryRoot, [switch] $Final)
Set-StrictMode -Version 3.0
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($DeliveryRoot)
$owner=Get-Content -LiteralPath (Join-Path $root 'owner.json') -Raw | ConvertFrom-Json
$optional=Get-Content -LiteralPath (Join-Path $root 'Optional/owner.json') -Raw | ConvertFrom-Json
if ($owner.purpose -cne 'YLFramework-Stage9-independent-delivery' -or [IO.Path]::GetFullPath($owner.root) -cne $root -or
    $optional.purpose -cne 'Stage9-explicit-optional-standalone-Logger-artifact') { throw 'Owned optional artifact identity required.' }
$source=[IO.Path]::GetFullPath($optional.source)
if ($source -cne (Join-Path ([IO.Path]::GetFullPath($owner.repository)) 'Packages/com.xfkj.xfgameframework.loggersystem')) { throw 'Do not package another original XF component.' }
$package=Join-Path $root $(if ($Final) { 'Optional/Final/com.xfkj.xfgameframework.loggersystem-1.0.1.tgz' } else { 'Optional/com.xfkj.xfgameframework.loggersystem-1.0.1.tgz' })
function Hash([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
$expected=@{}
foreach ($file in [IO.Directory]::GetFiles($source,'*',[IO.SearchOption]::AllDirectories)) {
    if (([IO.File]::GetAttributes($file) -band [IO.FileAttributes]::ReparsePoint) -or $file -match '\.(pfx|pem|key|p12)$') { throw 'Unapproved link or secret-bearing artifact.' }
    $expected[[IO.Path]::GetRelativePath($source,$file).Replace('\','/')]=Hash $file
}
Add-Type -AssemblyName System.Formats.Tar
$stream=[IO.File]::OpenRead($package); $gzip=[IO.Compression.GZipStream]::new($stream,[IO.Compression.CompressionMode]::Decompress); $reader=[System.Formats.Tar.TarReader]::new($gzip,$true)
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$rows=[Collections.Generic.List[object]]::new()
try {
    while ($null -ne ($entry=$reader.GetNextEntry())) {
        if ($entry.EntryType -eq [System.Formats.Tar.TarEntryType]::Directory) { continue }
        if ($entry.Name -notmatch '^package/(.+)$') { throw 'Unexpected archive root.' }
        $path=$Matches[1]
        if (-not $expected.ContainsKey($path) -or -not $seen.Add($path) -or $path -match '(^|/)\.\.?(/|$)' -or
            ($path.EndsWith('.dll') -and $path -cne 'Editor/XFGameFramework.LoggerSystemCodeAnalyzer.dll')) { throw "Unapproved optional payload: $path" }
        $memory=[IO.MemoryStream]::new()
        try {
            $entry.DataStream.CopyTo($memory); $bytes=$memory.ToArray()
            $sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
            if ($sha -cne $expected[$path]) { throw "Optional source/archive payload mismatch: $path" }
            $guid=if ($path.EndsWith('.meta')) { [regex]::Match([Text.Encoding]::UTF8.GetString($bytes),'(?m)^guid:\s*([0-9a-f]{32})').Groups[1].Value } else { $null }
            $rows.Add(@{ Path=$path; Sha256=$sha; Length=$bytes.Length; Guid=$guid })
        } finally { $memory.Dispose() }
    }
} finally { $reader.Dispose(); $gzip.Dispose(); $stream.Dispose() }
if ($seen.Count -ne $expected.Count -or -not $seen.Contains('LICENSE') -or -not $seen.Contains('CHANGELOG.md')) { throw 'Optional source/license closure incomplete.' }
$metadata=Get-Content -LiteralPath (Join-Path $source 'package.json') -Raw | ConvertFrom-Json
if ($metadata.version -cne '1.0.1' -or @($metadata.dependencies.PSObject.Properties).Count -ne 0) { throw 'Unexpected Logger package dependency graph.' }
$receipt=Join-Path $root $(if ($Final) { 'Optional/logger-final-receipt.json' } else { 'Optional/logger-artifact-receipt.json' })
if (Test-Path -LiteralPath $receipt) { throw 'Preserve optional artifact audit evidence.' }
$value=@{ Passed=$true; Artifact=$package; Sha256=Hash $package; Source=$source; SourceHead=(& git -C $owner.repository rev-parse HEAD);
    Package=$metadata.name; PackageVersion=$metadata.version; Dependencies=$metadata.dependencies; RequiredByYLFramework=$false;
    License='MIT, Copyright (c) 2025 弦风课堂'; Files=@($rows | Sort-Object Path -CaseSensitive); CoexistenceVerified=$false }
[IO.File]::WriteAllText($receipt,($value | ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
"Optional standalone Logger archive/license/source closure verified: $($rows.Count) files, $(Hash $package). Runtime coexistence remains a separate gate."
