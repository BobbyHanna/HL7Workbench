using HL7Workbench.Scripting;
using HL7Workbench.Workspace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HL7Workbench.Desktop.Viewer;

internal sealed record ScriptApiHelp(IReadOnlyList<string> Topics, string Notice, string Signatures);
internal sealed record ScriptApiCompletion(string Text, string Description);

/// <summary>Bounded compiler-only help over the catalog. No source, syntax tree or semantic model survives a request.</summary>
internal static class ScriptApiLookup
{
    private static ScriptApiReference Reference => ScriptApiReference.Current;
    internal static ScriptApiHelp Unknown => new([], "No supported script API recognized here. Search the reference or place the cursor on an API symbol.", "");

    internal static ScriptApiHelp Find(string source, int caret, CancellationToken token, bool signatureHelp = false)
    {
        if (source.Length is 0 or > ScriptLimits.ScriptCharacters) return Unknown;
        var (tree, model) = ScriptSymbolClassifier.Analyze(source, token);
        var root = tree.GetRoot(token); var position = Position(source, caret);
        if (InComment(root, position)) return Unknown;
        var syntax = root.FindToken(position);
        if (signatureHelp)
        {
            var invocation = syntax.Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
            return invocation is null ? Unknown : Describe(model.GetSymbolInfo(invocation, token));
        }
        if (syntax.Parent?.AncestorsAndSelf().Any(n => n is LiteralExpressionSyntax or InterpolatedStringTextSyntax) == true) return Unknown;
        if (syntax.Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { Parent: CompilationUnitSyntax } entry &&
            entry.Identifier.Span.Contains(position) && entry.Identifier.ValueText is "Main" or "Complete")
            return Help([Reference.Entries.Single(e => e.Id == "lifecycle")], false);
        var name = syntax.Parent?.AncestorsAndSelf().OfType<SimpleNameSyntax>().FirstOrDefault();
        if (name is null) return Unknown;
        if (name is IdentifierNameSyntax identifier && model.GetAliasInfo(identifier, token) is { } alias)
            return Help(Topics(alias.Target), false);
        if (name.Parent is ObjectCreationExpressionSyntax creation && creation.Type.Span.Contains(position))
            return Describe(model.GetSymbolInfo(creation, token));
        return Describe(model.GetSymbolInfo(name, token));
    }
    private static ScriptApiHelp Describe(SymbolInfo info)
    {
        if (info.Symbol is { } symbol) return Help(Topics(symbol), false);
        return Help(info.CandidateSymbols.SelectMany(Topics).DistinctBy(e => e.Id).ToArray(), true);
    }
    private static IReadOnlyList<ScriptApiEntry> Topics(ISymbol symbol)
    {
        if (symbol.ContainingNamespace?.ToDisplayString() == "System.Threading" &&
            symbol.ContainingType?.Name == "CancellationToken" && symbol.Name == "ThrowIfCancellationRequested")
            return [Reference.Entries.Single(e => e.Id == "globals")];
        var type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
        if (type is null || type.ContainingAssembly.Name != typeof(ScriptGlobals).Assembly.GetName().Name ||
            type.ContainingNamespace.ToDisplayString() != "HL7Workbench.Scripting") return [];
        if (Reference.InfrastructureTypes.ContainsKey(type.Name)) return Reference.Entries.Where(e => e.IsInfrastructure).ToArray();
        if (symbol is INamedTypeSymbol)
        {
            var declared = Reference.Entries.Where(e => e.Contracts.Any(c => c.DeclaringType == type.Name)).ToArray();
            // Opaque run-owned handles have no public constructors/members. Link
            // their producing/consuming methods rather than inventing an API.
            return declared.Length > 0 ? declared : Reference.Entries.Where(e => e.Contracts.Any(c =>
                c.ReturnType == type.Name || c.Parameters.Any(p => p.Type == type.Name))).ToArray();
        }
        var member = type.Name + "." + (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } ? ".ctor" : symbol.Name);
        return Reference.ForMember(member);
    }
    private static ScriptApiHelp Help(IReadOnlyList<ScriptApiEntry> entries, bool ambiguous)
    {
        if (entries.Count == 0) return Unknown;
        return new(entries.Select(e => e.Id).ToArray(), entries.All(e => e.IsInfrastructure) ? "Public host infrastructure: unsupported for script authoring. Review the boundary and metadata." :
            ambiguous ? "Ambiguous call: review the supported candidate contracts; no overload was chosen." :
            entries.Count > 1 ? "Several topics describe this API type. Choose a topic to inspect its exact contracts." : "API symbol recognized. Showing its supported contract.",
            string.Join("\n", entries.Select(e => e.ExactSignature)));
    }
    internal static IReadOnlyList<ScriptApiCompletion> Complete(string source, int caret, CancellationToken token)
    {
        if (source.Length > ScriptLimits.ScriptCharacters) return [];
        caret = Math.Clamp(caret, 0, source.Length);
        var start = caret;
        while (start > 0 && SyntaxFacts.IsIdentifierPartCharacter(source[start - 1])) start--;
        var prefix = source[start..caret];
        var (tree, model) = ScriptSymbolClassifier.Analyze(source, token); var root = tree.GetRoot(token);
        if (source.Length > 0 && InComment(root, Position(source, caret))) return [];
        if (start > 0 && source[start - 1] == '.')
        {
            var access = root.FindToken(start - 1).Parent?.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
            if (access is null) return [];
            var type = model.GetTypeInfo(access.Expression, token).Type;
            if (type is null || type.ContainingAssembly.Name != typeof(ScriptGlobals).Assembly.GetName().Name || type.ContainingNamespace.ToDisplayString() != "HL7Workbench.Scripting") return [];
            var isStatic = model.GetSymbolInfo(access.Expression, token).Symbol is INamedTypeSymbol;
            return Reference.Contracts.Where(c => c.DeclaringType == type.Name && c.Kind != "Constructor" && c.IsStatic == isStatic &&
                    c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).GroupBy(c => c.Name).Select(g =>
                    new ScriptApiCompletion(g.Key, string.Join("\n", g.Select(c => c.Signature)) + "\n" + Documentation(Reference.ForMember(g.First().Member).First())))
                .OrderBy(c => c.Text, StringComparer.Ordinal).ToArray();
        }
        // Only curated facade types/globals are offered. This is deliberately not
        // a general C#/.NET language service, analyzer host or directive resolver.
        if (source.Length > 0 && root.FindToken(Position(source, caret)).Parent?.AncestorsAndSelf().Any(n => n is LiteralExpressionSyntax or InterpolatedStringTextSyntax) == true) return [];
        return Reference.SupportedTypes.Concat(new[] { "Message", "Batch", "Cancellation" }).Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(n => new ScriptApiCompletion(n, n is "Message" or "Batch" or "Cancellation" ? Documentation(Reference.Entries.Single(e => e.Id == "globals")) :
                string.Join("\n", Reference.Entries.Where(e => e.Contracts.Any(c => c.DeclaringType == n)).Select(e => e.Description))))
            .OrderBy(c => c.Text, StringComparer.Ordinal).ToArray();
    }
    private static string Documentation(ScriptApiEntry entry) => entry.Description + "\nParameters: " + entry.Parameters +
        "\nReturns: " + entry.Returns + "\nNull/missing: " + entry.NullBehavior + "\nErrors: " + entry.Errors + "\nMutation: " + entry.Mutation + "\nModes: " + entry.Modes;
    private static int Position(string source, int caret)
    {
        caret = Math.Clamp(caret, 0, source.Length);
        return caret > 0 && (caret == source.Length || SyntaxFacts.IsIdentifierPartCharacter(source[caret - 1])) ? caret - 1 : caret;
    }
    private static bool InComment(SyntaxNode root, int position) => root.FindTrivia(position).Kind() is
        SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia or SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia;
}
