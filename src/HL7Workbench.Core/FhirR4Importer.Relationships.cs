using System.Collections.ObjectModel;

namespace HL7Workbench.Core;

public static partial class FhirR4Importer
{
    private sealed partial class Builder
    {
        private FhirResolvedLink Resolve(FhirResource source, FhirLink link)
        {
            var reference = link.Reference; int? target = null; var ambiguous = false;
            void AliasTarget(string alias)
            {
                if (!aliases.TryGetValue(alias, out var candidates)) return;
                if (candidates.Count == 1) target = candidates[0]; else ambiguous = true;
            }
            if (reference.Length == 0 || reference.Length > 512 || reference.Contains("/_history/", StringComparison.Ordinal) || reference.Contains('?') || reference.Contains('|'))
                return new(link.Role, FhirResolution.Unsupported, null);
            if (reference.StartsWith('#'))
            {
                var owner = source.Container ?? source.Key;
                target = reference == "#" ? owner : contained.GetValueOrDefault((owner, reference[1..]));
                if (target == 0) target = null;
            }
            else if (Uri.TryCreate(reference, UriKind.Absolute, out _)) AliasTarget(reference);
            else
            {
                var scope = source.Container is { } owner ? resources[owner - 1] : source;
                if (Uri.TryCreate(scope.FullUrl, UriKind.Absolute, out var full) && full.Scheme is "http" or "https")
                {
                    var suffix = scope.ResourceType + "/" + scope.Id;
                    if (scope.FullUrl.EndsWith(suffix, StringComparison.Ordinal))
                        AliasTarget(scope.FullUrl[..^suffix.Length] + reference);
                    // A REST-qualified identity must not fall back to another base/server.
                    if (target is null && !ambiguous) return new(link.Role, FhirResolution.Unresolved, null);
                }
                else if (local.TryGetValue((source.InputNumber, reference), out var localTarget)) target = localTarget;
                else AliasTarget(reference);
            }
            if (ambiguous) return new(link.Role, FhirResolution.Ambiguous, null);
            if (target is null) return new(link.Role, FhirResolution.Unresolved, null);
            var resource = resources[target.Value - 1];
            if (resource.FullUrl.Length > 0 && aliases.TryGetValue(resource.FullUrl, out var identities) && identities.Count > 1)
                return new(link.Role, FhirResolution.Ambiguous, null);
            var expected = link.Role switch
            {
                FhirLinkRole.Subject => FhirResourceKind.Patient, FhirLinkRole.Encounter => FhirResourceKind.Encounter,
                FhirLinkRole.Result => FhirResourceKind.Observation,
                FhirLinkRole.ServiceProvider or FhirLinkRole.ManagingOrganization => FhirResourceKind.Organization,
                _ => FhirResourceKind.Practitioner
            };
            if (resource.Kind != expected || link.DeclaredType.Length > 0 && link.DeclaredType != resource.ResourceType &&
                link.DeclaredType != "http://hl7.org/fhir/StructureDefinition/" + resource.ResourceType)
                return new(link.Role, FhirResolution.Conflicting, null);
            return new(link.Role, FhirResolution.Resolved, target);
        }
        public FhirImportSession Finish(string fingerprint, int fileCount, long byteCount)
        {
            var resolved = new Dictionary<int, IReadOnlyList<FhirResolvedLink>>();
            foreach (var resource in resources)
            { token.ThrowIfCancellationRequested(); resolved.Add(resource.Key, Array.AsReadOnly(resource.Links.Select(l => Resolve(resource, l)).ToArray())); }
            var patientKeys = new Dictionary<int, int>(); var encounterKeys = new Dictionary<int, int>(); var quarantined = new HashSet<int>();
            foreach (var resource in resources)
            {
                if (resource.Kind == FhirResourceKind.Patient) patientKeys.Add(resource.Key, resource.Key);
                else if (resolved[resource.Key].SingleOrDefault(l => l.Role == FhirLinkRole.Subject) is { Resolution: FhirResolution.Resolved, Target: { } patient }) patientKeys.Add(resource.Key, patient);
            }
            foreach (var resource in resources.Where(r => r.Kind == FhirResourceKind.Encounter))
                if (patientKeys.ContainsKey(resource.Key)) encounterKeys.Add(resource.Key, resource.Key);
            foreach (var resource in resources)
            {
                token.ThrowIfCancellationRequested();
                if (resolved[resource.Key].SingleOrDefault(l => l.Role == FhirLinkRole.Encounter) is { } encounter)
                {
                    if (encounter.Resolution == FhirResolution.Resolved && encounter.Target is { } key && patientKeys.TryGetValue(resource.Key, out var patient) && patientKeys.GetValueOrDefault(key) == patient)
                        encounterKeys[resource.Key] = key;
                    else
                    {
                        quarantined.Add(resource.Key);
                        if (encounter.Resolution == FhirResolution.Resolved)
                            resolved[resource.Key] = Array.AsReadOnly(resolved[resource.Key].Select(l => l.Role == FhirLinkRole.Encounter ? l with { Resolution = FhirResolution.Conflicting, Target = null } : l).ToArray());
                    }
                }
            }
            foreach (var report in resources.Where(r => r.Kind == FhirResourceKind.DiagnosticReport))
            {
                resolved[report.Key] = Array.AsReadOnly(resolved[report.Key].Select(link =>
                {
                    if (link.Role != FhirLinkRole.Result || link.Resolution != FhirResolution.Resolved || link.Target is not { } observation) return link;
                    if (!patientKeys.TryGetValue(report.Key, out var patient) || patientKeys.GetValueOrDefault(observation) != patient || quarantined.Contains(observation) ||
                        encounterKeys.TryGetValue(report.Key, out var reportEncounter) && encounterKeys.GetValueOrDefault(observation) != reportEncounter)
                        return link with { Resolution = FhirResolution.Conflicting, Target = null };
                    return link;
                }).ToArray());
            }
            var timeline = new Dictionary<int, List<FhirTimelineEvent>>(); var eventCount = 0; var inconsistent = 0;
            foreach (var resource in resources)
            {
                var dated = resource.Times.Where(t => t.Time.IsInstant).ToArray();
                if (resource.Times.Count >= 2 && resource.Times[0].Kind is FhirEventKind.EncounterStart or FhirEventKind.ObservationEffective or FhirEventKind.ReportEffective &&
                    resource.Times[1].Kind is FhirEventKind.EncounterEnd or FhirEventKind.ObservationEffectiveEnd or FhirEventKind.ReportEffectiveEnd &&
                    dated.Length >= 2 && resource.Times[0].Time.IsInstant && resource.Times[1].Time.IsInstant && resource.Times[1].Time.Order < resource.Times[0].Time.Order) inconsistent++;
                if (!patientKeys.TryGetValue(resource.Key, out var patient)) continue;
                if (!timeline.TryGetValue(patient, out var events)) timeline.Add(patient, events = []);
                foreach (var time in resource.Times)
                {
                    if (++eventCount > limits.TimelineEvents) throw Failure("TimelineLimit");
                    events.Add(new(resource.Key, time.Kind, time.Time, encounterKeys.TryGetValue(resource.Key, out var encounter) ? encounter : null));
                }
            }
            if (patientCount == 0) throw Failure("NoPatients");
            var timelines = timeline.ToDictionary(p => p.Key, p => (IReadOnlyList<FhirTimelineEvent>)Array.AsReadOnly(p.Value
                .OrderBy(e => e.Time.Order).ThenBy(e => resources[e.ResourceKey - 1].FullUrl, StringComparer.Ordinal)
                .ThenBy(e => resources[e.ResourceKey - 1].InputDigest, StringComparer.Ordinal).ThenBy(e => resources[e.ResourceKey - 1].EntryNumber).ThenBy(e => e.Kind).ToArray()));
            var allLinks = resolved.Values.SelectMany(v => v).ToArray();
            var counts = Enum.GetValues<FhirResourceKind>().ToDictionary(k => k, k => resources.Count(r => r.Kind == k));
            var summary = new FhirImportSummary(fileCount, byteCount, occurrences, resources.Count, patientCount, counts[FhirResourceKind.Unsupported], duplicates, aliases.Values.Count(v => v.Count > 1),
                allLinks.Count(l => l.Resolution == FhirResolution.Unresolved), allLinks.Count(l => l.Resolution == FhirResolution.Ambiguous),
                allLinks.Count(l => l.Resolution == FhirResolution.Conflicting), allLinks.Count(l => l.Resolution == FhirResolution.Unsupported) + Math.Max(0, literalReferences - projectedReferences),
                resources.Count(r => r.Kind is FhirResourceKind.Condition or FhirResourceKind.Encounter or FhirResourceKind.Observation or FhirResourceKind.DiagnosticReport && !patientKeys.ContainsKey(r.Key)),
                resources.Sum(r => r.Times.Count(t => t.Time.Precision == FhirTimePrecision.Invalid)), resources.Sum(r => r.Times.Count(t => t.Time.Precision == FhirTimePrecision.Missing)),
                inconsistent, resources.Count(r => r.Kind == FhirResourceKind.Patient && r.SyntheaIdentifier), new ReadOnlyDictionary<FhirResourceKind, int>(counts));
            return new(fingerprint, resources.ToArray(), summary, patientKeys, encounterKeys, resolved, timelines, quarantined);
        }
    }
}
