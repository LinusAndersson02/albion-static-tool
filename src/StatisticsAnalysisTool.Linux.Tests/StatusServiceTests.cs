using StatisticsAnalysisTool.Capture;
using StatisticsAnalysisTool.Linux.Core;

namespace StatisticsAnalysisTool.Linux.Tests;

public sealed class StatusServiceTests
{
    [Test]
    public void CreateStatus_includes_settings_and_capture_result()
    {
        var settings = new LinuxSettings
        {
            ServerLocation = "Europe",
            SelectedDeviceIdentifiers = ["eth0"]
        };
        var service = new StatusService(
            new LinuxAppPaths(Path.Combine(Path.GetTempPath(), $"sat-linux-tests-{Guid.NewGuid():N}")),
            new FakeCaptureDeviceService());

        var status = service.CreateStatus(settings);

        Assert.Multiple(() =>
        {
            Assert.That(status.Settings, Is.SameAs(settings));
            Assert.That(status.DeviceResult.Success, Is.True);
            Assert.That(status.DeviceResult.Devices, Has.Count.EqualTo(1));
            Assert.That(status.DeviceResult.Devices[0].IsSelected, Is.True);
            Assert.That(status.CapturePermissionStatus, Is.Not.Empty);
        });
    }

    [Test]
    public void CaptureDeviceEnumerationResult_failure_preserves_error_details()
    {
        var exception = new InvalidOperationException("libpcap unavailable");

        var result = CaptureDeviceEnumerationResult.Failure(exception);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Devices, Is.Empty);
            Assert.That(result.ErrorMessage, Is.EqualTo("libpcap unavailable"));
            Assert.That(result.ExceptionType, Is.EqualTo(nameof(InvalidOperationException)));
        });
    }

    private sealed class FakeCaptureDeviceService : ICaptureDeviceService
    {
        public CaptureDeviceEnumerationResult EnumerateDevices(IReadOnlySet<string> selectedDeviceIdentifiers)
        {
            return CaptureDeviceEnumerationResult.Ok([
                new CaptureDeviceInfo(
                    0,
                    "eth0",
                    "Ethernet (eth0)",
                    "Ethernet",
                    false,
                    true,
                    selectedDeviceIdentifiers.Contains("eth0"))
            ]);
        }
    }
}
