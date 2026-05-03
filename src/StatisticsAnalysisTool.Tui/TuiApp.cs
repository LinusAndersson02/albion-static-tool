using Serilog;
using StatisticsAnalysisTool.Capture;
using StatisticsAnalysisTool.Linux.Core;
using Terminal.Gui;
using GuiAttribute = Terminal.Gui.Attribute;

namespace StatisticsAnalysisTool.Tui;

public sealed class TuiApp
{
    private readonly LinuxSettingsStore _settingsStore;
    private readonly StatusService _statusService;
    private readonly ILiveCaptureService _liveCaptureService;
    private readonly ParserStats _parserStats;
    private readonly DpsMeterService _dpsMeter;
    private readonly LootLogService _lootLog;
    private readonly ActivityRatesService _activityRates;
    private readonly GameDataIndex _gameData;
    private readonly EntityNameService _entityNames;
    private readonly LootHtmlReportWriter _lootHtmlReportWriter;
    private readonly List<string> _logLines = [];
    private LinuxSettings _settings;
    private AppStatus? _status;
    private Label? _headerLabel;
    private Label? _statusLabel;
    private ListView? _sectionList;
    private TextView? _contentView;
    private ListView? _deviceListView;
    private TextField? _serverField;
    private TextField? _packetFilterField;
    private TextField? _logLevelField;
    private TextField? _selectedDevicesField;
    private TextField? _mainGameFolderField;
    private TextField? _gameDataDirectoryField;
    private TextField? _gameDataLanguageField;
    private TextField? _localPlayerNameField;
    private readonly List<View> _settingsViews = [];
    private readonly List<View> _deviceViews = [];
    private readonly List<CaptureDeviceInfo> _visibleDevices = [];
    private int _selectedSection;

    private static readonly string[] Sections = ["Status", "Devices", "Capture", "DPS", "Loot", "Rates", "Logs", "Settings"];
    private static readonly ColorScheme BaseScheme = new()
    {
        Normal = GuiAttribute.Make(Color.Gray, Color.Black),
        Focus = GuiAttribute.Make(Color.Gray, Color.Black),
        HotNormal = GuiAttribute.Make(Color.BrightCyan, Color.Black),
        HotFocus = GuiAttribute.Make(Color.BrightCyan, Color.Black),
        Disabled = GuiAttribute.Make(Color.DarkGray, Color.Black)
    };
    private static readonly ColorScheme SectionScheme = new()
    {
        Normal = GuiAttribute.Make(Color.Gray, Color.Black),
        Focus = GuiAttribute.Make(Color.Black, Color.BrightCyan),
        HotNormal = GuiAttribute.Make(Color.BrightCyan, Color.Black),
        HotFocus = GuiAttribute.Make(Color.Black, Color.BrightCyan),
        Disabled = GuiAttribute.Make(Color.DarkGray, Color.Black)
    };
    private static readonly ColorScheme PanelScheme = new()
    {
        Normal = GuiAttribute.Make(Color.Gray, Color.Black),
        Focus = GuiAttribute.Make(Color.White, Color.BrightBlue),
        HotNormal = GuiAttribute.Make(Color.BrightYellow, Color.Black),
        HotFocus = GuiAttribute.Make(Color.White, Color.BrightBlue),
        Disabled = GuiAttribute.Make(Color.DarkGray, Color.Black)
    };
    private static readonly ColorScheme FieldScheme = new()
    {
        Normal = GuiAttribute.Make(Color.BrightCyan, Color.Black),
        Focus = GuiAttribute.Make(Color.Black, Color.Gray),
        HotNormal = GuiAttribute.Make(Color.BrightCyan, Color.Black),
        HotFocus = GuiAttribute.Make(Color.Black, Color.Gray),
        Disabled = GuiAttribute.Make(Color.DarkGray, Color.Black)
    };
    private static readonly ColorScheme MenuScheme = new()
    {
        Normal = GuiAttribute.Make(Color.Gray, Color.Black),
        Focus = GuiAttribute.Make(Color.White, Color.Blue),
        HotNormal = GuiAttribute.Make(Color.BrightYellow, Color.Black),
        HotFocus = GuiAttribute.Make(Color.BrightYellow, Color.Blue),
        Disabled = GuiAttribute.Make(Color.DarkGray, Color.Black)
    };
    private static readonly ColorScheme HeaderScheme = new()
    {
        Normal = GuiAttribute.Make(Color.BrightYellow, Color.Black),
        Focus = GuiAttribute.Make(Color.BrightYellow, Color.Black),
        HotNormal = GuiAttribute.Make(Color.BrightYellow, Color.Black),
        HotFocus = GuiAttribute.Make(Color.BrightYellow, Color.Black),
        Disabled = GuiAttribute.Make(Color.DarkGray, Color.Black)
    };

