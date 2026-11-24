# Hardware verification record

Machine: Windows 10 22H2 x64, Ryzen 7 5700X, 16 GB, AMD Radeon RX 7800 XT, driver 32.0.32029.2010.
Displays: DISPLAY1 1920×1080@165 (primary), DISPLAY2 1200×1920@60 portrait. Standard user.

## Colour probe (AMD ADL)

`Veyra.ColourProbe.exe`, exit code 0.

```
loaded: True
ADL context opened.
adapter 0 display 0 -> \.\DISPLAY1 (caps 0x30, valids 0x3F)
  brightness: status=0 supported=True value=0 default=0 min=-100 max=100 step=1
  contrast: status=0 supported=True value=100 default=100 min=0 max=200 step=1
  saturation: status=0 supported=True value=100 default=100 min=0 max=200 step=1
  hue: status=0 supported=True value=0 default=0 min=-30 max=30 step=1
  temperature: status=0 supported=True value=6500 default=6500 min=4000 max=10000 step=100
adapter 0 display 1 -> \.\DISPLAY2 (caps 0x30, valids 0x3F)
  (same ranges and values)
write tests for \.\DISPLAY1:
  brightness: set 0->10  accepted=False (write -1, read 0, restore -1)
  contrast:   set 100->110 accepted=False (write -1, read 100, restore -1)
  saturation: set 100->110 accepted=False (write -1, read 100, restore -1)
  hue:        set 0->3   accepted=False (write -1, read 0, restore -1)
  temperature:set 6500->6800 accepted=False (write -1, read 6500, restore -1)
write tests for \.\DISPLAY2: same result for all five controls
```

Conclusion at the time: addressing adapter 0 with a GDI-number-derived display index made every
write return −1. That mapping was wrong, not the driver. Re-testing with each ADL adapter's own
mapped display (the VibranceGUI v2 approach) made reads and writes succeed:

```
adapter 0 display 8 -> \\.\DISPLAY1 (caps 0x3F, valids 0x3F)
adapter 1 display 12 -> \\.\DISPLAY2 (caps 0x3F, valids 0x3F)
write tests for \\.\DISPLAY1: brightness: set 0->10 accepted=True (write 0, read 10, restore 0) |
  contrast: set 100->110 accepted=True (write 0, read 110, restore 0) |
  saturation: set 100->110 accepted=True (write 0, read 110, restore 0) |
  hue: set 0->3 accepted=True (write 0, read 3, restore 0) |
  temperature: set 6500->6800 accepted=True (write 0, read 6800, restore 0)
write tests for \\.\DISPLAY2: same result for all five controls
```

Veyra uses only the saturation control. The other ADL controls remain on the Windows ramp; the
probe verified they are writable, but the app does not use them.

## Gamma probe

`Veyra.GammaProbe.exe`. The probe captured the desktop, applied brightness −30 / gamma 1.15,
re-read the ramp, captured again, restored the exact original ramp, and captured a third time.

```
read ramp: 1 gamma reported, values present: True
original ramp captured (2048 base64 chars)
desktop before:   mean RGB 21.97/23.41/25.68, luma 23.27
apply: Colour ramp applied.
ramp readback after apply: gamma 1.39, ramp changed: True
desktop after:    mean RGB 21.98/23.42/25.68, luma 23.27
restore: Original gamma ramp restored from the recovery snapshot.
desktop restored: mean RGB 21.99/23.42/25.69, luma 23.28
residual vs baseline: 0.01 luma
```

Conclusion: the ramp write is accepted and the driver returns a changed ramp, and the original was
restored with a 0.01 luma residual. The desktop capture did not change because BitBlt reads the
framebuffer rather than the post-gamma output, so software capture cannot prove gamma visually.
This is recorded as a limitation, not a success.

## Application

`Veyra.exe` (Debug and self-contained Release) starts, renders the shell and pages, and reads both
displays and their modes. Screenshot: `docs/screenshots/resolution.png`.

## AMD driver saturation (ADL2, verified)

Saturation is the AMD driver's own per-display colour control, written with
`ADL2_Display_Color_Set(..., colourType 4, value)`, not a gamma curve. The targeting comes from
VibranceGUI v2's approach (MIT): find the ADL adapter whose display name is the Windows GDI display,
enumerate that adapter's displays, and accept only an entry with `LogicalAdapterIndex == adapter` and
the connected+mapped flags set.

Driver write test with that mapping, on both displays:

```
adapter 0 display 8 -> \\.\DISPLAY1 (caps 0x3F, valids 0x3F)
adapter 1 display 12 -> \\.\DISPLAY2 (caps 0x3F, valids 0x3F)
saturation: set 100->110 accepted=True (write 0, read 110, restore 0)
```

In the running app (Debug build), the driver value was read back by a separate read-only ADL probe
after every UI action:

