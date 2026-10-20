# Approved application icon

Native Windows acceptance of the optimized icon passed per the user on 2026-10-20,
including the Stage 9 correction pass. It is part of the authorized local Stage 9
checkpoint after fresh verification. Earlier pending notes below are historical;
the checklist remains useful for future regressions. Original artwork is unchanged.

The user-approved M/W monogram is preserved unchanged in
`assets/branding/workbench-icon-source.png` (1254 × 1254). SHA-256:
`A47EFCAD4AC2C7E19CB296A280DFA02B2AABFB11DFE07DCFF9EB4951B3651095`.
It is separate from the accepted Stage 8D checkpoint.

## Reproduce the derivatives

Use Python with Pillow 12.3.0 and NumPy 2.3.5, from the repository root:

```powershell
python build/CreateBrandingAssets.py --preview artifacts/stage9/icon-preview.png
```

The generator checks the approved source hash before processing and after writing.
It removes only edge-connected dark exterior pixels, protected by the existing
blue outline. It preserves the navy interior, colors, lettering, message bars,
orange accent, border and curvature. No generative artwork or reconstruction is
involved. The shape bounds are `(96, 90, 1156, 1141)`; the centered square crop is
`(80, 69, 1172, 1161)` with approximately 1.5% transparent padding per side. Uniform
Lanczos downsampling preserves geometry without stretching or sharpening.

Native acceptance found the original small derivatives undersized. The revised
16/24/32-pixel path tightens outer framing, extracts and enlarges the **existing
monogram pixels** to 89% of the canvas, and simplifies the navy interior for contrast.
At 16/24 only, the two tiny blue message bars are omitted while the original orange
accent remains. The 32-pixel version retains all three bars. The cyan/blue palette,
M/W silhouette and curved border remain recognizable. This is the explicitly
authorized small-size derivative, not replacement artwork. Lanczos edge ringing at
alpha 1–3/255 is cleared to prevent faint corner halos. The 512 PNG and 48/64/128/256
frames retain the previous artwork byte-for-byte.

Outputs:

Normal .NET restore/build uses the retained derivatives and does not require Python.

| Asset | Purpose |
| --- | --- |
| `src/HL7Workbench.Desktop/Assets/workbench-icon.png` | Unchanged 512 × 512 RGBA master |
| `src/HL7Workbench.Desktop/Assets/workbench.ico` | Executable and Avalonia window resource: optimized 16/24/32 plus original 48/64/128/256, all 32-bit RGBA PNG frames |
| Optional ignored preview | Actual-size frames on light/dark backgrounds, with 2× nearest-pixel views of 16/24/32 |

The project `ApplicationIcon` embeds the ICO into the native Windows apphost.
The previous window style loaded the 512 PNG, bypassing ICO-specific small variants.
`Branding.axaml` now loads **the ICO**, and is included by `App.axaml`. It applies one shared WindowIcon to Window
and derived types, including ordinary dialogs and floating Dock hosts. It does not
change application/assembly identity or introduce packaging. Test applications load
that same style. A window can still explicitly override its icon if needed later.

## Verification and native acceptance

Automated tests check source hash, frame sizes/PNG RGBA metadata, exact frame bytes
in the Release executable's Win32 icon resources, embedded Avalonia PNG and shared
icons on main, preferences and another derived top-level window. Re-running the
processor produced byte-identical assets with the versions above. Visual inspection
of the generated preview found recognizable 16/24-pixel monograms, intact border
and corner transparency on both light and dark backgrounds. The revised preview
shows a larger, clearer monogram with the orange accent at 16/24 pixels; larger
frames remain unchanged. Four permanent processing tests in `build/TestBrandingAssets.py`
check source/master preservation, increased small-frame foreground coverage,
transparent corners, unchanged larger frames and byte-identical regeneration:

```powershell
python -B build/TestBrandingAssets.py
```

All three Desktop branding tests check the source, exact PE frames and shared window
style/resource. The headless icon backend's Save operation is a no-op, so resource
bytes are checked directly; native Windows frame selection still requires retesting.

Initial 2026-10-20 gate, before acceptance corrections: locked restore and zero-warning Release passed; all 1,483
solution tests, 712 complete Workspace stress tests and both ScriptHost lifecycles
passed. All three branding regressions pass within the focused Desktop and full
suite runs. The current master PNG remains unchanged; the ICO below replaces the
initial `4B0D2402…ECC3F0D6` derivative. Current asset SHA-256 values are:

- PNG: `5F2E224B01ADEC16ADA35AC51FA5D9D8D63550219E45CBA3C1FD6770E17405AA`
- ICO: `93A8D699259764DE4CA9AB0F91354D8C48A09D0460415EE9BDA0202BEC6A4DF4`

Final acceptance-correction gate: locked restore, zero-warning Release, 1,504 full
solution cases, 718 complete Workspace stress cases, both ScriptHost lifecycles and
all source/fixture/whitespace checks passed. Four image-processing tests also passed,
including larger-frame retention and bit-identical generation. Small-frame foreground
coverage exceeds the old downsample by at least 15% at each requested size. Visual
QA of the current preview found clearer monograms and intact orange accents on
light/dark backgrounds, with transparent corners. Actual native taskbar/Alt+Tab,
window-manager and DPI behavior remains pending; headless tests cannot accept it.

See [the Stage 9 acceptance record](stage9-deidentification-acceptance.md) for exact
inventory, test evidence and Git status. All icon changes remain unstaged/uncommitted.

Native Windows acceptance is pending. Run the built Release executable directly
from `src/HL7Workbench.Desktop/bin/Release/net10.0/HL7Workbench.Desktop.exe`:

1. Check the main title bar, unpinned taskbar button and Alt+Tab against the approved
   artwork. Verify the icon fills its slot with a modest transparent margin.
2. Open Preferences, Script Reports and representative modal dialogs; float a Dock
   tool and document. Check consistent branding wherever Windows displays an icon.
3. Check Windows light/dark themes and 100%, 125%, 150% and 200% scaling, including
   moving between monitors. Inspect edges/corners for halos or clipped blue border.
4. If a pinned shortcut shows a previous cached icon, compare a fresh unpinned
   process/shortcut before diagnosing a resource problem. Do not change assembly
   identity or delete system caches to work around acceptance.
5. Recheck the original source hash. Record native results before accepting this
   milestone; no installer, package, publication or icon/Stage 9 commit is authorized.
