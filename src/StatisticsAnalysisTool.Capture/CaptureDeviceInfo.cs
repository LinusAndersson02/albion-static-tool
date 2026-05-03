namespace StatisticsAnalysisTool.Capture;

public sealed record CaptureDeviceInfo(
    int Index,
    string Identifier,
    string Name,
    string Description,
    bool IsLoopback,
    bool IsUp,
    bool IsSelected);
