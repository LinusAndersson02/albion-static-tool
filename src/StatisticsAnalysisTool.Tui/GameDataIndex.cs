using System.Text.Json;
using StatisticAnalysisTool.Extractor;
using StatisticAnalysisTool.Extractor.Enums;
using StatisticsAnalysisTool.Linux.Core;

namespace StatisticsAnalysisTool.Tui;

public sealed class GameDataIndex
{
    private const string IndexedItemsFileName = "IndexedItems.json";
    private readonly LinuxAppPaths _paths;
    private LinuxSettings _settings;
    private readonly object _sync = new();
    private Dictionary<int, GameDataItem> _itemsByIndex = [];

    public GameDataIndex(LinuxAppPaths paths, LinuxSettings settings)
    {
        _paths = paths;
        _settings = settings;
    }

    public string GameDataDirectory => ResolveGameDataDirectory();

    public string IndexedItemsFile => Path.Combine(GameDataDirectory, IndexedItemsFileName);

    public int ItemCount
    {
        get
        {
            lock (_sync)
            {
                return _itemsByIndex.Count;
            }
        }
    }

    public string Status { get; private set; } = "not loaded";

    public void UpdateSettings(LinuxSettings settings)
    {
        _settings = settings;
    }

    public async Task InitializeAsync()
    {
        Status = "not loaded";
        if (!EnsureGameDataDirectory())
        {
            return;
        }

        await TryExtractIndexedItemsAsync().ConfigureAwait(false);
        await LoadIndexedItemsAsync().ConfigureAwait(false);
    }

    public string GetItemDisplayName(int itemIndex)
    {
        return GetItem(itemIndex)?.DisplayName ?? $"item #{itemIndex}";
    }

    public GameDataItem? GetItem(int itemIndex)
    {
        if (itemIndex <= 0)
        {
            return null;
        }

        lock (_sync)
        {
            return _itemsByIndex.GetValueOrDefault(itemIndex);
        }
    }

    private async Task TryExtractIndexedItemsAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.MainGameFolderPath))
        {
            return;
        }

        var serverType = ResolveServerType(_settings.ServerLocation);
        if (!Extractor.IsValidMainGameFolder(_settings.MainGameFolderPath, serverType))
        {
            Status = $"game folder invalid: {_settings.MainGameFolderPath}";
            return;
        }

        if (!Extractor.IsBinFileNewer(IndexedItemsFile, _settings.MainGameFolderPath, serverType, "items")
            && File.Exists(IndexedItemsFile))
        {
            return;
        }

        using var extractor = new Extractor(_settings.MainGameFolderPath, serverType);
        await extractor.ExtractIndexedItemGameDataAsync(GameDataDirectory, IndexedItemsFileName).ConfigureAwait(false);
    }

    private async Task LoadIndexedItemsAsync()
    {
        if (!File.Exists(IndexedItemsFile))
        {
            Status = $"missing {IndexedItemsFileName}; set mainGameFolderPath or gameDataDirectory";
            return;
        }

        try
        {
            await using var stream = File.OpenRead(IndexedItemsFile);
            using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            var items = new Dictionary<int, GameDataItem>();

            foreach (var element in document.RootElement.EnumerateArray())
            {
                var indexText = GetString(element, "Index");
                if (!int.TryParse(indexText, out var index) || index <= 0)
                {
                    continue;
                }

                var uniqueName = GetString(element, "UniqueName");
                var displayName = ResolveDisplayName(element, uniqueName, _settings.GameDataLanguage);
                items[index] = new GameDataItem(index, uniqueName, displayName);
            }

            lock (_sync)
            {
                _itemsByIndex = items;
            }

            Status = $"loaded {items.Count:N0} indexed items";
        }
        catch (Exception exception)
        {
            Status = $"item data load failed: {exception.Message}";
        }
    }

    private string ResolveGameDataDirectory()
    {
        return string.IsNullOrWhiteSpace(_settings.GameDataDirectory)
            ? _paths.GameFilesDirectory
            : _settings.GameDataDirectory;
    }

    private bool EnsureGameDataDirectory()
    {
        try
        {
            Directory.CreateDirectory(GameDataDirectory);
            return true;
        }
        catch (Exception exception)
        {
            Status = $"game data directory unavailable: {exception.Message}";
            return false;
        }
    }

    private static ServerType ResolveServerType(string serverLocation)
    {
        return string.Equals(serverLocation, "staging", StringComparison.OrdinalIgnoreCase)
            ? ServerType.Staging
            : string.Equals(serverLocation, "playground", StringComparison.OrdinalIgnoreCase)
                ? ServerType.Playground
                : ServerType.Live;
    }

    private static string ResolveDisplayName(JsonElement element, string uniqueName, string language)
    {
        if (element.TryGetProperty("LocalizedNames", out var localizedNames)
            && localizedNames.ValueKind == JsonValueKind.Object)
        {
            if (TryGetLocalizedName(localizedNames, language, out var configuredName))
            {
                return configuredName;
            }

            if (TryGetLocalizedName(localizedNames, "en-US", out var englishName))
            {
                return englishName;
            }
        }

        return string.IsNullOrWhiteSpace(uniqueName) ? "(unknown item)" : uniqueName;
    }

    private static bool TryGetLocalizedName(JsonElement localizedNames, string language, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        foreach (var property in localizedNames.EnumerateObject())
        {
            if (!string.Equals(property.Name, language, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = property.Value.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }
}
