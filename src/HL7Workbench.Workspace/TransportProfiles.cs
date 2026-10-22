using System.Text.Json;
using System.Text.Json.Serialization;
using HL7Workbench.Transport;

namespace HL7Workbench.Workspace;

/// <summary>Whitelisted endpoint metadata only. Payloads, connection state and credentials are never serialized.</summary>
public sealed class TransportProfiles(string path)
{
    private static readonly JsonSerializerOptions json = new() { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public const int MaximumBytes = 64 * 1024;
    private sealed record ProfileFile(int Version, TransportEndpoint[] Profiles);
    public IReadOnlyList<TransportEndpoint> Load()
    {
        if (!File.Exists(path)) return [];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("Endpoint profile file is oversized.");
        var file = JsonSerializer.Deserialize<ProfileFile>(stream, json) ?? throw new InvalidDataException("Invalid endpoint profiles.");
        if (file.Version != 1) throw new InvalidDataException("Unsupported endpoint profile version.");
        Validate(file.Profiles); return file.Profiles;
    }
    public void Save(IReadOnlyList<TransportEndpoint> profiles)
    {
        Validate(profiles);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new ProfileFile(1, profiles.ToArray()), json);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Endpoint profile file is oversized.");
        var fullPath = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            File.Move(temporary, fullPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Validate(IReadOnlyList<TransportEndpoint>? profiles)
    {
        if (profiles is null || profiles.Count > 32 || profiles.Any(p => p is null) || profiles.Select(p => p.Id).Distinct().Count() != profiles.Count)
            throw new InvalidDataException("Invalid endpoint profile inventory.");
        foreach (var profile in profiles) profile.Validate();
    }
}
