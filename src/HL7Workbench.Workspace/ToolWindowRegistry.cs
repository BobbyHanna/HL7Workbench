namespace HL7Workbench.Workspace;

public sealed record ToolDefinition(string Id, string Title, Func<object> CreateContent);

public sealed class ToolWindowRegistry
{
    private readonly Dictionary<string, ToolDefinition> definitions = [];
    private readonly Dictionary<string, object> contents = [];
    public IEnumerable<ToolDefinition> Definitions => definitions.Values;
    public void Register(ToolDefinition definition) => definitions.Add(definition.Id, definition);
    public object GetContent(string id)
    {
        if (!contents.TryGetValue(id, out var content)) contents.Add(id, content = definitions[id].CreateContent());
        return content;
    }
}
