using Veyra.Core.Models;

namespace Veyra.Core.Services;

/// <summary>
/// Matches a running process against a configured game. Names are compared
/// case-insensitively; a configured absolute path must also match when supplied.
/// </summary>
public static class GameMatcher
{
    public static bool Matches(GameProfile profile, string processName, string? processPath = null)
    {
        if (string.IsNullOrWhiteSpace(processName) || string.IsNullOrWhiteSpace(profile.ExecutableName))
        {
            return false;
        }

        if (!string.Equals(NormaliseName(processName), NormaliseName(profile.ExecutableName), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.ExecutablePath) || string.IsNullOrWhiteSpace(processPath))
        {
            return true;
        }

        try
        {
            var expected = Path.GetFullPath(profile.ExecutablePath);
            var actual = Path.GetFullPath(processPath);
            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string NormaliseName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + ".exe";
    }

    public static bool IsSystemProcess(string name) =>
        name.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("svchost.exe", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("dwm.exe", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<GameProfile> FindMatches(IEnumerable<GameProfile> profiles, IEnumerable<string> runningProcessNames)
    {
        var running = runningProcessNames.Select(NormaliseName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return profiles.Where(p => running.Contains(NormaliseName(p.ExecutableName))).ToArray();
    }
}
