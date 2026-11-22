using Veyra.Core.Models;
using Veyra.Display.Interop;

namespace Veyra.Display.Monitors;

/// <summary>
/// The single code path that changes a display mode. It validates with CDS_TEST first,
/// applies with CDS_UPDATEREGISTRY, then re-reads the mode to confirm what Windows
/// actually committed instead of assuming success.
/// </summary>
public static class DisplayModeApplier
{
    public static OperationResult Apply(DisplayInfo display, DisplayMode mode)
    {
        if (mode.Width < 320 || mode.Height < 200)
        {
            return OperationResult.Invalid("That resolution is below the supported minimum.");
        }

        var devMode = DisplayNative.CreateDevMode();
        if (!DisplayNative.EnumDisplaySettingsEx(display.GdiDeviceName, DisplayNative.EnumCurrentSettings, ref devMode, 0))
        {
            return OperationResult.Disconnected($"{display.FriendlyName} is no longer available.");
        }

        devMode.PelsWidth = (uint)mode.Width;
        devMode.PelsHeight = (uint)mode.Height;
        devMode.DisplayFrequency = (uint)mode.RefreshRate;
        devMode.BitsPerPel = (uint)mode.BitsPerPixel;
        devMode.Fields = DisplayNative.DmPelsWidth | DisplayNative.DmPelsHeight |
                         DisplayNative.DmDisplayFrequency | DisplayNative.DmBitsPerPel;

        if ((int)devMode.DisplayOrientation != mode.Orientation)
        {
            devMode.DisplayOrientation = (uint)mode.Orientation;
            devMode.Fields |= DisplayNative.DmDisplayOrientation;
        }

        var test = DisplayNative.ChangeDisplaySettingsEx(display.GdiDeviceName, ref devMode, IntPtr.Zero, DisplayNative.CdsTest, IntPtr.Zero);
        if (test != DisplayNative.DispChangeSuccessful)
        {
            return Map(test, mode, "validating the mode");
        }

        var applied = DisplayNative.ChangeDisplaySettingsEx(display.GdiDeviceName, ref devMode, IntPtr.Zero, DisplayNative.CdsUpdateRegistry, IntPtr.Zero);
        if (applied != DisplayNative.DispChangeSuccessful)
        {
            return Map(applied, mode, "applying the mode");
        }

        var check = DisplayNative.CreateDevMode();
        if (DisplayNative.EnumDisplaySettingsEx(display.GdiDeviceName, DisplayNative.EnumCurrentSettings, ref check, 0))
        {
            var mismatched = (int)check.PelsWidth != mode.Width ||
                             (int)check.PelsHeight != mode.Height ||
                             (int)check.DisplayFrequency != mode.RefreshRate;

            if (mismatched)
            {
                return OperationResult.Failure(
                    $"Windows applied {check.PelsWidth}×{check.PelsHeight} @ {check.DisplayFrequency} Hz instead of the requested mode.");
            }
        }

        return OperationResult.Success($"{display.FriendlyName} set to {mode.Width}×{mode.Height} @ {mode.RefreshRate} Hz.");
    }

    public static OperationResult Restore(DisplayState state)
    {
        var devMode = DisplayNative.CreateDevMode();
        if (!DisplayNative.EnumDisplaySettingsEx(state.GdiDeviceName, DisplayNative.EnumCurrentSettings, ref devMode, 0))
        {
            // The device may have been removed. Never invent a substitute mode.
            return OperationResult.Disconnected(
                $"The display {state.GdiDeviceName} is not present, so its previous mode could not be restored.");
        }

        devMode.PelsWidth = (uint)state.Width;
        devMode.PelsHeight = (uint)state.Height;
        devMode.DisplayFrequency = (uint)state.RefreshRate;
        devMode.BitsPerPel = (uint)state.BitsPerPixel;
        devMode.PositionX = state.PositionX;
        devMode.PositionY = state.PositionY;
        devMode.DisplayOrientation = (uint)state.Orientation;
        devMode.DisplayFixedOutput = (uint)state.DisplayFixedOutput;
        devMode.Fields = DisplayNative.DmPelsWidth | DisplayNative.DmPelsHeight |
                         DisplayNative.DmDisplayFrequency | DisplayNative.DmBitsPerPel |
                         DisplayNative.DmPosition | DisplayNative.DmDisplayOrientation |
                         DisplayNative.DmDisplayFixedOutput;

        var status = DisplayNative.ChangeDisplaySettingsEx(state.GdiDeviceName, ref devMode, IntPtr.Zero, DisplayNative.CdsUpdateRegistry, IntPtr.Zero);
        if (status == DisplayNative.DispChangeSuccessful)
        {
            return OperationResult.Success($"{state.GdiDeviceName} restored to {state.Width}×{state.Height} @ {state.RefreshRate} Hz.");
        }

        if (status == DisplayNative.DispChangeBadMode)
        {
            return OperationResult.Unsupported(
                $"{state.GdiDeviceName} no longer supports its previous mode; nothing was changed.");
        }

        return OperationResult.Failure($"Restoring {state.GdiDeviceName} failed (code {status}).");
    }

    public static OperationResult SetHdr(DisplayInfo display, bool enabled)
    {
        var target = CcdReader.FindByGdiName(display.GdiDeviceName);
        if (target is null)
        {
            return OperationResult.Unsupported("The display does not report an active CCD target, so HDR cannot be controlled.");
        }

        if (enabled && !target.HdrSupported)
        {
            return OperationResult.Unsupported("This display and GPU combination does not report HDR support.");
        }

        var state = new CcdNative.SetAdvancedColorState
        {
            Header = new CcdNative.DeviceInfoHeader
            {
                Type = CcdNative.SetAdvancedColorStateType,
                Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<CcdNative.SetAdvancedColorState>(),
                AdapterId = target.AdapterId,
                Id = target.TargetId
            },
            Value = enabled ? 1u : 0u
        };

        var result = CcdNative.DisplayConfigSetDeviceInfo(ref state);
        if (result != 0)
        {
            return OperationResult.Failure($"Windows rejected the HDR change (code {result}).");
        }

        var after = CcdReader.FindByGdiName(display.GdiDeviceName);
        var actual = after?.HdrEnabled ?? false;
        return actual == enabled
            ? OperationResult.Success($"HDR {(enabled ? "enabled" : "disabled")} on {display.FriendlyName}.")
            : OperationResult.Failure("Windows did not report the HDR state as changed.");
    }

    private static OperationResult Map(int code, DisplayMode mode, string stage)
    {
        var label = $"{mode.Width}×{mode.Height} @ {mode.RefreshRate} Hz";
        return code switch
        {
            DisplayNative.DispChangeBadMode => OperationResult.Unsupported($"The driver does not accept {label}."),
            DisplayNative.DispChangeBadFlags => OperationResult.Invalid("The mode request used unsupported flags."),
            DisplayNative.DispChangeBadParam => OperationResult.Invalid($"Windows rejected the parameters for {label}."),
            DisplayNative.DispChangeRestart => OperationResult.Failure("Windows requires a restart to apply this change."),
            DisplayNative.DispChangeFailed => OperationResult.Failure($"Windows failed while {stage} ({label})."),
            DisplayNative.DispChangeNotUpdated => OperationResult.Failure($"The display was not updated while {stage}."),
            _ => OperationResult.Failure($"Unexpected display result {code} while {stage}.")
        };
    }
}
