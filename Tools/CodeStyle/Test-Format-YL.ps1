#requires -Version 7.6
#requires -PSEdition Core
[CmdletBinding()]
param()

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

function Assert-That {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) {
        throw $Message
    }
}

$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$entry = Join-Path $PSScriptRoot 'Format-YL.ps1'
Assert-That (Test-Path -LiteralPath $entry -PathType Leaf) 'FAIL: the required Format-YL entry is not implemented.'
$temporaryParent = Join-Path $env:LOCALAPPDATA 'Temp/kilo'
Assert-That (Test-Path -LiteralPath $temporaryParent -PathType Container) 'The approved temporary parent must exist.'
$sandbox = Join-Path $temporaryParent ('yl-format-' + [Guid]::NewGuid().ToString('N'))
$root = Join-Path $sandbox 'Repository with spaces'
$utf8 = [Text.UTF8Encoding]::new($false)
$pwsh = (Get-Process -Id $PID).Path
$cases = 0

function Set-Fixture {
    param([string] $RelativePath, [string] $Content)
    $path = Join-Path $root $RelativePath
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path, $Content, $utf8)
    return $path
}

function Invoke-Entry {
    param([string[]] $Arguments = @())
    $output = & $pwsh -NoLogo -NoProfile -NonInteractive -File (Join-Path $root 'Tools/CodeStyle/Format-YL.ps1') @Arguments 2>&1
    return [pscustomobject]@{ Code = $LASTEXITCODE; Output = ($output -join "`n") }
}

function Pass {
    param([string] $Name)
    $script:cases++
    "PASS: $Name"
}

