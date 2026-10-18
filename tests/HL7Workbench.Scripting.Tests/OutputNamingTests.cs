using System.Globalization;
using System.Text;
using System.Text.Json;
using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class OutputNamingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Host => Path.Combine(AppContext.BaseDirectory, "ScriptHost", "HL7Workbench.ScriptHost.dll");
    private static byte[] Input => Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rPID|1\r");

    [Theory]
    [InlineData("Patient Deidentifier", "Patient Deidentifier.hl7")]
    [InlineData("Normalize Phone Numbers", "Normalize Phone Numbers.hl7")]
    [InlineData("Extract ADT Messages", "Extract ADT Messages.hl7")]
    [InlineData("Deidentified_Patients.HL7", "Deidentified_Patients.hl7")]
    [InlineData("version.2", "version.2.hl7")]
    [InlineData("患者 é 🧪", "患者 é 🧪.hl7")]
    public void ValidNamesNormalizeIndependentlyOfLocale(string suggestion, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(ScriptOutputFileName.TryNormalize(suggestion, out var actual)); Assert.Equal(expected, actual);
            Assert.Equal(expected, ScriptOutputFileName.FromScriptName(suggestion));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData(".")] [InlineData("..")]
    [InlineData("../escape.hl7")] [InlineData("..\\escape.hl7")] [InlineData("/absolute.hl7")]
    [InlineData("C:\\absolute.hl7")] [InlineData("C:relative.hl7")] [InlineData("\\\\server\\share.hl7")]
    [InlineData("bad:name")] [InlineData("bad<name")] [InlineData("bad>name")] [InlineData("bad\"name")]
    [InlineData("bad|name")] [InlineData("bad?name")] [InlineData("bad*name")]
    [InlineData("bad\0name")] [InlineData("bad\nname")] [InlineData("bad\tname")] [InlineData("bad\u007fname")]
    [InlineData("bad\u202ename")]
    [InlineData("trailing.")] [InlineData("trailing ")] [InlineData("trailing.hl7 ")] [InlineData(" leading")]
    [InlineData(".hl7")] [InlineData("CON")] [InlineData("con.hl7")] [InlineData("CON .hl7")]
    [InlineData("PRN.txt")] [InlineData("AUX")] [InlineData("NUL")] [InlineData("COM1")] [InlineData("com9.txt")]
    [InlineData("LPT1")] [InlineData("lpt9.hl7")] [InlineData("COM¹")] [InlineData("LPT²")] [InlineData("COM³")]
    [InlineData("CONIN$")] [InlineData("CONOUT$")]
    public void UnsafeNamesAreIgnoredWithoutAccessingTheFilesystem(string? suggestion)
    {
        Assert.False(ScriptOutputFileName.TryNormalize(suggestion, out var name)); Assert.Equal("", name);
        Assert.Equal(ScriptOutputFileName.Fallback, ScriptOutputFileName.FromScriptName(suggestion));
        var batch = new ScriptBatch(Token); batch.SetOutputFileName("Last valid"); batch.SetOutputFileName(suggestion);
        Assert.Equal("Last valid.hl7", batch.OutputNaming!.FileName); Assert.True(batch.OutputNaming.IgnoredSuggestion);
    }

    [Fact]
    public void LengthBoundIncludesExtensionAndDoesNotSplitUnicode()
    {
        var stem = new string('x', ScriptOutputFileName.MaximumCharacters - 4);
        Assert.True(ScriptOutputFileName.TryNormalize(stem, out var name)); Assert.Equal(128, name.Length);
        Assert.True(ScriptOutputFileName.TryNormalize(name, out _));
        Assert.False(ScriptOutputFileName.TryNormalize(stem + "x", out _));
        Assert.False(ScriptOutputFileName.TryNormalize(new string('x', 10000), out _));
        Assert.True(ScriptOutputFileName.TryNormalize(string.Concat(Enumerable.Repeat("🧪", 62)), out _));
        Assert.False(ScriptOutputFileName.TryNormalize(string.Concat(Enumerable.Repeat("🧪", 63)), out _));
        foreach (var unmatched in new[] { '\ud800', '\udc00' }) Assert.False(ScriptOutputFileName.TryNormalize("bad" + unmatched, out _));
        Assert.False(ScriptOutputFileName.TryNormalize("bad" + char.ConvertFromUtf32(0xE0001), out _));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WorkerTransfersOnlyFinalSuccessfulSuggestionIncludingComplete(bool complete)
    {
        var script = "Batch.SetOutputFileName(\"Initial\"); void Main() { Batch.SetOutputFileName(\"Main\"); Batch.SetOutputFileName(\"../invalid.hl7\"); }" +
            (complete ? " void Complete() { Batch.SetOutputFileName(\"Complete.HL7\"); Batch.SetOutputFileName(null); }" : "");
        await using var worker = await ScriptWorkerSession.StartAsync(Host, new(script), token: Token);
        Assert.Null(worker.OutputFileName); Assert.Equal(Input, (await worker.ProcessAsync(Input, 65001, 1, Token)).Bytes);
        Assert.Null(worker.OutputFileName); await worker.CompleteAsync(Token);
        Assert.Equal(complete ? "Complete.hl7" : "Main.hl7", worker.OutputFileName); Assert.True(worker.IgnoredOutputFileNameSuggestion);
        // Exercise the parent's independent trust boundary, not only the script facade.
        foreach (var imported in new[] { new ScriptOutputNaming("C:\\unsafe.hl7"), new(new string('x', 10000)), new("Future.hl7", Version: 99) })
        { worker.AcceptOutputNaming(imported); Assert.Null(worker.OutputFileName); Assert.True(worker.IgnoredOutputFileNameSuggestion); }
        worker.AcceptOutputNaming(null); Assert.Null(worker.OutputFileName); Assert.False(worker.IgnoredOutputFileNameSuggestion);
    }

    [Fact]
    public async Task NamingExtensionIsOptionalVersionedAndBackwardCompatibleWithV2Frames()
    {
        using var stream = new MemoryStream();
        await ScriptProtocol.WriteAsync(stream, new(FrameKind.Finished, OutputNaming: new("Synthetic.hl7")), Token);
        stream.Position = 0; var frame = await ScriptProtocol.ReadAsync(stream, Token);
        Assert.Equal(2, frame.Version); Assert.Equal(1, frame.OutputNaming!.Version); Assert.Equal("Synthetic.hl7", frame.OutputNaming.FileName);
        using var json = JsonDocument.Parse(stream.ToArray().AsMemory(4));
        var legacy = JsonSerializer.Deserialize<LegacyFinished>(json.RootElement.GetRawText())!;
        Assert.Equal(FrameKind.Finished, legacy.Kind); Assert.Equal(2, legacy.Version);
        using var missing = new MemoryStream();
        var old = JsonSerializer.SerializeToUtf8Bytes(new LegacyFinished(FrameKind.Finished, 2));
        missing.Write(BitConverter.GetBytes(old.Length)); missing.Write(old); missing.Position = 0;
        Assert.Null((await ScriptProtocol.ReadAsync(missing, Token)).OutputNaming);
    }
    private sealed record LegacyFinished(FrameKind Kind, int Version);
}
