using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class ScriptReferenceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static JsonDocument Reference()
    {
        using var stream = typeof(ScriptReferenceTests).Assembly.GetManifestResourceStream("HL7Workbench.ScriptApiReference.json")!;
        return JsonDocument.Parse(stream);
    }
    [Fact]
    public void EverySupportedDeclaredPublicMemberIsDocumentedAndHostTypesAreExplicitlyClassified()
    {
        using var reference = Reference(); var root = reference.RootElement;
        Assert.Equal(2, root.GetProperty("ApiVersion").GetInt32()); Assert.Equal(1, root.GetProperty("Version").GetInt32());
        var supported = root.GetProperty("SupportedTypes").EnumerateArray().Select(t => t.GetString()!).ToArray();
        var infrastructure = root.GetProperty("InfrastructureTypes").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
        var assembly = typeof(ScriptGlobals).Assembly;
        Assert.Equal(assembly.GetExportedTypes().Select(t => t.Name).Order(), supported.Concat(infrastructure.Keys).Order());
        Assert.All(infrastructure.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
        var actual = new List<string>();
        foreach (var name in supported)
        {
            var type = assembly.GetType("HL7Workbench.Scripting." + name)!;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            actual.AddRange(type.GetProperties(flags).Select(p => name + "." + p.Name));
            actual.AddRange(type.GetFields(flags).Where(f => f.Name != "value__").Select(f => name + "." + f.Name));
            actual.AddRange(type.GetMethods(flags).Where(m => !m.IsSpecialName && !m.IsDefined(typeof(CompilerGeneratedAttribute)))
                .Select(m => name + "." + m.Name));
            actual.AddRange(type.GetConstructors(flags).Select(_ => name + "..ctor"));
        }
        // Do not collapse overloads: adding one requires documenting its signature too.
        var documented = root.GetProperty("Entries").EnumerateArray()
            .SelectMany(e => e.GetProperty("Members").EnumerateArray().Select(m => m.GetString()!)).ToArray();
        Assert.Equal(actual.Order(), documented.Order());
        foreach (var entry in root.GetProperty("Entries").EnumerateArray())
        {
            foreach (var field in new[] { "Id", "Title", "Category", "Signature", "Description", "Parameters", "Returns", "Example", "Limitations" })
                Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty(field).GetString()), entry.GetProperty("Id").GetString() + ": " + field);
            foreach (var member in entry.GetProperty("Members").EnumerateArray().Select(m => m.GetString()!))
            {
                var leaf = member.EndsWith("..ctor", StringComparison.Ordinal) ? member[..^6] : member[(member.LastIndexOf('.') + 1)..];
                Assert.Contains(leaf, entry.GetProperty("Signature").GetString()!);
            }
        }
    }
    public static IEnumerable<object[]> DistinctExamples()
    {
        using var reference = Reference();
        return reference.RootElement.GetProperty("Entries").EnumerateArray()
            .DistinctBy(e => e.GetProperty("Example").GetString())
            .Select(e => new object[] { e.GetProperty("Id").GetString()!, e.GetProperty("Example").GetString()! }).ToArray();
    }
    [Theory]
    [MemberData(nameof(DistinctExamples))]
    public async Task EveryReferenceExampleCompilesAndExecutesThroughRealWorker(string topic, string source)
    {
        Assert.NotEmpty(topic);
        await using var worker = await ScriptWorkerSession.StartAsync(Path.Combine(AppContext.BaseDirectory, "ScriptHost", "HL7Workbench.ScriptHost.dll"), new(source), token: Token);
        var reports = new List<ScriptReportChunk>(worker.DrainReports());
        var input = Encoding.UTF8.GetBytes(SyntheticMessageGenerator.Generate(new() { Count = 1 }, Token).Messages[0]);
        for (var n = 1; n <= 2; n++)
        {
            var result = await worker.ProcessAsync(input, 65001, n, Token);
            Assert.Equal(result.Excluded, result.Bytes is null);
            if (result.Bytes is { } bytes) Assert.True(Hl7Parser.Parse(bytes).CanEdit);
            reports.AddRange(worker.DrainReports());
        }
        await worker.CompleteAsync(Token);
        while (await worker.TakeDeferredAsync(Token) is { } copy) Assert.True(Hl7Parser.Parse(copy.Bytes).CanEdit);
        reports.AddRange(worker.DrainReports());
        Assert.All(reports, report => Assert.InRange(report.Bytes.Length, 1, ScriptLimits.ReportOperationBytes));
    }
}
