using System.Text;

namespace Veyra.Storage;

/// <summary>All writable state lives under the user's local application data folder.</summary>
public static class VeyraPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Veyra");

    public static string Logs => Path.Combine(Root, "logs");
    public static string Database => Path.Combine(Root, "veyra.db");
    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Hotkeys => Path.Combine(Root, "hotkeys.json");
    public static string ColourRecovery => Path.Combine(Root, "colour-recovery.json");
    public static string DisplayRecovery => Path.Combine(Root, "display-recovery.json");
    public static string CrashMarker => Path.Combine(Root, "session.lock");

    public static string EnsureRoot()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
        return Root;
    }
}

/// <summary>
/// Atomic file writes: content goes to a temporary file, is flushed, then swapped in.
/// A previous copy is kept as .bak so a damaged write never destroys the last good file.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static void WriteAllText(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        File.WriteAllText(temp, content, Utf8NoBom);

        if (File.Exists(path))
        {
            File.Replace(temp, path, path + ".bak", ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, path);
        }
    }

    public static bool TryReadAllText(string path, out string content)
    {
        content = string.Empty;
        try
        {
            if (!File.Exists(path)) return false;
            content = File.ReadAllText(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Keeps a damaged file for diagnosis instead of deleting it.</summary>
    public static void Quarantine(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var target = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            File.Move(path, target, overwrite: true);
        }
        catch (IOException)
        {
            // Nothing more can be done; the caller has already fallen back to defaults.
        }
    }
}
