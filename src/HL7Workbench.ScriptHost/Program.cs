using System.Diagnostics;
using System.IO.Pipes;
using System.Threading.Channels;
using HL7Workbench.Core;
using HL7Workbench.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace HL7Workbench.ScriptHost;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 1 || !args[0].StartsWith("hl7-script-", StringComparison.Ordinal)) return 2;
        using var cancellation = new CancellationTokenSource();
        using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(30_000, cancellation.Token);
            var requests = Channel.CreateBounded<ScriptFrame>(1);
            var reader = ReadRequestsAsync(pipe, requests.Writer, cancellation);
            ScriptRunner<object?>? runner = null;
            await foreach (var request in requests.Reader.ReadAllAsync(cancellation.Token))
            {
                if (request.Kind == FrameKind.Initialize && runner is null)
                {
                    var definition = request.Definition ?? throw new InvalidDataException();
                    if (definition.Mode != ScriptMode.Message || definition.ApiVersion != 1 || definition.Source.Length > ScriptLimits.ScriptCharacters)
                        throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Configuration, "UnsupportedMode"));
                    var tree = CSharpSyntaxTree.ParseText(definition.Source, new CSharpParseOptions(kind: SourceCodeKind.Script));
                    if (tree.GetRoot().DescendantTrivia(descendIntoTrivia: true).Any(t =>
                        t.IsKind(SyntaxKind.ReferenceDirectiveTrivia) || t.IsKind(SyntaxKind.LoadDirectiveTrivia) ||
                        t.IsKind(SyntaxKind.LineDirectiveTrivia) || t.IsKind(SyntaxKind.LineSpanDirectiveTrivia)))
                        throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Compilation, "DirectiveNotSupported"));
                    var options = ScriptOptions.Default.AddReferences(typeof(ScriptGlobals).Assembly, typeof(Enumerable).Assembly)
                        .WithImports("System", "System.Linq", "System.Threading", "HL7Workbench.Scripting")
                        .WithFilePath("user-script.csx").WithEmitDebugInformation(true);
                    // UTF-8 source is required for portable PDBs; a plain string has no source encoding.
                    using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(definition.Source));
                    var script = CSharpScript.Create<object?>(source, options, typeof(ScriptGlobals));
                    var errors = script.Compile(cancellation.Token).Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Take(ScriptLimits.Diagnostics).Select(d =>
                        {
                            var position = d.Location.GetLineSpan().StartLinePosition;
                            return new ScriptDiagnostic(ScriptPhase.Compilation, d.Id, position.Line + 1, position.Character + 1);
                        }).ToArray();
                    if (errors.Length != 0) throw new ScriptRunException(errors);
                    runner = script.CreateDelegate(cancellation.Token);
                    await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Ready), cancellation.Token);
                }
                else if (request.Kind == FrameKind.Message && runner is not null && request.Bytes is { } bytes)
                {
                    ScriptMessage? facade = null;
                    try
                    {
                        var message = ScriptMessage.Parse(bytes, request.CodePage);
                        facade = new(message, cancellation.Token);
                        await runner(new ScriptGlobals(facade, cancellation.Token), cancellation.Token);
                        cancellation.Token.ThrowIfCancellationRequested();
                        await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Result,
                            Bytes: facade.Excluded ? null : Hl7Serializer.Serialize(message), Number: request.Number, Excluded: facade.Excluded), cancellation.Token);
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        var location = new StackTrace(error, true).GetFrames()?.FirstOrDefault(f => Path.GetFileName(f.GetFileName()) == "user-script.csx");
                        var diagnostics = error is ScriptRunException failure ? failure.Diagnostics.Select(d => d with
                        { MessageNumber = request.Number, Line = d.Line ?? location?.GetFileLineNumber(), Column = d.Column ?? location?.GetFileColumnNumber() }).ToArray() :
                            [new ScriptDiagnostic(ScriptPhase.Execution, "RuntimeFailed", location?.GetFileLineNumber(), location?.GetFileColumnNumber(), request.Number, facade?.LastPath)];
                        await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Failure, Diagnostics: diagnostics), cancellation.Token);
                        return 1;
                    }
                }
                else if (request.Kind == FrameKind.Finish && runner is not null)
                {
                    await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Finished), cancellation.Token);
                    return 0;
                }
                else throw new InvalidDataException();
            }
            await reader;
            return 1;
        }
        catch (ScriptRunException error)
        {
            try { await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Failure, Diagnostics: error.Diagnostics.ToArray()), CancellationToken.None); }
            catch (IOException) { }
            return 1;
        }
        catch (Exception) { return 1; } // Never print exception text or imported data to an inherited console.
    }
    private static async Task ReadRequestsAsync(Stream pipe, ChannelWriter<ScriptFrame> requests, CancellationTokenSource cancellation)
    {
        try
        {
            while (true)
            {
                var request = await ScriptProtocol.ReadAsync(pipe, cancellation.Token);
                if (request.Kind == FrameKind.Cancel) { cancellation.Cancel(); break; }
                await requests.WriteAsync(request, cancellation.Token);
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or System.Text.Json.JsonException)
        { cancellation.Cancel(); }
        finally { requests.TryComplete(); }
    }
}
