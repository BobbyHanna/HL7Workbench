namespace HL7Workbench.Workspace;

/// <param name="RestoreVisibility">Restore visible optional content only when saved as visible; hidden content remains uninitialized.</param>
public sealed record ToolDefinition(string Id, string Title, Func<object> CreateContent, bool Optional = false, bool ReleaseOnClose = false,
    bool RestoreVisibility = false);

public sealed class ToolWindowRegistry
{
    private readonly Dictionary<string, ToolDefinition> definitions = [];
    private readonly Dictionary<string, object> contents = [];
    public IEnumerable<ToolDefinition> Definitions => definitions.Values;
    public ToolDefinition Definition(string id) => definitions[id];
    public bool IsCreated(string id) => contents.ContainsKey(id);
    public void Release(string id)
    { if (contents.Remove(id, out var content) && content is IDisposable owned) owned.Dispose(); }
    public void Register(ToolDefinition definition) => definitions.Add(definition.Id, definition);
    public object GetContent(string id)
    {
        if (!contents.TryGetValue(id, out var content)) contents.Add(id, content = definitions[id].CreateContent());
        return content;
    }
}
