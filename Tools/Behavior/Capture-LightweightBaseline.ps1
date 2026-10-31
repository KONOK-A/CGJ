[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$path = Join-Path $root 'Assets\YLFramework\Runtime\Utilities\FSM'
$changes = @(& git -C $root diff --name-only c88500aef6651f5b13ac933dd1f209ecb314e92d -- Assets/YLFramework/Runtime/Utilities/FSM)
if ($LASTEXITCODE -ne 0 -or $changes.Count -ne 0) { throw 'Lightweight FSM differs from B0; refusing to baseline.' }
$target = Join-Path $PSScriptRoot 'LightweightFSM.baseline.json'
if (Test-Path -LiteralPath $target) { throw 'The immutable baseline already exists.' }
$records = @(Get-ChildItem -LiteralPath $path -File -Recurse | ForEach-Object { [ordered]@{ path = $_.FullName.Substring($root.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } })
[IO.File]::WriteAllText($target, ([ordered]@{ baseline = 'c88500aef6651f5b13ac933dd1f209ecb314e92d'; files = $records } | ConvertTo-Json -Depth 5) + "`n", [Text.UTF8Encoding]::new($false))
"Captured $($records.Count) read-only lightweight source/meta identities."
