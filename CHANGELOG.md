# Changelog

All notable changes to Veyra. The project follows semantic versioning from 1.0.0.

## [Unreleased]

### Added

- NVIDIA Digital Vibrance Control through NVAPI: per-display enumeration, live apply, readback
  verification, one-click restore and crash recovery. The UI maximum comes from the NVIDIA
  driver's own DVC range rather than assuming AMD's extended ceiling.

### Changed

- The saturation slider's maximum is supplied by the active driver (300 % on the verified AMD
  path, typically 200 % on NVIDIA). The badge identifies AMD or NVIDIA, and the description states
  which driver is active and whether the extended range is available.
- Overview and Settings no longer describe saturation as AMD-only.
- Sidebar rebuilt with grouped navigation, bold labels, and an ocean-blue selected state. The Local session card and sidebar licence footer were removed.
- Overview trimmed to the driver-colour hero, with a new GPU support panel covering NVIDIA, AMD Radeon (including integrated graphics) and Intel.
- Removed the Games sidebar page, the live Running Now process list and the automatic game-profile
  watcher. Every colour slider on the Colours page applies continuously to the selected display
  instead of waiting for **Apply to display**.
- Removed the unimplemented diagnostics-logging and compact-sidebar settings, the nonfunctional
  settings toggles and the profile-activation hotkey row. Added screen-reader names to sidebar,
  display and preset items.
- Replaced the remaining violet accents, hero gradient, ambient glows and app icon with the ocean-blue palette.
- Fixed the release workflow's changelog input and the remaining xUnit analyzer warning.

### Known gaps

- NVIDIA NVAPI was implemented but not hardware-tested: this machine has no NVIDIA GPU or
  `nvapi64.dll`. The probe reports that absence cleanly. The interop path was exercised with a
  mock NVAPI DLL, covering loading, GDI mapping, DVC read/write, driver-maximum capping and restore.
- Release artifacts were not rebuilt for this unreleased change.

## [1.0.0] — 2026-11-22

First full release. Unsigned, Windows 10 22H2 x64, verified on AMD RX 7800 XT.

### Added

- WPF shell with a glow-lit token theme, custom title bar, 246 px sidebar and eight pages:
  Overview, Displays, Colours, Games, Crosshair, Performance, Hotkeys, Settings.
- Custom Veyra app icon: a glowing violet V inside a calibration orbit, generated as a
  multi-resolution ICO and embedded in the executable, window, taskbar, tray, sidebar, Start-menu
  shortcut and Apps & Features entry.
- Display enumeration via QueryDisplayConfig and EnumDisplaySettingsEx, with refresh-rate apply
  guarded by CDS_TEST and post-change verification.
- Out-of-process recovery guard (`Veyra.DisplayGuard`) with atomic checksummed snapshots, its own
  timeout, named mutex and confirmation event.
- 15-second confirmation dialog with Keep/Revert, used for every display-mode change.
- Windows gamma ramp provider with persisted originals and startup crash recovery.
- AMD ADL diagnostic probe: real capability reads and per-control write tests.
- Presets (seven built-ins), preset application and restore.
- Game profiles: process watcher, priority resolution, colour-only auto-apply, restore on exit.
- Real per-display AMD driver saturation (ADL2 colour type 4), with the adapter/display mapping
  taken from VibranceGUI v2's approach: match the ADL adapter named after the GDI display, enumerate
  its displays and write only a connected, mapped display. 0–300 % (100 neutral, 200 driver max,
  300 verified extended ceiling), live slider, one-click bypass and presets. The exact native driver value is captured before the first write,
  persisted, and restored by Restore original, the toggle, or at the next launch after a crash.
  Verified by driver readback: 100 → 150 → 100, and 170 → kill → 100 after restart.
- Game-profile JSON export/import on the Games page (`veyra.game/1`), with strict schema, unknown
  member, size, range and executable-name validation; imports always receive a fresh id.
- Click-through crosshair overlay with shape, size, thickness, gap, colour, opacity and outline.
- Telemetry: system-wide GPU utilisation and dedicated memory (PDH), CPU, RAM; FPS explicitly
  unavailable.
- Global hotkeys with parsing, conflict detection and registration; tray icon with real actions.
- SQLite storage with versioned migrations, atomic JSON stores, validated settings.
- 53 xunit tests across Core, Storage, Display and Games, including CCD struct-size guards,
  game-profile JSON validation and saturation percent-to-driver-range mapping.
- `tools/Veyra.GuardHarness`: applies one refresh-rate change and lets the guard revert it, used to
  verify rollback on real hardware.
- Portable self-contained packaging script with SHA-256 checksums, and a per-user WiX 6 MSI
  (`Veyra-Setup-1.0.0-x64.msi`) generated from the published files.

### Changed

- Redesigned the shell and all eight pages around a glow-lit visual system: ambient background
  light, glass cards with hover depth and entrance fades, gradient page titles and primary actions,
  themed scrollbars, progress meters and selection chips, plus animated page transitions, nav and
  button hover motion, toggle slides and slider-thumb feedback. The Overview dashboard was rebuilt
  around the live colour pipeline and verified configuration.
- Replaced the ramp-based vibrance experiment with real driver saturation. Brightness, contrast,
  gamma and temperature are untouched ramp controls; saturation is mapped onto the driver's own
  min/neutral/max/step range. The gamma probe now gates on exact ramp equality instead of a
  screen-luma comparison that changes when window content changes.

### Fixed

- Heap corruption (exit `0xC0000374`) on roughly half of all launches: the managed
  `DISPLAYCONFIG_PATH_TARGET_INFO` omitted the `rotation` and `scaling` fields, so each path record
  was 64 bytes where Windows writes 72. `QueryDisplayConfig` overwrote the managed heap. The layout
  now matches the Windows 10 SDK (10.0.26100) and struct-size tests guard it.

### Findings recorded during development

- ADL saturation writes returned status −1 only because the probe addressed adapter 0 with a parsed
  GDI display number. The driver exposes one adapter per display path; addressing each adapter's
  mapped display (`LogicalAdapterIndex == adapter`, connected and mapped flags) makes reads and
  writes succeed. This is the same targeting VibranceGUI v2 uses.
- ADL's earlier in-process heap corruption came from allocating its structures with
  `Marshal.AllocHGlobal` while ADL frees through the C runtime. The callback now uses `ucrtbase`
  malloc; the app ran through live slider, toggle, restore and crash-recovery tests without a crash.
- GDI desktop capture cannot observe gamma-ramp or driver-output colour changes (BitBlt reads
  framebuffer values), so driver readback is the recorded evidence for saturation.

### Known gaps

- The 15-second dialog's Keep/Revert buttons were not clicked through. The guard path itself was
  verified live: a 165→60 Hz change on DISPLAY1 was reverted to 165 Hz by the guard's timeout.
- HDR toggle not offered.
- NVIDIA and Intel saturation unimplemented; they remain detection-only.
- Crosshair click-through not exercised in this session.
- MSI built but not installed; only the portable build was run.
- Game-profile JSON import/export and basic preset save/delete are implemented; toast
  notifications and the diagnostics panel are not.
- Not code-signed.
