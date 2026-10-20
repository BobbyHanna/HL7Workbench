using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Platform;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class BrandingTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly int[] Sizes = [16, 24, 32, 48, 64, 128, 256];

    [Fact]
    public void ApprovedSourceAndSevenPngBackedIconFramesArePreserved()
    {
        var source = File.ReadAllBytes(Path.Combine(Root, "assets/branding/workbench-icon-source.png"));
        Assert.Equal("A47EFCAD4AC2C7E19CB296A280DFA02B2AABFB11DFE07DCFF9EB4951B3651095", Convert.ToHexString(SHA256.HashData(source)));
        var icon = File.ReadAllBytes(Path.Combine(Root, "src/HL7Workbench.Desktop/Assets/workbench.ico"));
        Assert.Equal(0, U16(icon, 0)); Assert.Equal(1, U16(icon, 2)); Assert.Equal(Sizes.Length, U16(icon, 4));
        for (var i = 0; i < Sizes.Length; i++)
        {
            var entry = 6 + i * 16;
            Assert.Equal(Sizes[i] == 256 ? 0 : Sizes[i], icon[entry]); Assert.Equal(icon[entry], icon[entry + 1]);
            Assert.Equal(1, U16(icon, entry + 4)); Assert.Equal(32, U16(icon, entry + 6));
            AssertPng(icon.AsSpan(I32(icon, entry + 12), I32(icon, entry + 8)), Sizes[i]);
        }
        AssertPng(File.ReadAllBytes(Path.Combine(Root, "src/HL7Workbench.Desktop/Assets/workbench-icon.png")), 512);
    }

    [Fact]
    public void WindowsExecutableEmbedsEveryApprovedIconFrame()
    {
        if (!OperatingSystem.IsWindows()) return;
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var executable = Path.Combine(Root, "src/HL7Workbench.Desktop/bin", configuration, "net10.0/HL7Workbench.Desktop.exe");
        var bytes = File.ReadAllBytes(executable);
        using var stream = new MemoryStream(bytes); using var pe = new PEReader(stream);
        var resourceRva = pe.PEHeaders.PEHeader!.ResourceTableDirectory.RelativeVirtualAddress;
        Assert.NotEqual(0, resourceRva);
        int FileOffset(int rva)
        {
            var section = Assert.Single(pe.PEHeaders.SectionHeaders, s => rva >= s.VirtualAddress && rva < s.VirtualAddress + Math.Max(s.VirtualSize, s.SizeOfRawData));
            return section.PointerToRawData + rva - section.VirtualAddress;
        }
        var resourceBase = FileOffset(resourceRva);
        // PE resource nodes contain offsets relative to the resource root, while
        // leaf data entries contain RVAs. Keep these coordinate systems separate.
        List<(uint Id, uint Offset)> Children(uint offset)
        {
            Assert.True((offset & 0x80000000) != 0);
            var directory = resourceBase + (int)(offset & 0x7fffffff);
            var count = U16(bytes, directory + 12) + U16(bytes, directory + 14);
            return Enumerable.Range(0, count).Select(i =>
                (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(directory + 16 + i * 8)),
                 BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(directory + 20 + i * 8)))).ToList();
        }
        byte[] Resource(uint type, uint? id = null)
        {
            var typeNode = Assert.Single(Children(0x80000000), e => e.Id == type);
            var names = Children(typeNode.Offset);
            var name = id is { } value ? Assert.Single(names, e => e.Id == value) : names[0];
            var dataNode = Children(name.Offset)[0]; Assert.True((dataNode.Offset & 0x80000000) == 0);
            var data = resourceBase + (int)dataNode.Offset;
            return bytes.AsSpan(FileOffset(I32(bytes, data)), I32(bytes, data + 4)).ToArray();
        }
        var group = Resource(14); Assert.Equal(Sizes.Length, U16(group, 4));
        var icon = File.ReadAllBytes(Path.Combine(Root, "src/HL7Workbench.Desktop/Assets/workbench.ico"));
        for (var i = 0; i < Sizes.Length; i++)
        {
            var icoEntry = 6 + i * 16; var groupEntry = 6 + i * 14;
            var embedded = Resource(3, U16(group, groupEntry + 12));
            Assert.Equal(icon.AsSpan(I32(icon, icoEntry + 12), I32(icon, icoEntry + 8)).ToArray(), embedded);
            AssertPng(embedded, Sizes[i]);
        }
    }

    [Fact]
    public async Task ApplicationStyleBrandsMainSecondaryAndDerivedWindows()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-branding-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var main = new MainWindow(root);
            var secondary = new PreferencesWindow(new ApplicationPreferences(Path.Combine(root, "preferences.json")));
            var derived = new BrandingHostWindow();
            try
            {
                main.Show(); secondary.Show(); derived.Show();
                Assert.NotNull(main.Icon); Assert.Same(main.Icon, secondary.Icon); Assert.Same(main.Icon, derived.Icon);
                using var png = AssetLoader.Open(new Uri("avares://HL7Workbench.Desktop/Assets/workbench-icon.png"));
                using var copy = new MemoryStream(); png.CopyTo(copy); AssertPng(copy.ToArray(), 512);
                // The headless WindowIcon.Save is a no-op; verify the production
                // multi-frame resource directly and the shared styled icon above.
                using var encodedIcon = AssetLoader.Open(new Uri("avares://HL7Workbench.Desktop/Assets/workbench.ico"));
                using var windowIcon = new MemoryStream(); encodedIcon.CopyTo(windowIcon);
                Assert.Equal(File.ReadAllBytes(Path.Combine(Root, "src/HL7Workbench.Desktop/Assets/workbench.ico")), windowIcon.ToArray());
            }
            finally { derived.Close(); secondary.Close(); main.Close(); Directory.Delete(root, true); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    private sealed class BrandingHostWindow : Window { }
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static int I32(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
    private static void AssertPng(ReadOnlySpan<byte> png, int size)
    {
        Assert.True(png[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        Assert.Equal(size, BinaryPrimitives.ReadInt32BigEndian(png[16..]));
        Assert.Equal(size, BinaryPrimitives.ReadInt32BigEndian(png[20..]));
        Assert.Equal(8, png[24]); Assert.Equal(6, png[25]); // 8-bit RGBA, including transparency.
    }
}
