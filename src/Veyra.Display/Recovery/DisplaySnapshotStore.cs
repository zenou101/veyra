using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Veyra.Core.Models;
using Veyra.Storage;

namespace Veyra.Display.Recovery;

/// <summary>
/// Atomic, checksummed snapshot of the exact display configuration before a change.
/// The guard refuses a snapshot whose contents do not match the recorded checksum.
/// </summary>
public static class DisplaySnapshotStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ComputeChecksum(DisplayRecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var payload = new StringBuilder();
        payload.Append(snapshot.SchemaVersion).Append('|').Append(snapshot.Token).Append('|');
        foreach (var display in snapshot.Displays)
        {
            payload.Append(display.Id).Append(';')
                .Append(display.GdiDeviceName).Append(';')
                .Append(display.Width).Append(';')
                .Append(display.Height).Append(';')
                .Append(display.RefreshRate).Append(';')
                .Append(display.BitsPerPixel).Append(';')
                .Append(display.Orientation).Append(';')
                .Append(display.PositionX).Append(';')
                .Append(display.PositionY).Append(';')
                .Append(display.DisplayFixedOutput).Append(';')
                .Append(display.DisplayFlags).Append('|');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString()));
        return Convert.ToHexString(hash);
    }

    public static void Save(string path, DisplayRecoverySnapshot snapshot)
    {
        var sealedSnapshot = snapshot with { Checksum = ComputeChecksum(snapshot) };
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(sealedSnapshot, Options));
    }

    public static bool TryLoad(string path, out DisplayRecoverySnapshot? snapshot, out string error)
    {
        snapshot = null;
        error = string.Empty;

        if (!AtomicFile.TryReadAllText(path, out var json))
        {
            error = "The recovery snapshot could not be read.";
            return false;
        }

        DisplayRecoverySnapshot? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<DisplayRecoverySnapshot>(json, Options);
        }
        catch (JsonException ex)
        {
            error = "The recovery snapshot is not valid JSON: " + ex.Message;
            return false;
        }

        if (parsed is null || parsed.Displays.Count == 0)
        {
            error = "The recovery snapshot is empty.";
            return false;
        }

        var expected = ComputeChecksum(parsed);
        if (!string.Equals(expected, parsed.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            error = "The recovery snapshot failed its integrity check.";
            return false;
        }

        snapshot = parsed;
        return true;
    }
}
