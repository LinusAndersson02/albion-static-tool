using System.Text.Json;
using System.Text.Json.Serialization;

namespace StatisticsAnalysisTool.Linux.Core;

public sealed class LinuxSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly LinuxAppPaths _paths;

    public LinuxSettingsStore(LinuxAppPaths paths)
    {
        _paths = paths;
    }

    public async Task<LinuxSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureRuntimeDirectories();

        if (!File.Exists(_paths.SettingsFile))
        {
            var defaultSettings = new LinuxSettings();
            await SaveAsync(defaultSettings, cancellationToken).ConfigureAwait(false);
            return defaultSettings;
        }

        await using var stream = File.OpenRead(_paths.SettingsFile);
        var settings = await JsonSerializer.DeserializeAsync<LinuxSettings>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return Normalize(settings ?? new LinuxSettings());
    }

    public Task SaveAsync(LinuxSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Save(settings);
        return Task.CompletedTask;
    }

    public void Save(LinuxSettings settings)
    {
        _paths.EnsureRuntimeDirectories();

        var normalizedSettings = Normalize(settings);
        var json = JsonSerializer.Serialize(normalizedSettings, JsonOptions);
        var tempFile = _paths.SettingsFile + ".tmp";

        File.WriteAllText(tempFile, json);
        File.Move(tempFile, _paths.SettingsFile, overwrite: true);
    }

    private static LinuxSettings Normalize(LinuxSettings settings)
    {
        settings.ServerLocation = string.IsNullOrWhiteSpace(settings.ServerLocation)
            ? "Europe"
            : settings.ServerLocation.Trim();
        settings.PacketFilter = settings.PacketFilter?.Trim() ?? string.Empty;
        settings.LogLevel = string.IsNullOrWhiteSpace(settings.LogLevel)
            ? "Information"
            : settings.LogLevel.Trim();
        settings.MainGameFolderPath = settings.MainGameFolderPath?.Trim() ?? string.Empty;
        settings.GameDataDirectory = settings.GameDataDirectory?.Trim() ?? string.Empty;
        settings.GameDataLanguage = string.IsNullOrWhiteSpace(settings.GameDataLanguage)
            ? "en-US"
            : settings.GameDataLanguage.Trim();
        settings.LocalPlayerName = settings.LocalPlayerName?.Trim() ?? string.Empty;
        settings.SelectedDeviceIdentifiers = settings.SelectedDeviceIdentifiers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return settings;
    }
}
