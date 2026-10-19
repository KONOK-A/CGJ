[CmdletBinding()]
param([string] $Run = 'combined-1', [string] $BCommit = '09bec5a')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets\YLFramework'))) { throw 'Wrong worktree.' }
if ($Run -notmatch '^[A-Za-z0-9_-]+$' -or $BCommit -notmatch '^[0-9a-f]{7,40}$') { throw 'Unsafe run or commit.' }
$output = Join-Path $root "Temp\Domain.Excel\$Run"
if (Test-Path -LiteralPath $output) { throw "Use a fresh Run to preserve evidence: $output" }
$null = New-Item -ItemType Directory -Path $output -Force
$snapshot = Join-Path $output 'B'
$null = New-Item -ItemType Directory -Path $snapshot
$exact = & git rev-parse $BCommit
if ($LASTEXITCODE -ne 0) { throw 'B commit not found.' }
$paths = @('Assets/YLFramework/Runtime/Systems/DataAssets', 'Assets/YLFramework/Editor/Systems/DataAssets', 'Assets/YLFramework/Integrations/Excel')
& git archive --format=zip "--output=$output\B.zip" $exact -- @paths
if ($LASTEXITCODE -ne 0) { throw 'B snapshot archive failed.' }
Expand-Archive -LiteralPath (Join-Path $output 'B.zip') -DestinationPath $snapshot
[IO.File]::WriteAllText((Join-Path $output 'B-commit.txt'), $exact + "`n", [Text.UTF8Encoding]::new($false))
$mini = Join-Path $snapshot 'Assets\YLFramework\Integrations\Excel\Editor\Plugins\MiniExcel.dll'
if ((Get-FileHash -LiteralPath $mini -Algorithm SHA256).Hash.ToLowerInvariant() -ne '0a49b66224bef9fdcec12800e4660267a993b1ece87727d9fe15c7dabf58ba1e') { throw 'Unverified B backend binary.' }
& (Join-Path $PSScriptRoot 'Verify-Domain.ps1') -Mode Editor
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$compiler = 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore\csc.dll'
$mono = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data\MonoBleedingEdge\bin\mono.exe'
function Options([string] $mode) {
    @([IO.File]::ReadAllLines((Join-Path $root "Temp\UI.Panel.Offline\$mode\compile.rsp")) | ForEach-Object { $_.Trim('"') } | Where-Object { $_.StartsWith('/') -and -not $_.StartsWith('/out:') })
}
function Sources([string] $path) { @(Get-ChildItem -LiteralPath $path -Recurse -Filter '*.cs' | ForEach-Object FullName) }
function Compile([string] $name, [string[]] $options, [string[]] $sources) {
    $args = @($options) + @("/out:$output\$name") + @($sources)
    $rsp = Join-Path $output ($name + '.rsp')
    [IO.File]::WriteAllLines($rsp, [string[]]@($args | ForEach-Object { '"' + $_ + '"' }), [Text.UTF8Encoding]::new($false))
    $log = @(& $dotnet $compiler /noconfig "@$rsp" 2>&1)
    $exit = $LASTEXITCODE
    [IO.File]::WriteAllLines((Join-Path $output ($name + '.log')), [string[]]@($log | ForEach-Object { $_.ToString() }), [Text.UTF8Encoding]::new($false))
    "$name sources=$($sources.Count), exit=$exit, warnings=$(@($log | Where-Object { $_ -match 'warning CS' }).Count)"
    if ($exit -ne 0) { $log; throw "Combined compilation failed: $name" }
}
# Keep one real Mono BCL profile across the combined graph; EditorTools uses mscorlib.
$runtimeOptions = @(Options 'EditorTools' | Where-Object { $_ -notmatch '^/reference:.*YLFramework.Runtime.dll$' })
$runtimeSources = @(Sources (Join-Path $root 'Assets\YLFramework\Runtime')) + @(Sources (Join-Path $snapshot 'Assets\YLFramework\Runtime\Systems\DataAssets'))
Compile 'YLFramework.Runtime.dll' $runtimeOptions $runtimeSources
$editorOptions = @(Options 'EditorTools' | ForEach-Object { if ($_ -match '^/reference:.*YLFramework.Runtime.dll$') { "/reference:$output\YLFramework.Runtime.dll" } else { $_ } })
$editorSources = @(Sources (Join-Path $root 'Assets\YLFramework\Editor')) + @(Sources (Join-Path $snapshot 'Assets\YLFramework\Editor\Systems\DataAssets'))
Compile 'YLFramework.Editor.dll' $editorOptions $editorSources
$optionalOptions = @($runtimeOptions | Where-Object { -not $_.StartsWith('/define:') }) + @('/define:UNITY_EDITOR;YL_DOMAIN_EXCEL', "/reference:$output\YLFramework.Runtime.dll", "/reference:$output\YLFramework.Editor.dll", "/reference:$mini")
Compile 'YLFramework.Excel.Editor.dll' $optionalOptions (Sources (Join-Path $snapshot 'Assets\YLFramework\Integrations\Excel\Editor'))
$adapterOptions = $optionalOptions + "/reference:$output\YLFramework.Excel.Editor.dll"
Compile 'YLFramework.Domain.Excel.Editor.dll' $adapterOptions (Sources (Join-Path $root 'Assets\YLFramework\Integrations\DomainExcel\Editor'))
$nunit = Join-Path $env:USERPROFILE '.nuget\packages\nunit\3.14.0\lib\net45\nunit.framework.dll'
$testOptions = @($adapterOptions | Where-Object { -not $_.StartsWith('/target:') }) + @('/target:exe', "/reference:$output\YLFramework.Domain.Excel.Editor.dll", "/reference:$nunit")
$tests = @(Sources (Join-Path $root 'Assets\YLFramework\Integrations\DomainExcel\Tests\Editor')) + (Join-Path $root 'Tools\UIPanelOfflineRunner.cs')
Compile 'DomainExcelTests.exe' $testOptions $tests
$dependencyPaths = @($testOptions | Where-Object { $_.StartsWith('/reference:') } | ForEach-Object { $_.Substring(11) })
foreach ($path in $dependencyPaths) { if ((Split-Path $path -Parent) -ne $output) { Copy-Item -LiteralPath $path -Destination $output -Force } }
& $mono (Join-Path $output 'DomainExcelTests.exe') 'YLFramework.Domain.Excel.Tests.DomainExcelTests'
$exit = $LASTEXITCODE
"C+B exact snapshot=$exact; real xlsx managed tests exit=$exit. Not a Unity asset/GUI/Player result. Evidence=$output"
if ($exit -ne 0) { throw 'Domain Excel managed tests failed.' }
