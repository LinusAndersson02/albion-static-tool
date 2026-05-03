# Linux support migration

The current desktop application cannot be made Linux-native by changing only
`TargetFramework`.

The main executable is a WPF/WinForms application:

- `src/StatisticsAnalysisTool/StatisticsAnalysisTool.csproj` targets
  `net10.0-windows`.
- It enables `UseWPF` and `UseWindowsForms`.
- It depends on WPF-only packages such as `LiveChartsCore.SkiaSharpView.WPF`,
  `NetSparkleUpdater.UI.WPF`, `Notification.Wpf`, `Ookii.Dialogs.Wpf`,
  `OpenTK.GLWpfControl`, and `SkiaSharp.Views.WPF`.
- Many view models and models currently reference `System.Windows`,
  `System.Windows.Media`, or `System.Windows.Input` directly.

For a native Linux version, treat the work as a port, not a runtime update.

## Target platform

The first practical Linux target for this repository should be:

- Debian 13 or newer on `x64`
- .NET 10 SDK/runtime
- `libpcap` installed on the host
- packet capture run through `sudo`, `CAP_NET_RAW`/`CAP_NET_ADMIN`, or a
  distribution-specific capture group

## Recommended porting path

1. Keep the existing WPF project as the Windows app.
2. Add a new cross-platform desktop project, preferably Avalonia:
   `src/StatisticsAnalysisTool.Desktop`.
3. Target the new project at `net10.0`, not `net10.0-windows`.
4. Move non-UI logic out of the WPF project into cross-platform libraries:
   controllers, network parsing, market data, file storage, settings,
   localization, and calculations.
5. Replace WPF types in shared models with UI-neutral types:
   - `System.Windows.Visibility` -> `bool` or an app-specific enum
   - `System.Windows.Media.Brush`/`Color` -> serializable color values
   - `System.Windows.Input.ICommand` -> a UI-neutral command abstraction, or
     keep commands inside the UI project
   - `BitmapImage`/WPF image types -> file paths, URIs, or byte data
6. Rebuild the UI in Avalonia XAML. WPF XAML is not source-compatible with
   Avalonia, but the MVVM structure can be reused after the WPF dependencies
   are removed from shared view models.
7. Replace Windows-only services:
   - `ApplicationCore.IsAppStartedAsAdministrator()` should use
     `WindowsPrincipal` only on Windows and Unix effective UID/capability
     checks on Linux.
   - `SystemInfo` should use `RuntimeInformation` and Linux files/commands
     instead of WMI (`System.Management`) on Linux.
   - updater UI should use a cross-platform updater or be disabled for Linux
     until release packaging exists.
   - browser/file opening should use an injected platform service.
8. Keep packet capture on Linux to the libpcap provider. The raw socket provider
   uses Windows-oriented `Socket.IOControl(IOControlCode.ReceiveAll)` behavior
   and should not be the first Linux target.
9. Rename UI text from `Npcap` to a platform-neutral label such as `Packet
   capture`. Show `Npcap` on Windows and `libpcap` on Linux.
10. Add CI jobs for `linux-x64` that build shared projects and the new Linux UI
    project.

## Linux package dependencies

For Debian 13, a developer setup should include:

```bash
sudo apt install libpcap0.8 libpcap-dev
```

Install the .NET 10 SDK using Microsoft's Debian package feed or your
distribution's supported package source.

## Example native build target

Once the cross-platform desktop project exists:

```bash
dotnet publish src/StatisticsAnalysisTool.Desktop/StatisticsAnalysisTool.Desktop.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false
```

If the app needs capture permissions without running the whole UI as root, test
whether the published binary can use Linux capabilities:

```bash
sudo setcap cap_net_raw,cap_net_admin=eip ./StatisticsAnalysisTool.Desktop
```

This may need adjustment depending on how the selected libpcap binding opens
devices on the target distribution.

## Temporary workaround

Running the existing Windows release through Wine may start parts of the UI, but
it should not be treated as supported Linux operation. The current tracking
modes are designed around Windows WPF plus Windows packet-capture assumptions,
so native packet capture is the real compatibility requirement.
