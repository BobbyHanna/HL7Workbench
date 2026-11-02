#Requires -Version 7.2
param(
    [switch]$VerificationOnly,
    [string]$OutputDirectory = '',
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
if (-not $VerificationOnly) { throw 'Stage 21B.1 permits -VerificationOnly. Release/candidate archives are not authorized.' }
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot ('artifacts/stage21b1-test-' + [guid]::NewGuid().ToString('N')) }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$artifactPrefix = (Join-Path $repositoryRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar
$protectedPrefix = (Join-Path $repositoryRoot 'artifacts/private-preview')
if (-not $outputRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    $outputRoot.StartsWith($protectedPrefix, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $outputRoot)) {
    throw 'Use a new verification directory inside artifacts, outside protected private-preview outputs.'
}
# Refuse redirected ancestors before creating or recursively cleaning an owned tree.
$ancestor = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($outputRoot))
while ($null -ne $ancestor -and $ancestor.FullName.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    if ($ancestor.Exists -and ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Verification roots cannot use links.' }
    $ancestor = $ancestor.Parent
}
New-Item -ItemType Directory -Path $outputRoot | Out-Null
Set-Content -LiteralPath (Join-Path $outputRoot 'NON-DISTRIBUTABLE-VERIFICATION.txt') -Value 'Stage 21B.1 test evidence only. No ZIP, candidate or distribution.'
$workRoot = Join-Path $outputRoot 'temporary publish with spaces-雪'
New-Item -ItemType Directory -Path $workRoot | Out-Null
$previousTelemetry = @{}
foreach ($name in @('DOTNET_CLI_TELEMETRY_OPTOUT', 'AVALONIA_TELEMETRY_OPTOUT', 'TESTINGPLATFORM_TELEMETRY_OPTOUT')) {
    $previousTelemetry[$name] = [Environment]::GetEnvironmentVariable($name)
    [Environment]::SetEnvironmentVariable($name, '1')
}
$tree = $null; $originalAcl = $null
Push-Location -LiteralPath $repositoryRoot
try {
    $properties = @('-p:SelfContained=true', '-p:RuntimeFrameworkVersion=10.0.12', '-p:NuGetLockFilePath=packages.win-x64.lock.json',
        '-p:PublishScriptHostSeparately=true', '-p:PublishQueryHostSeparately=true')
    foreach ($project in @('Desktop', 'ScriptHost', 'QueryHost')) {
        $restore = @('restore', "src/HL7Workbench.$project/HL7Workbench.$project.csproj", '-r', 'win-x64', '--locked-mode', '--artifacts-path', $workRoot) + $properties
        & dotnet @restore 2>&1 | Tee-Object -FilePath (Join-Path $outputRoot "$project-locked-restore.log")
        if ($LASTEXITCODE) { throw "Locked $project portable restore failed." }
        $target = Join-Path $workRoot "publish/$project"
        $publish = @('publish', "src/HL7Workbench.$project/HL7Workbench.$project.csproj", '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore',
            '--artifacts-path', $workRoot, '-p:DebugType=None', '-p:DebugSymbols=false', '-p:PublishTrimmed=false', '-p:PublishSingleFile=false',
            '-p:PublishAot=false', '-p:PublishReadyToRun=false', '-p:ContinuousIntegrationBuild=true', '-o', $target, '-warnaserror') + $properties
        & dotnet @publish 2>&1 | Tee-Object -FilePath (Join-Path $outputRoot "$project-publish.log")
        if ($LASTEXITCODE) { throw "Self-contained $project test publish failed." }
    }
    $cache = [Environment]::GetEnvironmentVariable('NUGET_PACKAGES')
    if (-not $cache) { $cache = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $workRoot 'publish/Desktop/HL7Workbench.Desktop.dll')).ProductVersion
    $stage = @('-B', (Join-Path $PSScriptRoot 'portable_release.py'), '--scope', 'current', '--verification-only',
        '--desktop', (Join-Path $workRoot 'publish/Desktop'), '--worker', (Join-Path $workRoot 'publish/ScriptHost'), '--query', (Join-Path $workRoot 'publish/QueryHost'),
        '--output', (Join-Path $workRoot 'stage'), '--package-cache', $cache, '--binary-version', $version)
    & $Python @stage 2>&1 | Tee-Object -FilePath (Join-Path $outputRoot 'stage.log')
    if ($LASTEXITCODE) { throw 'Current verification staging failed.' }
    $tree = Join-Path $workRoot 'stage/MessageWorkbench'
    # Deny writes/deletes to the actual extraction while retaining owner WRITE_DAC
    # for deterministic restoration. This is not the ineffectual folder R attribute.
    $originalAcl = Get-Acl -LiteralPath $tree
    $readOnlyAcl = Get-Acl -LiteralPath $tree
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $rights = [Security.AccessControl.FileSystemRights]'Write,Delete,DeleteSubdirectoriesAndFiles'
    $rule = [Security.AccessControl.FileSystemAccessRule]::new($sid, $rights,
        [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit', [Security.AccessControl.PropagationFlags]::None,
        [Security.AccessControl.AccessControlType]::Deny)
    $readOnlyAcl.AddAccessRule($rule); Set-Acl -LiteralPath $tree -AclObject $readOnlyAcl
    $writeDenied = $false
    try { [IO.File]::WriteAllText((Join-Path $tree 'forbidden-write-probe.txt'), 'synthetic write probe') }
    catch [UnauthorizedAccessException] { $writeDenied = $true }
    if (-not $writeDenied) { throw 'The test extraction is not actually write-denied.' }
    & (Join-Path $PSScriptRoot 'VerifyPortableWorkers.ps1') -PublishDirectory $tree 2>&1 | Tee-Object -FilePath (Join-Path $outputRoot 'workers.log')
    & $Python -B (Join-Path $PSScriptRoot 'portable_release.py') --verify $tree --scope current 2>&1 | Tee-Object -FilePath (Join-Path $outputRoot 'integrity.log')
    if ($LASTEXITCODE) { throw 'Post-worker integrity check failed.' }
    Copy-Item -LiteralPath (Join-Path $tree 'BUILD-PROVENANCE.json') -Destination (Join-Path $outputRoot 'tested-provenance.json')
    Write-Output 'Verified three-process test publishing and both workers. No archive was created. Temporary binaries will be removed; logs/provenance remain.'
}
finally {
    if ($null -ne $originalAcl -and $null -ne $tree -and (Test-Path -LiteralPath $tree)) { Set-Acl -LiteralPath $tree -AclObject $originalAcl }
    Pop-Location
    foreach ($name in $previousTelemetry.Keys) { [Environment]::SetEnvironmentVariable($name, $previousTelemetry[$name]) }
    $resolvedWork = [IO.Path]::GetFullPath($workRoot)
    if ($resolvedWork.StartsWith($outputRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        $resolvedWork -eq $workRoot -and (Test-Path -LiteralPath (Join-Path $outputRoot 'NON-DISTRIBUTABLE-VERIFICATION.txt'))) {
        if (Test-Path -LiteralPath $resolvedWork) { Remove-Item -LiteralPath $resolvedWork -Recurse -Force }
    } else { throw 'Owned temporary publish path could not be verified; retained for review.' }
}
