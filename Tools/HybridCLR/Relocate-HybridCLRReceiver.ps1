#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021')
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$ownerPath = Join-Path $Root 'owner.json'
$owner = [IO.File]::ReadAllText($ownerPath) | ConvertFrom-Json -AsHashtable
if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-independent-reception') { throw 'Unexpected owner.' }
$source = [IO.Path]::GetFullPath($owner.project)
if ($source -ne (Join-Path $Root 'R') -and $source -ne [IO.Path]::GetFullPath((Join-Path $owner.repository 'Temp/H26'))) { throw 'Only the explicitly owned receivers can be copied.' }
$parent = [IO.DirectoryInfo]::new($owner.repository).Parent.Parent.FullName
$destination = [IO.Path]::GetFullPath((Join-Path $parent 'H26'))
$cache = [IO.Path]::GetFullPath((Join-Path $parent 'HC26'))
if (-not (Test-Path -LiteralPath $parent) -or [IO.Path]::Exists($destination) -or [IO.Path]::Exists($cache)) { throw 'The explicit short parent must exist and the new project/cache paths must be unused.' }
$active = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($source) })
if ($active.Count) { throw 'Exit the owned source receiver before copying; never duplicate a live project.' }
$records = [Collections.Generic.List[object]]::new()
foreach ($folder in @('Assets','Packages','ProjectSettings')) {
    $base = Join-Path $source $folder
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($base)
    while ($pending.Count) {
        $directory = $pending.Pop()
        if ([IO.File]::GetAttributes($directory) -band [IO.FileAttributes]::ReparsePoint) { throw 'Do not follow links in an acceptance project.' }
        $relativeDirectory = $directory.Substring($source.Length + 1)
        [IO.Directory]::CreateDirectory((Join-Path $destination $relativeDirectory)) | Out-Null
        foreach ($child in [IO.Directory]::GetDirectories($directory)) { $pending.Push($child) }
        foreach ($file in [IO.Directory]::GetFiles($directory)) {
            if ([IO.File]::GetAttributes($file) -band [IO.FileAttributes]::ReparsePoint) { throw 'Do not copy linked inputs.' }
            $relative = $file.Substring($source.Length + 1)
            $target = Join-Path $destination $relative
            [IO.File]::Copy($file, $target, $false)
            $before = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
            if ($before -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) { throw 'Physical copy did not preserve the input bytes.' }
            $records.Add(@{ path=$relative; sha256=$before })
        }
    }
}
$owner.projectHistory = @($owner.projectHistory) + @(@{ path=$source; reason='Formal Resource Windows path preflight rejected the long path before publication'; preserved=$true })
$owner.project = $destination
$owner.runtimeCacheRoot = $cache
[IO.File]::WriteAllText((Join-Path $Root 'persistent-short-project-relocation.json'), (@{ source=$source; destination=$destination; noLinks=$true; regeneratedFolders=@('Library','Temp','obj'); files=$records; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json -Depth 10))
[IO.File]::WriteAllText($ownerPath, ($owner | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
"Physically copied $($records.Count) verified source/configuration files to $destination; old inputs and logs preserved; no Library or symlink copied."
