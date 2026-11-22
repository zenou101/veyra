using System.Text.Json;
using Veyra.Core;
using Veyra.Core.Models;

namespace Veyra.Colour.AMD;

public sealed partial class AdlColourProvider
{
    public Task<OperationResult> ApplyAsync(string displayId, ColourSettings settings, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var target = FindTarget(displayId);
            if (target is null || _session is null)
            {
                return OperationResult.DriverUnavailable("The AMD driver runtime is not available for this display.");
            }

            var failures = new List<string>();
            var applied = 0;

            foreach (var type in new[]
                     {
                         AdlNative.ColorSaturation, AdlNative.ColorBrightness, AdlNative.ColorContrast,
                         AdlNative.ColorHue, AdlNative.ColorTemperature
                     })
            {
                var control = MapControl(type);
                var requested = settings.Get(control);
                if (requested is null) continue;

                var status = _session.GetColour(target.AdapterIndex, target.DisplayIndex, type,
                    out var current, out _, out var min, out var max, out var step);
                if (status != AdlNative.AdlOk || max <= min)
                {
                    failures.Add($"{control} is not supported by the driver");
                    continue;
                }

                lock (_sync)
                {
                    _originals.TryAdd((target.AdapterIndex, target.DisplayIndex, type), current);
                }

                var value = (int)Math.Round(Math.Clamp(requested.Value, min, max));
                var write = _session.SetColour(target.AdapterIndex, target.DisplayIndex, type, value);
                if (write != AdlNative.AdlOk)
                {
                    failures.Add($"{control} was rejected (ADL status {write})");
                    continue;
                }

                var readStatus = _session.GetColour(target.AdapterIndex, target.DisplayIndex, type,
                    out var after, out _, out _, out _, out _);
                if (readStatus != AdlNative.AdlOk || after != value)
                {
                    failures.Add($"{control} did not read back as requested");
                    continue;
                }

                applied++;
            }

            if (failures.Count == 0)
            {
                return applied == 0
                    ? OperationResult.Success("No AMD driver controls were part of this preset.")
                    : OperationResult.Success($"Applied {applied} AMD driver control(s).");
            }

            return OperationResult.Failure("Some AMD driver controls failed: " + string.Join("; ", failures));
        }, cancellationToken);

    public Task<OperationResult> RestoreAsync(string displayId, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var target = FindTarget(displayId);
            if (target is null || _session is null)
            {
                return OperationResult.DriverUnavailable("The AMD driver runtime is not available for this display.");
            }

            var restored = 0;
            var failures = new List<string>();

            lock (_sync)
            {
                foreach (var key in _originals.Keys
                             .Where(k => k.Adapter == target.AdapterIndex && k.Display == target.DisplayIndex)
                             .ToArray())
                {
                    var value = _originals[key];
                    var status = _session.SetColour(key.Adapter, key.Display, key.Type, value);
                    if (status == AdlNative.AdlOk)
                    {
                        _originals.Remove(key);
                        restored++;
                    }
                    else
                    {
                        failures.Add($"{AdlNative.DescribeColourType(key.Type)} restore returned {status}");
                    }
                }
            }

            return failures.Count == 0
                ? OperationResult.Success($"Restored {restored} original AMD driver value(s).")
                : OperationResult.Failure("Some original AMD values could not be restored: " + string.Join("; ", failures));
        }, cancellationToken);

    public Task<string?> CaptureRawSnapshotAsync(string displayId, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var target = FindTarget(displayId);
            if (target is null || _session is null) return null;

            var values = new Dictionary<int, int>();
            foreach (var type in AdlNative.AllColourTypes)
            {
                if (_session.GetColour(target.AdapterIndex, target.DisplayIndex, type, out var current,
                        out _, out _, out _, out _) == AdlNative.AdlOk)
                {
                    values[type] = current;
                }
            }

            return values.Count == 0 ? null : JsonSerializer.Serialize(values);
        }, cancellationToken);

    public Task<OperationResult> RestoreRawSnapshotAsync(string displayId, string raw, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var target = FindTarget(displayId);
            if (target is null || _session is null)
            {
                return OperationResult.DriverUnavailable("The AMD driver runtime is not available for this display.");
            }

            Dictionary<int, int>? values;
            try
            {
                values = JsonSerializer.Deserialize<Dictionary<int, int>>(raw);
            }
            catch (JsonException)
            {
                return OperationResult.Invalid("The stored AMD colour snapshot is not valid.");
            }

            if (values is null || values.Count == 0)
            {
                return OperationResult.Invalid("The stored AMD colour snapshot is empty.");
            }

            var failures = new List<string>();
            foreach (var pair in values)
            {
                var status = _session.SetColour(target.AdapterIndex, target.DisplayIndex, pair.Key, pair.Value);
                if (status != AdlNative.AdlOk)
                {
                    failures.Add($"{AdlNative.DescribeColourType(pair.Key)} returned {status}");
                }
            }

            return failures.Count == 0
                ? OperationResult.Success("Original AMD driver values restored from the recovery snapshot.")
                : OperationResult.Failure("Some AMD values could not be restored: " + string.Join("; ", failures));
        }, cancellationToken);
}
