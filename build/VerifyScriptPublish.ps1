param([string]$PublishDirectory = 'artifacts/stage5b-publish', [switch]$SelfContained)
$ErrorActionPreference = 'Stop'
$published = (Resolve-Path -LiteralPath $PublishDirectory).Path
$hostFile = Join-Path $published 'ScriptHost/HL7Workbench.ScriptHost.dll'
if ($SelfContained) {
    foreach ($directory in @($published, (Join-Path $published 'ScriptHost'))) {
        foreach ($native in @('coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll')) {
            if (-not (Test-Path -LiteralPath (Join-Path $directory $native))) { throw 'Missing self-contained runtime file' }
        }
        $configuration = Get-ChildItem -LiteralPath $directory -Filter '*.runtimeconfig.json' -File
        if (@($configuration).Count -ne 1) { throw 'Unexpected process configuration inventory' }
        $runtime = (Get-Content -LiteralPath $configuration.FullName -Raw | ConvertFrom-Json).runtimeOptions
        if ($runtime.framework -or $runtime.frameworks -or @($runtime.includedFrameworks).Count -ne 1 -or
            $runtime.includedFrameworks[0].name -ne 'Microsoft.NETCore.App' -or $runtime.includedFrameworks[0].version -ne '10.0.12') {
            throw 'Not the pinned self-contained runtime'
        }
    }
}

