using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class ScriptEditorPreferencesTests
{
    [Fact]
    public void FontRoundTripsIndependentlyOfEditModeAndLayoutWithoutTemporaryFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "hl7-font-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "script-editor-preferences.json");
        try
        {
            Assert.Equal(12, ScriptEditorPreferences.Load(path).FontSizePoints);
            new EditorPreferences(true).Save(Path.Combine(root, "editor-preferences.json"));
            new ScriptEditorPreferences(24).Save(path);
            Assert.Equal(24, ScriptEditorPreferences.Load(path).FontSizePoints);
            Assert.Contains("\"Version\":2", File.ReadAllText(path));
            Assert.DoesNotContain("\"FontSize\":", File.ReadAllText(path));
            Assert.True(EditorPreferences.Load(Path.Combine(root, "editor-preferences.json")).StartInEditMode);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
            File.WriteAllText(path, "{\"Version\":99,\"FontSize\":24}");
            Assert.Throws<InvalidDataException>(() => ScriptEditorPreferences.Load(path));
            File.WriteAllText(path, new string(' ', 4097));
            Assert.Throws<InvalidDataException>(() => ScriptEditorPreferences.Load(path));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(-10, 9)] [InlineData(1000, 24)] [InlineData(21, 21)]
    public void OutOfRangePersistedSizesAreBounded(int input, int expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "hl7-font-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root); var path = Path.Combine(root, "font.json");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { FontSizePoints = input, Version = 2 }));
            Assert.Equal(expected, ScriptEditorPreferences.Load(path).FontSizePoints);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(18.666666666666668, 14)] [InlineData(26.66666666666666, 20)]
    [InlineData(12, 9)] [InlineData(32, 24)] [InlineData(21, 16)]
    public void VersionOneSavedSizesMigrateWithoutChangingTheFileOrResettingToNewDefault(double dips, int points)
    {
        var root = Path.Combine(Path.GetTempPath(), "hl7-font-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root); var path = Path.Combine(root, "font.json");
            var original = System.Text.Json.JsonSerializer.Serialize(new { FontSize = dips, Version = 1 });
            File.WriteAllText(path, original);
            Assert.Equal(points, ScriptEditorPreferences.Load(path).FontSizePoints);
            Assert.Equal(original, File.ReadAllText(path));
            new ScriptEditorPreferences(points).Save(path);
            Assert.Equal(points, ScriptEditorPreferences.Load(path).FontSizePoints);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
