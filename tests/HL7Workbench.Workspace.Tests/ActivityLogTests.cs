using System.Reflection;
using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class ActivityLogTests
{
    private const string Ordinary = "MSH|^~\\&|SYN|SYN|SYN|SYN|202601010000||ORU^R01|SYN-ID|P|2.5\rPID|1\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void ChronologicalHistoryIsBoundedAndClearDoesNotResetSessionIdentity()
    {
        var log = new ActivityLog(); log.Record(ActivityKind.ApplicationReady);
        for (var i = 0; i < 10000; i++) log.Record(ActivityKind.FileIndexed, 1, count: i);
        var entries = log.Snapshot(); Assert.Equal(2000, entries.Count);
        Assert.Equal(8002, entries[0].Sequence); Assert.Equal(10001, entries[^1].Sequence);
        Assert.Equal(8000, entries[0].Count); Assert.Equal(9999, entries[^1].Count);
        Assert.Equal(entries.OrderBy(e => e.Sequence), entries);
        Assert.All(entries, e => Assert.Equal(Hl7DiagnosticSeverity.Information, e.Severity));
        Assert.True(entries[0].Timestamp <= entries[^1].Timestamp);
        log.Clear(); Assert.Empty(log.Snapshot()); log.Record(ActivityKind.ApplicationReady);
        Assert.Equal(10002, Assert.Single(log.Snapshot()).Sequence);
        Assert.Empty(new ActivityLog().Snapshot());
    }

    [Fact]
    public void DiagnosticProjectionRejectsImportedTextAndKeepsSafeStructuredLocations()
    {
        var log = new ActivityLog();
        const string sensitive = "SYN-PATIENT-JANE-998877 C:\\Patient\\record.hl7 OBX|1|PDF|SECRET";
        foreach (var code in new[] { sensitive, "InvalidEncoding", "UnsupportedEditEncoding" })
            log.RecordDiagnostic(2, 9, new(code, Hl7DiagnosticSeverity.Warning, sensitive, 7, sensitive), ActivityCategory.Structure,
                characterOffset: (long)int.MaxValue + 10, byteOffset: 12345678900);
        log.RecordDiagnostic(2, 9, new("MissingHeaderValue", Hl7DiagnosticSeverity.Warning, sensitive, Path: "MSH-12"), ActivityCategory.Structure);
        var text = string.Join("\n", log.Snapshot());
        foreach (var secret in new[] { "SYN-PATIENT", "998877", "record.hl7", "Patient", "OBX|", "SECRET" }) Assert.DoesNotContain(secret, text);
        Assert.Equal(ActivityDiagnostic.Unknown, log.Snapshot()[0].Diagnostic);
        Assert.Equal((long)int.MaxValue + 10, log.Snapshot()[0].CharacterOffset);
        Assert.Equal(12345678900, log.Snapshot()[0].ByteOffset);
        Assert.Equal(12, log.Snapshot()[^1].HeaderField);
        // Retained storage has no strings, source objects, arrays or exception references.
        Assert.All(typeof(ActivityEntry).GetFields(BindingFlags.Instance | BindingFlags.NonPublic), f => Assert.True(f.FieldType.IsValueType));
        Assert.All(typeof(ActivityLog).GetMethods().Where(m => m.DeclaringType == typeof(ActivityLog)).SelectMany(m => m.GetParameters()), p => Assert.NotEqual(typeof(string), p.ParameterType));
        Assert.Equal(ActivityFailure.Unavailable, ActivityLog.Classify(new IOException(sensitive)));
        log.Record(ActivityKind.FileOpenFailed, 2, failure: ActivityLog.Classify(new IOException(sensitive)));
        Assert.DoesNotContain(sensitive, log.Snapshot()[^1].ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => log.Record((ActivityKind)9999));
    }

    [Fact]
    public async Task OrdinaryOpenRecordsApplicationWideOperationsWithoutInventingWarnings()
    {
        using var manager = new DocumentManager(); manager.Activity.Record(ActivityKind.ApplicationReady);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Ordinary));
        var document = await MessageFileReader.OpenFileAsync(manager, "SYN-PATIENT-998877.hl7", stream, Token);
        Assert.Equal(new[] { ActivityKind.ApplicationReady, ActivityKind.OpeningFile, ActivityKind.FileIndexed, ActivityKind.FileOpened }, manager.Activity.Snapshot().Select(e => e.Kind));
        Assert.Equal(new ActivityHealth(0, 0), ActivityHealth.For(document));
        Assert.Equal("Opened 1 file(s) read-only.", ActivityHealth.OpenStatus(1, 0, ActivityHealth.For(document)));
        Assert.DoesNotContain("998877", string.Join("\n", manager.Activity.Snapshot()));
        Assert.All(manager.Activity.Snapshot().Skip(1), e => Assert.Equal(document.ActivityNumber, e.DocumentNumber));
        manager.Close(document.Id); await document.Disposal;
        Assert.Equal(4, manager.Activity.Snapshot().Count);
        using var nextSession = new DocumentManager(); Assert.Empty(nextSession.Activity.Snapshot());
    }

    [Fact]
    public async Task RealWarningsAreDeduplicatedAcrossNavigationAndIsolatedAcrossDocuments()
    {
        using var manager = new DocumentManager();
        using var firstInput = new MemoryStream(Encoding.UTF8.GetBytes("opaque\r" + Ordinary + "MSH|^~\\&|SYN\rPID|SYN-SECRET"));
        var first = await MessageFileReader.OpenFileAsync(manager, "SYN-MRN-12345.hl7", firstInput, Token);
        Assert.Contains(manager.Activity.Snapshot(), e => e.Diagnostic == ActivityDiagnostic.UnclassifiedSource && e.ByteOffset == 0);
        await first.NavigateAsync(2, Token);
        var warnings = manager.Activity.Snapshot().Count(e => e.Kind == ActivityKind.Diagnostic);
        for (var i = 0; i < 3; i++) { await first.NavigateAsync(1, Token); await first.NavigateAsync(2, Token); }
        Assert.Equal(warnings, manager.Activity.Snapshot().Count(e => e.Kind == ActivityKind.Diagnostic));
        using var secondInput = new MemoryStream(Encoding.UTF8.GetBytes("PID|SYN-SECRET"));
        var second = await MessageFileReader.OpenFileAsync(manager, "SYN-MRN-12345.hl7", secondInput, Token);
        Assert.NotEqual(first.ActivityNumber, second.ActivityNumber);
        Assert.Contains(manager.Activity.Snapshot(), e => e.DocumentNumber == second.ActivityNumber && e.Diagnostic == ActivityDiagnostic.MissingMsh);
        var count = manager.Activity.Snapshot().Count;
        manager.Activate(first.Id); manager.Activate(second.Id); second.Inspection.SelectDeepestOffset(5);
        Assert.Equal(count, manager.Activity.Snapshot().Count);
        Assert.DoesNotContain("SYN-SECRET", string.Join("\n", manager.Activity.Snapshot()));
        Assert.Contains("warning(s)", ActivityHealth.OpenStatus(1, 0, ActivityHealth.For(first)));
        Assert.DoesNotContain("Diagnostics", ActivityHealth.OpenStatus(1, 0, ActivityHealth.For(first)));
        manager.Activity.Clear(); await first.NavigateAsync(1, Token); await first.NavigateAsync(2, Token);
        Assert.Empty(manager.Activity.Snapshot());
        manager.Close(first.Id); manager.Close(second.Id); await Task.WhenAll(first.Disposal, second.Disposal);
    }

    [Fact]
    public async Task EncodingErrorsAndStructuralLimitsRemainStructuredAndDiscoverable()
    {
        using var manager = new DocumentManager(); using var invalid = new MemoryStream([255, 129]);
        var bad = await MessageFileReader.OpenFileAsync(manager, "Synthetic", invalid, Token);
        Assert.Contains(bad.Index!.Diagnostics, d => d.Code == "InvalidEncoding");
        Assert.Contains(manager.Activity.Snapshot(), e => e.Diagnostic == ActivityDiagnostic.InvalidEncoding && e.Severity == Hl7DiagnosticSeverity.Error);
        Assert.True(ActivityHealth.For(bad).Errors > 0);
        using var limited = new MemoryStream(Encoding.UTF8.GetBytes(Ordinary + "OBX|1|TX|||A~B\r"));
        var budget = await MessageFileReader.OpenFileAsync(manager, "Synthetic", limited, Token, inspectionOptions: new() { MaxElements = 4 });
        Assert.Contains(budget.ActiveDiagnostics, d => d.Code == "InspectionIncomplete");
        Assert.Contains(manager.Activity.Snapshot(), e => e.DocumentNumber == budget.ActivityNumber && e.Diagnostic == ActivityDiagnostic.InspectionIncomplete);
        manager.Close(bad.Id); manager.Close(budget.Id); await Task.WhenAll(bad.Disposal, budget.Disposal);
    }

    [Fact]
    public async Task FailuresAndCanceledOpensAreSafeAndHaveTheCorrectSeverity()
    {
        using var manager = new DocumentManager(); using var failure = new ThrowingStream();
        await Assert.ThrowsAsync<IOException>(() => MessageFileReader.OpenFileAsync(manager, "SYN-PATIENT.hl7", failure, Token));
        var error = manager.Activity.Snapshot()[^1]; Assert.Equal(ActivityKind.FileOpenFailed, error.Kind);
        Assert.Equal(Hl7DiagnosticSeverity.Error, error.Severity); Assert.Equal(ActivityFailure.Unavailable, error.Failure);
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); using var stream = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MessageFileReader.OpenFileAsync(manager, "SYN-PATIENT.hl7", stream, canceled.Token));
        Assert.Equal(ActivityKind.FileOpenCanceled, manager.Activity.Snapshot()[^1].Kind);
        Assert.Equal(Hl7DiagnosticSeverity.Information, manager.Activity.Snapshot()[^1].Severity);
        Assert.DoesNotContain("SYN-PATIENT", string.Join("\n", manager.Activity.Snapshot()));
        Assert.Equal("Opened 0 file(s) read-only; 1 failed.", ActivityHealth.OpenStatus(0, 1, default));
    }

    [Fact]
    public async Task ExportRecordsOnlyStateAndByteCountAndSourceReadsDoNotSpamHistory()
    {
        using var manager = new DocumentManager(); using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Ordinary + "OBX|1|TX|||SYN-SECRET-PAYLOAD\r"));
        var document = await MessageFileReader.OpenFileAsync(manager, "SYN-PATIENT.hl7", stream, Token);
        var destination = Path.Combine(Path.GetTempPath(), "SYN-PATIENT-" + Guid.NewGuid() + ".hl7");
        try
        {
            using var source = new RawInspectorSession(document);
            var before = manager.Activity.Snapshot().Count;
            for (var i = 0; i < 5; i++) await source.ReadAsync(0, Token);
            Assert.Equal(before, manager.Activity.Snapshot().Count);
            await source.ExportAsync(destination, Token);
            var completed = manager.Activity.Snapshot()[^1]; Assert.Equal(ActivityKind.ExportCompleted, completed.Kind);
            Assert.Equal(new FileInfo(destination).Length, completed.Count);
            Assert.Equal(ActivityKind.ExportStarted, manager.Activity.Snapshot()[^2].Kind);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ExportAsync(destination, cancellation.Token));
            Assert.Equal(ActivityKind.ExportCanceled, manager.Activity.Snapshot()[^1].Kind);
            await Assert.ThrowsAsync<IOException>(() => source.ExportAsync(Path.Combine(document.Snapshot!.DirectoryPath, "forbidden.hl7"), Token));
            Assert.Equal(ActivityKind.ExportFailed, manager.Activity.Snapshot()[^1].Kind);
            source.ReportReadFailure(); source.ReportReadFailure();
            Assert.Single(manager.Activity.Snapshot(), e => e.Kind == ActivityKind.SourceReadFailed);
            var text = string.Join("\n", manager.Activity.Snapshot()); Assert.DoesNotContain("SYN-SECRET", text); Assert.DoesNotContain("SYN-PATIENT", text); Assert.DoesNotContain(destination, text);
        }
        finally { if (File.Exists(destination)) File.Delete(destination); manager.Close(document.Id); await document.Disposal; }
    }

    [Fact]
    public async Task ActualIndexBudgetFailureIsReportedWithoutImportingExceptionText()
    {
        using var manager = new DocumentManager();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("MSH\r", MessageFileIndex.MaxIndexedMessages + 1))));
        await Assert.ThrowsAsync<Hl7LimitException>(() => MessageFileReader.OpenFileAsync(manager, "SYN-PATIENT.hl7", stream, Token));
        Assert.Empty(manager.Documents);
        var entry = manager.Activity.Snapshot()[^1]; Assert.Equal(ActivityKind.FileOpenFailed, entry.Kind);
        Assert.Equal(ActivityFailure.ResourceLimit, entry.Failure); Assert.Equal(Hl7DiagnosticSeverity.Error, entry.Severity);
        Assert.DoesNotContain("SYN-PATIENT", entry.ToString());
    }

    [Fact]
    public void ConcurrentProducersMaintainBoundedSequenceOrder()
    {
        var log = new ActivityLog();
        Parallel.For(0, 10000, i => log.Record(ActivityKind.FileIndexed, i + 1, count: i));
        var entries = log.Snapshot(); Assert.Equal(2000, entries.Count);
        Assert.Equal(Enumerable.Range(8001, 2000).Select(i => (long)i), entries.Select(e => e.Sequence));
    }

    [Fact]
    public async Task InformationalEncodingDiagnosticsDoNotBecomeStatusWarnings()
    {
        using var manager = new DocumentManager(); var encoding = Encoding.UTF32;
        using var stream = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(Ordinary)).ToArray());
        var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", stream, Token);
        Assert.Contains(manager.Activity.Snapshot(), e => e.Diagnostic == ActivityDiagnostic.UnsupportedEditEncoding && e.Severity == Hl7DiagnosticSeverity.Information);
        Assert.Equal(new ActivityHealth(0, 0), ActivityHealth.For(document));
        Assert.Equal("Opened 1 file(s) read-only.", ActivityHealth.OpenStatus(1, 0, ActivityHealth.For(document)));
        manager.Close(document.Id); await document.Disposal;
    }

    [Fact]
    public async Task DiagnosticDeduplicationHasABoundEvenForPathologicalMessageCounts()
    {
        using var manager = new DocumentManager();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\vMSH|^~\\&|SYN\r", 8300))));
        var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", stream, Token);
        Assert.Single(manager.Activity.Snapshot(), e => e.Kind == ActivityKind.DiagnosticLimit);
        Assert.Equal(ActivityLog.Capacity, manager.Activity.Snapshot().Count);
        await document.NavigateAsync(2, Token); Assert.Single(manager.Activity.Snapshot(), e => e.Kind == ActivityKind.DiagnosticLimit);
        manager.Close(document.Id); await document.Disposal;
    }

    private sealed class ThrowingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("SYN-PATIENT patient-named file C:\\Patient\\MRN.hl7"));
    }
}
