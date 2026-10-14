[CmdletBinding()]
param([switch] $Deploy)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param([string[]] $Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $($Arguments -join ' ')" }
}

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tests = Join-Path $PSScriptRoot 'Tests/YLFramework.Analyzers.Tests.csproj'
$output = Join-Path $root 'Temp/YLFramework.Analyzers'
$dll = Join-Path $output 'YLFramework.Analyzers/bin/Release/netstandard2.0/YLFramework.Analyzers.dll'
$destination = Join-Path $root 'Assets/YLFramework/Runtime/Core/Analyzers/YLFramework.Analyzers.dll'
if (-not (Test-Path -LiteralPath (Join-Path $root 'Temp'))) { throw 'Repository Temp parent is missing.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
try { $entryLock = [IO.File]::Open((Join-Path $output 'entry.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
catch [IO.IOException] { throw 'Another analyzer build/deploy entry is running; refusing shared output access.' }
$before = if (Test-Path -LiteralPath $destination) { (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash } else { $null }

Push-Location -LiteralPath $PSScriptRoot
try {
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdk -ne '9.0.312') { throw "Expected SDK 9.0.312; actual: $sdk" }
    "PowerShell=$($PSVersionTable.PSVersion) SDK=$sdk"
    Invoke-DotNet -Arguments @('restore', $tests, '--locked-mode')
    Invoke-DotNet -Arguments @('build', $tests, '-c', 'Release', '--no-restore')
    Invoke-DotNet -Arguments @('test', $tests, '-c', 'Release', '--no-build', '--no-restore', '--results-directory', (Join-Path $output 'TestResults'), '--logger', 'trx;LogFileName=analyzers.trx', '--logger', 'console;verbosity=minimal')

    $inputs = @('global.json', 'Directory.Build.props', 'Analyzer/YLFramework.Analyzers.csproj', 'Analyzer/packages.lock.json')
    $inputs += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Analyzer') -Filter '*.cs' -File -Recurse |
        ForEach-Object { $_.FullName.Substring($PSScriptRoot.Length + 1).Replace('\', '/') })
    $files = @($inputs | Sort-Object | ForEach-Object {
        [ordered]@{ Path = $_; Sha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $_) -Algorithm SHA256).Hash }
    })
    $receipt = [ordered]@{
        Sdk = $sdk
        Version = '1.0.0'
        Inputs = $files
        DllSha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    } | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $output 'build-info.json'), $receipt, [Text.UTF8Encoding]::new($false))

    if ($Deploy) {
        # 合作入口串行；指纹检查拒绝已观察到的外部改动，不能替代任意外部写者的 compare-and-swap。
        $current = if (Test-Path -LiteralPath $destination) { (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash } else { $null }
        if ($current -ne $before) { throw 'Analyzer destination changed during verification; deployment refused.' }
        $parent = [IO.Path]::GetDirectoryName($destination)
        if (-not (Test-Path -LiteralPath $parent)) { [IO.Directory]::CreateDirectory($parent) | Out-Null }
        if ($current -ne (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash) {
            $stage = Join-Path $parent ('YLFramework.Analyzers.' + [Guid]::NewGuid().ToString('N') + '.tmp')
            try {
                [IO.File]::Copy($dll, $stage, $false)
                $stagedCurrent = if (Test-Path -LiteralPath $destination) { (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash } else { $null }
                if ($stagedCurrent -ne $before) { throw 'Analyzer destination changed during staging; deployment refused.' }
                if ($null -eq $current) { [IO.File]::Move($stage, $destination) }
                else { [IO.File]::Replace($stage, $destination, [NullString]::Value) }
            }
            finally { if ([IO.File]::Exists($stage)) { [IO.File]::Delete($stage) } }
        }
        "Deployed=$destination"
    }
    "DLL=$dll"
    "SHA256=$((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash)"
    "BuildInfo=$(Join-Path $output 'build-info.json')"
}
finally { Pop-Location; $entryLock.Dispose() }
