# Architecture

Veyra is a WPF application on .NET 10 with a strict separation between UI, domain rules, storage,
display control and colour control. Every layer is testable without a monitor where possible.

## Projects

| Project | Responsibility |
| --- | --- |
| `Veyra.Core` | Models, ramp maths, profile matching and resolution, hotkey parsing, JSON profile schema, interfaces. No Windows API calls. |
| `Veyra.Storage` | SQLite database with versioned migrations, repositories, atomic JSON stores, settings validation. |
| `Veyra.Display` | CCD enumeration, `EnumDisplaySettingsEx` modes, DEVMODE application with `CDS_TEST`, HDR read, recovery snapshots, guard session, display-change mutex. |
| `Veyra.Colour` | Windows gamma ramp provider, ramp maths, crash-recovery snapshot provider, NVIDIA/Intel detection, AMD ADL diagnostics. |
| `Veyra.Games` | Profile matching and process-watcher library. It has tests, but no game-profile page is exposed in 1.0.0. |
| `Veyra.Telemetry` | PDH GPU counters, `GetSystemTimes` CPU, `GlobalMemoryStatusEx` memory. |
| `Veyra.Overlay` | Click-through layered crosshair window, geometry, monitor bounds. |
| `Veyra.UI` | WPF shell, design system, six pages, tray, hotkeys, confirmation dialog. |
| `Veyra.DisplayGuard` | Separate watchdog process that reverts a display change if the app dies or does not confirm. |
| `Veyra.ColourProbe` | Console diagnostic for AMD ADL capabilities and write tests. |
| `Veyra.GammaProbe` | Console proof harness for ramp writes and desktop capture. |
| Tests | xunit projects for Core, Storage, Display and Games. |

There is deliberately no C++ project. CCD, GDI gamma, ADL, `RegisterHotKey`, layered windows and PDH
are all reachable through P/Invoke, so a native toolchain would add a build dependency without adding
capability. `app.manifest` declares PerMonitorV2 DPI awareness.

## Display changes and the guard

1. Capture the exact current state (`DisplayState`: CCD path, resolution, refresh rate, orientation,
   position, flags) per display.
2. Write an atomic, checksummed snapshot (`DisplaySnapshotStore`): temp file, then rename.
3. Start `Veyra.DisplayGuard.exe` as a separate process, passing the snapshot path, the app PID, a
   named confirmation event and a timeout.
4. Validate the new mode with `CDS_TEST`, apply it, and verify by re-reading the mode.
5. The guard waits for the confirmation event, the app's exit, or its own timeout. If it loses that
   race it re-applies the snapshot under a named mutex. It never invents a mode: if the snapshot is
   unreadable, fails its checksum, or a restore fails, it logs and refuses.

The UI shows a 15-second countdown with explicit Keep/Revert. Profile activation never changes the
refresh rate; that requires the dialog every time.

## Colour

`WindowsGammaColourProvider` builds a 3×256 ramp and calls `SetDeviceGammaRamp`. The raw original
ramp is captured and persisted before the first write. On startup, any dirty recovery snapshot is
re-applied, so a crash or an unclean exit is recoverable.

Saturation is separate from the gamma ramp. On AMD, `AdlColourProvider` uses ADL2 colour type 4,
resolves the adapter and mapped display for each Windows display, writes the exact native value,
reads it back and persists the original. The verified RX 7800 XT driver accepts 0–300 % (100 neutral).
On NVIDIA, `NvidiaColourProvider` uses NVAPI Digital Vibrance and derives its range from the driver;
that path is implemented but was hardware-tested only with a mock NVAPI library. Intel has no
driver saturation path in 1.0.0, so it keeps the Windows ramp controls only.

The target is selected per display. Veyra never injects into a game, hooks rendering or interacts
with anti-cheat systems. Driver colour can still be overwritten later by Radeon Software or another
third-party tool; Veyra only restores the exact value it captured.

Desktop-capture verification was attempted with BitBlt: the ramp changed and read back differently,
but the captured framebuffer did not change. This is expected — GDI screen capture reads the
framebuffer, not the post-LUT output — so visual confirmation of gamma can only come from the user's
eyes. The limitation is recorded rather than papered over.

## State and storage

- SQLite: presets, plus the retained game-profile schema, migrations v1/v2.
- JSON, atomic writes: settings, hotkeys, colour recovery snapshot, display guard snapshots.
- Location: `%LOCALAPPDATA%\Veyra`. Logs in `logs\`.
- All user input is validated at the edge (settings clamping, hotkey parsing, profile JSON schema).

## Threading

- UI work on the WPF dispatcher.
- Telemetry sampling on a background task with cancellation.
- Colour application serialised by a `SemaphoreSlim`; display changes serialised by a named mutex.
- No background game watcher runs in the 1.0.0 UI; the library keeps its timer-based watcher for future use.

## Design system

Dark token theme (background `#0B0C0F`, surface `#13151A`, accent `#8B7CF6`), Segoe UI Variable type
scale, 12 px card radii, 8 px control radii, 210 px sidebar, custom title bar via `WindowChrome`, and
reusable styles for buttons, toggles, sliders, chips, inputs and progress bars. Disabled controls
carry the driver's actual reason rather than a blank state.