    public TuiApp(
        LinuxSettingsStore settingsStore,
        LinuxSettings settings,
        StatusService statusService,
        ILiveCaptureService liveCaptureService,
        ParserStats parserStats,
        DpsMeterService dpsMeter,
        LootLogService lootLog,
        ActivityRatesService activityRates,
        GameDataIndex gameData,
        EntityNameService entityNames,
        LootHtmlReportWriter lootHtmlReportWriter)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _statusService = statusService;
        _liveCaptureService = liveCaptureService;
        _parserStats = parserStats;
        _dpsMeter = dpsMeter;
        _lootLog = lootLog;
        _activityRates = activityRates;
        _gameData = gameData;
        _entityNames = entityNames;
        _lootHtmlReportWriter = lootHtmlReportWriter;
    }

    public void Run()
    {
        RefreshStatus();

        Application.Init();
        try
        {
            ApplyDarkTheme();
            BuildUi();
            RenderContent();
            StartLiveRefreshTimer();
            Application.Run();
        }
        finally
        {
            _liveCaptureService.Stop();
            Application.Shutdown();
        }
    }

    private void BuildUi()
    {
        var top = Application.Top;
        top.ColorScheme = BaseScheme;

        var menu = new MenuBar(new[]
        {
            new MenuBarItem("_File", new[]
            {
                new MenuItem("_Save settings", "Save Linux settings", SaveSettings),
                new MenuItem("_Refresh", "Refresh status and capture devices", RefreshAndRender),
                new MenuItem("Reset _DPS", "Clear current DPS meter", ResetDpsMeter),
                new MenuItem("Reset _Loot and rates", "Clear loot log and per-hour counters", ResetLootAndRates),
                new MenuItem("_Export loot HTML", "Write an HTML loot report with item icons", ExportLootHtml),
                new MenuItem("_Quit", "Exit", () => Application.RequestStop())
            }),
            new MenuBarItem("_Help", new[]
            {
                new MenuItem("_About", "Show app information", ShowAbout)
            })
        })
        {
            ColorScheme = MenuScheme
        };
        top.Add(menu);

        var window = new Window("Albion Online Statistics Analysis - Linux TUI")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ColorScheme = PanelScheme
        };

        _headerLabel = new Label(string.Empty)
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = 1,
            ColorScheme = HeaderScheme
        };

        _statusLabel = new Label(string.Empty)
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = 1,
            ColorScheme = HeaderScheme
        };

        _sectionList = new ListView(Sections)
        {
            X = 1,
            Y = 3,
            Width = 18,
            Height = Dim.Fill(8),
            ColorScheme = SectionScheme
        };
        _sectionList.SelectedItemChanged += args =>
        {
            _selectedSection = args.Item;
            RenderContent();
        };

        _contentView = new TextView
        {
            X = 21,
            Y = 3,
            Width = Dim.Fill(1),
            Height = Dim.Fill(8),
            ColorScheme = BaseScheme,
            ReadOnly = true,
            WordWrap = false,
            CanFocus = false,
            TabStop = false,
            DesiredCursorVisibility = CursorVisibility.Invisible
        };

        var deviceHelpLabel = new Label("F3 focuses devices. Space/Enter toggles an interface. Esc returns to sections. F2 saves.")
        {
            X = 21,
            Y = 7,
            Width = Dim.Fill(1),
            Height = 1,
            ColorScheme = BaseScheme
        };

        _deviceListView = new ListView(Array.Empty<string>())
        {
            X = 21,
            Y = 9,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1),
            ColorScheme = SectionScheme,
            AllowsMarking = false,
            AllowsMultipleSelection = false,
            CanFocus = true
        };
        _deviceListView.OpenSelectedItem += _ => ToggleSelectedDevice();
        _deviceListView.KeyDown += args =>
        {
            if (args.KeyEvent.Key == Key.Space || args.KeyEvent.Key == Key.Enter)
            {
                ToggleSelectedDevice();
                args.Handled = true;
                return;
            }

            if (args.KeyEvent.Key == Key.Esc)
            {
                _sectionList.SetFocus();
                args.Handled = true;
            }
        };

        _deviceViews.AddRange([deviceHelpLabel, _deviceListView]);

        var serverLabel = new Label("Server:") { X = 21, Y = Pos.Bottom(_contentView), Width = 10 };
        var logLevelLabel = new Label("Log level:") { X = 39, Y = Pos.Bottom(_contentView), Width = 11 };
        var packetFilterLabel = new Label("Packet filter:") { X = 21, Y = Pos.Bottom(_contentView) + 2, Width = 16 };
        var selectedDevicesLabel = new Label("Selected device ids:") { X = 21, Y = Pos.Bottom(_contentView) + 4, Width = 22 };
        var gameDataLanguageLabel = new Label("Data language:") { X = 21, Y = Pos.Bottom(_contentView) + 6, Width = 16 };
        var mainGameFolderLabel = new Label("Albion folder:") { X = 39, Y = Pos.Bottom(_contentView) + 6, Width = 16 };
        var gameDataDirectoryLabel = new Label("Game data dir:") { X = 21, Y = Pos.Bottom(_contentView) + 8, Width = 16 };
        var localPlayerNameLabel = new Label("Player name:") { X = 21, Y = Pos.Bottom(_contentView) + 10, Width = 16 };

        _serverField = new TextField(_settings.ServerLocation)
        {
            X = 21,
            Y = Pos.Bottom(_contentView) + 1,
            Width = 16,
            ColorScheme = FieldScheme
        };

        _logLevelField = new TextField(_settings.LogLevel)
        {
            X = 52,
            Y = Pos.Bottom(_contentView) + 1,
            Width = 16,
            ColorScheme = FieldScheme
        };

        _packetFilterField = new TextField(_settings.PacketFilter)
        {
            X = 21,
            Y = Pos.Bottom(_contentView) + 3,
            Width = Dim.Fill(1),
            ColorScheme = FieldScheme
        };

        _selectedDevicesField = new TextField(string.Join(", ", _settings.SelectedDeviceIdentifiers))
        {
            X = 21,
            Y = Pos.Bottom(_contentView) + 5,
            Width = Dim.Fill(1),
            ColorScheme = FieldScheme
        };

        _gameDataLanguageField = new TextField(_settings.GameDataLanguage)
        {
            X = 21,
            Y = Pos.Bottom(_contentView) + 7,
            Width = 16,
            ColorScheme = FieldScheme
        };

        _mainGameFolderField = new TextField(_settings.MainGameFolderPath)
        {
            X = 39,
            Y = Pos.Bottom(_contentView) + 7,
            Width = Dim.Fill(1),
            ColorScheme = FieldScheme
        };

        _gameDataDirectoryField = new TextField(_settings.GameDataDirectory)
        {
            X = 21,
            Y = Pos.Bottom(_contentView) + 9,
            Width = Dim.Fill(1),
            ColorScheme = FieldScheme
        };

        _localPlayerNameField = new TextField(_settings.LocalPlayerName)
        {
            X = 21,
            Y = Pos.Bottom(_contentView) + 11,
            Width = 28,
            ColorScheme = FieldScheme
        };

        _settingsViews.AddRange([
            serverLabel,
            _serverField,
            logLevelLabel,
            _logLevelField,
            packetFilterLabel,
            _packetFilterField,
            selectedDevicesLabel,
            _selectedDevicesField,
            gameDataLanguageLabel,
            _gameDataLanguageField,
            mainGameFolderLabel,
            _mainGameFolderField,
            gameDataDirectoryLabel,
            _gameDataDirectoryField,
            localPlayerNameLabel,
            _localPlayerNameField
        ]);

        foreach (var settingView in _settingsViews)
        {
            settingView.KeyDown += args =>
            {
                if (args.KeyEvent.Key != Key.Esc)
                {
                    return;
                }

                _sectionList.SetFocus();
                args.Handled = true;
            };
        }

        window.Add(
            _headerLabel,
            _statusLabel,
            _sectionList,
            _contentView,
            deviceHelpLabel,
            _deviceListView,
            serverLabel,
            _serverField,
            logLevelLabel,
            _logLevelField,
            packetFilterLabel,
            _packetFilterField,
            selectedDevicesLabel,
            _selectedDevicesField,
            gameDataLanguageLabel,
            _gameDataLanguageField,
            mainGameFolderLabel,
            _mainGameFolderField,
            gameDataDirectoryLabel,
            _gameDataDirectoryField,
            localPlayerNameLabel,
            _localPlayerNameField);

        top.Add(window);
        top.Add(new StatusBar(new[]
        {
            new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Quit", () => Application.RequestStop()),
            new StatusItem(Key.F2, "~F2~ Save", SaveSettings),
            new StatusItem(Key.F3, "~F3~ Devices", FocusDevices),
            new StatusItem(Key.F5, "~F5~ Refresh", RefreshAndRender),
            new StatusItem(Key.F6, "~F6~ Start/Stop Capture", ToggleLiveCapture),
            new StatusItem(Key.F7, "~F7~ Reset DPS", ResetDpsMeter),
            new StatusItem(Key.F8, "~F8~ Reset Loot", ResetLootAndRates),
            new StatusItem(Key.F9, "~F9~ Export Loot", ExportLootHtml)
        })
        {
            ColorScheme = MenuScheme
        });

        _sectionList.SetFocus();
    }

    private void RefreshAndRender()
    {
        RefreshStatus();
        RenderContent();
    }

    private void StartLiveRefreshTimer()
    {
        Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(1), _ =>
        {
            RenderCurrentSectionText();
            return true;
        });
    }

    private void RefreshStatus()
    {
        _status = _statusService.CreateStatus(_settings);
        AddLog($"Status refreshed at {_status.RefreshedAt:HH:mm:ss}");
        if (!_status.DeviceResult.Success)
        {
            AddLog($"Capture device enumeration failed: {_status.DeviceResult.ExceptionType}: {_status.DeviceResult.ErrorMessage}");
        }
    }

    private void SaveSettings()
    {
        try
        {
            _settings.ServerLocation = _serverField?.Text.ToString() ?? _settings.ServerLocation;
            _settings.PacketFilter = _packetFilterField?.Text.ToString() ?? _settings.PacketFilter;
            _settings.LogLevel = _logLevelField?.Text.ToString() ?? _settings.LogLevel;
            _settings.SelectedDeviceIdentifiers = SplitDeviceIdentifiers(_selectedDevicesField?.Text.ToString());
            _settings.MainGameFolderPath = _mainGameFolderField?.Text.ToString() ?? _settings.MainGameFolderPath;
            _settings.GameDataDirectory = _gameDataDirectoryField?.Text.ToString() ?? _settings.GameDataDirectory;
            _settings.GameDataLanguage = _gameDataLanguageField?.Text.ToString() ?? _settings.GameDataLanguage;
            _settings.LocalPlayerName = _localPlayerNameField?.Text.ToString() ?? _settings.LocalPlayerName;
            _entityNames.SetLocalEntity(null, null, _settings.LocalPlayerName);

            _settingsStore.Save(_settings);
            if (_status is not null)
            {
                _status = _status with { Settings = _settings };
            }

            AddLog("Settings saved. Press F5 to refresh capture devices.");
            if (_liveCaptureService.IsRunning)
            {
                AddLog("Live capture is still using the settings it started with. Press F6 twice to restart it.");
            }

            RenderCurrentSectionText();
        }
        catch (Exception exception)
        {
            AddLog($"Settings save failed: {exception.Message}");
            Log.Error(exception, "Settings save failed");
            RenderCurrentSectionText();
        }
    }

    private void RenderContent()
    {
        if (_status is null || _headerLabel is null || _statusLabel is null || _contentView is null)
        {
            return;
        }

        _headerLabel.Text = $"Server: {_settings.ServerLocation} | Provider: libpcap | Devices: {_status.DeviceResult.Devices.Count} | Items: {_gameData.ItemCount:N0}";
        _statusLabel.Text = $"OS: {_status.OperatingSystem} | .NET: {_status.RuntimeDescription} | Capture: {_status.CapturePermissionStatus}";

        var isSettingsSection = _selectedSection == 7;
        var isDevicesSection = _selectedSection == 1;
        _contentView.Height = isSettingsSection ? Dim.Fill(14) : isDevicesSection ? 4 : Dim.Fill(1);
        foreach (var settingView in _settingsViews)
        {
            settingView.Visible = isSettingsSection;
        }

        foreach (var deviceView in _deviceViews)
        {
            deviceView.Visible = isDevicesSection;
        }

        if (isDevicesSection)
        {
            UpdateDeviceListSource();
        }

        _contentView.Text = BuildCurrentSectionText();
    }

    private void RenderCurrentSectionText()
    {
        if (_status is null || _headerLabel is null || _statusLabel is null || _contentView is null)
        {
            return;
        }

        _headerLabel.Text = $"Server: {_settings.ServerLocation} | Provider: libpcap | Devices: {_status.DeviceResult.Devices.Count} | Items: {_gameData.ItemCount:N0}";
        _statusLabel.Text = $"OS: {_status.OperatingSystem} | .NET: {_status.RuntimeDescription} | Capture: {_status.CapturePermissionStatus}";
        _contentView.Text = BuildCurrentSectionText();
    }

    private string BuildCurrentSectionText()
    {
        if (_status is null)
        {
            return string.Empty;
        }

        return _selectedSection switch
        {
            1 => RenderDevices(_status.DeviceResult, _settings),
            2 => RenderLiveCapture(_liveCaptureService.GetStatus(), _parserStats.GetSnapshot()),
            3 => RenderDpsMeter(_dpsMeter.GetSnapshot()),
            4 => RenderLootLog(_lootLog.GetSnapshot()),
            5 => RenderRates(_activityRates.GetSnapshot()),
            6 => string.Join(Environment.NewLine, _logLines),
            7 => RenderSettings(_status),
            _ => RenderStatus(_status)
        };
    }

    private void ToggleLiveCapture()
    {
        try
        {
            var status = _liveCaptureService.IsRunning
                ? _liveCaptureService.Stop()
                : _liveCaptureService.Start(LiveCaptureOptions.FromSettings(
                    _settings.SelectedDeviceIdentifiers,
                    _settings.PacketFilter));

            AddLog(status.IsRunning
                ? $"Live capture started on {status.OpenedDeviceCount} device(s)"
                : "Live capture stopped");

            if (!string.IsNullOrWhiteSpace(status.LastErrorMessage))
            {
                AddLog($"Live capture: {status.LastErrorMessage}");
            }
        }
        catch (Exception exception)
        {
            AddLog($"Live capture toggle failed: {exception.Message}");
            Log.Error(exception, "Live capture toggle failed");
        }

        RenderContent();
    }

    private void ResetDpsMeter()
    {
        _dpsMeter.Reset();
        AddLog("DPS meter reset.");
        RenderCurrentSectionText();
    }

    private void ResetLootAndRates()
    {
        _lootLog.Reset();
        _activityRates.Reset();
        AddLog("Loot log and rates reset.");
        RenderCurrentSectionText();
    }

    private void ExportLootHtml()
    {
        try
        {
            var filePath = _lootHtmlReportWriter.Write(_lootLog.GetEntries());
            AddLog($"Loot HTML report written: {filePath}");
        }
        catch (Exception exception)
        {
            AddLog($"Loot HTML export failed: {exception.Message}");
            Log.Error(exception, "Loot HTML export failed");
        }

        RenderCurrentSectionText();
    }

    private void FocusDevices()
    {
        if (_sectionList is null || _deviceListView is null)
        {
            return;
        }

        if (_selectedSection != 1)
        {
            _sectionList.SelectedItem = 1;
            _selectedSection = 1;
            RenderContent();
        }

        if (_visibleDevices.Count > 0)
        {
            _deviceListView.SetFocus();
        }
    }

    private string RenderStatus(AppStatus status)
    {
        return string.Join(Environment.NewLine,
            "Status",
            "------",
            "Native Linux startup/status milestone",
            string.Empty,
            $"Refreshed: {status.RefreshedAt:yyyy-MM-dd HH:mm:ss zzz}",
            $"Runtime: {status.RuntimeDescription}",
            $"OS: {status.OperatingSystem}",
            $"Architecture: {status.Architecture}",
            $"Settings: {status.SettingsPath}",
            $"Root: {(status.IsRoot ? "yes" : "no")}",
            $"Capture permission: {status.CapturePermissionStatus}",
            $"Capture device enumeration: {(status.DeviceResult.Success ? "ok" : "failed")}",
            $"Live capture: {(status.Settings is not null ? "available" : "not configured")}",
            $"Game data: {_gameData.Status}",
            $"Known entities: {_entityNames.KnownEntityCount:N0}",
            string.Empty,
            "Navigation: Up/Down changes sections. F6 starts/stops live capture. Tab enters settings fields. Esc returns to section navigation.",
            status.DeviceResult.Success ? string.Empty : $"{status.DeviceResult.ExceptionType}: {status.DeviceResult.ErrorMessage}");
    }

    private static string RenderDevices(CaptureDeviceEnumerationResult deviceResult, LinuxSettings settings)
    {
        if (!deviceResult.Success)
        {
            return $"Device enumeration failed{Environment.NewLine}{deviceResult.ExceptionType}: {deviceResult.ErrorMessage}";
        }

        if (deviceResult.Devices.Count == 0)
        {
            return "No active non-loopback libpcap devices were found.";
        }

        var lines = new List<string>
        {
            "Devices",
            "-------"
        };

        var selectedIdentifiers = settings.SelectedDeviceIdentifiers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var device in deviceResult.Devices)
        {
            var selected = selectedIdentifiers.Contains(device.Identifier) ? "*" : " ";
            lines.Add($"[{selected}] #{device.Index} {device.Name}");
            lines.Add($"    id: {device.Identifier}");
        }

        lines.Add(string.Empty);
        lines.Add("Press F3 to focus the device list. Space/Enter toggles selected interfaces. Press F2 to save.");

        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderLiveCapture(LiveCaptureStatus status, ParserStatsSnapshot parserStats)
    {
        return string.Join(Environment.NewLine,
            "Capture",
            "-------",
            $"State: {(status.IsRunning ? "running" : "stopped")}",
            $"Opened devices: {status.OpenedDeviceCount}",
            $"Captured packets: {status.CapturedPacketCount}",
            $"Photon UDP payloads: {status.PhotonPayloadCount}",
            $"Parsed events: {parserStats.EventCount}",
            $"Parsed requests: {parserStats.RequestCount}",
            $"Parsed responses: {parserStats.ResponseCount}",
            $"Parser errors: {status.ReceiverErrorCount}",
            $"Active device: {status.ActiveDeviceName ?? "(none yet)"}",
            $"Last Photon payload: {FormatNullableTime(status.LastPhotonPayloadAt)}",
            $"Last error: {status.LastErrorMessage ?? "(none)"}",
            string.Empty,
            "Press F6 to start or stop capture.",
            "Use DPS, Loot, and Rates while capture is running. F7 resets DPS. F8 resets loot/rates. F9 exports loot HTML.",
            "Use Settings to select device ids and packet filter before starting.");
    }

    private string RenderDpsMeter(DpsMeterSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "DPS Meter",
            "---------",
            $"State: {(snapshot.InCombat ? "combat" : "idle")}",
            $"Combat started: {FormatNullableTime(snapshot.CombatStartedAt)}",
            $"Last combat event: {FormatNullableTime(snapshot.LastCombatEventAt)}",
            $"Total damage: {FormatNumber(snapshot.TotalDamage)} | Total heal: {FormatNumber(snapshot.TotalHeal)} | Taken: {FormatNumber(snapshot.TotalTakenDamage)}",
            string.Empty,
            "Party member        Damage       DPS       Heal       HPS      Taken  Hits",
            "--------------------------------------------------------------------------"
        };

        if (snapshot.Entries.Count == 0)
        {
            lines.Add("No party health packets observed yet. Start capture and join/observe party combat.");
        }
        else
        {
            foreach (var entry in snapshot.Entries)
            {
                lines.Add($"{Trim(_entityNames.Resolve(entry.EntityId), 18),-18} {FormatNumber(entry.Damage),8} {entry.Dps,9:0.0} {FormatNumber(entry.Heal),9} {entry.Hps,9:0.0} {FormatNumber(entry.TakenDamage),10} {entry.HitCount,5}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("F7 resets DPS. Only your local character and known party members are shown.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderLootLog(LootLogSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "Loot Logger",
            "-----------",
            $"Item pickups: {snapshot.TotalEvents}",
            string.Empty,
            "Time      Looter              Source              Loot",
            "------------------------------------------------------"
        };

        if (snapshot.RecentEntries.Count == 0)
        {
            lines.Add("No item pickup packets observed yet. Silver is tracked in Rates, not Loot.");
        }
        else
        {
            foreach (var entry in snapshot.RecentEntries)
            {
                var lootText = entry.IsSilver
                    ? $"{FormatNumber(entry.Quantity)} silver"
                    : $"{FormatNumber(entry.Quantity)} x {entry.ItemName}";
                lines.Add($"{entry.Time:HH:mm:ss}  {Trim(entry.LooterName, 18),-18} {Trim(entry.SourceName, 18),-18} {lootText}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("F8 resets loot/rates. F9 exports HTML with Albion render item icons.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderRates(ActivityRatesSnapshot snapshot)
    {
        return string.Join(Environment.NewLine,
            "Rates",
            "-----",
            $"Started: {FormatNullableTime(snapshot.StartedAt)}",
            $"Last event: {FormatNullableTime(snapshot.LastEventAt)}",
            $"Tracked time: {FormatDuration(snapshot.ActiveHours)}",
            string.Empty,
            "Metric              Total          Per hour",
            "-------------------------------------------",
            $"Silver       {FormatNumber(snapshot.Silver),12} {FormatNumber(snapshot.SilverPerHour),16}",
            $"Fame         {FormatNumber(snapshot.Fame),12} {FormatNumber(snapshot.FamePerHour),16}",
            $"ReSpec       {FormatNumber(snapshot.ReSpecPoints),12} {FormatNumber(snapshot.ReSpecPointsPerHour),16}",
            $"Faction      {FormatNumber(snapshot.FactionPoints),12} {FormatNumber(snapshot.FactionPointsPerHour),16}",
            $"Might        {FormatNumber(snapshot.Might),12} {FormatNumber(snapshot.MightPerHour),16}",
            $"Favor        {FormatNumber(snapshot.Favor),12} {FormatNumber(snapshot.FavorPerHour),16}",
            string.Empty,
            "Rates are based on observed packets since reset/start. F8 resets loot and rates.");
    }

    private string RenderSettings(AppStatus status)
    {
        return string.Join(Environment.NewLine,
            "Settings",
            "--------",
            "Editable now:",
            "- serverLocation: Europe, America, Asia, or any text value for later mapping",
            "- packetFilter: optional libpcap/BPF filter",
            "- logLevel: currently stored for later logging controls",
            "- selectedDeviceIdentifiers: comma-separated libpcap device ids",
            "- mainGameFolderPath: Albion install folder; used to generate IndexedItems.json",
            "- gameDataDirectory: optional folder containing IndexedItems.json",
            "- gameDataLanguage: item display language such as en-US",
            "- localPlayerName: character name used when capture starts after login",
            string.Empty,
            "Tab into fields to edit. Press Esc to return to section navigation, then F2 to save.",
            "Restart the TUI after changing game data paths so the item index reloads.",
            string.Empty,
            $"Server location: {status.Settings.ServerLocation}",
            $"Packet filter: {status.Settings.PacketFilter}",
            $"Log level: {status.Settings.LogLevel}",
            $"Selected devices: {(status.Settings.SelectedDeviceIdentifiers.Count == 0 ? "(none)" : string.Join(", ", status.Settings.SelectedDeviceIdentifiers))}",
            $"Albion folder: {BlankAsNone(status.Settings.MainGameFolderPath)}",
            $"Game data dir: {BlankAsNone(status.Settings.GameDataDirectory)}",
            $"Game data language: {status.Settings.GameDataLanguage}",
            $"Local player name: {BlankAsNone(status.Settings.LocalPlayerName)}",
            $"Indexed items file: {_gameData.IndexedItemsFile}",
            $"Game data status: {_gameData.Status}");
    }

    private static void ShowAbout()
    {
        MessageBox.Query(
            "About",
            "Albion Online Statistics Analysis - Linux TUI\nStartup/status milestone",
            "OK");
    }

    private static string FormatNullableTime(DateTimeOffset? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "(none)";
    }

    private static string FormatDuration(double hours)
    {
        if (hours <= 0)
        {
            return "00:00:00";
        }

        return TimeSpan.FromHours(hours).ToString(@"hh\:mm\:ss");
    }

    private static string FormatEntity(long entityId)
    {
        return entityId == 0 ? "(unknown)" : $"entity {entityId}";
    }

    private static string BlankAsNone(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    }

    private static string FormatNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "0";
        }

        return FormatNumber((long)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    private static string FormatNumber(long value)
    {
        return value.ToString("N0");
    }

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(unknown)";
        }

        return value.Length <= maxLength ? value : value[..Math.Max(0, maxLength - 1)] + "~";
    }

    private void ToggleSelectedDevice()
    {
        if (_deviceListView is null || _deviceListView.SelectedItem < 0 || _deviceListView.SelectedItem >= _visibleDevices.Count)
        {
            return;
        }

        var device = _visibleDevices[_deviceListView.SelectedItem];
        var selected = _settings.SelectedDeviceIdentifiers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedNow = !selected.Remove(device.Identifier);
        if (selectedNow)
        {
            selected.Add(device.Identifier);
        }

        _settings.SelectedDeviceIdentifiers = selected.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        if (_selectedDevicesField is not null)
        {
            _selectedDevicesField.Text = string.Join(", ", _settings.SelectedDeviceIdentifiers);
        }

        if (_status is not null)
        {
            _status = _status with { Settings = _settings };
        }

        UpdateDeviceListSource();
        AddLog($"{(selectedNow ? "Selected" : "Unselected")} device {device.Identifier}. Press F2 to save.");
        RenderCurrentSectionText();
        _deviceListView.SetFocus();
    }

    private void UpdateDeviceListSource()
    {
        if (_status is null || _deviceListView is null)
        {
            return;
        }

        var selected = _settings.SelectedDeviceIdentifiers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _visibleDevices.Clear();
        _visibleDevices.AddRange(_status.DeviceResult.Devices);

        var previousSelectedItem = Math.Max(0, _deviceListView.SelectedItem);
        var deviceListItems = new List<string>();
        foreach (var device in _visibleDevices)
        {
            var marker = selected.Contains(device.Identifier) ? "[x]" : "[ ]";
            deviceListItems.Add($"{marker} #{device.Index} {device.Name}");
        }

        if (deviceListItems.Count == 0)
        {
            deviceListItems.Add("No selectable interfaces. Press F5 to refresh.");
        }

        _deviceListView.SetSource(deviceListItems);
        _deviceListView.SelectedItem = Math.Min(previousSelectedItem, Math.Max(0, _visibleDevices.Count - 1));
    }

    private void AddLog(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > 500)
        {
            _logLines.RemoveAt(0);
        }

        Log.Information("{Message}", line);
    }

    private static List<string> SplitDeviceIdentifiers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ApplyDarkTheme()
    {
        Colors.ColorSchemes["Base"] = BaseScheme;
        Colors.ColorSchemes["TopLevel"] = BaseScheme;
        Colors.ColorSchemes["Dialog"] = PanelScheme;
        Colors.ColorSchemes["Menu"] = MenuScheme;
        Colors.ColorSchemes["Error"] = new ColorScheme
        {
            Normal = GuiAttribute.Make(Color.White, Color.Red),
            Focus = GuiAttribute.Make(Color.White, Color.BrightRed),
            HotNormal = GuiAttribute.Make(Color.BrightYellow, Color.Red),
            HotFocus = GuiAttribute.Make(Color.BrightYellow, Color.BrightRed),
            Disabled = GuiAttribute.Make(Color.Gray, Color.Red)
        };
    }
}
