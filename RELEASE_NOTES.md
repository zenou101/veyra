# Veyra 1.0.0

**Display and colour control for Windows, with a way back.**

Veyra is a calibration tool, not a game booster: it changes how a display looks, never how a
game performs, and it does not inject into games or touch anti-cheat systems.

## Highlights

- A compact navigation sidebar for eight pages: Display, Library, Resolution, Monitor,
  Customize, Crosshair, Profiles, and More.
- The original Veyra identity is intact: dark navy surfaces, cyan accents, glass cards,
  gradient titles, original buttons, sliders and toggles, hover motion, glows and rounded
  corners.
- Display puts driver saturation and Windows gamma side by side, with brightness, contrast,
  temperature, blacks, highlights and red tint in one fine-tuning grid, plus a scene preset
  panel.
- Library and Profiles keep every built-in scene and saved look together.
- DDC/CI monitor controls for brightness, contrast and low blue light where the monitor
  supports them, with one-click restore.
- Customize offers Default and Slate panels, panel opacity, a background picture, a soft or
  petal backdrop, and a live text-contrast check.

## Fixed

- Customize panel opacity and mode changes now update every card.
- Resolution keeps the selected display and refreshes its current mode after applying a change.
- Crisper UI text and lower rendering overhead from removing card-wide effects and blurred
  background surfaces.
- Driver badges and other dynamic labels no longer collide with adjacent text.

## Download

- **`Veyra-Setup-1.0.0-x64.msi`** — per-user installer. Installs into `%LOCALAPPDATA%\Veyra`
  with a Start-menu shortcut, without an administrator prompt.
- **`Veyra-Portable-1.0.0-x64.zip`** — self-contained Windows x64 build. Extract it and run
  `Veyra.exe`; no installer or administrator rights are required.
- **`SHA256SUMS.txt`** — checksums for the release assets. Verify with
  `Get-FileHash .\Veyra-Setup-1.0.0-x64.msi -Algorithm SHA256` or the same command for the ZIP.

## Requirements

- Windows 10 22H2 or later, x64.
- No .NET runtime install is needed for the portable build.

The binaries are unsigned, so Windows SmartScreen may warn on first launch.

## Verification

- 57 unit tests pass across Core, Storage, Display and Games.
- All eight pages were launched and captured in the final UI.
- Verified on an AMD Radeon RX 7800 XT, Ryzen 7 5700X, 16 GB RAM, Windows 10 22H2 x64.

## Known limitations

- NVIDIA Digital Vibrance is implemented but was not tested on NVIDIA hardware.
- Intel driver saturation is not implemented; Intel users get the Windows ramp controls.
- DDC/CI support varies by monitor and adapter; refused controls stay disabled.
- HDR capability is read but HDR switching is not offered.
- Windows 11, hybrid laptops and multi-GPU systems are not verified.
- A crosshair cannot be guaranteed above every exclusive-fullscreen game.

## Licence

Veyra is free software licensed under the **GNU General Public License v3.0 or later**.
See [LICENSE](https://github.com/zenou101/veyra/blob/main/LICENSE).

Copyright © 2026 zenou101. Veyra is not affiliated with AMD, NVIDIA, Intel or Microsoft.
