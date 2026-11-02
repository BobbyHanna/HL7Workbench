# First task: generate, inspect and save synthetic HL7

Use an owner-approved complete Windows x64 extraction. This exercise needs no
repository, sample download, installed SDK or internet connection. Initial private
preview use is **synthetic-only**; do not open real patient data.

1. Verify the owner's supplied archive checksum, extract the **whole ZIP**, and
   launch **MessageWorkbench.exe**. Keep ScriptHost/, QueryHost/ and all notices.
   A source build uses its existing desktop executable instead.
2. Open **Help → About Message Workbench…** and note Version and Build for feedback.
   Build may say unavailable when reliable source metadata was not supplied.
3. Choose **File → Generate Messages…**, then standalone **ADT^A01**, quantity **3**,
   **Realistic Synthetic**, and a **new document**. Leave Seed blank for new data or
   enter **42** for replay; keep the other defaults. Choose Generate.
4. The generated batch opens in an unsaved document tab. Use First/Previous/Next/Last
   or the message index/Go controls to inspect all three messages. Generation Report
   shows the actual seed; it does not establish clinical conformance.
5. If hidden, open **View → HL7 Inspection → Segment Navigator** and **Field Inspector**.
   Select PID, expand field 5 and its first repetition, then select component 2
   (given name). Read the raw encoded value and structural path.
6. Enable the owning document's **Edit Mode** if needed. Right-click the existing
   component and choose **Edit Value…**. Enter the synthetic given name **Rowan**,
   review and confirm. Raw text and Inspector should agree after refresh.
7. Try **Undo**, then **Redo**. The document remains an unsaved working copy;
   turning Edit Mode off does not discard changes. Structured editing targets existing
   values; it does not create absent fields or change MSH separators.
8. Right-click the component and choose **List Unique Values…**. Use **Active file**,
   Analyze, and inspect counts/values. This read-only action does not edit the batch;
   close the review when done.
9. On the same component, choose **Search & Replace…**. Find value should contain its
   current complete raw value. Enter **Morgan** in Replace with. Keep Active file
   and the selected PID occurrence, then **Analyze / Preview**.
10. Review the exact raw matches, affected messages and every proposed change.
    Other generated names need not match. Before Apply the input tab and its Inspector
    must still show Rowan. Confirm the preview, then **Apply all to new document**.
11. Exactly one separate **Bulk Result.hl7** tab opens and activates. Raw text and
    Inspector show Morgan for the changed message. All three messages remain, including
    messages outside the modification scope. Switch back to the starting generated
    tab: its values/history are unchanged by Apply. Return to the result.
12. Choose **File → Save As…**, select a new destination/name and explicitly save the
    result. Reopen that saved file with **File → Open File…** to check the persisted
    output. Nothing was automatically written to a source file or transmitted.
13. Open **Help → Documentation** to search the bundled guides offline. Use the
    existing Script API Reference for trusted C# contracts. No repository is required.

## Know which document you are using

- An **original source** is the captured input bytes of an opened file. Its
  **editable document** has separate working text/history; authorized Save can replace
  the file, while Save As chooses another destination.
- A **generated document** is new unsaved synthetic output. In this exercise it is
  the input to the bulk operation; it is not an existing original file on disk.
- An **independent bulk result** copies the complete captured working dataset and
  applies only the reviewed changes. It does not edit its input document. Its dirty
  indicator denotes new unsaved output; Apply does not add dirty state to the input.
- A **saved output file** exists only after an explicit successful save/export.
  Closing or changing a tab does not automatically save it.

## Cancel or recover

Changing Find/Replace parameters invalidates preview and confirmation; analyze again.
An intentionally empty Replace with clears matching existing values. Matching uses
exact, case-sensitive raw HL7 encoded values, not substrings or decoded text.

Cancel before confirmation to retain the input without output. A rejected/canceled
save retains working text/history and does not publish a partial replacement.
Dirty close offers Save / Discard / Cancel. If the result cannot open, the review
reports no result retained; review and confirm again to retry.

Large/ambiguous messages, missing nodes, unsafe delimiters or unsupported encoding
can disable editing. Inspect the status and Help's limits instead of bypassing a guard.
Keep desired work saved before exit; placement restoration does not recover drafts.

Use the supplied FEEDBACK.md template privately with a synthetic reproduction.
Do not send real HL7, PHI, credentials, endpoint secrets, production filenames,
temporary storage, raw logs or crash dumps. Review screenshots manually. Plaintext
snapshots and clipboard history can retain content; local-first is not a privacy or
de-identification guarantee. Trusted C# has your account permissions and TCP/MLLP
is unencrypted and unauthenticated. No clinical/production use is approved.
