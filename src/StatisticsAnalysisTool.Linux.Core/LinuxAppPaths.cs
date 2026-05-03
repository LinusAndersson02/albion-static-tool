namespace StatisticsAnalysisTool.Linux.Core;

public sealed class LinuxAppPaths
{
    private const string AppDataFolderName = "StatisticsAnalysisTool";
    private const string InstancesDirectoryName = "Instances";
    private const string DefaultInstanceName = "Default";
    private const string SettingsFileName = "LinuxSettings.json";
    private const string LogsDirectoryName = "logs";
    private const string GameFilesDirectoryName = "GameFiles";
    private const string ReportsDirectoryName = "Reports";
    private readonly string? _dataHomeOverride;

    public LinuxAppPaths(string? dataHomeOverride = null)
    {
        _dataHomeOverride = dataHomeOverride;
    }

    public string RuntimeDirectory => Path.Combine(
        GetDataHome(),
        AppDataFolderName,
        InstancesDirectoryName,
        DefaultInstanceName);

    public string SettingsFile => Path.Combine(RuntimeDirectory, SettingsFileName);

    public string LogsDirectory => Path.Combine(RuntimeDirectory, LogsDirectoryName);

    public string GameFilesDirectory => Path.Combine(RuntimeDirectory, GameFilesDirectoryName);

    public string ReportsDirectory => Path.Combine(RuntimeDirectory, ReportsDirectoryName);

    public string LogFilePattern => Path.Combine(LogsDirectory, "sat-linux-.log");

    public void EnsureRuntimeDirectories()
    {
        Directory.CreateDirectory(RuntimeDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    private string GetDataHome()
    {
        if (!string.IsNullOrWhiteSpace(_dataHomeOverride))
        {
            return _dataHomeOverride;
        }

        var sudoUserDataHome = GetSudoUserDataHome();
        if (!string.IsNullOrWhiteSpace(sudoUserDataHome))
        {
            return sudoUserDataHome;
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
        {
            return xdgDataHome;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share");
    }

    private static string GetSudoUserDataHome()
    {
        if (!string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
        if (string.IsNullOrWhiteSpace(sudoUser)
            || string.Equals(sudoUser, "root", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var sudoHome = GetHomeDirectoryFromPasswd(sudoUser);
        return string.IsNullOrWhiteSpace(sudoHome)
            ? string.Empty
            : Path.Combine(sudoHome, ".local", "share");
    }

    private static string GetHomeDirectoryFromPasswd(string username)
    {
        try
        {
            foreach (var line in File.ReadLines("/etc/passwd"))
            {
                var parts = line.Split(':');
                if (parts.Length >= 6 && string.Equals(parts[0], username, StringComparison.Ordinal))
                {
                    return parts[5];
                }
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }
}
