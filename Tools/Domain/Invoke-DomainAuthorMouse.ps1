[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Skill', 'Buff', 'Mission')][string] $Module,
    [Parameter(Mandatory = $true)][ValidateSet('Focus', 'Click')][string] $Action,
    [ValidatePattern('^[0-9]+$')][string] $Attempt = '01',
    [int] $X = 80,
    [int] $Y = 68
)
$ErrorActionPreference = 'Stop'
$root = 'G:\Unity\MC26-20261020T173208Z'
$owner = [IO.File]::ReadAllText((Join-Path $root 'owner.json')) | ConvertFrom-Json
if ($owner.pid -ne 16464 -or $owner.sourceTip -ne 'aadc11b5099b76f16667aa9818ca927104cd12be') { throw 'Wrong main Editor owner.' }
$editor = Get-CimInstance Win32_Process -Filter 'ProcessId = 16464'
if (-not $editor -or $editor.Name -ne 'Unity.exe' -or $editor.CommandLine.Replace('\', '/').IndexOf('G:/Unity/Projects/CGJ2024', [StringComparison]::OrdinalIgnoreCase) -lt 0) { throw 'Bound Editor missing; do not target another process.' }
$folder = Join-Path $root 'author-gui-01'
if (-not (Test-Path -LiteralPath (Join-Path $folder 'roots.json'))) { throw 'No owned author GUI fixture is active.' }
$suffix = if ($Attempt -eq '01') { '' } else { '-' + $Attempt }
$path = Join-Path $folder ($Module.ToLowerInvariant() + '-physical-' + $Action.ToLowerInvariant() + $suffix + '.json')
if (Test-Path -LiteralPath $path) { throw 'Physical input evidence already exists.' }
if (-not (Test-Path -LiteralPath $env:TEMP)) { throw 'Missing compiler temporary parent.' }
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; using System.Text; public static class MC26AuthorInput { public delegate bool EnumProc(IntPtr h, IntPtr p); [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; } [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; } [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p); [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid); [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder text, int length); [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h); [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow(); [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int state); [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect rect); [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h); [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context); [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point); [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y); [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra); }'
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class MC26AuthorFocus { [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId(); [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint from, uint to, bool attach); [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr window); }'
$windows = New-Object 'System.Collections.Generic.List[System.IntPtr]'
$title = "MC26 $Module Author Acceptance"
$callback = [MC26AuthorInput+EnumProc] { param($handle, $parameter)
    $windowPid = [uint32]0
    $null = [MC26AuthorInput]::GetWindowThreadProcessId($handle, [ref]$windowPid)
    if ($windowPid -eq 16464 -and [MC26AuthorInput]::IsWindowVisible($handle)) {
        $text = New-Object Text.StringBuilder(512)
        $null = [MC26AuthorInput]::GetWindowText($handle, $text, 512)
        if ($text.ToString() -eq $title) { $windows.Add($handle) }
    }
    return $true
}
$null = [MC26AuthorInput]::EnumWindows($callback, [IntPtr]::Zero)
if ($windows.Count -ne 1) { throw 'The exact new owned author utility window is not uniquely visible.' }
$window = $windows[0]
$oldContext = [MC26AuthorInput]::SetThreadDpiAwarenessContext([IntPtr](-4))
$cursor = New-Object MC26AuthorInput+Point
$null = [MC26AuthorInput]::GetCursorPos([ref]$cursor)
try {
    $null = [MC26AuthorInput]::ShowWindow($window, 9)
    $foregroundRequested = [MC26AuthorInput]::SetForegroundWindow($window)
    if ([MC26AuthorInput]::GetForegroundWindow() -ne $window) {
        $foregroundPid = [uint32]0
        $foregroundThread = [MC26AuthorInput]::GetWindowThreadProcessId([MC26AuthorInput]::GetForegroundWindow(), [ref]$foregroundPid)
        $thread = [MC26AuthorFocus]::GetCurrentThreadId()
        $attached = [MC26AuthorFocus]::AttachThreadInput($thread, $foregroundThread, $true)
        try {
            $null = [MC26AuthorFocus]::BringWindowToTop($window)
            $foregroundRequested = [MC26AuthorInput]::SetForegroundWindow($window)
        } finally { if ($attached) { $null = [MC26AuthorFocus]::AttachThreadInput($thread, $foregroundThread, $false) } }
    }
    $rect = New-Object MC26AuthorInput+Rect
    if (-not [MC26AuthorInput]::GetWindowRect($window, [ref]$rect)) { throw 'Owned window bounds unavailable.' }
    $dpi = [MC26AuthorInput]::GetDpiForWindow($window)
    $receipt = @{ module = $Module; action = $Action; pid = 16464; window = $window.ToInt64(); dpi = $dpi;
        bounds = $rect; foregroundRequested = $foregroundRequested; foreground = [MC26AuthorInput]::GetForegroundWindow().ToInt64(); utc = [DateTime]::UtcNow.ToString('o'); physicalMouse = $false }
    if ($Action -eq 'Click') {
        if ([MC26AuthorInput]::GetForegroundWindow() -ne $window -or $X -lt 10 -or $X -gt 300 -or $Y -lt 30 -or $Y -gt 130) { throw 'Click is restricted to the visible owned left tree.' }
        $xPixel = $rect.Left + [int]($X * $dpi / 96.0)
        $yPixel = $rect.Top + [int]($Y * $dpi / 96.0)
        $null = [MC26AuthorInput]::SetCursorPos($xPixel, $yPixel)
        [MC26AuthorInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
        [MC26AuthorInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        $receipt.physicalMouse = $true; $receipt.x = $xPixel; $receipt.y = $yPixel
    }
    [IO.File]::WriteAllText($path, ($receipt | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
    $receipt | ConvertTo-Json -Depth 5
} finally {
    $null = [MC26AuthorInput]::SetCursorPos($cursor.X, $cursor.Y)
    $null = [MC26AuthorInput]::SetThreadDpiAwarenessContext($oldContext)
}
