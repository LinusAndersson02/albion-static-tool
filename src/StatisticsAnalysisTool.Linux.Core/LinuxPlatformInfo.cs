using System.Runtime.InteropServices;

namespace StatisticsAnalysisTool.Linux.Core;

public static class LinuxPlatformInfo
{
    public static bool IsRoot()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            return geteuid() == 0;
        }
        catch
        {
            return string.Equals(Environment.UserName, "root", StringComparison.Ordinal);
        }
    }

    public static string GetCapturePermissionStatus(bool isRoot)
    {
        return isRoot
            ? "root privileges detected"
            : "not root; libpcap may require sudo, capabilities, or capture group permissions";
    }

    public static string RuntimeDescription => RuntimeInformation.FrameworkDescription;

    public static string OperatingSystemDescription => RuntimeInformation.OSDescription;

    public static string Architecture => RuntimeInformation.OSArchitecture.ToString();

    [DllImport("libc")]
    private static extern uint geteuid();
}
