#requires -Version 7.6
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('V1','Update','V2','BadPayload','BadMetadata')][string]$Action,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+$')][string]$Run,
    [ValidateSet('Development','Release')][string]$Variant = 'Development',
    [string]$TrustReceipt,
    [string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021'
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$owner = [IO.File]::ReadAllText((Join-Path $Root 'owner.json')) | ConvertFrom-Json
if ($owner.purpose -ne 'YLFramework-HybridCLR-9.0.3-independent-reception') { throw 'Unexpected receiver owner.' }
$playerRoot = [IO.Path]::GetFullPath($(if ($Variant -eq 'Development') { $owner.playerRoot } else { $owner.releasePlayerRoot }))
$player = Join-Path $playerRoot 'HybridCLR.exe'
if (-not (Test-Path -LiteralPath $Root) -or -not (Test-Path -LiteralPath $player)) { throw 'A real successful base Player is required.' }
$log = Join-Path $Root ($Run + '.log')
$receipt = Join-Path $Root ($Run + '.json')
if ([IO.File]::Exists($log) -or [IO.File]::Exists($receipt)) { throw 'Do not overwrite prior Player evidence.' }
function Get-PlayerFingerprint {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $rows = @([IO.Directory]::GetFiles($playerRoot, '*', [IO.SearchOption]::AllDirectories) | Sort-Object -CaseSensitive | ForEach-Object {
            $relative = $_.Substring($playerRoot.Length + 1).Replace('\','/')
            $relative + ':' + (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
        })
        return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($rows -join "`n")))).ToLowerInvariant()
    } finally { $sha.Dispose() }
}
$before = Get-PlayerFingerprint
$baseReceipt = Join-Path $Root $(if ($Variant -eq 'Development') { $owner.baseReceiptStem + '.json' } else { 'base-release-receipt.json' })
$arguments = @('-batchmode','-nographics','-logFile',$log,'-hclr-root',$Root,'-hclr-action',$Action,'-hclr-run',$Run,'-hclr-base-receipt',$baseReceipt)
if ($Action -in @('Update','BadPayload')) {
    $ready = if ($TrustReceipt) { [IO.Path]::GetFullPath($TrustReceipt) } else { Join-Path $Root $(if ($Action -eq 'Update') { 'ServerV2-ready.json' } else { 'ServerBad-ready.json' }) }
    if (-not (Test-Path -LiteralPath $ready)) { throw 'Start the tracked owned HTTPS fixture first.' }
    $arguments += @('-hclr-trust',$ready)
}
if ($Action -eq 'BadMetadata') { $arguments += @('-hclr-candidate',(Join-Path $Root 'bad-metadata-fixture.json')) }
$start = [Diagnostics.ProcessStartInfo]::new($player)
$start.UseShellExecute = $false
$start.WorkingDirectory = $Root
foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($start)
[IO.File]::WriteAllText($log + '.process.json', (@{ pid=$process.Id; action=$Action; playerSha256=(Get-FileHash -LiteralPath $player -Algorithm SHA256).Hash; utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
if (-not $process.WaitForExit(600000)) { $process.Kill($true); throw 'Owned Player exceeded its bounded acceptance execution time.' }
$code = $process.ExitCode
$process.Dispose()
$after = Get-PlayerFingerprint
[IO.File]::WriteAllText($log + '.exit.json', (@{ exitCode=$code; before=$before; after=$after; unchanged=($before -eq $after); utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json))
if ($code -ne 0) { throw "Actual Player failed with exit $code; preserve $log and $receipt" }
$result = [IO.File]::ReadAllText($receipt) | ConvertFrom-Json
if (-not $result.passed -or $result.simulation -or $before -ne $after) { throw 'The native result, Simulation flag, or unchanged-Player check failed.' }
"Native $Action passed; executable and all Player files unchanged; receipt=$receipt"
