namespace HL7Workbench.Workspace;

/// <summary>Whether inspection describes the current working revision or is presentation-only history.</summary>
public enum DocumentInspectionState { Current, Pending, Stale, Empty, Unavailable }
