param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '../../test-data/generated/stage7')
)

# Independently authored invented values only. Generate disposable native-acceptance
# inputs outside the permanent fixture set, preserving exact CR protocol terminators.
$targetDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($targetDirectory) | Out-Null
$headerA = 'MSH|^~\&|SYN|TEST|DEST|TEST|20261016090000||ORU^R01|SYN-COMP-A|T|2.5'
$headerB = 'MSH|^~\&|SYN|TEST|DEST|TEST|20261016100000||ORU^R01|SYN-COMP-B|T|2.5'
$a = (@(
    $headerA,
    'PID|1||SYN-COMP-001^^^TEST^MR||Example^Ada',
    'OBX|1|TX|SYN-A||ALPHA',
    'OBX|2|TX|SYN-B||BETA',
    'OBX|3|TX|SYN-C||GAMMA',
    'NTE|1||REMOVE THIS SYNTHETIC NOTE'
) -join "`r") + "`r"
$b = (@(
    $headerB,
    'PID|1||SYN-COMP-001^^^TEST^MR~SYN-ALT^^^TEST^AN||Example^Bea',
    'OBX|1|TX|SYN-A||ALPHA',
    'OBX|99|TX|SYN-INSERT||INSERTED',
    'OBX|2|TX|SYN-B||BETA',
    'OBX|3|TX|SYN-C||GAMMA-CHANGED',
    'ZST|TRAILING||'
) -join "`r") + "`r"
$encoding = [System.Text.UTF8Encoding]::new($false)
$outputs = [ordered]@{
    'comparison-a.hl7' = $a
    'comparison-b.hl7' = $b
    'comparison-combined.hl7' = $a + $b + $a.Replace('SYN-COMP-A', 'SYN-COMP-THIRD')
}
foreach ($name in $outputs.Keys) {
    $path = Join-Path $targetDirectory $name
    if (Test-Path -LiteralPath $path) { throw "Refusing to replace an existing file: $path" }
}
foreach ($name in $outputs.Keys) {
    $path = Join-Path $targetDirectory $name
    [System.IO.File]::WriteAllText($path, $outputs[$name], $encoding)
    Write-Output $path
}
