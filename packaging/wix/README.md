# Installer sources

Veyra ships an MSI built with WiX 6. It installs per user into
`%LOCALAPPDATA%\Veyra`, so no elevation prompt appears.

## Build the MSI

```
dotnet tool install --global wix --version 6.0.1
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
powershell -ExecutionPolicy Bypass -File scripts/harvest-wix.ps1
wix build -arch x64 -o dist/Veyra-Setup-1.0.0-x64.msi `
    packaging/wix/Product.wxs packaging/wix/Components.wxs
```

`scripts/harvest-wix.ps1` rewrites `Components.wxs` from the published files in
`artifacts/app` using repository-relative paths. The generated file is ignored by Git;
do not hand-edit or commit it.

## Notes

- The installer is unsigned. Windows SmartScreen will warn.
- Uninstalling removes the program files and the Start-menu shortcut. User settings, presets and
  the colour recovery snapshot in `%LOCALAPPDATA%\Veyra` are left in place so a reinstall keeps your
  configuration; delete that folder to remove everything.
- `Veyra.DisplayGuard.exe` is installed next to `Veyra.exe`; display changes are refused when it is
  missing.
- Installation of the generated MSI was not exercised in this release; the portable ZIP is the
  verified artifact.
