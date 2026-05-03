using Serilog;
using StatisticsAnalysisTool.Capture;
using StatisticsAnalysisTool.Linux.Core;
using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Tui;

var paths = new LinuxAppPaths();
paths.EnsureRuntimeDirectories();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(paths.LogFilePattern, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var settingsStore = new LinuxSettingsStore(paths);
    var settings = await settingsStore.LoadAsync().ConfigureAwait(false);
    var statusService = new StatusService(paths, new LibpcapCaptureDeviceService());
    var gameData = new GameDataIndex(paths, settings);
    await gameData.InitializeAsync().ConfigureAwait(false);
    var entityNames = new EntityNameService();
    entityNames.SetLocalEntity(null, null, settings.LocalPlayerName);
    var party = new PartyService(gameData);
    party.SetLocal(null, null, settings.LocalPlayerName);
    var timeline = new SessionTimelineService();
    timeline.SetLocalPlayer(settings.LocalPlayerName);
    var snapshotDiagnostics = new PartySnapshotDiagnosticsService();
    var parserStats = new ParserStats();
    var dpsMeter = new DpsMeterService();
    var lootLog = new LootLogService();
    var estimatedItemValues = new EstimatedItemValueService();
    var activityRates = new ActivityRatesService();
    var lootHtmlReportWriter = new LootHtmlReportWriter(paths);
    var receiverBuilder = ReceiverBuilder.Create();
    parserStats.RegisterHandlers(receiverBuilder);
    receiverBuilder.AddHandler(new EntityNameEventHandler(entityNames, party, timeline, snapshotDiagnostics));
    receiverBuilder.AddHandler(new EntityNameResponseHandler(entityNames, party, timeline, snapshotDiagnostics));
    receiverBuilder.AddHandler(new EstimatedItemValuePacketHandler(estimatedItemValues));
    receiverBuilder.AddHandler(new DpsMeterPacketHandler(dpsMeter, entityNames));
    receiverBuilder.AddHandler(new LootLogPacketHandler(lootLog, entityNames, gameData, estimatedItemValues));
    receiverBuilder.AddHandler(new ActivityRatesPacketHandler(activityRates, entityNames));
    var photonReceiver = receiverBuilder.Build();
    var liveCaptureService = new LibpcapLiveCaptureService(photonReceiver);

    var app = new TuiApp(settingsStore, settings, statusService, liveCaptureService, parserStats, dpsMeter, lootLog, activityRates, gameData, entityNames, lootHtmlReportWriter);
    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Linux TUI crashed");
    Console.Error.WriteLine($"Statistics Analysis Tool failed to start: {exception.Message}");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}

return 0;
