# Building Veyra

## Prerequisites

- Windows 10 22H2 or later, x64.
- .NET SDK 10.0.404 or later. The repository was built with a user-scoped SDK extracted to
  `%USERPROFILE%\.dotnet`; use that `dotnet.exe` if the machine has no system SDK.
- No Visual Studio requirement; the WPF SDK is enough.
- Network access for NuGet restore on the first build.

## Build and test

```
dotnet build Veyra.slnx -c Debug
dotnet test Veyra.slnx
```

The solution is `Veyra.slnx` (the .NET 10 solution format). All projects target `net10.0-windows`
and `x64`.

## Run from source

```
dotnet run --project src/Veyra.UI/Veyra.UI.csproj
```

`Veyra.DisplayGuard.exe` is required for display-mode changes. It is produced by building
`src/Veyra.DisplayGuard`. The packaging script copies it next to the app; without it Veyra refuses the
change rather than applying one it cannot roll back.

## Diagnostics

```
dotnet run --project src/Veyra.ColourProbe/Veyra.ColourProbe.csproj
dotnet run --project src/Veyra.ColourProbe/Veyra.ColourProbe.csproj -- --read
dotnet run --project src/Veyra.ColourProbe/Veyra.ColourProbe.csproj -- --nvidia
dotnet run --project src/Veyra.GammaProbe/Veyra.GammaProbe.csproj
```

The colour probe writes nothing permanently: it performs one small write per control and restores the
original integer. `--read` reports the current AMD saturation values; `--nvidia` reports whether
NVAPI Digital Vibrance is available. The gamma probe captures the desktop, applies a ramp, reads it
back, restores the original and reports measured deltas.

## Package

```
pwsh ./scripts/package.ps1
```

This publishes the app and the guard self-contained for `win-x64`, copies the guard into the app
folder, writes `dist/Veyra-Portable-1.0.0-x64.zip` and `dist/SHA256SUMS.txt`. It does not sign
anything.

## Installer

WiX 6 produces a per-user MSI into `%LOCALAPPDATA%\Veyra` (no elevation):

```
dotnet tool install --global wix --version 6.0.1
powershell -executionpolicy bypass -file scripts/package.ps1
powershell -executionpolicy bypass -file scripts/harvest-wix.ps1
wix build -arch x64 -o dist/Veyra-Setup-1.0.0-x64.msi `
    packaging/wix/Product.wxs packaging/wix/Components.wxs
```

`harvest-wix.ps1` regenerates `packaging/wix/Components.wxs` from `artifacts/app`; run it after every
publish. See `packaging/wix/README.md` for what the installer does and does not remove.
