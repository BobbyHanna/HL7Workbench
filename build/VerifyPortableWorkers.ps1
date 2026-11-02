#Requires -Version 7.2
param([Parameter(Mandatory = $true)][string]$PublishDirectory)
$ErrorActionPreference = 'Stop'
$published = (Resolve-Path -LiteralPath $PublishDirectory).Path
foreach ($name in @('ScriptHost', 'QueryHost')) {
    foreach ($leaf in @("HL7Workbench.$name.exe", "HL7Workbench.$name.dll", "HL7Workbench.$name.deps.json", "HL7Workbench.$name.runtimeconfig.json", 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $published "$name/$leaf") -PathType Leaf)) { throw "Incomplete $name test deployment." }
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $published 'QueryHost/e_sqlite3.dll') -PathType Leaf)) { throw 'Missing native SQLite.' }
# Both lifecycles run from an unrelated working directory. Each child gets an
# unusable DOTNET_ROOT and no SDK host on PATH; direct apphosts must suffice.
Push-Location -LiteralPath (Join-Path $env:SystemRoot 'System32')
try { & (Join-Path $PSScriptRoot 'VerifyScriptPublish.ps1') -PublishDirectory $published -SelfContained }
finally { Pop-Location }
$pipeName = 'hl7-query-' + [guid]::NewGuid().ToString('N')
$pipe = [IO.Pipes.NamedPipeServerStream]::new($pipeName, [IO.Pipes.PipeDirection]::InOut, 1,
    [IO.Pipes.PipeTransmissionMode]::Byte, [IO.Pipes.PipeOptions]::Asynchronous)
$deadline = [Threading.CancellationTokenSource]::new(30000)
$worker = $null
function Read-Exact([byte[]]$buffer) {
    $offset = 0
    while ($offset -lt $buffer.Length) {
        $read = $pipe.ReadAsync($buffer, $offset, $buffer.Length - $offset, $deadline.Token).GetAwaiter().GetResult()
        if ($read -eq 0) { throw 'Query worker closed an incomplete frame.' }
        $offset += $read
    }
}
function Exchange-Query($frame) {
    $data = [Text.Encoding]::UTF8.GetBytes(($frame | ConvertTo-Json -Depth 16 -Compress))
    $header = [BitConverter]::GetBytes([int]$data.Length)
    [void]$pipe.WriteAsync($header, 0, 4, $deadline.Token).GetAwaiter().GetResult()
    [void]$pipe.WriteAsync($data, 0, $data.Length, $deadline.Token).GetAwaiter().GetResult()
    $pipe.Flush(); Read-Exact $header
    $length = [BitConverter]::ToInt32($header, 0)
    if ($length -lt 1 -or $length -gt 2097152) { throw 'Invalid query-worker frame size.' }
    $data = [byte[]]::new($length); Read-Exact $data
    return [Text.Encoding]::UTF8.GetString($data) | ConvertFrom-Json
}
try {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = Join-Path $published 'QueryHost/HL7Workbench.QueryHost.exe'
    $start.Arguments = $pipeName; $start.WorkingDirectory = Join-Path $env:SystemRoot 'System32'
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.EnvironmentVariables['PATH'] = Join-Path $env:SystemRoot 'System32'
    $start.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $published 'unavailable-dotnet'
    $start.EnvironmentVariables['DOTNET_ROOT_X64'] = Join-Path $published 'unavailable-dotnet'
    $start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $worker = [Diagnostics.Process]::Start($start)
    $stdout = $worker.StandardOutput.ReadToEndAsync(); $stderr = $worker.StandardError.ReadToEndAsync()
    [void]$pipe.WaitForConnectionAsync($deadline.Token).GetAwaiter().GetResult()
    $ready = Exchange-Query @{ Kind = 0; Version = 2; Budgets = @{ ResultRows = 10; ResultBytes = 65536; ImportedRows = 10; ImportBytes = 65536; QueryMilliseconds = 5000; ProgressCallbacks = 10000 } }
    if ($ready.Kind -ne 1 -or $ready.EngineVersion -ne '3.53.4') { throw 'Query worker/native SQLite initialization failed.' }
    $rows = @(1..3 | ForEach-Object { @{ Table = 0; Values = @(@{ Kind = 3; Text = "synthetic-$_" }, @{ Kind = 1; Integer = $_ }, @{ Kind = 1; Integer = 0 }) } })
    $imported = Exchange-Query @{ Kind = 2; Version = 2; Input = $rows }
    if ($imported.Kind -ne 3) { throw 'Query worker typed import failed.' }
    $header = Exchange-Query @{ Kind = 4; Version = 2; Sql = 'SELECT COUNT(*) AS DocumentCount FROM Documents' }
    $page = Exchange-Query @{ Kind = 6; Version = 2 }
    if ($header.Kind -ne 5 -or @($header.Columns)[0] -ne 'DocumentCount' -or $page.Kind -ne 7 -or
        -not $page.Complete -or @($page.Rows).Count -ne 1 -or $page.Rows[0].Cells[0].Integer -ne 3) { throw 'Query worker complete result verification failed.' }
    $closed = Exchange-Query @{ Kind = 10; Version = 2 }
    if ($closed.Kind -ne 11 -or -not $worker.WaitForExit(5000) -or $worker.ExitCode -ne 0) { throw 'Query worker did not close cleanly.' }
    [void]$stdout.GetAwaiter().GetResult(); [void]$stderr.GetAwaiter().GetResult()
    Write-Output 'Verified QueryHost: isolated apphost, pinned native SQLite, typed import, complete SQL result and clean exit; no installed runtime or working-directory dependency.'
}
finally {
    $pipe.Dispose(); $deadline.Dispose()
    if ($null -ne $worker) {
        try { if (-not $worker.HasExited) { $worker.Kill($true); if (-not $worker.WaitForExit(5000)) { throw 'Query test worker cleanup timed out.' } } }
        finally { $worker.Dispose() }
    }
}
