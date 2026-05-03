namespace StatisticsAnalysisTool.Capture;

public interface ICaptureDeviceService
{
    CaptureDeviceEnumerationResult EnumerateDevices(IReadOnlySet<string> selectedDeviceIdentifiers);
}
