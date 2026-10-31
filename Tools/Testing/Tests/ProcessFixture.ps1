#requires -Version 7.6
#requires -PSEdition Core
param(
    [ValidateSet('Exit', 'Wait', 'Tree', 'Orphan')][string] $Mode = 'Exit',
    [int] $ExitCode = 0,
    [int] $DelayMilliseconds = 60000,
    [string] $ChildIdentity,
    [string] $Marker
)

$ErrorActionPreference = 'Stop'
[Console]::Out.WriteLine('stdout-marker')
[Console]::Error.WriteLine('stderr-marker')
if ($Mode -eq 'Wait') {
    if ($ChildIdentity) {
        $current = [Diagnostics.Process]::GetCurrentProcess()
        try {
            $identity = @{ pid = $PID; startUtc = $current.StartTime.ToUniversalTime().ToString('o') }
            [IO.File]::WriteAllText($ChildIdentity, ($identity | ConvertTo-Json))
        } finally { $current.Dispose() }
    }
    [Threading.Thread]::Sleep($DelayMilliseconds)
}
if ($Mode -in @('Tree', 'Orphan')) {
    $start = [Diagnostics.ProcessStartInfo]::new([Environment]::ProcessPath)
    $start.UseShellExecute = $false
    $start.WorkingDirectory = $PSScriptRoot
    foreach ($argument in @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $PSCommandPath,
        '-Mode', 'Wait', '-ChildIdentity', $ChildIdentity, '-DelayMilliseconds', [string]$DelayMilliseconds)) { $start.ArgumentList.Add($argument) }
    $child = [Diagnostics.Process]::Start($start)
    try {
        $clock = [Diagnostics.Stopwatch]::StartNew()
        while (-not [IO.File]::Exists($ChildIdentity) -and $clock.ElapsedMilliseconds -lt 10000) {
            [Threading.Thread]::Sleep(20)
        }
        if (-not [IO.File]::Exists($ChildIdentity)) { throw 'Descendant identity was not published.' }
        if ($Mode -eq 'Tree') { $child.WaitForExit() }
    } finally { $child.Dispose() }
}
exit $ExitCode
