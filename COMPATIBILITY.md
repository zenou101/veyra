# Compatibility

This file separates what has been measured from what has only been designed for. If a row says
"unverified", it means it has not been run on this hardware, not that it is expected to fail.

## Verified hardware

| Component | Detail |
| --- | --- |
| GPU | AMD Radeon RX 7800 XT |
| Driver | 32.0.32029.2010 |
| OS | Windows 10 22H2, x64 |
| CPU | Ryzen 7 5700X |
| Memory | 16 GB |
| Display 1 | LS24AG32x, 1920 × 1080 @ 165 Hz, landscape (primary) |
| Display 2 | HP ZR2440w, 1200 × 1920 @ 60 Hz, portrait |
| Session | Standard user, no elevation |

## Colour paths

| Path | Status | Evidence |
| --- | --- | --- |
| Windows gamma ramp (brightness, contrast, gamma, temperature) | **verified read/write** | ramp accepted and readback changed; original restored byte-for-byte |
| AMD ADL2 driver saturation (per display, colour type 4) | **verified apply/restore, 0–300 %** | driver readback 100 → 150 → 100 through the app slider, bypass toggle and Restore original; extended 300 accepted and read back; crash path 170 → kill → 100 on restart |
| AMD ADL capability read | **verified** | probe returned caps 0x3F and saturation 0–200, step 1, neutral 100 on both mapped displays |
| AMD ADL other colour writes (brightness, contrast, hue, temperature) | **verified writable, not used by Veyra** | probe set and restored each control successfully after the mapping fix; Veyra uses the Windows ramp for these |
| Desktop capture of driver saturation | **not observable** | same output stage as gamma; ADL readback is the evidence |
| ADLX | not integrated | not attempted; ADL holds the AMD path for this release |
| NVIDIA (NVAPI) | implemented, **not hardware-tested** | `nvapi64.dll` is absent on this machine; loading, mapping, DVC read/write, range capping and restore were exercised with a mock DLL |
| Intel (IGCL) | not implemented | no Intel display adapter on this machine; Intel receives the Windows ramp controls only |
| HDR switching | unverified | capability is read and displayed; toggling is not offered |
| Desktop-capture proof of gamma | **not observable** | BitBlt reads the framebuffer, not the DAC output |
| AMD saturation exclusive-fullscreen coverage | **expected, not re-tested in a game** | driver output control, not a compositor effect; no game was launched for this release |

## Displays

| Feature | Status |
| --- | --- |
| Enumeration (name, adapter, resolution, refresh, orientation, HDR support) | verified in the running app |
| Mode list and refresh-rate apply | verified: 1920×1080 applied at 60 Hz with readback showing 60 Hz (user-confirmed test) |
| 15-second confirmation and rollback | guard rollback verified: unconfirmed change reverted to 165 Hz by the guard's own timeout; the dialog's Keep/Revert buttons were not clicked |
| Guard snapshot checksum refusal | verified by unit test |
| Display-change mutex | verified by unit test |

## Crosshair and system telemetry

| Feature | Status |
| --- | --- |
| Game-profile engine | library and tests retained; no profile page or background watcher in the 1.0.0 UI |
| Crosshair click-through window | implemented; click-through and hotkey toggle not exercised in this session |
| FPS | deliberately unavailable, stated in the UI |
| GPU utilisation and memory | implemented with PDH GPU Engine / GPU Process Memory counters, system-wide |
| GPU temperature | unavailable; no supported sensor interface answered |
| CPU and RAM | implemented (`GetSystemTimes`, `GlobalMemoryStatusEx`) |

## Not claimed

- NVIDIA hardware saturation (NVAPI is implemented but unverified) or Intel saturation (not implemented).
- Windows 11 behaviour, multi-GPU systems, hybrid laptops.
- Per-game performance effects. Veyra changes appearance only; presets are not performance guarantees.
- Code signing. The MSI and portable ZIP are unsigned.
- Exclusive-fullscreen overlay visibility and anti-cheat policy compliance for any specific game.

## Resource use

- Telemetry sampling: one sample when Overview refreshes; there is no background sampling loop.
- No game-profile process watcher runs in the current UI.
- The overlay is created only when enabled and destroyed when disabled.
