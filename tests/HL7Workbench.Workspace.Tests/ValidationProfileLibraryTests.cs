using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class ValidationProfileLibraryTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-profile-library-" + Guid.NewGuid().ToString("N"));
    internal static Hl7ValidationProfile Profile(string id = "synthetic.hie") => new()
    {
        Id = id, Name = "Synthetic HIE profile", MessageType = "ADT", TriggerEvent = "A01", MessageStructure = "ADT_A01", BaseProfile = "hl7.v251.ADT.A01",
        TradingPartner = "Synthetic HIE", Organization = "Synthetic organization",
        Segments = [new() { Id = "PV1", Fields = [new() { Position = 2, Min = 1, DataType = "IS", AllowedValues = ["I"] }] }]
    };
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact]
    public void ImportPersistsAtomicallyAndReloadsWithMetadataAndExplicitUpdateRevision()
    {
        var library = new ValidationProfileLibrary(root); Assert.Equal(7, library.Entries.Count); Assert.False(Directory.Exists(root));
        var calls = 0; library.Changed += (_, _) => calls++;
        var entry = library.Import(Hl7ProfileImporter.ExportJson(Profile()), false, "Synthetic-HIE.json", token: Token);
        Assert.Equal(1, calls); Assert.False(entry.IsBuiltIn); Assert.Equal("Synthetic-HIE.json", entry.SourceFile); Assert.NotEqual(DateTimeOffset.MinValue, entry.ImportedAt);
        Assert.Equal(8, library.Entries.Count); Assert.Single(Directory.GetFiles(root));
        var reloaded = new ValidationProfileLibrary(root); Assert.Equal(0, reloaded.InvalidEntries); Assert.Equal(entry.Revision, reloaded.Find(entry.Profile.Id)!.Revision);
        Assert.Equal(new[] { "hl7.v251.ADT.A01", "synthetic.hie" }, reloaded.Resolve(entry.Profile.Id).Lineage);
        Assert.Throws<InvalidOperationException>(() => library.Import(Hl7ProfileImporter.ExportJson(Profile()), false, "duplicate.json", token: Token));
        var update = library.Import(Hl7ProfileImporter.ExportJson(Profile() with { Name = "Synthetic HIE revised", ProfileVersion = "2" }), false, "update.json", entry.Revision, Token);
        Assert.Equal(2, calls); Assert.NotEqual(entry.Revision, update.Revision); Assert.Single(Directory.GetFiles(root));
        Assert.Throws<InvalidOperationException>(() => library.Remove(update.Profile.Id, entry.Revision));
        library.Remove(update.Profile.Id, update.Revision); Assert.Equal(3, calls); Assert.Equal(7, library.Entries.Count); Assert.Empty(Directory.GetFiles(root));
    }

    [Theory]
    [InlineData("invalid")] [InlineData("missing-parent")] [InlineData("builtin")]
    public void InvalidImportsRollBackLibraryAndNeverReplaceBuiltIns(string reason)
    {
        var library = new ValidationProfileLibrary(root); var initial = library.Import(Hl7ProfileImporter.ExportJson(Profile()), false, "initial.json", token: Token);
        var path = Assert.Single(Directory.GetFiles(root)); var bytes = File.ReadAllBytes(path);
        var next = reason switch { "missing-parent" => Profile("new") with { BaseProfile = "missing" }, "builtin" => Profile() with { Id = "hl7.v251.ADT.A01" }, _ => Profile() };
        Assert.ThrowsAny<Exception>(() => library.Import(reason == "invalid" ? Encoding.UTF8.GetBytes("{}") : Hl7ProfileImporter.ExportJson(next), false, "bad.json", token: Token));
        Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Equal(initial.Revision, library.Find(initial.Profile.Id)!.Revision); Assert.Single(Directory.GetFiles(root));
    }

    [Fact]
    public void UpdateCannotBreakExistingDerivedProfilesAndRemovalLeavesThemExplicitlyUnresolvable()
    {
        var library = new ValidationProfileLibrary(root); var parent = library.Import(Hl7ProfileImporter.ExportJson(Profile()), false, "parent.json", token: Token);
        var child = library.Import(Hl7ProfileImporter.ExportJson(Profile("synthetic.hospital") with { BaseProfile = parent.Profile.Id }), false, "child.json", token: Token);
        var next = Profile() with { Segments = [new() { Id = "PV1", Fields = [new() { Position = 2, Min = 1, DataType = "IS", AllowedValues = ["O"] }] }] };
        Assert.ThrowsAny<ArgumentException>(() => library.Import(Hl7ProfileImporter.ExportJson(next), false, "next.json", parent.Revision, Token));
        Assert.Equal(parent.Revision, library.Find(parent.Profile.Id)!.Revision); Assert.Equal(3, library.Resolve(child.Profile.Id).Lineage.Count);
        library.Remove(parent.Profile.Id, parent.Revision); Assert.ThrowsAny<ArgumentException>(() => library.Resolve(child.Profile.Id));
        Assert.NotNull(new ValidationProfileLibrary(root).Find(child.Profile.Id));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CorruptOrOversizedLocalEntriesAreReportedAndRetained(bool oversized)
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "corrupt.json");
        File.WriteAllText(path, oversized ? new string('x', Hl7ValidationLimits.ProfileBytes * 2 + 1) : "{broken");
        var library = new ValidationProfileLibrary(root); Assert.Equal(1, library.InvalidEntries); Assert.Equal(7, library.Entries.Count); Assert.True(File.Exists(path));
    }

    [Fact]
    public void PreCancellationAndExternalChangesDoNotPublishOrLoseExistingDefinitions()
    {
        var library = new ValidationProfileLibrary(root); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => library.Import(Hl7ProfileImporter.ExportJson(Profile()), false, "new.json", token: cancellation.Token)); Assert.False(Directory.Exists(root));
        var entry = library.Import(Hl7ProfileImporter.ExportJson(Profile()), false, "new.json", token: Token); var path = Assert.Single(Directory.GetFiles(root));
        File.AppendAllText(path, " "); var changed = File.ReadAllBytes(path);
        Assert.Throws<InvalidOperationException>(() => library.Import(Hl7ProfileImporter.ExportJson(Profile()), false, "update.json", entry.Revision, Token));
        Assert.Equal(changed, File.ReadAllBytes(path)); Assert.Throws<InvalidOperationException>(() => library.Remove(entry.Profile.Id, entry.Revision));
        Assert.DoesNotContain(Directory.GetFiles(root), p => p.EndsWith(".tmp", StringComparison.Ordinal));
    }
}
