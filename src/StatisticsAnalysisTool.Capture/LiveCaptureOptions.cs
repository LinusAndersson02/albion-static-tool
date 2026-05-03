namespace StatisticsAnalysisTool.Capture;

public sealed record LiveCaptureOptions(
    IReadOnlySet<string> SelectedDeviceIdentifiers,
    string PacketFilter)
{
    public static LiveCaptureOptions FromSettings(
        IEnumerable<string> selectedDeviceIdentifiers,
        string? packetFilter)
    {
        return new LiveCaptureOptions(
            selectedDeviceIdentifiers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            packetFilter?.Trim() ?? string.Empty);
    }
}
