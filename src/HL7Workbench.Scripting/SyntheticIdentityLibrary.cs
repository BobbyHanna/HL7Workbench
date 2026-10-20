using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HL7Workbench.Scripting;

/// <summary>An entirely generated test identity. Names are invented combinations, not a real-person dataset.
/// Postal code/region are deliberately non-deliverable; telephone subscribers and email domains are reserved examples.</summary>
public sealed record SyntheticIdentity(int Allocation, string GivenName, string MiddleName, string FamilyName,
    string Street, string Locality, DateOnly DateOfBirth, string AdministrativeSex, string MaritalStatus)
{
    public string Identifier => (100_000_000L + Allocation).ToString(CultureInfo.InvariantCulture);
    public string Region => "ZZ";
    public string PostalCode => "00000";
    public string Country => "USA";
    public string DwellingNumber => Street[..Street.IndexOf(' ')];
    public string Unit => "Unit " + (1 + Allocation / (9900 * 8)).ToString(CultureInfo.InvariantCulture);
    public string AreaCode => "202";
    public string LocalTelephone => "555" + (100 + Allocation % 100).ToString("D4", CultureInfo.InvariantCulture);
    public string Telephone => "+1" + AreaCode + LocalTelephone;
    public string Email => $"{GivenName.ToLowerInvariant()}.{FamilyName.ToLowerInvariant()}.{Allocation.ToString("D7", CultureInfo.InvariantCulture)}@example.org";
    public int AgeOn(DateOnly reference) => reference.Year - DateOfBirth.Year - (reference < DateOfBirth.AddYears(reference.Year - DateOfBirth.Year) ? 1 : 0);
}

/// <summary>Bounded execution-local allocation from bundled, versioned vocabulary. Keys are opaque identity
/// digests supplied by the caller; original names/demographics are never inputs. No state is persisted.</summary>
public sealed class SyntheticIdentityLibrary
{
    public const int DictionaryVersion = 1;
    private sealed record Vocabulary(int Version, string Attribution, string[] GivenNames, string[] MiddleNames, string[] FamilyNames,
        string[] Streets, string[] Localities, string[] AdministrativeSex, string[] MaritalStatus);
    private static readonly Vocabulary Words = Load();
    private readonly uint seed;
    private readonly int maximum, slots;
    private readonly Dictionary<string, SyntheticIdentity> identities = new(StringComparer.Ordinal);
    private readonly HashSet<int> occupied = [];
    public SyntheticIdentityLibrary(uint seed, int maximumIdentities = 100_000) : this(seed, maximumIdentities, 1_000_003) { }
    internal SyntheticIdentityLibrary(uint seed, int maximumIdentities, int allocationSlots)
    {
        if (maximumIdentities is < 1 or > 100_000 || allocationSlots is < 1 or > 1_000_003) throw new ArgumentOutOfRangeException(nameof(maximumIdentities));
        this.seed = seed; maximum = maximumIdentities; slots = allocationSlots;
    }
    /// <summary>Stable for identical seed/key/allocation order. Collisions probe successive free slots;
    /// exhaustion fails without reusing a complete identity. Finite names/fictional phone numbers may repeat.</summary>
    public SyntheticIdentity Get(string identityKey, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(identityKey); cancellation.ThrowIfCancellationRequested();
        if (identityKey.Length is < 1 or > 256) throw Limit();
        if (identities.TryGetValue(identityKey, out var existing)) return existing;
        if (identities.Count >= maximum || occupied.Count == slots) throw Limit();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"WB-Synthetic-v{DictionaryVersion}:{seed.ToString(CultureInfo.InvariantCulture)}:{identityKey}"));
        var slot = (int)(BinaryPrimitives.ReadUInt32LittleEndian(hash) % slots);
        while (occupied.Contains(slot)) { cancellation.ThrowIfCancellationRequested(); slot = (slot + 1) % slots; }
        // Names and demographics depend only on seed + allocated synthetic slot,
        // never source age, sex, name, geography or clock. A fixed epoch is reproducible.
        var random = SHA256.HashData(Encoding.ASCII.GetBytes($"WB-Demographics-v1:{seed.ToString(CultureInfo.InvariantCulture)}:{slot.ToString(CultureInfo.InvariantCulture)}"));
        string Pick(string[] values, int offset) => values[BinaryPrimitives.ReadUInt32LittleEndian(random.AsSpan(offset)) % values.Length];
        var birth = new DateOnly(1945, 1, 1).AddDays((int)(BinaryPrimitives.ReadUInt32LittleEndian(random.AsSpan(20)) % 25_202));
        var profile = new SyntheticIdentity(slot, Pick(Words.GivenNames, 0), Pick(Words.MiddleNames, 4), Pick(Words.FamilyNames, 8),
            (100 + slot % 9900).ToString(CultureInfo.InvariantCulture) + " " + Words.Streets[slot / 9900 % Words.Streets.Length], Pick(Words.Localities, 16), birth,
            Pick(Words.AdministrativeSex, 24), Pick(Words.MaritalStatus, 28));
        occupied.Add(slot); identities.Add(identityKey, profile); return profile;
    }
    private static Vocabulary Load()
    {
        using var stream = typeof(SyntheticIdentityLibrary).Assembly.GetManifestResourceStream("HL7Workbench.Scripting.SyntheticIdentities.v1.json")!;
        var words = JsonSerializer.Deserialize<Vocabulary>(stream) ?? throw new InvalidDataException("Synthetic vocabulary unavailable.");
        if (words.Version != DictionaryVersion || words.Streets.Length != 8 || new[] { words.GivenNames, words.MiddleNames, words.FamilyNames, words.Streets, words.Localities, words.AdministrativeSex, words.MaritalStatus }
            .Any(list => list.Length is < 3 or > 64 || list.Any(v => v.Length is < 1 or > 80 || v.Any(c => c < 32 || c > 126))))
            throw new InvalidDataException("Unsupported synthetic vocabulary.");
        return words;
    }
    private static ScriptRunException Limit() => new(new ScriptDiagnostic(ScriptPhase.Execution, "TransformationLimit"));
}
