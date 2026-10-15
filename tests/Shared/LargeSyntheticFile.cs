using System.Text;

namespace HL7Workbench.Tests;

public static class LargeSyntheticFile
{
    public static string ArtifactDirectory
    {
        get
        {
            var path = new DirectoryInfo(AppContext.BaseDirectory);
            while (path is not null && !File.Exists(Path.Combine(path.FullName, "HL7Workbench.sln"))) path = path.Parent;
            var root = Path.Combine(path?.FullName ?? AppContext.BaseDirectory, "artifacts", "stage3");
            Directory.CreateDirectory(root);
            return root;
        }
    }
    public static async Task<string> CreateAsync(long encodedCharacters, CancellationToken token)
    {
        if (encodedCharacters < 8 || encodedCharacters % 4 != 0) throw new ArgumentOutOfRangeException(nameof(encodedCharacters));
        var path = Path.Combine(ArtifactDirectory, $"synthetic-{encodedCharacters / (1024 * 1024)}MiB-{encodedCharacters}.hl7");
        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);
        await output.WriteAsync(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN|TEST|VIEWER|TEST|20261014120000||ORU^R01|SYN-LARGE|T|2.5\rPID|1||SYN-ONLY\rOBR|1|||SYN\rOBX|1|ED|SYN||^application^pdf^Base64^"), token);
        var block = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("QUJD", 16384)));
        for (long written = 0; written < encodedCharacters - 8;)
        {
            var count = (int)Math.Min(block.Length, encodedCharacters - 8 - written);
            await output.WriteAsync(block.AsMemory(0, count), token); written += count;
        }
        await output.WriteAsync(Encoding.UTF8.GetBytes("VEFJTA==|AFTER-FIELD\rNTE|1||AFTER-SEGMENT\rMSH|^~\\&|NEXT|TEST|||||ACK|SYN-NEXT|T|2.5\rMSA|AA|SYN-LARGE\r"), token);
        return path;
    }
}
