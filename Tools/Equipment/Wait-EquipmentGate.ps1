param([Parameter(Mandatory = $true)][string]$Gate, [int]$TimeoutSeconds = 600)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($Gate)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Gate directory does not exist.' }
$result = Join-Path $root 'gate-complete.json'
if (-not (Test-Path -LiteralPath $result -PathType Leaf)) {
    $watcher = New-Object System.IO.FileSystemWatcher $root, 'gate-complete.json'
    try {
        $watcher.EnableRaisingEvents = $true
        if (-not (Test-Path -LiteralPath $result -PathType Leaf)) {
            $change = $watcher.WaitForChanged([System.IO.WatcherChangeTypes]::Created -bor [System.IO.WatcherChangeTypes]::Renamed, $TimeoutSeconds * 1000)
            if ($change.TimedOut) { throw 'Gate notification timed out; the Unity process is preserved.' }
        }
    } finally { $watcher.Dispose() }
}
$text = [System.IO.File]::ReadAllText($result)
$value = $text | ConvertFrom-Json
$text
if (-not $value.succeeded) { exit 1 }
