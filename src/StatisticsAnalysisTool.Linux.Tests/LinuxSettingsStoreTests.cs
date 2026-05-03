using StatisticsAnalysisTool.Linux.Core;

namespace StatisticsAnalysisTool.Linux.Tests;

public sealed class LinuxSettingsStoreTests
{
    [Test]
    public async Task LoadAsync_creates_default_settings_when_file_is_missing()
    {
        var tempDirectory = CreateTempDirectory();
        var paths = new LinuxAppPaths(tempDirectory);
        var store = new LinuxSettingsStore(paths);

        var settings = await store.LoadAsync();

        Assert.That(settings.ServerLocation, Is.EqualTo("Europe"));
        Assert.That(settings.PacketFilter, Is.Empty);
        Assert.That(settings.LogLevel, Is.EqualTo("Information"));
        Assert.That(File.Exists(paths.SettingsFile), Is.True);
    }

    [Test]
    public async Task SaveAsync_normalizes_and_round_trips_settings()
    {
        var tempDirectory = CreateTempDirectory();
        var store = new LinuxSettingsStore(new LinuxAppPaths(tempDirectory));

        await store.SaveAsync(new LinuxSettings
        {
            ServerLocation = " Europe ",
            PacketFilter = " udp ",
            LogLevel = " Debug ",
            SelectedDeviceIdentifiers = [" eth0 ", "eth0", "", "wlan0"]
        });

        var settings = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(settings.ServerLocation, Is.EqualTo("Europe"));
            Assert.That(settings.PacketFilter, Is.EqualTo("udp"));
            Assert.That(settings.LogLevel, Is.EqualTo("Debug"));
            Assert.That(settings.SelectedDeviceIdentifiers, Is.EqualTo(new[] { "eth0", "wlan0" }));
        });
    }

    [Test]
    public void LinuxAppPaths_uses_explicit_override_before_environment()
    {
        var tempDirectory = CreateTempDirectory();
        var paths = new LinuxAppPaths(tempDirectory);

        Assert.That(paths.RuntimeDirectory, Does.StartWith(tempDirectory));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sat-linux-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
