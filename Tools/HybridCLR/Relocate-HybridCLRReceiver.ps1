#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021')
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$scope = . (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root
$Root = $scope.Root
$ownerPath = Join-Path $Root 'owner.json'
$receiptPath = Join-Path $Root 'persistent-short-project-relocation.json'
Assert-NoLinks $PSCommandPath
Assert-NoLinks $receiptPath
if ([IO.Path]::Exists($receiptPath)) { throw 'Preserve the existing relocation receipt.' }
$owner = $scope.Owner
$originalOwnerJson = $scope.OwnerJson
$source = $scope.Project
if ($source -ne (Join-Path $Root 'R') -and $source -ne [IO.Path]::GetFullPath((Join-Path $owner.repository 'Temp/H26'))) { throw 'Only the explicitly owned receivers can be copied.' }
$parent = [IO.DirectoryInfo]::new($scope.Repository).Parent.Parent.FullName
$destination = $scope.PersistentProject
$cache = $scope.CacheRoot
Assert-NoLinks $destination
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
Assert-NoLinks $receiptPath
Assert-NoLinks $ownerPath
$current = & (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root
if ($current.OwnerJson -cne $originalOwnerJson) { throw 'Receiver ownership changed during the physical copy.' }
$receipt = [IO.FileStream]::new($receiptPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $bytes = [Text.Encoding]::UTF8.GetBytes((@{ source=$source; destination=$destination; noLinks=$true; regeneratedFolders=@('Library','Temp','obj'); files=$records; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json -Depth 10))
    $receipt.Write($bytes, 0, $bytes.Length)
    $receipt.Flush($true)
} finally { $receipt.Dispose() }
$pending = $ownerPath + '.pending-' + [Guid]::NewGuid().ToString('N')
Assert-NoLinks $pending
$output = [IO.FileStream]::new($pending, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $bytes = [Text.Encoding]::UTF8.GetBytes(($owner | ConvertTo-Json -Depth 10))
    $output.Write($bytes, 0, $bytes.Length)
    $output.Flush($true)
} finally { $output.Dispose() }
Assert-NoLinks $ownerPath
$current = & (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root
if ($current.OwnerJson -cne $originalOwnerJson) { throw 'Receiver ownership changed before its atomic update; preserve the pending file.' }
[IO.File]::Move($pending, $ownerPath, $true)
"Physically copied $($records.Count) verified source/configuration files to $destination; old inputs and logs preserved; no Library or symlink copied."
