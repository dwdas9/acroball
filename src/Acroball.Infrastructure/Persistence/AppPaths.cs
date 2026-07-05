namespace Acroball.Infrastructure.Persistence;

/// <summary>
/// Resolves and creates the directories Acroball stores data in.
/// </summary>
/// <remarks>
/// Locations follow platform conventions (see ADR-0007):
/// <list type="bullet">
///   <item><description>Windows: <c>%APPDATA%\Acroball</c></description></item>
///   <item><description>macOS: <c>~/Library/Application Support/Acroball</c></description></item>
///   <item><description>Linux: <c>$XDG_CONFIG_HOME/Acroball</c> (default <c>~/.config/Acroball</c>)</description></item>
/// </list>
/// Setting the <c>Acroball_DATA_DIR</c> environment variable overrides all of the
/// above, which enables portable installs and hermetic tests.
/// </remarks>
public sealed class AppPaths
{
    /// <summary>Name of the environment variable that overrides the data directory.</summary>
    public const string DataDirEnvironmentVariable = "Acroball_DATA_DIR";

    /// <summary>Creates paths rooted at <paramref name="dataDirectory"/>, creating directories as needed.</summary>
    public AppPaths(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    /// <summary>Root directory for all persisted application data.</summary>
    public string DataDirectory { get; }

    /// <summary>Directory that receives rolling log files.</summary>
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>Path of the settings JSON file.</summary>
    public string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");

    /// <summary>Path of the recent-files JSON file.</summary>
    public string RecentFilesFilePath => Path.Combine(DataDirectory, "recent.json");

    /// <summary>Resolves the platform-appropriate default location (or the env override).</summary>
    public static AppPaths CreateDefault()
    {
        var overrideDir = Environment.GetEnvironmentVariable(DataDirEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideDir))
        {
            return new AppPaths(overrideDir);
        }

        string baseDir;
        if (OperatingSystem.IsWindows())
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support");
        }
        else
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            baseDir = string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : xdg;
        }

        return new AppPaths(Path.Combine(baseDir, "Acroball"));
    }
}

