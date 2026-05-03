namespace StatisticsAnalysisTool.Capture;

public sealed record LiveCaptureStatus(
    bool IsRunning,
    int OpenedDeviceCount,
    long CapturedPacketCount,
    long PhotonPayloadCount,
    long ReceiverErrorCount,
    string? ActiveDeviceName,
    DateTimeOffset? LastPhotonPayloadAt,
    string? LastErrorMessage);
