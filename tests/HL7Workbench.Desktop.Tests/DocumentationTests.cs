using System.Text;
using System.Text.Json.Nodes;
using HL7Workbench.Desktop.Documentation;
using HL7Workbench.Desktop.ViewModels;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class DocumentationTests
{
    [Fact]
    public void EmbeddedCatalogContainsOnlyCompleteCuratedUserAndSharedTopics()
    {
        var catalog = DocumentationCatalog.Current;
        Assert.Equal(20, catalog.Topics.Count);
        Assert.Equal(18, catalog.Topics.Count(t => t.Source.StartsWith("docs/user/", StringComparison.Ordinal)));
        Assert.Equal(2, catalog.Topics.Count(t => t.Source.StartsWith("docs/reference/", StringComparison.Ordinal)));
        Assert.Equal(20, catalog.Topics.Select(t => t.Id).Distinct().Count());
        Assert.All(catalog.Topics, topic =>
        {
            Assert.Equal(64, topic.SourceHash.Length); Assert.NotEmpty(topic.Blocks);
            Assert.Equal(topic.Title, topic.Blocks[0].Text);
            Assert.DoesNotContain("developer", topic.Source); Assert.DoesNotContain("private-preview", topic.Source);
            foreach (var link in topic.Blocks.SelectMany(b => b.Spans.Concat(b.Rows.SelectMany(r => r).SelectMany(c => c))).Select(s => s.Link).OfType<HelpLink>())
                if (link.Kind == "topic") Assert.True(catalog.HasAnchor(link.Target, link.Anchor));
        });
        Assert.Contains(catalog.Find("privacy-and-storage")!.Blocks, b => b.Kind == "table");
        Assert.Contains(catalog.Find("query-with-sql")!.Blocks, b => b.Kind == "code" && b.Text.Contains("SELECT DocumentId", StringComparison.Ordinal));
        Assert.Contains(catalog.Find("run-and-manage-scripts")!.Blocks, b => b.Spans.Any(s => s.Kind == "strong" && s.Text.Contains("not an operating-system", StringComparison.Ordinal)));
    }
    [Fact]
    public void LoadedTopicBlockAndSpanCollectionsAreReadOnly()
    {
        var catalog = DocumentationCatalog.Current;
        Assert.Throws<NotSupportedException>(() => ((IList<HelpTopic>)catalog.Topics).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HelpBlock>)catalog.Topics[0].Blocks).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HelpSpan>)catalog.Topics[0].Blocks[0].Spans).Clear());
    }
    [Theory]
    [InlineData("CRYPTOGRAPHICALLY seed", "generate-synthetic-hl7")]
    [InlineData("SQLite", "query-with-sql")]
    [InlineData("privacy", "privacy-and-storage")]
    [InlineData("working draft", "inspect-and-edit")]
    public void SearchMatchesApprovedTextTitlesAndKeywordsWithoutCaseSensitivity(string query, string id)
    {
        var results = DocumentationCatalog.Current.Search(query);
        Assert.Contains(results, r => r.Topic.Id == id);
        Assert.All(results, r => { Assert.True(r.Snippet.Length <= DocumentationCatalog.SnippetCharacters + 1); Assert.True(DocumentationCatalog.Current.HasAnchor(r.Topic.Id, r.Anchor)); });
    }
    [Fact]
    public void SearchAndHistoryBoundsAndNoResultsAreExplicit()
    {
        using var model = new DocumentationViewModel();
        model.Query = new string('x', 400); Assert.Equal(256, model.Query.Length); Assert.Empty(model.Results); Assert.Contains("No documentation", model.Feedback);
        model.Query = string.Join(' ', Enumerable.Repeat("seed", 17)); Assert.Empty(model.Results); Assert.Contains("16 search terms", model.Feedback);
        model.Query = ""; Assert.Equal(20, model.Results.Count);
        for (var i = 0; i < 100; i++) model.Navigate(i % 2 == 0 ? "limits" : "workspace");
        var count = 0; while (model.CanBack) { model.Back(); count++; } Assert.Equal(64, count);
    }
    [Fact]
    public void TopicAnchorAndBackRetainOnlySessionNavigationAndRejectUnknownTargets()
    {
        using var model = new DocumentationViewModel();
        model.Query = "seed"; model.Navigate("generate-synthetic-hl7", "replay-seeds-and-choose-a-style");
        model.Navigate("limits"); model.Back();
        Assert.Equal("seed", model.Query); Assert.Equal("generate-synthetic-hl7", model.Topic.Id); Assert.Equal("replay-seeds-and-choose-a-style", model.Anchor);
        Assert.False(model.Navigate("missing")); Assert.False(model.Navigate("limits", "missing"));
        model.Contents(); Assert.Equal("contents", model.Topic.Id); Assert.Empty(model.Query);
    }
    [Fact]
    public void ApiLinksUseExistingReferenceAndOtherReferencesHaveNoExternalAction()
    {
        using var model = new DocumentationViewModel(); var requests = 0; model.ScriptApiRequested += () => requests++;
        var links = DocumentationCatalog.Current.Topics.SelectMany(t => t.Blocks).SelectMany(b => b.Spans.Concat(b.Rows.SelectMany(r => r).SelectMany(c => c)))
            .Select(s => s.Link).OfType<HelpLink>().ToArray();
        model.Follow(links.First(l => l.Kind == "api")); Assert.Equal(1, requests);
        model.Follow(links.First(l => l.Kind == "repository")); Assert.Contains("not bundled", model.Feedback); Assert.Equal(1, requests);
        model.Dispose(); model.Follow(new("api", "", "")); Assert.Equal(1, requests);
    }
    [Theory]
    [InlineData("version")] [InlineData("duplicate")] [InlineData("anchor")] [InlineData("kind")] [InlineData("null")]
    public void DamagedEmbeddedMetadataIsRejectedBeforeRendering(string fault)
    {
        using var stream = typeof(DocumentationCatalog).Assembly.GetManifestResourceStream("HL7Workbench.Documentation.json")!;
        var root = JsonNode.Parse(stream)!.AsObject(); var topics = root["topics"]!.AsArray();
        switch (fault)
        {
            case "version": root["version"] = 99; break;
            case "duplicate": topics[1]!["id"] = topics[0]!["id"]!.GetValue<string>(); break;
            case "anchor": topics[0]!["blocks"]![0]!["anchor"] = ""; break;
            case "kind": topics[0]!["blocks"]![0]!["kind"] = "script"; break;
            case "null": topics[0]!["blocks"] = null; break;
        }
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => DocumentationCatalog.Load(input));
    }
}
