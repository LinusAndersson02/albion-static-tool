using StatisticsAnalysisTool.Capture;

namespace StatisticsAnalysisTool.Linux.Core;

public sealed record AppStatus(
    DateTimeOffset RefreshedAt,
    string RuntimeDescription,
    string OperatingSystem,
    string Architecture,
    string SettingsPath,
    bool IsRoot,
    string CapturePermissionStatus,
    LinuxSettings Settings,
    CaptureDeviceEnumerationResult DeviceResult);
