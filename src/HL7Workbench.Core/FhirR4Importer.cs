using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HL7Workbench.Core;

/// <summary>One input factory. The importer owns/disposes the returned stream; no path or network behavior belongs to Core.</summary>
public sealed record FhirImportInput(Func<CancellationToken, Task<Stream>> OpenReadAsync)
{ public override string ToString() => "Local synthetic FHIR input"; }
public sealed record FhirImportProgress(int Files, int Resources, long Bytes);

/// <summary>Narrow bounded R4 projection, not a general FHIR validator or proof of synthetic provenance.</summary>
public static partial class FhirR4Importer
{
    public const string SyntheticWarning = "FHIR JSON may contain real PHI. Import synthetic/test data only. A filename or Synthea identifier is not proof of safety.";
    /// <summary>Compatibility entry point for callers that still require their own acknowledgment.</summary>
    public static async Task<FhirImportSession> ImportAsync(IEnumerable<FhirImportInput> inputs, bool syntheticConfirmed,
        FhirImportLimits? limits = null, CancellationToken token = default, IProgress<FhirImportProgress>? progress = null)
    {
        if (!syntheticConfirmed) throw new InvalidOperationException("Confirm synthetic/test data before importing FHIR JSON.");
        return await ImportAsync(inputs, limits, token, progress).ConfigureAwait(false);
    }
    /// <summary>Projects local FHIR data under the same validation/resource limits. Synthetic provenance is not inferred or certified.</summary>
    public static async Task<FhirImportSession> ImportAsync(IEnumerable<FhirImportInput> inputs,
        FhirImportLimits? limits = null, CancellationToken token = default, IProgress<FhirImportProgress>? progress = null)
    {
        limits ??= new(); limits.Validate();
        var selected = inputs.Take(limits.Files + 1).ToArray();
        if (selected.Length == 0 || selected.Length > limits.Files) throw Failure("FileLimit");
        var builder = new Builder(limits, token); var digests = new List<string>(); long totalBytes = 0;
        try
        {
            for (var n = 0; n < selected.Length; n++)
            {
                token.ThrowIfCancellationRequested(); await using var input = await selected[n].OpenReadAsync(token).ConfigureAwait(false);
                using var bytes = new MemoryStream(); var buffer = new byte[65536];
                while (true)
                {
                    var read = await input.ReadAsync(buffer, token).ConfigureAwait(false); if (read == 0) break;
                    if (bytes.Length + read > limits.FileBytes || totalBytes + read > limits.TotalBytes) throw Failure("ByteLimit");
                    bytes.Write(buffer, 0, read); totalBytes += read;
                }
                var digest = Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)))); digests.Add(digest);
                var memory = bytes.GetBuffer().AsMemory(0, checked((int)bytes.Length));
                if (memory.Length >= 3 && memory.Span[0] == 0xEF && memory.Span[1] == 0xBB && memory.Span[2] == 0xBF) memory = memory[3..];
                using var json = JsonDocument.Parse(memory, new() { MaxDepth = limits.Depth });
                builder.AddBundle(json.RootElement, n + 1, digest);
                progress?.Report(new(n + 1, builder.ResourceCount, totalBytes));
            }
            token.ThrowIfCancellationRequested();
            // File names/order are not identities. Reordering the same source set preserves its fingerprint.
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', digests.Order(StringComparer.Ordinal)))));
            return builder.Finish(fingerprint, selected.Length, totalBytes);
        }
        catch (Exception error) when (error is JsonException or FormatException or DecoderFallbackException or EncoderFallbackException or OverflowException or InvalidOperationException)
        { throw Failure("MalformedJson"); }
    }
    private static InvalidDataException Failure(string code) => new("Synthetic FHIR import refused: " + code + ". Previous session and source files are unchanged.");

    private sealed partial class Builder(FhirImportLimits limits, CancellationToken token)
    {
        private readonly List<FhirResource> resources = [];
        private readonly Dictionary<(int Input, string Id), int> local = [];
        private readonly Dictionary<(int Owner, string Id), int> contained = [];
        private readonly Dictionary<string, List<int>> aliases = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> identical = new(StringComparer.Ordinal);
        private readonly HashSet<(int Input, string Url)> bundleUrls = [];
        private int nodes, literalReferences, retained, occurrences, duplicates, projectedReferences, patientCount;
        public int ResourceCount => resources.Count;
        private string Keep(string text)
        {
            retained = checked(retained + text.Length); if (retained > limits.ProjectedCharacters) throw Failure("ProjectionLimit"); return text;
        }
        private void ValidateJson(JsonElement element)
        {
            token.ThrowIfCancellationRequested(); if (++nodes > limits.JsonNodes) throw Failure("JsonNodeLimit");
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Length > 128 || names.Count >= 256 || !names.Add(property.Name)) throw Failure("ObjectShape");
                    if (property.Name == "reference" && ++literalReferences > limits.References) throw Failure("ReferenceLimit");
                    ValidateJson(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                if (element.GetArrayLength() > limits.Resources) throw Failure("ArrayLimit");
                foreach (var child in element.EnumerateArray()) ValidateJson(child);
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString()!;
                if (text.Length > limits.StringCharacters) throw Failure("StringLimit");
                _ = new UTF8Encoding(false, true).GetByteCount(text);
            }
            else if (element.ValueKind == JsonValueKind.Number && element.GetRawText().Length > 128) throw Failure("NumberLimit");
        }
        public void AddBundle(JsonElement bundle, int input, string digest)
        {
            ValidateJson(bundle);
            if (S(bundle, "resourceType") != "Bundle" || S(bundle, "type") is not ("transaction" or "collection")) throw Failure("BundleType");
            CheckVersion(bundle);
            var entries = A(bundle, "entry"); if (entries.Length == 0) throw Failure("EmptyBundle");
            var entryNumber = 0;
            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested(); entryNumber++;
                var resource = O(entry, "resource"); if (resource.ValueKind == JsonValueKind.Undefined) throw Failure("MissingResource");
                _ = AddResource(resource, S(entry, "fullUrl"), input, digest, entryNumber, null);
            }
        }
        private void CheckVersion(JsonElement resource)
        {
            var version = S(resource, "fhirVersion");
            if (version.Length > 0 && version != "4.0.1") throw Failure("UnsupportedVersion");
            foreach (var profile in A(O(resource, "meta"), "profile"))
            {
                if (profile.ValueKind != JsonValueKind.String) throw Failure("ProfileShape");
                var uri = profile.GetString()!;
                if (new[] { "/fhir/3.0", "/fhir/1.0", "/fhir/5.0", "/fhir/4.3", "/fhir/R5/", "/fhir/R4B/", "/fhir/STU3/" }.Any(uri.Contains)) throw Failure("UnsupportedVersion");
            }
        }
        private int AddResource(JsonElement json, string fullUrl, int input, string digest, int entry, int? owner)
        {
            token.ThrowIfCancellationRequested(); if (++occurrences > limits.Resources) throw Failure("ResourceLimit");
            var type = S(json, "resourceType"); var id = S(json, "id"); CheckVersion(json);
            if (!Regex.IsMatch(type, "^[A-Z][A-Za-z]{0,63}$", RegexOptions.CultureInvariant)) throw Failure("ResourceType");
            var known = Enum.TryParse<FhirResourceKind>(type, out var kind) && kind != FhirResourceKind.Unsupported;
            if ((known || owner is not null || id.Length > 0) && !Regex.IsMatch(id, "^[A-Za-z0-9.-]{1,64}$", RegexOptions.CultureInvariant)) throw Failure("ResourceId");
            if (id.Length > 64 || fullUrl.Length > 512 || fullUrl.Contains("/_history/", StringComparison.Ordinal) ||
                fullUrl.Length > 0 && (!Uri.TryCreate(fullUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("urn" or "http" or "https"))) throw Failure("ResourceUrl");
            if (Uri.TryCreate(fullUrl, UriKind.Absolute, out var address) && address.Scheme is "http" or "https")
            {
                if (address.Query.Length > 0 || address.Fragment.Length > 0) throw Failure("ResourceUrl");
                var parts = address.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && Enum.TryParse<FhirResourceKind>(parts[^2], out var urlKind) && urlKind != FhirResourceKind.Unsupported &&
                    (parts[^2] != type || parts[^1] != id)) throw Failure("ResourceIdentityConflict");
            }
            var localKey = (input, type + "/" + id);
            if (owner is null && id.Length > 0 && local.ContainsKey(localKey)) throw Failure("DuplicateId");
            if (owner is null && fullUrl.Length > 0 && !bundleUrls.Add((input, fullUrl))) throw Failure("DuplicateFullUrl");
            if (owner is not null && (A(json, "contained").Length > 0 || contained.ContainsKey((owner.Value, id)))) throw Failure("ContainedShape");
            var content = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.GetRawText())));
            // Shared provider metadata is repeated by Synthea across patient files.
            // Clinical records can have scope-dependent relative links, so identical
            // JSON alone must never coalesce their patient/encounter provenance.
            var identity = fullUrl.Length == 0 || kind is not (FhirResourceKind.Organization or FhirResourceKind.Practitioner) ? "" : type + "\n" + id + "\n" + fullUrl + "\n" + content;
            int key;
            if (identity.Length > 0 && identical.TryGetValue(identity, out key)) duplicates++;
            else
            {
                key = resources.Count + 1;
                var resource = Project(json, key, known ? kind : FhirResourceKind.Unsupported, type, id, fullUrl, input, digest, entry, owner, content);
                resources.Add(resource);
                if (known && kind == FhirResourceKind.Patient && ++patientCount > limits.Patients) throw Failure("PatientLimit");
                if (identity.Length > 0) identical.Add(identity, key);
            }
            if (owner is null)
            {
                if (id.Length > 0) { local.Add(localKey, key); Alias(type + "/" + id, key); }
                if (fullUrl.Length > 0) Alias(fullUrl, key);
            }
            else contained.Add((owner.Value, id), key);
            // Contained identity is scoped to the canonical container; identical parent imports reuse it.
            if (resources[key - 1].InputNumber == input)
                foreach (var child in A(json, "contained")) _ = AddResource(child, "", input, digest, entry, key);
            return key;
        }
        private void Alias(string alias, int key)
        {
            if (!aliases.TryGetValue(alias, out var targets)) aliases.Add(alias, targets = []);
            if (!targets.Contains(key)) targets.Add(key);
        }
        private static JsonElement O(JsonElement json, string name)
        {
            if (json.ValueKind == JsonValueKind.Undefined) return default;
            if (json.ValueKind != JsonValueKind.Object) throw Failure("ObjectShape");
            if (!json.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return default;
            if (value.ValueKind != JsonValueKind.Object) throw Failure("ObjectShape"); return value;
        }
        private static JsonElement[] A(JsonElement json, string name)
        {
            if (json.ValueKind == JsonValueKind.Undefined) return [];
            if (json.ValueKind != JsonValueKind.Object) throw Failure("ObjectShape");
            if (!json.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return [];
            if (value.ValueKind != JsonValueKind.Array) throw Failure("ArrayShape"); return value.EnumerateArray().ToArray();
        }
        private static string S(JsonElement json, string name)
        {
            if (json.ValueKind == JsonValueKind.Undefined) return "";
            if (json.ValueKind != JsonValueKind.Object) throw Failure("ObjectShape");
            if (!json.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return "";
            if (value.ValueKind != JsonValueKind.String) throw Failure("StringShape"); return value.GetString()!;
        }
    }
}
