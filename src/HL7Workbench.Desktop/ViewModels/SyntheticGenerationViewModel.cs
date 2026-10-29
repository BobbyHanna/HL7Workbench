using System.ComponentModel;
using System.Globalization;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

internal enum SyntheticGenerationMode { Standalone, AdmissionDischarge, OrderResult, CombinedEncounter }

/// <summary>Dialog-local configuration; preview computes only identifiers, never message bodies.</summary>
internal sealed class SyntheticGenerationViewModel(DocumentManager documents, MessageDocument? target, Func<uint>? scenarioSeedProvider = null) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private SyntheticMessageType messageType;
    private string count = "1", standaloneSeed = "", scenarioSeed = "", timestamp = "2030-01-01T00:00:00+00:00", prefix = "SYN";
    private bool insertIntoTarget;
    private MessageInsertionPosition position = MessageInsertionPosition.End;
    private CancellationTokenSource? cancellation;
    private SyntheticGenerationMode mode;
    private string encounters = "1", orders = "1", results = "1";
    private string sendingApplication = "", sendingFacility = "", receivingApplication = "", receivingFacility = "";
    private string patientPrefix = "", orderCode = "", resultStatus = "F", observationValue = "";
    private bool includeTransfer;
    private SyntheticEncounterType encounterType;
    private SyntheticClinicalExample clinicalExample;
    private SyntheticDemographicStyle demographicStyle = SyntheticDemographicStyle.RealisticSynthetic;
    public SyntheticDemographicStyle DemographicStyle { get => demographicStyle; set { demographicStyle = value; Changed(); } }
    public SyntheticGenerationMode Mode { get => mode; set { mode = value; Changed(); } }
    public bool IsScenario => Mode != SyntheticGenerationMode.Standalone;
    public bool HasOrders => IsScenario && Mode != SyntheticGenerationMode.AdmissionDischarge;
    public bool HasAdt => IsScenario && Mode != SyntheticGenerationMode.OrderResult;
    public string Encounters { get => encounters; set { encounters = value; Changed(); } }
    public string Orders { get => orders; set { orders = value; Changed(); } }
    public string Results { get => results; set { results = value; Changed(); } }
    public bool IncludeTransfer { get => includeTransfer; set { includeTransfer = value; Changed(); } }
    public SyntheticEncounterType EncounterType { get => encounterType; set { encounterType = value; Changed(); } }
    public SyntheticClinicalExample ClinicalExample { get => clinicalExample; set { clinicalExample = value; Changed(); } }
    public string SendingApplication { get => sendingApplication; set { sendingApplication = value; Changed(); } }
    public string SendingFacility { get => sendingFacility; set { sendingFacility = value; Changed(); } }
    public string ReceivingApplication { get => receivingApplication; set { receivingApplication = value; Changed(); } }
    public string ReceivingFacility { get => receivingFacility; set { receivingFacility = value; Changed(); } }
    public string PatientPrefix { get => patientPrefix; set { patientPrefix = value; Changed(); } }
    public string OrderCode { get => orderCode; set { orderCode = value; Changed(); } }
    public string ResultStatus { get => resultStatus; set { resultStatus = value; Changed(); } }
    public string ObservationValue { get => observationValue; set { observationValue = value; Changed(); } }
    public SyntheticMessageType MessageType { get => messageType; set { messageType = value; Changed(); } }
    public string Count { get => count; set { count = value; Changed(); } }
    public string Seed { get => IsScenario ? scenarioSeed : standaloneSeed; set { if (IsScenario) scenarioSeed = value; else standaloneSeed = value; Changed(); } }
    public string Timestamp { get => timestamp; set { timestamp = value; Changed(); } }
    public string Prefix { get => prefix; set { prefix = value; Changed(); } }
    public bool InsertIntoTarget { get => insertIntoTarget; set { insertIntoTarget = value; Changed(); } }
    public MessageInsertionPosition Position { get => position; set { position = value; Changed(); } }
    public bool HasTarget => target is not null;
    public string TargetLabel => target is null ? "No active document" : "Active document: " + target.Title;
    public bool IsBusy { get; private set; }
    public string Status { get; private set; } = "";
    public SyntheticGenerationResult? Result { get; private set; }
    public bool CanGenerate => !IsBusy && TryOptions(out _, out _, out _);
    public string Preview
    {
        get
        {
            if (!TryOptions(out var options, out var scenario, out var error)) return error;
            var description = scenario is null
                ? $"HL7 v2.5.1 · generator v{options!.GeneratorVersion} · {options.DemographicStyleLabel}\n{options.TypeLabel} · {options.Count} message(s)\n" +
                    (string.IsNullOrWhiteSpace(Seed) ? "Seed: automatic (chosen on Generate); control IDs use the resolved seed." : $"Control IDs: {options.ControlId(1)} … {options.ControlId(options.Count)}")
                : $"HL7 v2.5.1 · scenario generator v{SyntheticScenarioGenerator.Version}\n{scenario.Label} · {scenario.DemographicStyleLabel} · estimated {scenario.EstimateMessageCount()} messages\n{scenario.Patients} patients · {scenario.Patients * scenario.EncountersPerPatient} encounters · seed {(string.IsNullOrWhiteSpace(Seed) ? "automatic (chosen on Generate)" : scenario.Seed.ToString(CultureInfo.InvariantCulture))}\n" +
                  (scenario.HasOrders ? $"{scenario.OrdersPerEncounter} orders/encounter · {scenario.ResultsPerOrder} reports/order (two OBX observations/report)" : "Connected admission/registration and discharge");
            return description + "\n" + (InsertIntoTarget ? TargetLabel + " · " + MessageInsertionWorkflow.Choices.Single(c => c.Position == Position).Label : "New unsaved document") +
                "\nSynthetic examples only; no clinical or implementation-guide conformance claim.";
        }
    }
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
    private bool TryOptions(out SyntheticGenerationOptions? options, out SyntheticScenarioOptions? scenario, out string error)
    {
        options = null; scenario = null; error = "";
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(Position)) { error = "Choose a supported generation mode and insertion position."; return false; }
        if (!int.TryParse(Count, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) { error = "Enter a whole count from 1 to 1,000."; return false; }
        var seedValue = SyntheticGenerationOptions.DefaultSeed;
        if (!string.IsNullOrWhiteSpace(Seed) && !uint.TryParse(Seed, NumberStyles.None, CultureInfo.InvariantCulture, out seedValue))
        { error = "Seed must be an integer from 0 to 4,294,967,295, or blank for one new cryptographic seed per Generate."; return false; }
        if (!DateTimeOffset.TryParseExact(Timestamp, ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss'Z'"],
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
        { error = "Use a timestamp with seconds and offset, e.g. 2030-01-01T00:00:00+00:00."; return false; }
        try
        {
            if (IsScenario)
            {
                if (!int.TryParse(Encounters, NumberStyles.None, CultureInfo.InvariantCulture, out var encountersValue) ||
                    HasOrders && (!int.TryParse(Orders, NumberStyles.None, CultureInfo.InvariantCulture, out _) || !int.TryParse(Results, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                    throw new ArgumentException("Enter whole encounter, order and report quantities.");
                decimal? observation = null;
                if (HasOrders && !string.IsNullOrWhiteSpace(ObservationValue))
                {
                    if (!decimal.TryParse(ObservationValue, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                        throw new ArgumentException("Observation override must be an invariant decimal literal.");
                    observation = value;
                }
                var request = new SyntheticScenarioOptions
                {
                    Scenario = (SyntheticScenarioType)((int)Mode - 1), Patients = number, EncountersPerPatient = encountersValue,
                    OrdersPerEncounter = HasOrders ? int.Parse(Orders, CultureInfo.InvariantCulture) : 1,
                    ResultsPerOrder = HasOrders ? int.Parse(Results, CultureInfo.InvariantCulture) : 1,
                    IncludeTransfer = HasAdt && IncludeTransfer, EncounterType = EncounterType, ClinicalExample = HasOrders ? ClinicalExample : SyntheticClinicalExample.Chemistry,
                    Seed = seedValue, BaseTimestamp = at, Prefix = Prefix, DemographicStyle = DemographicStyle,
                    Overrides = new()
                    {
                        PatientIdentifierPrefix = string.IsNullOrWhiteSpace(PatientPrefix) ? null : PatientPrefix,
                        OrderCode = HasOrders && !string.IsNullOrWhiteSpace(OrderCode) ? OrderCode : null,
                        ResultStatus = HasOrders ? ResultStatus : null, ObservationValue = observation
                    }
                };
                // Blank UI fields mean automatic. Nonblank values are explicit
                // overrides, including strings that happen to match old defaults.
                if (!string.IsNullOrWhiteSpace(SendingApplication)) request = request with { SendingApplication = SendingApplication };
                if (!string.IsNullOrWhiteSpace(SendingFacility)) request = request with { SendingFacility = SendingFacility };
                if (!string.IsNullOrWhiteSpace(ReceivingApplication)) request = request with { ReceivingApplication = ReceivingApplication };
                if (!string.IsNullOrWhiteSpace(ReceivingFacility)) request = request with { ReceivingFacility = ReceivingFacility };
                number = request.EstimateMessageCount();
                scenario = request;
            }
            else
            {
                var request = new SyntheticGenerationOptions { MessageType = MessageType, Count = number, Seed = seedValue, BaseTimestamp = at, Prefix = Prefix,
                    DemographicStyle = DemographicStyle, PatientIdentifierPrefix = string.IsNullOrWhiteSpace(PatientPrefix) ? null : PatientPrefix };
                if (!string.IsNullOrWhiteSpace(SendingApplication)) request = request with { SendingApplication = SendingApplication };
                if (!string.IsNullOrWhiteSpace(SendingFacility)) request = request with { SendingFacility = SendingFacility };
                if (!string.IsNullOrWhiteSpace(ReceivingApplication)) request = request with { ReceivingApplication = ReceivingApplication };
                if (!string.IsNullOrWhiteSpace(ReceivingFacility)) request = request with { ReceivingFacility = ReceivingFacility };
                _ = request.Normalize(); // Validate without freezing automatic seed-dependent defaults.
                options = request;
            }
        }
        catch (ArgumentException e) { error = e.Message; return false; }
        if (InsertIntoTarget)
        {
            if (target is null || !documents.Documents.Contains(target)) { error = "The captured destination is no longer open. Choose a new document."; return false; }
            if (target.InsertionUnavailableReason is { } reason) { error = reason; return false; }
            if (number > MessageFileIndex.MaxIndexedMessages - target.MessageCount) { error = "The complete batch would exceed the 100,000-message document limit."; return false; }
            if (target.ActiveMessageId is null && Position is MessageInsertionPosition.BeforeCurrent or MessageInsertionPosition.AfterCurrent)
            { error = "Select a current message or use Beginning/End."; return false; }
        }
        return true;
    }
    public async Task GenerateAsync()
    {
        if (IsBusy) return;
        if (!TryOptions(out var options, out var scenario, out var error)) { Status = error; Changed(); return; }
        var automaticSeed = string.IsNullOrWhiteSpace(Seed);
        IsBusy = true; Result = null; Status = "Generating…"; cancellation = new(); Changed();
        try
        {
            var operation = cancellation;
            var progress = new Progress<SyntheticGenerationProgress>(p => { if (IsBusy && ReferenceEquals(cancellation, operation)) { Status = $"{p.Phase}: {p.Completed:N0} / {p.Total:N0}"; Changed(); } });
            var service = new SyntheticGeneration(documents, scenarioSeedProvider);
            Result = scenario is null
                ? await service.GenerateAsync(options!, InsertIntoTarget ? target : null, Position, progress, cancellation.Token, automaticSeed)
                : await service.GenerateScenariosAsync(scenario, InsertIntoTarget ? target : null, Position, progress, cancellation.Token, automaticSeed);
            Status = $"Generated {Result.MessageIds.Count} synthetic message(s).";
        }
        catch (OperationCanceledException) { Status = "Generation canceled; no batch published."; }
        catch (System.Security.Cryptography.CryptographicException) { Status = "An automatic seed could not be selected. Enter an explicit seed and try again."; }
        catch (Hl7LimitException) { Status = "The complete batch exceeds a content, message, line, document or edit/history limit. Reduce the count or choose a new document."; }
        catch (InvalidOperationException e) { Status = e.Message + " Complete or undo unfinished drafts, use a safe insertion position, or choose a new document."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.EncoderFallbackException or NotSupportedException or ArgumentException)
        { Status = "Generation could not be published. Check temporary storage access and destination encoding, or choose a new document. Existing work is retained."; }
        finally { cancellation.Dispose(); cancellation = null; IsBusy = false; Changed(); }
    }
    public void Cancel() => cancellation?.Cancel();
}
