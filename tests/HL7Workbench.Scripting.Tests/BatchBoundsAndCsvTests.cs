using System.Text;
using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class BatchBoundsAndCsvTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HeldCopiesRejectWrongOwnerPhaseAndRepeatedEmissionBeyondOutputBound(bool large)
    {
        var batch = new ScriptBatch(CancellationToken.None);
        var other = new ScriptBatch(CancellationToken.None);
        var message = new ScriptMessage(ScriptMessage.Parse(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\r" +
            (large ? string.Concat(Enumerable.Repeat("NTE|1||" + new string('X', 16000) + "\r", 60)) : "")), 65001), CancellationToken.None);
        ScriptDeferredMessage? held = null;
        batch.Initialized(() => held = batch.Hold(message), () => { for (var i = 0; i <= ScriptLimits.DeferredMessages; i++) batch.Emit(held!); });
        batch.Invoke(message, 1);
        Assert.Throws<ScriptRunException>(() => batch.Emit(held!));
        other.Initialized(() => {}, () => other.Emit(held!));
        Assert.Throws<ScriptRunException>(() => other.Finish());
        var failure = Assert.Throws<ScriptRunException>(() => batch.Finish());
        Assert.Contains(failure.Diagnostics, d => d.Code == "DeferredLimit");
        if (large) Assert.True(batch.DeferredCount < ScriptLimits.DeferredMessages);
    }
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("a,b", "a,b")]
    [InlineData("say \"yes\"", "say \"\"yes\"\"")]
    [InlineData("a\r\nb\nc\r", "a\r\nb\nc\r")]
    [InlineData("患者 é 🧪", "患者 é 🧪")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-123", "'-123")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("  =1", "'  =1")]
    [InlineData("\tSAFE", "'\tSAFE")]
    [InlineData("\rSAFE", "'\rSAFE")]
    [InlineData("\nSAFE", "'\nSAFE")]
    [InlineData("\0=1", "'\0=1")]
    [InlineData("word=1", "word=1")]
    public void CsvEscapesStringsAndProtectsFormulaPrefixes(string? input, string escaped)
    {
        var batch = new ScriptBatch(CancellationToken.None);
        var report = batch.Report("safe", "=heading");
        Assert.Equal("\"'=heading\"\r\n", Encoding.UTF8.GetString(Assert.Single(batch.DrainReports()).Bytes));
        report.Row(input);
        Assert.Equal("\"" + escaped + "\"\r\n", Encoding.UTF8.GetString(Assert.Single(batch.DrainReports()).Bytes));
    }
    [Fact]
    public void SchemasAndNamesAreBoundedAndColumnsKeepDefinitionOrder()
    {
        var batch = new ScriptBatch(CancellationToken.None);
        var report = batch.Report("ordered", "B", "A");
        report.Row("second", "first");
        Assert.Equal(new[] { "\"B\",\"A\"\r\n", "\"second\",\"first\"\r\n" }, batch.DrainReports().Select(c => Encoding.UTF8.GetString(c.Bytes)));
        Assert.Throws<ScriptRunException>(() => report.Row("only one"));
        Assert.Throws<ScriptRunException>(() => batch.Report("ORDERED", "A"));
        Assert.Throws<ScriptRunException>(() => batch.Report("../unsafe", "A"));
        Assert.Throws<ScriptRunException>(() => batch.Report("bad", new string[ScriptLimits.ReportColumns + 1]));
        for (var i = 1; i < ScriptLimits.Reports; i++) batch.Report("r" + i, "A");
        Assert.Throws<ScriptRunException>(() => batch.Report("overflow", "A"));
    }
    [Fact]
    public void ReportOperationCellAndGlobalByteLimitsAreEnforced()
    {
        var batch = new ScriptBatch(CancellationToken.None); var report = batch.Report("bounded", "A"); batch.DrainReports();
        Assert.Throws<ScriptRunException>(() => report.Row(new string('x', ScriptLimits.ReportCellCharacters + 1)));
        var row = new string('x', ScriptLimits.ReportCellCharacters);
        for (var i = 0; i < 15; i++) report.Row(row);
        Assert.Throws<ScriptRunException>(() => report.Row(row)); batch.DrainReports();
        ScriptRunException? error = null;
        for (var i = 0; i < 1100; i++)
        {
            try { report.Row(row); batch.DrainReports(); }
            catch (ScriptRunException failure) { error = failure; break; }
        }
        Assert.NotNull(error); Assert.Contains(error.Diagnostics, d => d.Code == "ReportLimit");
    }
    [Fact]
    public void GlobalRowLimitAndBatchMessageLimitAreEnforcedWithoutRetainingInputs()
    {
        var batch = new ScriptBatch(CancellationToken.None); var report = batch.Report("rows", "A"); batch.Initialized(() => {}); batch.DrainReports();
        var message = new ScriptMessage(ScriptMessage.Parse(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\r"), 65001), CancellationToken.None);
        for (var i = 0; i < ScriptLimits.BatchMessages; i++) { batch.Invoke(message, i + 1); report.Row(""); batch.DrainReports(); }
        Assert.Throws<ScriptRunException>(() => batch.Invoke(message, ScriptLimits.BatchMessages + 1));
        Assert.Throws<ScriptRunException>(() => report.Row(""));
        Assert.Throws<ScriptRunException>(() => batch.Report("late-schema", "A"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DeferredCountAndByteLimitsAreEnforced(bool large)
    {
        var batch = new ScriptBatch(CancellationToken.None);
        var message = new ScriptMessage(ScriptMessage.Parse(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\r" +
            (large ? string.Concat(Enumerable.Repeat("NTE|1||" + new string('X', 16000) + "\r", 60)) : "")), 65001), CancellationToken.None);
        batch.Initialized(() => batch.Defer(message));
        Assert.Throws<ScriptRunException>(() => batch.Defer(message));
        var accepted = 0;
        while (true)
        {
            try { batch.Invoke(message, accepted + 1); accepted++; }
            catch (ScriptRunException error) { Assert.Contains(error.Diagnostics, d => d.Code == "DeferredLimit"); break; }
        }
        Assert.InRange(accepted, 1, ScriptLimits.DeferredMessages);
        if (large) Assert.True(accepted < ScriptLimits.DeferredMessages); else Assert.Equal(ScriptLimits.DeferredMessages, accepted);
    }
}
