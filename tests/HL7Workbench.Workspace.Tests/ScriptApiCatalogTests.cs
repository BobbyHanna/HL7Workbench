using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class ScriptApiCatalogTests
{
    [Fact]
    public void RuntimeContractsClassifyEveryExposedTypeAndEveryDeclaredOverloadWithoutHostInternals()
    {
        var reference = ScriptApiReference.Current; var assembly = typeof(ScriptGlobals).Assembly;
        Assert.Equal(assembly.GetExportedTypes().Select(t => t.Name).Order(), reference.SupportedTypes.Concat(reference.InfrastructureTypes.Keys).Order());
        var actual = new List<string>();
        foreach (var name in reference.SupportedTypes)
        {
            var type = assembly.GetType("HL7Workbench.Scripting." + name)!;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            actual.AddRange(type.GetMethods(flags).Where(m => !m.IsSpecialName && !m.IsDefined(typeof(CompilerGeneratedAttribute))).Select(m => name + "." + m.Name));
            actual.AddRange(type.GetConstructors(flags).Select(_ => name + "..ctor"));
            actual.AddRange(type.GetProperties(flags).Select(p => name + "." + p.Name));
            actual.AddRange(type.GetFields(flags).Where(f => f.Name != "value__").Select(f => name + "." + f.Name));
        }
        Assert.Equal(actual.Order(), reference.Contracts.Select(c => c.Member).Order());
        Assert.Equal(reference.Contracts.Count, reference.Contracts.DistinctBy(c => (c.Member, c.Signature)).Count());
        Assert.DoesNotContain(reference.Contracts, c => c.Member.Contains("Serialize", StringComparison.Ordinal) || c.Member.StartsWith("ScriptWorker", StringComparison.Ordinal));
        Assert.DoesNotContain(reference.Contracts, c => c.Member == "ScriptGlobals..ctor" || c.Member == "ScriptMessage..ctor");
        Assert.All(reference.Contracts, c => Assert.Single(reference.ForMember(c.Member)));
    }

    [Theory]
    [InlineData("ScriptMessage.SetText", "void ScriptMessage.SetText(string path, string value)")]
    [InlineData("ScriptOutputFileName.TryNormalize", "static bool ScriptOutputFileName.TryNormalize(string? suggestion, out string fileName)")]
    [InlineData("SyntheticIdentityLibrary..ctor", "SyntheticIdentityLibrary(uint seed, int maximumIdentities = 100000)")]
    [InlineData("SyntheticIdentityLibrary.Get", "SyntheticIdentity SyntheticIdentityLibrary.Get(string identityKey, CancellationToken cancellation = default)")]
    [InlineData("DeidentificationOptions.ShiftDays", "int? DeidentificationOptions.ShiftDays { get; init; }")]
    [InlineData("ScriptCsvReport.Row", "void ScriptCsvReport.Row(params string?[] values)")]
    [InlineData("ScriptGlobals.Message", "ScriptMessage ScriptGlobals.Message { get; }")]
    public void ExactSignaturesRetainParameterNamesNullabilityDefaultValuesModifiersAndAccessors(string member, string signature)
    { Assert.Equal(signature, Assert.Single(ScriptApiReference.Current.Contracts, c => c.Member == member).Signature); }

    [Fact]
    public void BehaviorRelatedTopicsAndParameterSearchAreCompleteAndExportAsMachineReadableCatalog()
    {
        var reference = ScriptApiReference.Current;
        // Also useful when a signature snapshot is stale: expose current metadata
        // as an ignored verification artifact so maintainers can review the diff.
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "script-api-catalog.json"), reference.ExportJson());
        foreach (var entry in reference.Entries)
        {
            Assert.All(new[] { entry.Description, entry.Parameters, entry.Returns, entry.Errors, entry.Mutation, entry.NullBehavior, entry.Modes }, Assert.NotEmpty);
            Assert.All(entry.Related, id => Assert.Contains(reference.Entries, e => e.Id == id));
            if (!entry.IsInfrastructure) Assert.Equal(entry.Members.Order(), entry.Contracts.Select(c => c.Member).Order());
            if (entry.Members.Count > 0 || entry.IsInfrastructure) Assert.Equal(entry.ExactSignature, entry.Signature);
            if (!entry.IsInfrastructure) Assert.Contains("independent unsaved output", entry.Modes);
        }
        Assert.Contains(reference.Search("maximumIdentities"), e => e.Id == "identity-library");
        Assert.Contains(reference.Search("MissingValue"), e => e.Id == "text");
        Assert.Contains(reference.Search("suggestion out"), e => e.Id == "filename");
        var exported = reference.ExportJson(); using var json = JsonDocument.Parse(exported);
        Assert.Equal(reference.Contracts.Count, json.RootElement.GetProperty("Contracts").GetArrayLength());
        Assert.Equal(1, json.RootElement.GetProperty("ContractVersion").GetInt32());
        Assert.Contains("ScriptMessage.SetText(string path, string value)", exported);
        // A reusable, synthetic-only verification artifact beside test outputs.
        // Copy API catalog JSON in the application produces this same document.
        Assert.NotEmpty(reference.InfrastructureContracts);
        Assert.All(reference.InfrastructureContracts, c => Assert.Contains(c.DeclaringType, reference.InfrastructureTypes.Keys));
    }
}
