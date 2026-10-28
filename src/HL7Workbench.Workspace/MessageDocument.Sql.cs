namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    // The initial byte array never mutates. SQL copies it under the same capture lease as
    // logical order/drafts; recreating it from RawText would lose BOM or original spelling.
    internal byte[]? SqlMemoryOriginalBytes => memoryOriginalBytes;
}
