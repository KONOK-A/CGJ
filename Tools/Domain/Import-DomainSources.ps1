[CmdletBinding()]
param([ValidateSet('Skill', 'Buff', 'Mission')][string] $Module)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$sources = @{
    Skill = 'G:\Unity\Projects\CGJ2024\Library\PackageCache\com.xfkj.xfgameframework.skillsystem@4c9d8f5d31'
    Buff = 'G:\Unity\Projects\CGJ2024\Library\PackageCache\com.xfkj.xfgameframework.buffsystem@01d05626b0'
    Mission = 'G:\Unity\Projects\CGJ2024\Library\PackageCache\com.xfkj.xfgameframework.missionsystem@a3da5d37ae'
}
$source = $sources[$Module]
if (-not (Test-Path -LiteralPath $source)) { throw "Missing approved source: $source" }
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets\YLFramework\Runtime\Systems'))) { throw 'Wrong worktree.' }

# First mechanical import only; never overwrite later manual fixes or import metadata/binaries.
function Import-File([string] $path, [string] $target, [bool] $editor) {
    if (Test-Path -LiteralPath $target) { throw "Refusing to overwrite: $target" }
    $text = [IO.File]::ReadAllText($path)
    $text = $text.Replace('XFGameFramework.SkillSystem', 'YLFramework.SkillSystem').Replace('XFGameFramework.BuffSystem', 'YLFramework.BuffSystem').Replace('XFGameFramework.MissionSystem', 'YLFramework.MissionSystem')
    $text = $text.Replace('XFGameFramework.ExcelSystem', 'YLFramework.Excel').Replace('ExcelSystem.', 'YLFramework.Excel.')
    $text = $text.Replace('AssemblyTools', 'AssemblyTool').Replace('using XFABManager;', '').Replace('using XFGameFramework;', '')
    $text = $text.Replace('Assets/Extensions/SkillSystem', 'Assets/YLFrameworkData/Skill').Replace('Assets/Extensions/BuffSystem', 'Assets/YLFrameworkData/Buff').Replace('Assets/Extensions/MissionSystem', 'Assets/YLFrameworkData/Mission')
    $text = $text.Replace('Window/XFKT/XFGameFramework/InstalledExtension/', 'Window/YLFramework/').Replace('XFKT/', 'YLFramework/')
    $text = $text.Replace('"SELECT_GUID_KEY"', '"YLFramework.' + $Module + '.SelectedConfig"')
    if ($editor) {
        $text = "using YLFramework.Foundation;`nusing YLFramework.Foundation.Attributes;`nusing YLFramework.Core.Editor;`n" + $text
        $text = $text.Replace('DragableTreeView<', 'ReorderableTreeView<')
        $text = $text.Replace(': Editor', ': UnityEditor.Editor')
        $text = $text.Replace('base(skillsConfig, skillsConfig.skills,state)', 'base(skillsConfig, () => skillsConfig.skills, state)')
        $text = $text.Replace('base(buffsConfig, buffsConfig.buffs, state)', 'base(buffsConfig, () => buffsConfig.buffs, state)')
        $text = $text.Replace('base(buffsConfig,buffsConfig.buffs,state)', 'base(buffsConfig, () => buffsConfig.buffs, state)')
        $text = $text.Replace('base(missionsConfig, missionsConfig.missions, state)', 'base(missionsConfig, () => missionsConfig.missions, state)')
        if ($path -match 'ScriptBindController.cs$') {
            $text = $text.Replace('[InitializeOnLoadMethod]', '[MenuItem("Tools/YLFramework/' + $Module + '/Refresh Script Bindings")]')
        }
        # xlsx author commands await the real group B backend.
        if ($Module -eq 'Skill' -and $path -match 'SkillsConfigInspector.cs$') {
            $lines = $text -split "`n"
            # Three using lines precede the exact approved source revision.
            $text = (($lines | ForEach-Object -Begin { $i = 0 } -Process { $i++; if ($i -ne 60 -and $i -ne 64 -and ($i -lt 76 -or $i -gt 229)) { $_ } }) -join "`n")
        }
        if ($Module -eq 'Buff' -and $path -match 'BuffsConfigInspector.cs$') {
            $lines = $text -split "`n"
            $text = (($lines | ForEach-Object -Begin { $i = 0 } -Process { $i++; if ($i -ne 54 -and ($i -lt 56 -or $i -gt 63) -and ($i -lt 79 -or $i -gt 234)) { $_ } }) -join "`n")
        }
        if ($Module -eq 'Buff' -and $path -match 'EffectConfigsWindow.cs$') {
            $lines = $text -split "`n"
            $text = (($lines | ForEach-Object -Begin { $i = 0 } -Process { $i++; if ($i -lt 127 -or $i -gt 170) { $_ } }) -join "`n")
        }
    } else {
        # Move GUI annotations and Editor methods out of Runtime data types.
        if ($path -notmatch 'Manager.cs$') {
            $text = [regex]::Replace($text, '(?ms)^\s*#if UNITY_EDITOR\s*$.*?^\s*#endif\s*$', '')
            $text = $text.Replace('using UnityEditor;', '')
        }
        $text = "using YLFramework.Foundation;`n" + $text
        $text = $text.Replace(' : IReuseable', '')
    }
    $text = "// Adapted from XF $Module. Original copyright and MIT License: Runtime/Systems/$Module/LICENSE.txt.`n" + $text
    $text = [regex]::Replace($text, '[ \t]+(?=\r?$)', '', [Text.RegularExpressions.RegexOptions]::Multiline)
    $text = $text.TrimEnd() + "`n"
    $parent = [IO.Path]::GetDirectoryName($target)
    $null = New-Item -ItemType Directory -Path $parent -Force
    [IO.File]::WriteAllText($target, $text, [Text.UTF8Encoding]::new($false))
}
$runtime = Join-Path $root "Assets\YLFramework\Runtime\Systems\$Module"
$editorRoot = Join-Path $root "Assets\YLFramework\Editor\Systems\$Module"
$runtimeNames = switch ($Module) {
    Skill { @('SkillBase.cs', 'SkillConfigData.cs', 'SkillsConfig.cs', 'IEquippableSkill.cs') }
    Buff { @('BuffBase.cs', 'BuffConfigData.cs', 'BuffsConfig.cs', 'IAddableBuff.cs', 'EffectData.cs', 'EffectName.cs') }
    Mission { @('MissionBase.cs', 'MissionConfigData.cs', 'MissionsConfig.cs', 'MissionCondition.cs', 'MissionParameter.cs', 'MissionReward.cs') }
}
foreach ($name in $runtimeNames) { Import-File (Join-Path $source "Runtime\$name") (Join-Path $runtime $name) $false }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $source 'Editor') -Recurse -Filter '*.cs') {
    if ($file.Name -eq 'MissionTypePopWindow.cs') { continue }
    $relative = $file.FullName.Substring((Join-Path $source 'Editor').Length + 1)
    Import-File $file.FullName (Join-Path $editorRoot $relative) $true
}
$drawers = if ($Module -eq 'Skill') { 'Runtime\CustomSkillConfigDataProperty' } else { 'Runtime\Attribute' }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $source $drawers) -Recurse -Filter '*.cs') {
    if ($file.Name -match 'IconGUI.cs$|Excel.cs$') { continue }
    Import-File $file.FullName (Join-Path $editorRoot ('Drawers\' + $file.Name)) $true
}
if ($Module -eq 'Buff') { Import-File (Join-Path $source 'Runtime\BuffSystemSettings.cs') (Join-Path $editorRoot 'Settings\BuffSystemSettings.cs') $true }
Copy-Item -LiteralPath (Join-Path $source 'LICENSE') -Destination (Join-Path $runtime 'LICENSE.txt')
"Imported $Module approved source snapshot. Manual adaptations and validation are still required."
