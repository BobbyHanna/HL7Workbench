using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

public sealed partial class ScriptLibrary
{
    public static readonly Guid PatientDeidentifierId = new("5bf273df-67cb-42d9-8cf1-c6bec732ae0f");
    public static readonly Guid IdentifierPseudonymizerId = new("8060bdc8-728d-48d7-afc6-de23cd1d2761");
    public static readonly Guid TimestampShifterId = new("fc0729eb-d79a-468f-b1e0-4812bdf932c9");
    public static LibraryScript PatientDeidentifier => Preset(PatientDeidentifierId, "Patient Deidentifier", DeidentificationPreset.PatientDeidentifier, "Deidentified Review Required");
    public static LibraryScript IdentifierPseudonymizer => Preset(IdentifierPseudonymizerId, "Identifier Pseudonymizer", DeidentificationPreset.IdentifierPseudonymizer, "Pseudonymized Identifiers Review Required");
    public static LibraryScript TimestampShifter => Preset(TimestampShifterId, "Timestamp Shifter", DeidentificationPreset.TimestampShifter, "Shifted Timestamps Review Required");
    private static LibraryScript[] Builtins => [Example, PatientExample, PatientDeidentifier, IdentifierPseudonymizer, TimestampShifter];
    private static bool IsBuiltin(Guid id) => id == ExampleId || id == PatientExampleId || id == PatientDeidentifierId || id == IdentifierPseudonymizerId || id == TimestampShifterId;
    private static LibraryScript Preset(Guid id, string name, DeidentificationPreset preset, string output) => new(id, name, $$"""
        // REVIEW REQUIRED: a de-identification aid, not a PHI-free/compliance guarantee.
        // Review the field-coverage matrix in docs/stage9-deidentification-acceptance.md.
        // Duplicate to configure the deterministic seed; mappings live only in this run.
        // Patient mode uses bundled fictional demographics, independent of source names/age/sex/location.
        // Unqualified/contradictory patient identities are not silently correlated.
        // Identifier/Timestamp presets leave other sensitive values unchanged for review.
        const uint seed = 42;
        var workflow = new DeidentificationWorkflow(new DeidentificationOptions
        {
            Preset = DeidentificationPreset.{{preset}}, Seed = seed,
            // Optional: ShiftDays = 30 (otherwise derived deterministically from seed).
        }, Cancellation);
        var review = Batch.Report("deidentification-review", DeidentificationSummary.Columns);
        Batch.SetOutputFileName("{{output}}");
        void Main() { workflow.Apply(Message); }
        void Complete() { review.Row(workflow.Summary.Cells); }
        """, preset == DeidentificationPreset.PatientDeidentifier ? "bundled-deidentification-v2" : "bundled-deidentification-v1", true);
}
