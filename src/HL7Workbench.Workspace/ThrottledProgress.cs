namespace HL7Workbench.Workspace;

/// <summary>Bounds dispatcher updates during large streaming operations.</summary>
public sealed class ThrottledProgress(IProgress<long> target) : IProgress<long>
{
    private long last;
    public void Report(long value)
    {
        var now = Environment.TickCount64;
        var previous = Interlocked.Read(ref last);
        if (now - previous >= 100 && Interlocked.CompareExchange(ref last, now, previous) == previous) target.Report(value);
    }
}
