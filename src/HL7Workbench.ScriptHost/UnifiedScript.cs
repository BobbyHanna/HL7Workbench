using HL7Workbench.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;

namespace HL7Workbench.ScriptHost;

/// <summary>Compiles one user submission and one fixed delegate-binding continuation per run.
/// The resulting callbacks retain that submission; no continuation is created per message.</summary>
internal static class UnifiedScript
{
    internal static ScriptRunner<Action[]> Compile(ScriptDefinition definition, CancellationToken token)
    {
        if (definition.ApiVersion != 2) throw Failure("UnsupportedApi");
        if (definition.Source.Length > ScriptLimits.ScriptCharacters) throw Failure("ScriptLimit");
        var tree = CSharpSyntaxTree.ParseText(definition.Source, new CSharpParseOptions(kind: SourceCodeKind.Script), cancellationToken: token);
        if (tree.GetRoot(token).DescendantTrivia(descendIntoTrivia: true).Any(t =>
            t.IsKind(SyntaxKind.ReferenceDirectiveTrivia) || t.IsKind(SyntaxKind.LoadDirectiveTrivia) ||
            t.IsKind(SyntaxKind.LineDirectiveTrivia) || t.IsKind(SyntaxKind.LineSpanDirectiveTrivia))) throw Failure("DirectiveNotSupported");
        var options = ScriptOptions.Default.AddReferences(typeof(ScriptGlobals).Assembly, typeof(Enumerable).Assembly)
            .WithImports("System", "System.Linq", "System.Threading", "System.Collections.Generic", "HL7Workbench.Scripting")
            .WithFilePath("user-script.csx").WithEmitDebugInformation(true);
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(definition.Source));
        var script = CSharpScript.Create<object?>(source, options, typeof(ScriptGlobals));
        var compilation = script.GetCompilation();
        var errors = compilation.GetDiagnostics(token).Where(d => d.Severity == DiagnosticSeverity.Error)
            .Take(ScriptLimits.Diagnostics).Select(d =>
            {
                var position = d.Location.GetLineSpan().StartLinePosition;
                return new ScriptDiagnostic(ScriptPhase.Compilation, d.Id, position.Line + 1, position.Character + 1);
            }).ToArray();
        if (errors.Length > 0) throw new ScriptRunException(errors);
        var userTree = compilation.SyntaxTrees.Single();
        var root = (CompilationUnitSyntax)userTree.GetRoot(token);
        var model = compilation.GetSemanticModel(userTree);
        bool Entry(string name, bool required)
        {
            var methods = root.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText == name).ToArray();
            if (methods.Length == 0)
            {
                if (compilation.ScriptClass?.GetMembers(name).FirstOrDefault() is { } other)
                    throw Failure("EntryPointSignature", other.Locations[0].GetLineSpan().StartLinePosition);
                if (required) throw Failure("MissingMain");
                return false;
            }
            var location = methods[^1].Identifier.GetLocation().GetLineSpan().StartLinePosition;
            if (methods.Length != 1) throw Failure("AmbiguousEntryPoint", location);
            var symbol = model.GetDeclaredSymbol(methods[0], token) as IMethodSymbol;
            if (symbol is null || symbol.IsStatic || symbol.IsAsync || symbol.IsAbstract || symbol.IsExtern || symbol.Arity != 0 ||
                symbol.Parameters.Length != 0 || symbol.ReturnType.SpecialType != SpecialType.System_Void)
                throw Failure("EntryPointSignature", location);
            return true;
        }
        Entry("Main", required: true);
        var complete = Entry("Complete", required: false);
        // Bind through the C# compiler, not generated-name reflection or source rewriting.
        // The adapter has a separate identity and no user diagnostics/PDB source mappings.
        var adapter = script.ContinueWith<Action[]>(complete ? "new global::System.Action[] { Main, Complete }" : "new global::System.Action[] { Main }",
            options.WithFilePath("host-lifecycle.csx").WithEmitDebugInformation(false));
        return adapter.CreateDelegate(token);
    }
    private static ScriptRunException Failure(string code, Microsoft.CodeAnalysis.Text.LinePosition? position = null) =>
        new(new ScriptDiagnostic(ScriptPhase.Compilation, code, position?.Line + 1 ?? 1, position?.Character + 1 ?? 1));
}
