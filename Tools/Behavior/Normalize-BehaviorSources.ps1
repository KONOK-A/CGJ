[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$paths = @('Assets\YLFramework\Runtime', 'Assets\YLFramework\Editor')
foreach ($parent in $paths) {
    foreach ($module in @('Overlap', 'CharacterController2D', 'MoveablePlatform', 'StateMachine')) {
        $section = if ($module -eq 'StateMachine') { 'Systems' } else { 'Utilities' }
        $directory = Join-Path (Join-Path $root $parent) "$section\$module"
        if (-not (Test-Path -LiteralPath $directory)) { continue }
        foreach ($file in @(Get-ChildItem -LiteralPath $directory -Filter '*.cs' -Recurse)) {
            $text = [IO.File]::ReadAllText($file.FullName).Replace("`r`n", "`n")
            $text = [regex]::Replace($text, '(?m)[\t ]+$', '')
            # Only indent the guarded original query bodies, not unrelated upstream code.
            $text = [regex]::Replace($text, '(?s)(TryBeginCheck\(\)\) return;\r?\n            try\r?\n            \{\r?\n)(.*?)(\r?\n            \}\r?\n            finally)', {
                param($match)
                $body = $match.Groups[2].Value
                if ($body -match '(?m)^ {12}\S') { $body = [regex]::Replace($body, '(?m)^(?=\S|[ ]{12})', '    ') }
                $match.Groups[1].Value + $body + $match.Groups[3].Value
            })
            [IO.File]::WriteAllText($file.FullName, $text.TrimEnd() + "`n", [Text.UTF8Encoding]::new($false))
        }
    }
}