try {
    $null = [IO.Directory]::CreateDirectory((Join-Path $root 'Tools/CodeStyle'))
    [IO.File]::Copy($entry, (Join-Path $root 'Tools/CodeStyle/Format-YL.ps1'))
    [IO.File]::Copy((Join-Path $repository '.editorconfig'), (Join-Path $root '.editorconfig'))
    $null = Set-Fixture 'Tools/YLFramework.Analyzers/global.json' ([IO.File]::ReadAllText((Join-Path $repository 'Tools/YLFramework.Analyzers/global.json')))

    $bad = "namespace Demo{public class Example{public int Sum(int a,int b){return a+b;}}}`r`n"
    $first = Set-Fixture 'Assets/YLFramework/Runtime/Example [one].cs' $bad
    $second = Set-Fixture 'Assets/YLFramework/Editor/ExampleTwo.cs' $bad
    $third = Set-Fixture 'Tools/YLFramework.Analyzers/Tests/ExampleThree.cs' $bad
    $protectedPaths = @(
        'Assets/Games/Example.cs',
        'Assets/YLFramework/Plugins/Example.cs',
        'Assets/YLFramework/Runtime/Utilities/Json/Example.cs',
        'Assets/YLFramework/Runtime/Utilities/ZipTool/ICSharpCode.SharpZipLib/Example.cs',
        'Assets/YLFramework/Integrations/Excel/Editor/Plugins/Example.cs',
        'Assets/YLFramework/Runtime/Generated/Example.cs',
        'Assets/YLFramework/Runtime/Example.g.cs',
        'Assets/YLFramework/Runtime/obj/Example.cs',
        'Assets/YLFramework/Examples/Core/Modules/Example.cs',
        'Assets/YLFrameworkData/Example.cs',
        'Assets/YLFramework/Tests/Integrations/HybridCLR/PlayMode/Resources/YLHybridCLRTests/Example.cs',
        'Assets/YLFramework/Runtime/Example.cs.meta'
    )
    foreach ($relative in $protectedPaths) {
        $null = Set-Fixture $relative $bad
    }

    $result = Invoke-Entry
    Assert-That ($result.Code -ne 0 -and $result.Output -match 'Mode=Check' -and $result.Output -match 'WHITESPACE') "Default Check must report real formatting diagnostics: $($result.Output)"
    foreach ($path in @($first, $second, $third)) {
        Assert-That ([IO.File]::ReadAllText($path) -ceq $bad) 'Default Check must not change source bytes.'
    }
    Pass 'default mode checks without writing'

    $result = Invoke-Entry @('-Mode', 'Apply')
    Assert-That ($result.Code -ne 0 -and $result.Output -match 'explicit') 'Apply without explicit paths must be rejected.'
    Pass 'Apply requires explicit paths'

    foreach ($relative in $protectedPaths) {
        $result = Invoke-Entry @('-Mode', 'Apply', '-Paths', $relative)
        Assert-That ($result.Code -ne 0 -and $result.Output -match 'not allowed') "Protected path accepted: $relative`n$($result.Output)"
        Assert-That ([IO.File]::ReadAllText((Join-Path $root $relative)) -ceq $bad) "Protected path changed: $relative"
    }
    Pass 'vendor, game, generated and non-CSharp inputs rejected'

    foreach ($relative in @('../Outside.cs', $first, 'Assets/YLFramework/Runtime/*.cs')) {
        $result = Invoke-Entry @('-Mode', 'Apply', '-Paths', $relative)
        Assert-That ($result.Code -ne 0 -and $result.Output -match 'not allowed') "Non-relative or wildcard input accepted: $relative"
    }
    Pass 'outside, absolute and wildcard inputs rejected'

    $result = Invoke-Entry @('-Mode', 'Apply', '-Paths', 'Assets/YLFramework/Runtime/Example [one].cs', 'Assets/Games/Example.cs')
    Assert-That ($result.Code -ne 0 -and $result.Output -match 'not allowed') "Every path must be validated before formatting any file: $($result.Output)"
    Assert-That ([IO.File]::ReadAllText($first) -ceq $bad) 'A later rejected path must not leave earlier inputs formatted.'
    Pass 'mixed requests fail before any write'

    $result = Invoke-Entry @('-Mode', 'Apply', '-Paths', 'Assets/YLFramework/Runtime/Example [one].cs')
    Assert-That ($result.Code -eq 0) "Legal Apply failed: $($result.Output)"
    $formatted = [IO.File]::ReadAllText($first)
    Assert-That ($formatted.Contains("`n    public class Example`n    {")) 'Expected four-space Allman formatting.'
    Assert-That ($formatted.Contains('return a + b;')) 'Expected operator spacing.'
    Assert-That (-not $formatted.Contains("`r") -and $formatted.EndsWith("`n")) 'Expected LF and final newline.'
    $bytes = [IO.File]::ReadAllBytes($first)
    Assert-That (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191)) 'Unexpected UTF-8 BOM.'
    Assert-That ([IO.File]::ReadAllText($second) -ceq $bad) 'Apply must not format a sibling file.'
    $result = Invoke-Entry @('-Paths', 'Assets/YLFramework/Runtime/Example [one].cs')
    Assert-That ($result.Code -eq 0) "Formatted file is not idempotent: $($result.Output)"
    Pass 'literal spaces/brackets, formatting, encoding and idempotence'

    $result = Invoke-Entry @('-Mode', 'Apply', '-Paths', 'Assets/YLFramework', 'Tools/YLFramework.Analyzers')
    Assert-That ($result.Code -eq 0) "Allowed root selection failed: $($result.Output)"
    $result = Invoke-Entry
    Assert-That ($result.Code -eq 0) "Final Check failed: $($result.Output)"
    foreach ($relative in $protectedPaths) {
        Assert-That ([IO.File]::ReadAllText((Join-Path $root $relative)) -ceq $bad) "Directory traversal touched excluded file: $relative"
    }
    Pass 'directory selection excludes protected descendants'

    for ($index = 0; $index -lt 65; $index++) {
        $name = 'Batch' + $index.ToString('D2')
        # The official whitespace formatter preserves some compact catch/finally blocks; expand those before this gate.
        $content = "namespace Demo{public sealed class $name{public string Name{get;} public int Count{get;set;} public $name(){Name=string.Empty;Count=1;try{Count++;Count++;}finally`n{`nCount++;`n}}}}`r`n"
        $null = Set-Fixture ("Assets/YLFramework/Runtime/Batch/$name.cs") $content
    }
    $result = Invoke-Entry @('-Mode', 'Apply', '-Paths', 'Assets/YLFramework/Runtime')
    Assert-That ($result.Code -eq 0) "Large directory Apply failed: $($result.Output)"
    $result = Invoke-Entry @('-Paths', 'Assets/YLFramework/Runtime')
    Assert-That ($result.Code -eq 0) "A multi-batch directory Apply must converge before reporting success: $($result.Output)"
    $batch = [IO.File]::ReadAllText((Join-Path $root 'Assets/YLFramework/Runtime/Batch/Batch00.cs'))
    Assert-That ($batch.Contains("Name = string.Empty;`n            Count = 1;")) 'Separate statements must be placed on separate lines.'
    Assert-That ($batch.Contains("try`n            {`n                Count++;`n                Count++;`n            }`n            finally`n            {")) "Control-flow blocks must expand to Allman style.`n$batch"
    Pass 'multi-batch directory selection formats nested declarations idempotently'

    $target = Join-Path $sandbox 'Outside'
    $null = [IO.Directory]::CreateDirectory($target)
    [IO.File]::WriteAllText((Join-Path $target 'Example.cs'), $bad, $utf8)
    $link = Join-Path $root 'Assets/YLFramework/Runtime/Linked'
    $null = New-Item -ItemType Junction -Path $link -Target $target
    try {
        $result = Invoke-Entry @('-Mode', 'Apply', '-Paths', 'Assets/YLFramework/Runtime/Linked/Example.cs')
        Assert-That ($result.Code -ne 0 -and $result.Output -match 'link') 'Linked ancestor must be rejected.'
        $result = Invoke-Entry @('-Mode', 'Apply', '-Paths', 'Assets/YLFramework/Runtime')
        Assert-That ($result.Code -ne 0 -and $result.Output -match 'link') 'Directory enumeration must not traverse links.'
        Assert-That ([IO.File]::ReadAllText((Join-Path $target 'Example.cs')) -ceq $bad) 'Link target was modified.'
    }
    finally {
        [IO.Directory]::Delete($link)
    }
    Pass 'junctions rejected before traversal or write'

    "Format-YL tests: $cases passed; repository sources were not formatted."
}
finally {
    if ([IO.Directory]::Exists($sandbox)) {
        [IO.Directory]::Delete($sandbox, $true)
    }
}
