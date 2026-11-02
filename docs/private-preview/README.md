# Message Workbench — private technical preview

Draft instructions for a future owner-approved, invitation-only handoff. **No sharing
is authorized yet.** Message Workbench is a local-first tool for inspecting and
editing HL7 v2, generating synthetic messages, querying, scripting and interface testing.
No account or cloud service is required. This early preview may contain defects;
a general clean-Windows-VM smoke pass is reported, but full native acceptance is incomplete.

Read [LIMITATIONS](LIMITATIONS.md) first. Use **synthetic messages only**, one
application instance, and no production systems or real patient data.

## Extract and launch

1. Use a patched **Windows 11 x64** machine/VM. Other architectures/Windows versions
   have not been accepted for this preview. No .NET SDK/runtime installation,
   Python, SQLite server or development tools are required for this self-contained build.
2. Verify the owner-supplied ZIP and trusted checksum, then Extract All into a new
   folder. Preserve every file, especially ScriptHost/, QueryHost/, ThirdPartyNotices/
   and covered-source/. Do not run inside the ZIP or copy only the executable.
3. Start **MessageWorkbench.exe**. It is unsigned; if Windows security blocks it,
   record the block and contact the owner. Do not disable or bypass protections.
4. **Help → About Message Workbench…** should show **0.1.0-beta**, Build **8f5c43cc**.
   This guide targets Candidate 02, whose archive SHA-256 is:

   ```text
   ab71b803e927d3def44090de826968e932c077769dd352afb8f4152e5a819044
   ```

The frozen ZIP still carries awaiting-owner/non-distribution wording. A future
handoff needs explicit owner permission and accompanying status clarification;
these draft instructions do not override that wording or grant permission.

## First useful tasks

- **Generate:** File → Generate Messages…; choose standalone ADT^A01, Realistic
  Synthetic, three messages and a new document. Blank Seed creates fresh data;
  seed 42 with the same settings supports replay. Review the Generation Report.
- **View:** navigate with First/Previous/Next/Last or message index/Go. Open
  View → HL7 Inspection → Segment Navigator / Field Inspector if hidden. Expand
  PID field 5 and its components; select/copy the synthetic value and path.
- **Edit:** enable the document's Edit Mode. Right-click an existing Inspector
  component → Edit Value…; confirm a synthetic change. Check both views, Undo and
  Redo. File → Save As… writes a separate file; reopen with File → Open File….
- **SQL:** View → Data & Query → SQL Query; choose Current document. Run
  `SELECT COUNT(*) FROM Messages;`. Queries use a local read-only worker over
  captured messages, not an external database.
- **Trusted C#:** View → Data & Query → Script Editor. Only run code you have
  reviewed. Help → Script API Reference provides contracts and editable starters;
  select Apply to scope, then Run. Scripts have your account's permissions.
- **Validation:** HL7 Tools → Schema Validation → Apply/Change Schema; explicitly
  choose a profile and Apply Schema. Review inline findings and coverage. Zero
  errors with Incomplete coverage is not clinical conformance certification.
- **Help:** Help → Documentation and Help → Script API Reference are bundled offline.
  Repository-only examples referenced by Help are not included downloads; start
  with generated data. Save desired work before closing; layout does not recover drafts.

[TEST-SCENARIOS](TEST-SCENARIOS.md) offers short exercises, including independent
bulk results. Use [FEEDBACK](FEEDBACK.md) to report problems manually to the owner;
no feedback service or automatic upload is provided. Reproduce with synthetic data
and review screenshots/excerpts before sharing.

Keep all third-party notices and the exact MPL resource source in covered-source/
(v251-model.json, NOTICE.md and MPL-2.0.txt). The application's sharing restriction
does not remove rights granted by those third-party licenses. No production,
clinical, de-identification or security certification is implied.
