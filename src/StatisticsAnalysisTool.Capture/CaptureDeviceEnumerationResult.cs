namespace StatisticsAnalysisTool.Capture;

public sealed record CaptureDeviceEnumerationResult(
    bool Success,
    IReadOnlyList<CaptureDeviceInfo> Devices,
    string? ErrorMessage,
    string? ExceptionType)
{
    public static CaptureDeviceEnumerationResult Ok(IReadOnlyList<CaptureDeviceInfo> devices)
    {
        return new CaptureDeviceEnumerationResult(true, devices, null, null);
    }

    public static CaptureDeviceEnumerationResult Failure(Exception exception)
    {
        return new CaptureDeviceEnumerationResult(
            false,
            Array.Empty<CaptureDeviceInfo>(),
            exception.Message,
            exception.GetType().Name);
    }
}