```
initial driver read:   saturation=100
slider 0 -> 150
driver read:           saturation=150
toggle off
driver read:           saturation=100
toggle on
driver read:           saturation=150
Restore original
driver read:           saturation=100
status: Original colour settings restored for LS24AG32x.
```

The UI maps 0–300 % onto the driver range (min 0, neutral 100, max 200, step 1) and the driver
readback confirms the actual native value, not just a UI state. The extended top of the range was
tested directly before wiring the slider:

```
saturation target 300: write=0 read=300
restored 100
```

The driver advertises 200 as its maximum but accepts and reads back 300. Veyra exposes that defined,
tested 300 % top end rather than assuming the driver accepts an arbitrary higher value. The build was
then checked end to end through the Colours page:

```
SLIDER name='' min=0 max=300 value=100
SATURATION_SET requested=300
SATURATION_AFTER value=300
PROBE READ \\.\DISPLAY1 saturation=300
PROBE READ \\.\DISPLAY2 saturation=100
Restore original
PROBE READ \\.\DISPLAY1 saturation=100
```

The `300` shown above is the native value returned by driver readback, not just the UI label.

Crash recovery was exercised by killing the process while 170 % was applied (no clean shutdown):

```
set slider 170; driver read 170; recovery file present
kill app;       driver read 170; recovery file present
launch app;     driver read 100; recovery file removed
```

The exact pre-change native value is written to `%LOCALAPPDATA%\Veyra\adl-saturation-recovery.json`
before the first change and is restored on the next start, so a crash cannot leave the desktop vivid.

Limitations: the AMD path is the hardware-verified one. GDI desktop capture cannot see the driver's
output stage (BitBlt reads the framebuffer), so visual confirmation still needs the user's eyes; the
driver readback is the recorded evidence. Exclusive-fullscreen coverage is expected because this is
a GPU output control rather than a compositor effect, but no game was launched to re-test it in this
release.

## NVIDIA NVAPI saturation

`Veyra.ColourProbe --nvidia` was run on this AMD-only machine. It reports the absence cleanly instead
of crashing or offering a dead control:

```
NVIDIA available: False
nvapi64.dll was not found. Install the NVIDIA display driver to use digital vibrance.
```

The NVIDIA provider is implemented against NVAPI's Digital Vibrance Control (`NvAPI_GetDVCInfoEx`
function `0x0E45002D`, `NvAPI_SetDVCLevelEx` function `0x4A82C2B1`), maps displays by the GDI name
reported by `NvAPI_GetAssociatedNvidiaDisplayName`, verifies every write by reading the native DVC
value back, and stores the exact original for restore and crash recovery. Its UI maximum is derived
from the driver's reported range instead of assuming AMD's 300 % extended ceiling is available.

The native interop was exercised against a temporary mock `nvapi64.dll` exporting the same function
IDs and DVC layout. This is not an NVIDIA driver, but it proves Veyra's DLL loading, display-name
mapping, structure version, native write/read and restore path:

```
NVIDIA available: True
NVIDIA NVAPI digital vibrance is available for 2 display(s).
CAP \\.\DISPLAY1 supported=True range=0-200 (native 0..100, neutral 50)
TEST before=100 apply=150 % (native 75) after=150
     capped=200 % (native 100); this display's driver maximum is 200 %.
     restore=Original NVIDIA Digital Vibrance restored. final=100
```

That mock is not part of the app or release. No NVIDIA GPU was available here, so this path must
still be tested on real NVIDIA hardware before it can be called verified.

## Guard-protected refresh change (verified)

Run with the user's explicit confirmation on DISPLAY1 (LS24AG32x), through
`tools/Veyra.GuardHarness` — the same `DisplayGuardSession` path the UI uses, with the change
intentionally left unconfirmed.

```
display: LS24AG32x (\\?\DISPLAY#SAM71DA#...)
before:  1920 x 1080 165 Hz
guard:   started for pid 10776, timeout 25s, token fab59d806327484b8a8b20b023c3b2b9
apply:   1920 x 1080 @ 60 Hz
apply:   Success: LS24AG32x set to 1920x1080 @ 60 Hz.
after apply: 60 Hz
waiting: 33s without confirming, so the guard's own timeout must revert the change.
```

The harness console was cut off before it printed its final line, so the result is taken from the
guard's own log and the live display state:

```
2026-11-22T14:27:38.4746944+08:00 Reverting because confirmation timed out.
2026-11-22T14:27:38.9000273+08:00 Restored \\.\DISPLAY1 to 1920x1080@165.
```

`Win32_VideoController` afterwards: 1920 × 1080, **165 Hz**. The monitor went to 60 Hz and the
separate guard process restored 165 Hz with no confirmation, so the unconfirmed-change rollback is
verified. The UI countdown dialog itself was not clicked through; it calls the same guard session.

## Not exercised

- The 15-second confirmation dialog's Keep/Revert buttons.
- End-to-end Notepad profile activation applying colour.
- Crosshair click-through and hotkey toggle.
- MSI installation.