# Compiler-only Roslyn is expected in Desktop; execution dependencies must stay in ScriptHost.
foreach ($name in @('Microsoft.CodeAnalysis.Scripting.dll', 'Microsoft.CodeAnalysis.CSharp.Scripting.dll', 'HL7Workbench.ScriptHost.dll')) {
    if (Test-Path -LiteralPath (Join-Path $published $name)) { throw 'Worker execution dependency leaked to Desktop root' }
    if (-not (Test-Path -LiteralPath (Join-Path $published "ScriptHost/$name"))) { throw 'Worker execution dependency missing' }
}
foreach ($name in @('Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.CSharp.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $published $name))) { throw 'Desktop compiler dependency missing' }
}
$desktopDependencies = Get-Content -LiteralPath (Join-Path $published 'HL7Workbench.Desktop.deps.json') -Raw
if ($desktopDependencies.Contains('Microsoft.CodeAnalysis.CSharp.Scripting/') -or $desktopDependencies.Contains('Microsoft.CodeAnalysis.Scripting/')) {
    throw 'Desktop dependency graph includes scripting runtime'
}
$workerBuild = if ($SelfContained) { $null } else { (Resolve-Path 'src/HL7Workbench.ScriptHost/bin/Release/net10.0').Path }
# Include the directory separator so a sibling with the same prefix cannot match.
# GetRelativePath is unavailable in Windows PowerShell 5.1's .NET Framework.
$workerPrefix = if ($workerBuild) { $workerBuild.TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar } else { '' }
foreach ($file in $(if ($workerBuild) {
    Get-ChildItem -LiteralPath $workerBuild -File
    Get-ChildItem -LiteralPath $workerBuild -Directory | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Filter '*.resources.dll' -File }
})) {
    if (-not $file.FullName.StartsWith($workerPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Worker build file is outside its root'
    }
    $relative = $file.FullName.Substring($workerPrefix.Length)
    $deployed = Join-Path $published "ScriptHost/$relative"
    if (-not (Test-Path -LiteralPath $deployed) -or (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $deployed).Hash) {
        throw 'Release/published worker hash mismatch'
    }
}

function Test-PublishedWorker([int]$messageCount) {
    $pipeName = 'hl7-script-publish-' + [guid]::NewGuid().ToString('N')
    $pipe = [IO.Pipes.NamedPipeServerStream]::new($pipeName, [IO.Pipes.PipeDirection]::InOut, 1,
        [IO.Pipes.PipeTransmissionMode]::Byte, [IO.Pipes.PipeOptions]::Asynchronous)
    $deadline = [Threading.CancellationTokenSource]::new(30000)
    $worker = $null
    function Read-WorkerBytes([byte[]]$buffer) {
        # Stream reads may be short. Retain the same deadline across every read and
        # reject early EOF rather than accepting a partial header or JSON payload.
        $offset = 0
        while ($offset -lt $buffer.Length) {
            $read = $pipe.ReadAsync($buffer, $offset, $buffer.Length - $offset, $deadline.Token).GetAwaiter().GetResult()
            if ($read -eq 0) { throw 'Worker pipe closed before a complete frame arrived' }
            $offset += $read
        }
    }
    function Exchange-Frame($frame) {
        $data = [Text.Encoding]::UTF8.GetBytes(($frame | ConvertTo-Json -Depth 6 -Compress))
        $header = [BitConverter]::GetBytes([int]$data.Length)
        # .NET Framework can expose VoidTaskResult through PowerShell's adapter;
        # discard it so only the decoded response enters the function's pipeline.
        [void]$pipe.WriteAsync($header, 0, 4, $deadline.Token).GetAwaiter().GetResult()
        [void]$pipe.WriteAsync($data, 0, $data.Length, $deadline.Token).GetAwaiter().GetResult()
        $pipe.Flush()
        $header = [byte[]]::new(4)
        Read-WorkerBytes $header
        $length = [BitConverter]::ToInt32($header, 0)
        if ($length -lt 1 -or $length -gt 12582912) { throw 'Invalid worker frame' }
        $data = [byte[]]::new($length)
        Read-WorkerBytes $data
        return ([Text.Encoding]::UTF8.GetString($data) | ConvertFrom-Json)
    }
    try {
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = if ($SelfContained) { Join-Path $published 'ScriptHost/HL7Workbench.ScriptHost.exe' } else { 'dotnet' }
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true
        # The absolute DLL path ends in .dll (no trailing slash); Windows file paths
        # cannot contain quotes. The generated pipe name contains only safe letters/hex.
        $start.Arguments = if ($SelfContained) { $pipeName } else { '"' + $hostFile + '" ' + $pipeName }
        if ($SelfContained) {
            # A successful lifecycle must not borrow an installed dotnet host/runtime.
            $start.EnvironmentVariables['PATH'] = "$env:SystemRoot\System32"
            $start.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $published 'unavailable-dotnet'
            $start.EnvironmentVariables['DOTNET_ROOT_X64'] = Join-Path $published 'unavailable-dotnet'
            $start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
        }
        $worker = [Diagnostics.Process]::Start($start)
        [void]$pipe.WaitForConnectionAsync($deadline.Token).GetAwaiter().GetResult()
        $source = 'var n = 0; ScriptDeferredMessage held = null!; var r = Batch.Report("summary", "Count"); Batch.SetOutputFileName("Initial"); void Main() { n++; Message.SetText("PID-5.2", n.ToString()); if(n == 1) held = Batch.Hold(Message); } void Complete() { r.Row(n.ToString()); Batch.Emit(held); Batch.SetOutputFileName("Synthetic smoke.HL7"); Batch.SetOutputFileName("../unsafe"); }'
        $ready = Exchange-Frame @{ Kind = 0; Definition = @{ Source = $source; ApiVersion = 2 }; Version = 2 }
        if ($ready.Kind -ne 1) { throw ('Published compilation failed: ' + ($ready.Diagnostics.Code -join ', ')) }
        $begun = Exchange-Frame @{ Kind = 8; Version = 2 }
        if ($begun.Kind -ne 9 -or $begun.Reports.Count -ne 1) { throw 'Published initialization failed' }
        $bytes = [Text.Encoding]::UTF8.GetBytes("MSH|^~\&|SYN`rPID|1||SYN-001||Example^Ada`r")
        foreach ($number in 1..$messageCount) {
            $result = Exchange-Frame @{ Kind = 2; Bytes = [Convert]::ToBase64String($bytes); CodePage = 65001; Number = $number; Version = 2 }
            $expected = "Example^$number"
            if ($result.Kind -ne 3 -or $result.Excluded -or -not [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($result.Bytes)).Contains($expected)) {
                throw 'Published transformation/state failed'
            }
        }
        $finished = Exchange-Frame @{ Kind = 4; Version = 2 }
        if ($finished.Kind -ne 5) { throw 'Published completion failed' }
        if ($finished.OutputNaming.Version -ne 1 -or $finished.OutputNaming.FileName -ne 'Synthetic smoke.hl7' -or -not $finished.OutputNaming.IgnoredSuggestion) {
            throw 'Worker output filename metadata failed'
        }
        if ($finished.DeferredCount -ne 1 -or [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($finished.Reports[0].Bytes)) -ne "`"$messageCount`"`r`n") {
            throw 'Published CSV completion failed'
        }
        $copy = Exchange-Frame @{ Kind = 10; Version = 2 }
        if ($copy.Kind -ne 3 -or $copy.Number -ne 1 -or -not [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($copy.Bytes)).Contains('Example^1')) {
            throw 'Published deferred copy failed'
        }
        if (-not $worker.WaitForExit(5000) -or $worker.ExitCode -ne 0) { throw 'Published worker did not exit cleanly' }
    } finally {
        $pipe.Dispose(); $deadline.Dispose()
        if ($null -ne $worker) {
            try {
                if (-not $worker.HasExited) {
                    # .NET Framework lacks Kill(entireProcessTree). Windows taskkill
                    # preserves descendant cleanup on timeout or failed verification.
                    $cleanup = Start-Process -FilePath "$env:SystemRoot\System32\taskkill.exe" -ArgumentList @('/PID', [string]$worker.Id, '/T', '/F') -WindowStyle Hidden -PassThru -Wait
                    try {
                        if ($cleanup.ExitCode -ne 0 -and -not $worker.HasExited) { throw 'Published worker tree cleanup failed' }
                        if (-not $worker.WaitForExit(5000)) { throw 'Published worker cleanup timed out' }
                    } finally { $cleanup.Dispose() }
                }
            } finally { $worker.Dispose() }
        }
    }
}
Test-PublishedWorker 1
Test-PublishedWorker 2
Write-Output "Verified unified Main current/whole lifecycles, shared state, CSV, completion, deferred output, clean exit and dependency isolation (self-contained: $SelfContained)."
