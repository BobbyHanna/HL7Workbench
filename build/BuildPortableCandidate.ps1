param(
    [string]$OutputDirectory = '',
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot ('artifacts/stage14/local-' + [guid]::NewGuid().ToString('N')) }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$artifactPrefix = (Join-Path $repositoryRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar
if (-not $outputRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $outputRoot)) {
    throw 'Use a new directory inside repository artifacts; existing outputs are never replaced.'
}
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$previousTelemetry = @{}
foreach ($name in @('DOTNET_CLI_TELEMETRY_OPTOUT', 'AVALONIA_TELEMETRY_OPTOUT', 'TESTINGPLATFORM_TELEMETRY_OPTOUT')) {
    $previousTelemetry[$name] = [Environment]::GetEnvironmentVariable($name)
    [Environment]::SetEnvironmentVariable($name, '1')
}
Push-Location -LiteralPath $repositoryRoot
try {
    $restore = @('restore', 'src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj', '-r', 'win-x64', '--locked-mode',
        '-p:SelfContained=true', '-p:RuntimeFrameworkVersion=10.0.12', '-p:NuGetLockFilePath=packages.win-x64.lock.json')
    & dotnet @restore 2>&1 | Tee-Object -FilePath (Join-Path $outputRoot 'restore.log')
    if ($LASTEXITCODE) { throw 'Locked portable restore failed.' }
    foreach ($project in @('Desktop', 'ScriptHost')) {
        $target = Join-Path $outputRoot "publish/$project"
        $arguments = @('publish', "src/HL7Workbench.$project/HL7Workbench.$project.csproj", '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore',
            '-p:RuntimeFrameworkVersion=10.0.12', '-p:NuGetLockFilePath=packages.win-x64.lock.json', '-p:PublishScriptHostSeparately=true',
            '-p:DebugType=None', '-p:DebugSymbols=false', '-p:PublishTrimmed=false', '-p:PublishSingleFile=false', '-p:PublishReadyToRun=false',
            '-p:ContinuousIntegrationBuild=true', '-o', $target, '-warnaserror')
        & dotnet @arguments 2>&1 | Tee-Object -FilePath (Join-Path $outputRoot "$project-publish.log")
        if ($LASTEXITCODE) { throw "Self-contained $project publish failed." }
    }
    $cache = [Environment]::GetEnvironmentVariable('NUGET_PACKAGES')
    if (-not $cache) { $cache = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
    & $Python -B (Join-Path $PSScriptRoot 'portable_release.py') --desktop (Join-Path $outputRoot 'publish/Desktop') --worker (Join-Path $outputRoot 'publish/ScriptHost') --output (Join-Path $outputRoot 'CANDIDATE') --package-cache $cache
    if ($LASTEXITCODE) { throw 'Candidate assembly/verification failed; retain logs for review.' }
    $candidateRoot = Join-Path $outputRoot 'CANDIDATE/MessageWorkbench'
    $metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $candidateRoot 'MessageWorkbench.exe'))
    if ($metadata.ProductName -ne 'Message Workbench' -or $metadata.FileDescription -ne 'Message Workbench' -or
        $metadata.FileVersion -ne '0.1.0.0' -or $metadata.ProductVersion -notlike '0.1.0-beta*' -or
        [Reflection.AssemblyName]::GetAssemblyName((Join-Path $candidateRoot 'HL7Workbench.Desktop.dll')).Name -ne 'HL7Workbench.Desktop') {
        throw 'Candidate product/version or technical identity mismatch.'
    }
    & (Join-Path $PSScriptRoot 'VerifyScriptPublish.ps1') -PublishDirectory (Join-Path $outputRoot 'CANDIDATE/MessageWorkbench') -SelfContained
    & $Python -B (Join-Path $PSScriptRoot 'portable_release.py') --verify (Join-Path $outputRoot 'CANDIDATE/MessageWorkbench')
    if ($LASTEXITCODE) { throw 'Post-smoke candidate integrity failed.' }
    Write-Output 'LOCAL TEST CANDIDATE ONLY. No distribution, upload, release or publication is authorized.'
}
finally {
    Pop-Location
    foreach ($name in $previousTelemetry.Keys) { [Environment]::SetEnvironmentVariable($name, $previousTelemetry[$name]) }
}
