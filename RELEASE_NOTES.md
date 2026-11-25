# Veyra 1.0.2

**Display and colour control for Windows, with a way back.**

Veyra is a calibration tool, not a game booster: it changes how a display looks, never how a
game performs, and it does not inject into games or touch anti-cheat systems.

## Highlights

- Six main pages and More: Display, Resolution, Monitor, Customize, Crosshair and Profiles,
  with startup, hotkey, provider and licence details on More.
- Display puts driver saturation and Windows gamma side by side, with brightness, contrast,
  temperature, blacks, highlights and red tint in one fine-tuning grid.
- Saved looks live on Display, and every card now has a delete action. Apply a look with one
  click or remove it without leaving the page.
- The former Library page is gone. Looks saved by older installations are preserved.
- DDC/CI monitor controls for brightness, contrast and low blue light where the monitor
  supports them, with one-click restore.
- Customize offers Default and Slate panels, panel opacity, a background picture, a soft or
  petal backdrop, and a live text-contrast check.
- Larger small text, wider saved-look cards and calmer spacing make the sidebar, page headers
  and cards easier to read.

## Changed

- The window releases its page visuals when hidden or minimised, trims its working set while
  sitting in the notification area, and decodes a background picture at a capped size. Measured
  memory fell from about 190 MB visible to about 31 MB in the tray.
- The seven pre-made presets are gone. Display lists only looks you save yourself, and rows
  left behind by older installations are removed on launch.
- The README and website now show the six-page app without Library.

## Fixed

- Saved looks can be deleted again after Library was removed; each Display card has a delete
  button instead of a separate page.
- The MSI now carries version 1.0.2, so installing it upgrades 1.0.0 or 1.0.1 instead of being
  refused as the same version.

## Download

- **`Veyra-Setup-1.0.2-x64.msi`** — per-user installer. Installs into `%LOCALAPPDATA%\Veyra`
  with Start-menu and desktop shortcuts, without an administrator prompt.
- **`Veyra-Portable-1.0.2-x64.zip`** — self-contained Windows x64 build. Extract it and run
  `Veyra.exe`; no installer or administrator rights are required.
- **`SHA256SUMS.txt`** — checksums for the release assets. Verify with
  `Get-FileHash .\Veyra-Setup-1.0.2-x64.msi -Algorithm SHA256` or the same command for the ZIP.

More at [zenou101.github.io/veyra](https://zenou101.github.io/veyra/).

## Requirements

- Windows 10 22H2 or later, x64.
- No .NET runtime install is needed for the portable build.

The binaries are unsigned, so Windows SmartScreen may warn on first launch.

## Verification

- 56 unit tests pass across Core, Storage, Display and Games.
- The six main pages were launched and inspected in the final UI; More remains available.
- The 1.0.2 MSI installed over 1.0.1: Windows Installer removed the old product and reported
  the new build at 1.0.2.0.
- Saving a look and deleting it from its Display card was verified end to end.
- Working set: about 190 MB visible and about 31 MB after hiding to the notification area.
- Verified on an AMD Radeon RX 7800 XT, Ryzen 7 5700X, 16 GB RAM, Windows 10 22H2 x64.

## Known limitations

- NVIDIA Digital Vibrance is implemented but was not tested on NVIDIA hardware in the recorded
  verification.
- Intel driver saturation is not implemented; Intel users get the Windows ramp controls.
- DDC/CI support varies by monitor and adapter; refused controls stay disabled.
- HDR capability is read but HDR switching is not offered.
- Windows 11, hybrid laptops and multi-GPU systems are not verified.
- A crosshair cannot be guaranteed above every exclusive-fullscreen game.

## Licence

Veyra is free software licensed under the **GNU General Public License v3.0 or later**.
See [LICENSE](https://github.com/zenou101/veyra/blob/main/LICENSE).

Copyright © 2026 zenou101. Veyra is not affiliated with AMD, NVIDIA, Intel or Microsoft.
