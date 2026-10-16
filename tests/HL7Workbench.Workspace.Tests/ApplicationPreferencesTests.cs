using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class ApplicationPreferencesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-preferences-synthetic-" + Guid.NewGuid().ToString("N"));
    private string PathFor(string name = "preferences.json") => Path.Combine(root, name);
    public ApplicationPreferencesTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void DefaultsLegacySettingsAndUnknownFieldsRoundTripIndependentlyOfLibraryAndLayout()
    {
        var state = new ApplicationPreferences(PathFor());
        Assert.False(state.Current.StartInEditMode); Assert.False(state.Current.RibbonExpanded);
        Assert.Equal(SchemaInterpretationMode.MessageDeclared, state.Current.SchemaMode); Assert.Null(state.Current.TargetSchema);
        Assert.False(File.Exists(PathFor()));
        File.WriteAllText(PathFor(), "{\"StartInEditMode\":true,\"FutureOption\":{\"Enabled\":true}}");
        state = new(PathFor()); Assert.True(state.Current.StartInEditMode); Assert.False(state.Current.RibbonExpanded);
        Assert.True(state.Apply(state.Current with { RibbonExpanded = true }));
        var restarted = new ApplicationPreferences(PathFor());
        Assert.True(restarted.Current.StartInEditMode); Assert.True(restarted.Current.RibbonExpanded);
        Assert.True(restarted.Current.AdditionalSettings!["FutureOption"].GetProperty("Enabled").GetBoolean());
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Theory]
    [InlineData("{broken")]
    [InlineData("[1,2]")]
    [InlineData("{\"StartInEditMode\":\"bad\"}")]
    public void CorruptSettingsUseSafeDefaultsWithoutRewritingUntilExplicitApply(string text)
    {
        File.WriteAllText(PathFor(), text); var state = new ApplicationPreferences(PathFor());
        Assert.Equal(new EditorPreferences(), state.Current); Assert.Contains("defaults", state.Status);
        Assert.Equal(text, File.ReadAllText(PathFor()));
        Assert.True(state.Apply(state.Current with { RibbonExpanded = true }));
        Assert.True(new ApplicationPreferences(PathFor()).Current.RibbonExpanded);
    }
    [Fact]
    public void OversizedSettingsAndFailedSavePreserveActiveStateAndCleanTemporaryFiles()
    {
        File.WriteAllText(PathFor(), new string(' ', 4097));
        var oversized = new ApplicationPreferences(PathFor()); Assert.False(oversized.Current.RibbonExpanded); Assert.Contains("defaults", oversized.Status);
        var blocked = new ApplicationPreferences(root);
        Assert.False(blocked.Apply(new(true, true))); Assert.False(blocked.Current.StartInEditMode); Assert.False(blocked.Current.RibbonExpanded);
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Fact]
    public void ReservedSchemaChoicesNeverRewriteDeclaredVersionsOrBytesAndEditDefaultOnlyAffectsNewDocuments()
    {
        const string first = "MSH|^~\\&|SYN|SYN|SYN|SYN|20200101||ADT^A01|SYN-1|P|2.5\rPID|1||SYN-001\r";
        var state = new ApplicationPreferences(PathFor()); using var docs = new DocumentManager();
        var existing = new MessageDocument("one", "Synthetic", Hl7Parser.Parse(first)); docs.Open(existing);
        Assert.True(state.Apply(new(true, true, SchemaInterpretationMode.SelectedTarget, "custom-target-pending")));
        docs.Preferences = state.Current;
        var other = new MessageDocument("two", "Synthetic", Hl7Parser.Parse(first.Replace("2.5", "2.3", StringComparison.Ordinal))); docs.Open(other);
        Assert.False(existing.IsEditMode); Assert.True(other.IsEditMode);
        Assert.Equal("2.5", existing.DeclaredVersion); Assert.Equal("2.3", other.DeclaredVersion);
        Assert.Equal(Encoding.UTF8.GetBytes(first), Hl7Serializer.Serialize(existing.Message)); Assert.False(existing.IsDirty); Assert.False(other.IsDirty);
        Assert.Equal("custom-target-pending", new ApplicationPreferences(PathFor()).Current.TargetSchema);
        File.WriteAllText(PathFor(), "{\"SchemaMode\":99,\"RibbonExpanded\":true}");
        var newer = new ApplicationPreferences(PathFor()); Assert.True(newer.Current.RibbonExpanded); Assert.Equal(SchemaInterpretationMode.MessageDeclared, newer.Current.SchemaMode);
    }
}
