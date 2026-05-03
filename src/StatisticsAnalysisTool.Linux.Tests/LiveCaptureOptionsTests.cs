using StatisticsAnalysisTool.Capture;

namespace StatisticsAnalysisTool.Linux.Tests;

public sealed class LiveCaptureOptionsTests
{
    [Test]
    public void FromSettings_normalizes_device_ids_and_filter()
    {
        var options = LiveCaptureOptions.FromSettings(
            [" eth0 ", "eth0", "", "wlan0"],
            " udp ");

        Assert.Multiple(() =>
        {
            Assert.That(options.SelectedDeviceIdentifiers, Is.EqualTo(new[] { "eth0", "wlan0" }));
            Assert.That(options.PacketFilter, Is.EqualTo("udp"));
        });
    }
}
