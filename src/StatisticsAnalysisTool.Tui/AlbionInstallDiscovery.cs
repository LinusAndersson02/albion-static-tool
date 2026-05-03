using StatisticAnalysisTool.Extractor;
using StatisticAnalysisTool.Extractor.Enums;

namespace StatisticsAnalysisTool.Tui;

public static class AlbionInstallDiscovery
{
    public static string Find(ServerType serverType)
    {
        foreach (var candidate in GetCandidates().Distinct(StringComparer.Ordinal))
        {
            if (Extractor.IsValidMainGameFolder(candidate, serverType))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> GetCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            yield break;
        }

        foreach (var path in DirectCandidates(home))
        {
            yield return path;
        }

        foreach (var steamApps in SteamAppsDirectories(home))
        {
            var common = Path.Combine(steamApps, "common");
            yield return Path.Combine(common, "Albion Online");
            yield return Path.Combine(common, "Albion Online", "game_x64");
            yield return Path.Combine(common, "Albion Online", "game");
            yield return Path.Combine(common, "AlbionOnline");
            yield return Path.Combine(common, "AlbionOnline", "game_x64");
            yield return Path.Combine(common, "AlbionOnline", "game");
        }
    }

    private static IEnumerable<string> DirectCandidates(string home)
    {
        yield return Path.Combine(home, "Albion Online");
        yield return Path.Combine(home, "Albion Online", "game_x64");
        yield return Path.Combine(home, "Albion Online", "game");
        yield return Path.Combine(home, "AlbionOnline");
        yield return Path.Combine(home, "AlbionOnline", "game_x64");
        yield return Path.Combine(home, "AlbionOnline", "game");
        yield return Path.Combine(home, "Games", "Albion Online");
        yield return Path.Combine(home, "Games", "Albion Online", "game_x64");
        yield return Path.Combine(home, "Games", "Albion Online", "game");
        yield return Path.Combine(home, "Games", "AlbionOnline");
        yield return Path.Combine(home, "Games", "AlbionOnline", "game_x64");
        yield return Path.Combine(home, "Games", "AlbionOnline", "game");
    }

    private static IEnumerable<string> SteamAppsDirectories(string home)
    {
        var roots = new[]
        {
            Path.Combine(home, ".steam", "steam", "steamapps"),
            Path.Combine(home, ".local", "share", "Steam", "steamapps"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam", "steamapps")
        };

        foreach (var root in roots)
        {
            if (Directory.Exists(root))
            {
                yield return root;
            }

            foreach (var libraryPath in ReadSteamLibraryPaths(Path.Combine(root, "libraryfolders.vdf")))
            {
                var steamApps = Path.Combine(libraryPath, "steamapps");
                if (Directory.Exists(steamApps))
                {
                    yield return steamApps;
                }
            }
        }
    }

    private static IEnumerable<string> ReadSteamLibraryPaths(string libraryFile)
    {
        if (!File.Exists(libraryFile))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(libraryFile))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmed.Split('"', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                yield return parts[^1].Replace(@"\\", @"\");
            }
        }
    }
}
