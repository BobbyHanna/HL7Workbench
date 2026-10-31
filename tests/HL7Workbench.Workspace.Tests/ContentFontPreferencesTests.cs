using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class ContentFontPreferencesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-content-font-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(root, "preferences.json");
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    [Theory]
    [InlineData(-1, 12)] [InlineData(0, 12)] [InlineData(99, 32)] [InlineData(20, 20)] [InlineData(double.NaN, 14)] [InlineData(double.PositiveInfinity, 14)]
    public void BoundsUseDipWithoutPlatformOrDpiDependence(double input, double expected) => Assert.Equal(expected, ContentFontPreferences.Bound(input));
    [Fact]
    public void GlobalAndFeatureOverridesSurviveRestartAndResetWithoutPersistingDocumentIdentity()
    {
        var state = new ApplicationPreferences(FilePath); Assert.Null(state.Current.ContentFonts); Assert.Equal(14, new ContentFontPreferences().For("tool.documentation"));
        Assert.True(state.Apply(state.Current with { ContentFonts = new(20), RibbonExpanded = true }));
        Assert.True(state.SetContentFont("tool.documentation", 25)); Assert.False(state.SetContentFont("patient-filename", 22));
        var restarted = new ApplicationPreferences(FilePath); Assert.Equal(20, restarted.Current.ContentFonts!.For("tool.field-inspector"));
        Assert.Equal(25, restarted.Current.ContentFonts.For("tool.documentation")); Assert.True(restarted.Current.RibbonExpanded);
        Assert.True(restarted.SetContentFont("tool.documentation", null)); Assert.Equal(20, restarted.Current.ContentFonts!.For("tool.documentation"));
        Assert.DoesNotContain("patient", File.ReadAllText(FilePath)); Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Fact]
    public void InvalidStoredValuesAreBoundedAndUnknownKeysDiscarded()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(FilePath, "{\"ContentFonts\":{\"DefaultSize\":900,\"Overrides\":{\"tool.documentation\":-5,\"unknown\":22}},\"FutureOption\":7}");
        var state = new ApplicationPreferences(FilePath); Assert.Equal(32, state.Current.ContentFonts!.DefaultSize);
        Assert.Equal(12, state.Current.ContentFonts.For("tool.documentation")); Assert.Single(state.Current.ContentFonts.Overrides);
        Assert.Equal(7, state.Current.AdditionalSettings!["FutureOption"].GetInt32());
    }
    [Fact]
    public void FailedSaveDoesNotApplyFontChange()
    {
        Directory.CreateDirectory(root); var state = new ApplicationPreferences(root);
        Assert.False(state.SetContentFont("tool.documentation", 25)); Assert.Null(state.Current.ContentFonts);
    }
}
