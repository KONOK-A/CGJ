[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $Run, [switch] $Stack)
$ErrorActionPreference = 'Stop'
if ($Run -notmatch '^[A-Za-z0-9_-]+$') { throw 'Unsafe Run.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$path = Join-Path $root "Logs\YLFramework.Domain\$Run\results.xml"
$xml = New-Object Xml.XmlDocument
$xml.XmlResolver = $null
$xml.Load($path)
$runNode = $xml.DocumentElement
[pscustomobject]@{ Run = $Run; Total = $runNode.GetAttribute('total'); Passed = $runNode.GetAttribute('passed'); Failed = $runNode.GetAttribute('failed'); Skipped = $runNode.GetAttribute('skipped'); Result = $runNode.GetAttribute('result') } | Format-List
foreach ($test in $xml.SelectNodes("//test-case[@result='Failed']")) {
    "FAILED: $($test.GetAttribute('fullname'))"
    $message = $test.SelectSingleNode('failure/message')
    if ($message) { $message.InnerText }
    if ($Stack) { $trace = $test.SelectSingleNode('failure/stack-trace'); if ($trace) { $trace.InnerText } }
}
