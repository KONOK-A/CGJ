#requires -Version 7.6
[CmdletBinding()]
param([string]$Root = 'C:\Users\Kano\AppData\Local\Temp\kilo\H26-20261021', [string]$Receiver,
    [ValidateSet('Common','Owned')][string]$SceneProfile)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$scope = . (Join-Path $PSScriptRoot 'Get-HybridCLRReceptionScope.ps1') -Root $Root -Receiver $Receiver
$Root = $scope.Root
$owner = $scope.Owner
$project = $scope.Project
Assert-HybridCLRReady $scope
Assert-HybridCLRProductImports $scope
$profile = Resolve-HybridCLRTestSceneProfile $scope $SceneProfile
$receiptPath = Join-Path $Root 'tests-installation.json'
Assert-NoLinks $receiptPath
if ([IO.Path]::Exists($receiptPath)) { throw 'Preserve the declared test installation profile.' }
$active = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($project) })
if ($active.Count) { throw 'Do not alter a receiver while its Editor is running.' }
$source = Join-Path $owner.repository 'Assets/YLFramework/Tests/Integrations/HybridCLR'
$destination = Join-Path $project 'Assets/YLFramework/Tests/Integrations/HybridCLR'
Assert-NoLinks $source
Assert-NoLinks $destination
if ([IO.Path]::Exists($destination) -or [IO.Path]::Exists($destination + '.meta')) { throw 'Never adopt or overwrite another test fixture tree.' }
$parent = Join-Path $project 'Assets/YLFramework'
if (-not (Test-Path -LiteralPath $parent)) { throw 'Import the real default framework first.' }
[IO.Directory]::CreateDirectory((Join-Path $project 'Assets/YLFramework/Tests/Fixtures')) | Out-Null
$pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($source)
$count = 0
$sources = @()
while ($pending.Count) {
    $directory = $pending.Pop()
    if ([IO.File]::GetAttributes($directory) -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked test input refused.' }
    $relative = $directory.Substring($source.Length).TrimStart([char]92,[char]47)
    $target = if ($relative) { Join-Path $destination $relative } else { $destination }
    [IO.Directory]::CreateDirectory($target) | Out-Null
    foreach ($child in [IO.Directory]::GetDirectories($directory)) { $pending.Push($child) }
    foreach ($file in [IO.Directory]::GetFiles($directory)) {
        if ([IO.File]::GetAttributes($file) -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked test file refused.' }
        [IO.File]::Copy($file,(Join-Path $target ([IO.Path]::GetFileName($file))),$false); $count++
        if ([IO.Path]::GetExtension($file) -in @('.cs','.asmdef','.asmref')) {
            $sources += @{ Path=(Join-Path $target ([IO.Path]::GetFileName($file))); SourcePath=$file
                Sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
                MetaSha256=(Get-FileHash -LiteralPath ($file+'.meta') -Algorithm SHA256).Hash.ToLowerInvariant() }
        }
    }
}
$generated = $null
if ($profile -ceq 'Owned') {
    $template = Join-Path $PSScriptRoot 'HybridCLROwnedRunnerSceneTests.cs.txt'
    $path = Join-Path $destination 'EditMode/HybridCLROwnedRunnerSceneTests.cs'
    Assert-NoLinks $template
    Assert-NoLinks $path
    if ([IO.Path]::Exists($path) -or [IO.Path]::Exists($path+'.meta')) { throw 'Never replace another owned fixture.' }
    [IO.File]::Copy($template,$path,$false)
    $generated = @{ Path=$path; TemplatePath=$template; Sha256=(Get-FileHash -LiteralPath $template -Algorithm SHA256).Hash.ToLowerInvariant() }
    $count++
}
$manifestPath = Join-Path $project 'Packages/manifest.json'
$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -AsHashtable
$manifest.dependencies['com.unity.test-framework'] = $scope.TestFrameworkVersion
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 15), [Text.UTF8Encoding]::new($false))
if ($scope.IsCompatibility) {
    $receipt = @{ Purpose='YLFramework-HybridCLR-receiver-tests-v1'; Project=$project; UnityVersion=$scope.ExpectedUnityVersion
        UnityRevision=$scope.ExpectedUnityRevision; Receiver=$owner.receiver; OwnerSha256=$scope.OwnerSha256
        SceneProfile=$profile; CommonSceneCases=5; OwnedSceneCases=$(if ($profile -ceq 'Owned') { 8 } else { 0 })
        Sources=$sources; Generated=$generated; CopiedFiles=$count }
    [IO.File]::WriteAllText($receiptPath,($receipt | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
}
"Installed $count real test files after product-only import; Unity must resolve exact Test Framework $($scope.TestFrameworkVersion). Call HybridCLRTestFixtures.Prepare explicitly in this engine before tests; it was not called here."
