namespace Veyra.Core.Models;

/// <summary>Original driver values captured before the first colour change in a session.</summary>
public sealed record ColourRecoveryEntry(
    string DisplayId,
    string ProviderName,
    string? GammaRampBase64,
    IReadOnlyDictionary<int, int> DriverValues,
    DateTimeOffset CapturedAt);

public sealed record ColourRecoverySnapshot
{
    public string SchemaVersion { get; init; } = "1";
    public bool Dirty { get; init; }
    public IReadOnlyList<ColourRecoveryEntry> Entries { get; init; } = Array.Empty<ColourRecoveryEntry>();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Exact previous display configuration handed to the out-of-process guard.</summary>
public sealed record DisplayRecoverySnapshot
{
    public string SchemaVersion { get; init; } = "1";
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<DisplayState> Displays { get; init; } = Array.Empty<DisplayState>();
    public string? Checksum { get; init; }
}
