using HL7Workbench.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HL7Workbench.Desktop.Viewer;

internal enum ScriptSymbolKind { Neutral, Method, Type, Keyword, String, Comment, Number }
internal readonly record struct ScriptSymbolSpan(int Offset, int Length, ScriptSymbolKind Kind);

/// <summary>Bounded compiler-only classification: no emit, execution, reference directives, analyzers or user-source cache.</summary>
internal static class ScriptSymbolClassifier
{
    // Only application/runtime metadata survives a request. Syntax trees, semantic
    // models and source strings belong to that request and are never cached or logged.
    private static readonly Lazy<MetadataReference[]> references = new(CreateReferences);
    internal static ScriptSymbolSpan[] Classify(string source, CancellationToken token)
    {
        if (source.Length > ScriptLimits.ScriptCharacters) return [];
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(kind: SourceCodeKind.Script), cancellationToken: token);
        var root = tree.GetRoot(token);
        var compilation = CSharpCompilation.CreateScriptCompilation("EditorSymbols", tree, references.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                usings: ["System", "System.Linq", "System.Threading", "HL7Workbench.Scripting"],
                sourceReferenceResolver: null, metadataReferenceResolver: null), globalsType: typeof(ScriptGlobals));
        var model = compilation.GetSemanticModel(tree);
        var spans = new Dictionary<int, ScriptSymbolSpan>();
        void Put(SyntaxToken syntax, ScriptSymbolKind kind)
        { if (!syntax.IsMissing && syntax.Span.Length > 0) spans[syntax.SpanStart] = new(syntax.SpanStart, syntax.Span.Length, kind); }
        foreach (var syntax in root.DescendantTokens())
        {
            token.ThrowIfCancellationRequested();
            var kind = syntax.Parent switch
            {
                PredefinedTypeSyntax => ScriptSymbolKind.Type,
                LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) ||
                    literal.IsKind(SyntaxKind.CharacterLiteralExpression) || literal.IsKind(SyntaxKind.Utf8StringLiteralExpression) => ScriptSymbolKind.String,
                InterpolatedStringTextSyntax or InterpolatedStringExpressionSyntax => ScriptSymbolKind.String,
                _ when syntax.IsKind(SyntaxKind.NumericLiteralToken) => ScriptSymbolKind.Number,
                _ when SyntaxFacts.IsKeywordKind(syntax.Kind()) => ScriptSymbolKind.Keyword,
                _ => ScriptSymbolKind.Neutral
            };
            Put(syntax, kind);
        }
        foreach (var trivia in root.DescendantTrivia())
        {
            token.ThrowIfCancellationRequested();
            if (trivia.Kind() is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia or
                SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia)
                spans[trivia.SpanStart] = new(trivia.SpanStart, trivia.Span.Length, ScriptSymbolKind.Comment);
        }
        foreach (var node in root.DescendantNodes())
        {
            token.ThrowIfCancellationRequested();
            if (node is SimpleNameSyntax name)
            {
                var symbol = model.GetSymbolInfo(name, token).Symbol;
                if (name is IdentifierNameSyntax identifier && model.GetAliasInfo(identifier, token) is { } alias) symbol = alias.Target;
                // Only resolved symbols are colored. Overload failures/unresolved names
                // stay neutral; calling a delegate variable is not a method reference.
                if (symbol is IMethodSymbol method)
                    Put(name.Identifier, method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor ? ScriptSymbolKind.Type : ScriptSymbolKind.Method);
                else if (symbol is ITypeSymbol { TypeKind: not TypeKind.Error })
                    Put(name.Identifier, name is IdentifierNameSyntax { IsVar: true } ? ScriptSymbolKind.Keyword : ScriptSymbolKind.Type);
            }
            else if (node is MethodDeclarationSyntax method && model.GetDeclaredSymbol(method, token) is not null)
                Put(method.Identifier, ScriptSymbolKind.Method);
            else if (node is LocalFunctionStatementSyntax function && model.GetDeclaredSymbol(function, token) is not null)
                Put(function.Identifier, ScriptSymbolKind.Method);
            else if (node is BaseTypeDeclarationSyntax type) Put(type.Identifier, ScriptSymbolKind.Type);
            else if (node is DelegateDeclarationSyntax declaration) Put(declaration.Identifier, ScriptSymbolKind.Type);
            else if (node is TypeParameterSyntax parameter) Put(parameter.Identifier, ScriptSymbolKind.Type);
        }
        token.ThrowIfCancellationRequested();
        return spans.Values.OrderBy(s => s.Offset).ToArray();
    }

    private static MetadataReference[] CreateReferences()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System.Private.CoreLib", "System.Runtime", "System.Console", "System.Linq", "System.Collections",
            "System.Threading", "System.Threading.Tasks", "System.Linq.Expressions", "System.Memory", "netstandard"
        };
        var paths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator)
            .Where(p => names.Contains(Path.GetFileNameWithoutExtension(p)))
            .Concat([typeof(ScriptGlobals).Assembly.Location, typeof(Core.Hl7Parser).Assembly.Location]);
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p)).ToArray();
    }
}
