namespace StatisticsAnalysisTool.Linux.Core;

public sealed class LinuxSettings
{
    public string ServerLocation { get; set; } = "Europe";

    public string PacketFilter { get; set; } = string.Empty;

    public List<string> SelectedDeviceIdentifiers { get; set; } = [];

    public string LogLevel { get; set; } = "Information";

    public string MainGameFolderPath { get; set; } = string.Empty;

    public string GameDataDirectory { get; set; } = string.Empty;

    public string GameDataLanguage { get; set; } = "en-US";

    public string LocalPlayerName { get; set; } = string.Empty;
}
