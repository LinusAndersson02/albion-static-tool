using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Json;
using Serilog;
using StatisticAnalysisTool.Extractor.Enums;
using StatisticsAnalysisTool.Capture;
using StatisticsAnalysisTool.Linux.Core;
using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Linux.Runtime;

if (!LinuxPlatformInfo.IsRoot())
{
    Console.Error.WriteLine("Statistics Analysis Tool Web UI must be started with sudo so libpcap can capture Albion packets.");
    Console.Error.WriteLine("Run it again with: sudo dotnet run --project src/StatisticsAnalysisTool.Web/StatisticsAnalysisTool.Web.csproj");
    return 1;
}

var paths = new LinuxAppPaths();
paths.EnsureRuntimeDirectories();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(paths.LogFilePattern, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
});
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<LinuxSettingsStore>();
builder.Services.AddSingleton<RuntimeState>();

var app = builder.Build();
var runtimeState = app.Services.GetRequiredService<RuntimeState>();
var listenUrl = Environment.GetEnvironmentVariable("SAT_WEB_URL");
if (string.IsNullOrWhiteSpace(listenUrl))
{
    listenUrl = "http://127.0.0.1:5050";
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/status", (RuntimeState state) => state.GetStatus());
app.MapGet("/api/devices", (RuntimeState state) => state.GetStatus().DeviceResult);
app.MapGet("/api/settings", (RuntimeState state) => state.Settings);
app.MapPut("/api/settings", async (RuntimeState state, LinuxSettings settings) =>
{
    await state.SaveSettingsAsync(settings).ConfigureAwait(false);
    return Results.Ok(state.Settings);
});

app.MapGet("/api/capture", (RuntimeState state) => state.LiveCapture.GetStatus());
app.MapPost("/api/capture/start", (RuntimeState state) => Results.Ok(state.StartCapture()));
app.MapPost("/api/capture/stop", (RuntimeState state) => Results.Ok(state.StopCapture()));

app.MapGet("/api/dps", (RuntimeState state) =>
{
    var snapshot = state.DpsMeter.GetSnapshot();
    var totalDamage = Math.Max(1, snapshot.TotalDamage);
    return new
    {
        snapshot.InCombat,
        snapshot.CombatStartedAt,
        snapshot.LastCombatEventAt,
        snapshot.TotalDamage,
        snapshot.TotalHeal,
        snapshot.TotalOverheal,
        snapshot.TotalTakenDamage,
        Entries = snapshot.Entries.Select(entry => new
        {
            entry.EntityId,
            Name = state.EntityNames.Resolve(entry.EntityId),
            entry.Damage,
            DamageShare = entry.Damage / (double)totalDamage * 100d,
            entry.Dps,
            entry.Heal,
            entry.Hps,
            entry.Overheal,
            entry.TakenDamage,
            entry.HitCount,
            entry.FirstSeenAt,
            entry.LastSeenAt
        })
    };
});
app.MapGet("/api/party", (RuntimeState state) => state.Party.GetSnapshot());
app.MapGet("/api/diagnostics/party-snapshots", (RuntimeState state) => state.PartySnapshotDiagnostics.GetRecent());
app.MapGet("/api/timeline", (RuntimeState state) => state.Timeline.GetSnapshot(valueEvents: state.LootLog.GetValueEvents()));
app.MapPost("/api/timeline/silver", (RuntimeState state, SilverCheckpointRequest request) =>
{
    state.Timeline.AddSilverCheckpoint(request.Silver, request.Note);
    return Results.Ok(state.Timeline.GetSnapshot(valueEvents: state.LootLog.GetValueEvents()));
});
app.MapGet("/api/loot", (RuntimeState state) => state.LootLog.GetSnapshot(40));
app.MapGet("/api/rates", (RuntimeState state) => state.ActivityRates.GetSnapshot());
app.MapGet("/api/parser", (RuntimeState state) => state.ParserStats.GetSnapshot());
app.MapGet("/api/item-images/{uniqueName}.png", async (RuntimeState state, string uniqueName, CancellationToken cancellationToken) =>
    await state.ItemImages.GetAsync(uniqueName, cancellationToken).ConfigureAwait(false));
app.MapGet("/api/game-data", (RuntimeState state) => new
{
    state.GameData.ItemCount,
    state.GameData.Status,
    state.GameData.IndexedItemsFile
});
app.MapPost("/api/game-data/detect", async (RuntimeState state) =>
{
    var path = await state.AutoDetectGameFolderAsync().ConfigureAwait(false);
    return string.IsNullOrWhiteSpace(path)
        ? Results.NotFound(new { message = "Albion game folder was not found in common install locations." })
        : Results.Ok(new { path, state.Settings });
});

app.MapPost("/api/dps/reset", (RuntimeState state) =>
{
    state.DpsMeter.Reset();
    return Results.Ok(state.DpsMeter.GetSnapshot());
});

app.MapPost("/api/loot/reset", (RuntimeState state) =>
{
    state.LootLog.Reset();
    state.ActivityRates.Reset();
    return Results.Ok(state.LootLog.GetSnapshot());
});

app.MapPost("/api/timeline/reset", (RuntimeState state) =>
{
    state.Timeline.Reset(state.Settings.LocalPlayerName);
    return Results.Ok(state.Timeline.GetSnapshot());
});

app.MapPost("/api/loot/export", (RuntimeState state) =>
{
    var path = state.LootHtmlReportWriter.Write(state.LootLog.GetEntries());
    return Results.Ok(new { path });
});

app.MapFallbackToFile("index.html");

try
{
    await app.RunAsync(listenUrl).ConfigureAwait(false);
}
catch (Exception exception)
{
    Log.Fatal(exception, "Linux Web UI failed to start");
    Console.Error.WriteLine($"Statistics Analysis Tool Web UI failed to start: {exception.Message}");
    return 1;
}
finally
{
    runtimeState.LiveCapture.Stop();
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}

return 0;

internal sealed class RuntimeState
{
    private readonly LinuxAppPaths _paths;
    private readonly LinuxSettingsStore _settingsStore;
    private readonly StatusService _statusService;
    private readonly object _settingsSync = new();

    public RuntimeState(LinuxAppPaths paths, LinuxSettingsStore settingsStore)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        Settings = _settingsStore.LoadAsync().GetAwaiter().GetResult();

        GameData = new GameDataIndex(_paths, Settings);
        GameData.InitializeAsync().GetAwaiter().GetResult();

        EntityNames = new EntityNameService();
        EntityNames.SetLocalEntity(null, null, Settings.LocalPlayerName);
        Party = new PartyService(GameData);
        Party.SetLocal(null, null, Settings.LocalPlayerName);
        Timeline = new SessionTimelineService();
        Timeline.SetLocalPlayer(Settings.LocalPlayerName);
        PartySnapshotDiagnostics = new PartySnapshotDiagnosticsService();

        ParserStats = new ParserStats();
        DpsMeter = new DpsMeterService();
        LootLog = new LootLogService();
        EstimatedItemValues = new EstimatedItemValueService();
        ActivityRates = new ActivityRatesService();
        LootHtmlReportWriter = new LootHtmlReportWriter(_paths);
        ItemImages = new ItemImageCacheService(_paths);

        var receiverBuilder = ReceiverBuilder.Create();
        ParserStats.RegisterHandlers(receiverBuilder);
        receiverBuilder.AddHandler(new EntityNameEventHandler(EntityNames, Party, Timeline, PartySnapshotDiagnostics));
        receiverBuilder.AddHandler(new EntityNameResponseHandler(EntityNames, Party, Timeline, PartySnapshotDiagnostics));
        receiverBuilder.AddHandler(new EstimatedItemValuePacketHandler(EstimatedItemValues));
        receiverBuilder.AddHandler(new DpsMeterPacketHandler(DpsMeter, EntityNames));
        receiverBuilder.AddHandler(new LootLogPacketHandler(LootLog, EntityNames, GameData, EstimatedItemValues));
        receiverBuilder.AddHandler(new ActivityRatesPacketHandler(ActivityRates, EntityNames));

        LiveCapture = new LibpcapLiveCaptureService(receiverBuilder.Build());
        _statusService = new StatusService(_paths, new LibpcapCaptureDeviceService());
    }

    public LinuxSettings Settings { get; private set; }
    public ParserStats ParserStats { get; }
    public DpsMeterService DpsMeter { get; }
    public LootLogService LootLog { get; }
    public EstimatedItemValueService EstimatedItemValues { get; }
    public ActivityRatesService ActivityRates { get; }
    public ItemImageCacheService ItemImages { get; }
    public GameDataIndex GameData { get; }
    public EntityNameService EntityNames { get; }
    public PartyService Party { get; }
    public SessionTimelineService Timeline { get; }
    public PartySnapshotDiagnosticsService PartySnapshotDiagnostics { get; }
    public LootHtmlReportWriter LootHtmlReportWriter { get; }
    public LibpcapLiveCaptureService LiveCapture { get; }

    public AppStatus GetStatus()
    {
        lock (_settingsSync)
        {
            return _statusService.CreateStatus(Settings);
        }
    }

    public async Task SaveSettingsAsync(LinuxSettings settings)
    {
        lock (_settingsSync)
        {
            Settings = settings;
            EntityNames.SetLocalEntity(null, null, Settings.LocalPlayerName);
            Party.SetLocal(null, null, Settings.LocalPlayerName);
            Timeline.SetLocalPlayer(Settings.LocalPlayerName);
            GameData.UpdateSettings(Settings);
        }

        await _settingsStore.SaveAsync(settings).ConfigureAwait(false);
        await GameData.InitializeAsync().ConfigureAwait(false);
    }

    public async Task<string> AutoDetectGameFolderAsync()
    {
        var serverType = string.Equals(Settings.ServerLocation, "staging", StringComparison.OrdinalIgnoreCase)
            ? ServerType.Staging
            : string.Equals(Settings.ServerLocation, "playground", StringComparison.OrdinalIgnoreCase)
                ? ServerType.Playground
                : ServerType.Live;
        var path = AlbionInstallDiscovery.Find(serverType);
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        Settings.MainGameFolderPath = path;
        GameData.UpdateSettings(Settings);
        await _settingsStore.SaveAsync(Settings).ConfigureAwait(false);
        await GameData.InitializeAsync().ConfigureAwait(false);
        return path;
    }

    public LiveCaptureStatus StartCapture()
    {
        lock (_settingsSync)
        {
            return LiveCapture.Start(LiveCaptureOptions.FromSettings(
                Settings.SelectedDeviceIdentifiers,
                Settings.PacketFilter));
        }
    }

    public LiveCaptureStatus StopCapture()
    {
        return LiveCapture.Stop();
    }
}

