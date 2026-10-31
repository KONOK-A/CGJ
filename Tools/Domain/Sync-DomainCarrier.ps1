[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $Run)
$ErrorActionPreference = 'Stop'
if ($Run -notmatch '^[A-Za-z0-9_-]+$') { throw 'Unsafe Run.' }
$owner = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$project = Join-Path $owner "Logs\YLFramework.Domain\Carriers\$Run"
$markerPath = Join-Path $project 'domain-carrier.json'
if (-not (Test-Path -LiteralPath $markerPath)) { throw 'Not an owned carrier.' }
$marker = [IO.File]::ReadAllText($markerPath) | ConvertFrom-Json
if ($marker.owner -ne $owner) { throw 'Foreign carrier owner.' }
foreach ($process in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") {
    if ($process.CommandLine -and $process.CommandLine.IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw 'Carrier Unity is active; never replace its live inputs.' }
}
$changes = @()
if (Test-Path -LiteralPath (Join-Path $project 'Assets/YLFramework/Integrations/DomainExcel')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $owner 'Assets/YLFramework/Integrations/DomainExcel') -Recurse -File) {
        $relative = $file.FullName.Substring($owner.Length + 1).Replace('\', '/')
        if (-not @($marker.sourceFiles | Where-Object { $_.path -eq $relative }).Count) { $marker.sourceFiles += [pscustomobject]@{ path = $relative; sha256 = '' } }
    }
}
foreach ($entry in $marker.sourceFiles) {
    $source = Join-Path $owner $entry.path
    $destination = Join-Path $project $entry.path
    $before = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $before) { continue }
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $before -or (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $before) { throw 'Unstable source sync.' }
    $entry.sha256 = $before
    $changes += $entry.path
}
$bootstrap = Join-Path $project 'Assets/DomainFixture/Editor/DomainCarrierBootstrap.cs'
if (Test-Path -LiteralPath $bootstrap) {
    $template = Join-Path $PSScriptRoot 'DomainCarrierBootstrap.cs.txt'
    $before = (Get-FileHash -LiteralPath $template -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $bootstrap -Algorithm SHA256).Hash -ne $before) {
        Copy-Item -LiteralPath $template -Destination $bootstrap -Force
        if ((Get-FileHash -LiteralPath $template -Algorithm SHA256).Hash -ne $before -or (Get-FileHash -LiteralPath $bootstrap -Algorithm SHA256).Hash -ne $before) { throw 'Unstable bootstrap sync.' }
        $changes += 'Assets/DomainFixture/Editor/DomainCarrierBootstrap.cs'
    }
}
[IO.File]::WriteAllText($markerPath, ($marker | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
"Carrier=$project synchronized=$($changes.Count)"
$changes
