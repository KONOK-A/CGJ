[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Overlap', 'CharacterController2D', 'MoveablePlatform', 'StateMachine')][string] $Module,
    [string] $SourceRoot = 'G:\Unity\Projects\CGJ2024\Library\PackageCache'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$sources = @{
    Overlap = @('com.xfkj.xfgameframework.overlap@4961d83581', '4961d8358119485c94d6f0f74b6ca5d8b4163460', '1.0.4')
    CharacterController2D = @('com.xfkj.xfgameframework.charactercontroller2d@f024105474', 'f0241054744ca0673ec370d5fad39c32b789d81c', '1.1.6')
    MoveablePlatform = @('com.xfkj.xfgameframework.moveableplatform@1e1265d8e6', '1e1265d8e6adb4ebab6febe8bc51288972e2b234', '1.0.6')
    StateMachine = @('com.xfkj.xffsm@b06333c2cd', 'b06333c2cdc801fcc401f14c252853bf099aa1b7', '1.5.4')
}
$identity = $sources[$Module]
$source = Join-Path $SourceRoot $identity[0]
if (-not (Test-Path -LiteralPath $source)) { throw "Missing source: $source" }
$runtimeParent = Join-Path $root 'Assets\YLFramework\Runtime\Systems'
$editorParent = Join-Path $root 'Assets\YLFramework\Editor\Systems'
foreach ($parent in @($runtimeParent, $editorParent, $PSScriptRoot)) {
    if (-not (Test-Path -LiteralPath $parent)) { throw "Missing destination parent: $parent" }
}
$runtime = Join-Path $runtimeParent $Module
$editor = Join-Path $editorParent $Module
if ((Test-Path -LiteralPath $runtime) -or (Test-Path -LiteralPath $editor)) { throw 'Import refuses to overwrite existing implementation. Continue incrementally with patches.' }
if ($Module -notin @('Overlap', 'CharacterController2D', 'MoveablePlatform')) { throw 'This module conversion has not been reviewed yet.' }
$records = @()
foreach ($side in @('Runtime', 'Editor')) {
    $destination = if ($side -eq 'Runtime') { $runtime } else { $editor }
    $null = New-Item -ItemType Directory -Path $destination -Force
    foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $source $side) -Filter '*.cs' -Recurse)) {
        $relative = $file.FullName.Substring((Join-Path $source $side).Length).TrimStart('\')
        $text = [IO.File]::ReadAllText($file.FullName)
        $records += [ordered]@{ path = "$side/$($relative.Replace('\','/'))"; sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
        if ($Module -eq 'Overlap') {
            $text = $text.Replace('XFGameFramework.Overlap', 'YLFramework.Overlap').Replace('namespace XFGameFramework', 'namespace YLFramework.Overlap').Replace('using XFGameFramework;', 'using YLFramework.Core.Editor;')
        } elseif ($Module -eq 'CharacterController2D') {
            $text = $text.Replace('XFGameFramework.Character.Controller2D', 'YLFramework.Character.Controller2D')
            $text = $text.Replace('CharacterController2D.hits', 'characher.hits')
        } elseif ($Module -eq 'MoveablePlatform') {
            $text = $text.Replace('XFGameFramework.Platform2D', 'YLFramework.Platform2D')
        }
        if ($side -eq 'Runtime') {
            $lines = [Collections.Generic.List[string]]::new()
            $depth = 0
            foreach ($line in ($text -split '\r?\n')) {
                if ($depth -gt 0) {
                    if ($line -match '^\s*#if\b') { $depth++ }
                    if ($line -match '^\s*#endif\b') { $depth-- }
                    continue
                }
                if ($line -match '^\s*#if UNITY_EDITOR\s*$') { $depth = 1; continue }
                if ($line -match '^using (UnityEditor|System.Drawing);') { continue }
                $lines.Add($line)
            }
            $text = $lines -join "`n"
        } else {
            $text = 'using YLFramework.Core.Editor;' + "`n" + $text
            $text = $text.Replace(' : Editor', ' : UnityEditor.Editor')
        }
        $target = Join-Path $destination $relative
        $null = New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force
        [IO.File]::WriteAllText($target, ($text.TrimEnd() + "`n"), [Text.UTF8Encoding]::new($false))
    }
}
$license = Join-Path $source 'LICENSE'
$records += [ordered]@{ path = 'LICENSE'; sha256 = (Get-FileHash -LiteralPath $license -Algorithm SHA256).Hash.ToLowerInvariant() }
[IO.File]::WriteAllText((Join-Path $runtime 'ThirdPartyNotices.txt'), "Source: $($identity[0]); version $($identity[2]); revision $($identity[1])`nAdapted for YLFramework. Original algorithms retained; local changes tracked in git.`n`n" + [IO.File]::ReadAllText($license), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot "$Module.source.json"), ([ordered]@{ package = $identity[0]; version = $identity[2]; revision = $identity[1]; sourceRoot = $source; files = $records } | ConvertTo-Json -Depth 6) + "`n", [Text.UTF8Encoding]::new($false))
"Imported $Module without original .meta, asmdef, logos, demos or binaries. Source manifest: Tools/Behavior/$Module.source.json"
