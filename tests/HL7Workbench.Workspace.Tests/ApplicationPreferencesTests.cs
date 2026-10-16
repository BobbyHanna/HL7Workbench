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
    [Theory]
    [InlineData(0.1)] [InlineData(0.6)] [InlineData(0.9)]
    public void SearchSplitIsVersionedPlacementOnlyAndPreservesExistingPreferences(double fraction)
    {
        var state = new ApplicationPreferences(PathFor()); Assert.Null(state.Current.SearchLayout);
        Assert.True(state.Apply(state.Current with { Theme = ApplicationThemePreference.Light, RibbonExpanded = true,
            SearchLayout = new(AdvancedBuilderFraction: fraction) }));
        var loaded = EditorPreferences.Load(PathFor()); Assert.Equal(fraction, loaded.SearchLayout!.AdvancedBuilderFraction);
        Assert.Equal(1, loaded.SearchLayout.Version); Assert.True(loaded.RibbonExpanded); Assert.Equal(ApplicationThemePreference.Light, loaded.Theme);
        var json = File.ReadAllText(PathFor()); Assert.DoesNotContain("Value", json); Assert.DoesNotContain("Results", json); Assert.DoesNotContain("Document", json);
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)] [InlineData(5)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidSearchSplitValuesFallBackWithoutAffectingOtherSettings(double fraction)
    {
        Assert.Equal(0.6, new SearchPaneLayout(AdvancedBuilderFraction: fraction).Validated().AdvancedBuilderFraction);
        Assert.Equal(0.6, new SearchPaneLayout(99, 0.7).Validated().AdvancedBuilderFraction);
        File.WriteAllText(PathFor(), "{\"RibbonExpanded\":true,\"SearchLayout\":{\"Version\":99,\"AdvancedBuilderFraction\":0.8}}");
        var loaded = EditorPreferences.Load(PathFor()); Assert.True(loaded.RibbonExpanded); Assert.Equal(0.6, loaded.SearchLayout!.AdvancedBuilderFraction);
        File.WriteAllText(PathFor(), "{\"StartInEditMode\":true,\"SearchLayout\":{\"Version\":1,\"AdvancedBuilderFraction\":-1}}");
        loaded = EditorPreferences.Load(PathFor()); Assert.True(loaded.StartInEditMode); Assert.Equal(0.6, loaded.SearchLayout!.AdvancedBuilderFraction);
        File.WriteAllText(PathFor(), "{\"SearchLayout\":{\"AdvancedBuilderFraction\":\"broken\"}}");
        var corrupt = new ApplicationPreferences(PathFor()); Assert.Null(corrupt.Current.SearchLayout); Assert.Contains("defaults", corrupt.Status);
    }
    [Theory]
    [InlineData(ApplicationThemePreference.FollowSystem)] [InlineData(ApplicationThemePreference.Light)] [InlineData(ApplicationThemePreference.Dark)]
    public void ThemeDefaultsLegacyCompatibilityAndExplicitPersistence(ApplicationThemePreference theme)
    {
        Assert.Equal(ApplicationThemePreference.FollowSystem, new EditorPreferences().Theme);
        File.WriteAllText(PathFor(), "{\"StartInEditMode\":true,\"FutureOption\":17}");
        var state = new ApplicationPreferences(PathFor()); Assert.Equal(ApplicationThemePreference.FollowSystem, state.Current.Theme);
        Assert.True(state.Apply(state.Current with { Theme = theme })); var restarted = new ApplicationPreferences(PathFor());
        Assert.Equal(theme, restarted.Current.Theme); Assert.True(restarted.Current.StartInEditMode); Assert.Equal(17, restarted.Current.AdditionalSettings!["FutureOption"].GetInt32());
        File.WriteAllText(PathFor(), "{\"Theme\":99,\"RibbonExpanded\":true}");
        restarted = new(PathFor()); Assert.Equal(ApplicationThemePreference.FollowSystem, restarted.Current.Theme); Assert.True(restarted.Current.RibbonExpanded);
    }

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
