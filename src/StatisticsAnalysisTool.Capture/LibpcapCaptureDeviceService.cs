using Libpcap;

namespace StatisticsAnalysisTool.Capture;

public sealed class LibpcapCaptureDeviceService : ICaptureDeviceService
{
    public CaptureDeviceEnumerationResult EnumerateDevices(IReadOnlySet<string> selectedDeviceIdentifiers)
    {
        try
        {
            var devices = Pcap.ListDevices();
            var result = new List<CaptureDeviceInfo>(devices.Count);

            for (var i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var identifier = device.Name ?? string.Empty;
                var description = device.Description ?? string.Empty;
                var displayName = string.IsNullOrWhiteSpace(description)
                    ? identifier
                    : $"{description} ({identifier})";
                var isLoopback = device.Flags.HasFlag(PcapDeviceFlags.Loopback);
                var isUp = device.Flags.HasFlag(PcapDeviceFlags.Up);

                if (isLoopback || !isUp)
                {
                    continue;
                }

                result.Add(new CaptureDeviceInfo(
                    i,
                    identifier,
                    displayName,
                    description,
                    isLoopback,
                    isUp,
                    selectedDeviceIdentifiers.Contains(identifier)));
            }

            return CaptureDeviceEnumerationResult.Ok(result);
        }
        catch (Exception exception)
        {
            return CaptureDeviceEnumerationResult.Failure(exception);
        }
    }
}
