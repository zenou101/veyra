using Veyra.Core.Models;

namespace Veyra.Core.Services;

public sealed record ProfileSelection(GameProfile? Active, IReadOnlyList<GameProfile> AlsoRunning, string Explanation)
{
    public bool HasConflict => Active is not null && AlsoRunning.Count > 0;
}

/// <summary>
/// Deterministic profile selection. Priority descending, then name ordinal, so the
/// same set of running games always resolves to the same profile.
/// </summary>
public static class ProfileResolver
{
    public static ProfileSelection Resolve(IEnumerable<GameProfile> candidates)
    {
        var ordered = candidates
            .Where(p => p.AutoSwitch)
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

        if (ordered.Length == 0)
        {
            return new ProfileSelection(null, Array.Empty<GameProfile>(), "No auto-switching game profile is running.");
        }

        var active = ordered[0];
        var others = ordered.Skip(1).ToArray();
        var explanation = others.Length == 0
            ? $"{active.Name} is the only running profile."
            : $"{active.Name} wins on priority ({active.Priority}); also running: {string.Join(", ", others.Select(o => $"{o.Name} ({o.Priority})"))}.";

        return new ProfileSelection(active, others, explanation);
    }

    public static ProfileSelection ResolveForProcesses(IEnumerable<GameProfile> profiles, IEnumerable<string> runningProcessNames) =>
        Resolve(GameMatcher.FindMatches(profiles, runningProcessNames));

    /// <summary>Priority 0-1000; higher wins. Values outside the range are rejected at the edge.</summary>
    public static bool IsValidPriority(int priority) => priority is >= 0 and <= 1000;
}