internal sealed class ItemImageCacheService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly string _imageDirectory;

    public ItemImageCacheService(LinuxAppPaths paths)
    {
        _imageDirectory = Path.Combine(paths.GameFilesDirectory, "ItemImages");
    }

    public async Task<IResult> GetAsync(string uniqueName, CancellationToken cancellationToken)
    {
        uniqueName = Uri.UnescapeDataString(uniqueName).Trim();
        if (!IsValidUniqueName(uniqueName))
        {
            return Results.BadRequest();
        }

        Directory.CreateDirectory(_imageDirectory);
        var path = Path.Combine(_imageDirectory, GetCacheFileName(uniqueName));
        if (File.Exists(path))
        {
            return Results.File(path, "image/png");
        }

        try
        {
            var source = $"https://render.albiononline.com/v1/item/{Uri.EscapeDataString(uniqueName)}.png";
            using var response = await Http.GetAsync(source, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Results.NotFound();
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return Results.NotFound();
            }

            var tempPath = path + ".tmp";
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
            return Results.File(path, "image/png");
        }
        catch
        {
            return Results.NotFound();
        }
    }

    private static string GetCacheFileName(string uniqueName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(uniqueName.ToUpperInvariant()));
        return $"{Convert.ToHexString(hash)}.png";
    }

    private static bool IsValidUniqueName(string uniqueName)
    {
        return uniqueName.Length is > 0 and <= 160
            && uniqueName.All(x => char.IsLetterOrDigit(x) || x is '_' or '-' or '@' or '.');
    }
}

internal sealed record SilverCheckpointRequest(long Silver, string? Note);
