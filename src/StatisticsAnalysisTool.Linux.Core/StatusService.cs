using StatisticsAnalysisTool.Capture;

namespace StatisticsAnalysisTool.Linux.Core;

public sealed class StatusService
{
    private readonly LinuxAppPaths _paths;
    private readonly ICaptureDeviceService _captureDeviceService;

    public StatusService(LinuxAppPaths paths, ICaptureDeviceService captureDeviceService)
    {
        _paths = paths;
        _captureDeviceService = captureDeviceService;
    }

    public AppStatus CreateStatus(LinuxSettings settings)
    {
        var selectedDevices = settings.SelectedDeviceIdentifiers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var deviceResult = _captureDeviceService.EnumerateDevices(selectedDevices);
        var isRoot = LinuxPlatformInfo.IsRoot();

        return new AppStatus(
            DateTimeOffset.Now,
            LinuxPlatformInfo.RuntimeDescription,
            LinuxPlatformInfo.OperatingSystemDescription,
            LinuxPlatformInfo.Architecture,
            _paths.SettingsFile,
            isRoot,
            LinuxPlatformInfo.GetCapturePermissionStatus(isRoot),
            settings,
            deviceResult);
    }
}
