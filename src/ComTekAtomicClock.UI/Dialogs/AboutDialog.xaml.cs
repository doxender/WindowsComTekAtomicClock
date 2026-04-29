// ComTekAtomicClock.UI.Dialogs.AboutDialog
//
// Modal About dialog reachable from MainWindow's Help -> About menu.
// Shows the current version (read from the assembly's
// AssemblyInformationalVersion or fallback to AssemblyVersion),
// copyright, MIT license text, NIST attribution, and source-repo URL
// per requirements.txt § 2.9.

using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using ComTekAtomicClock.UI.Services;
using Wpf.Ui.Controls;

namespace ComTekAtomicClock.UI.Dialogs;

[SupportedOSPlatform("windows")]
public partial class AboutDialog : FluentWindow
{
    public AboutDialog()
    {
        InitializeComponent();
        VersionText.Text = $"Version {GetAppVersion()}";
    }

    private static string GetAppVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (info is not null && !string.IsNullOrEmpty(info.InformationalVersion))
            return info.InformationalVersion;

        var ver = asm.GetName().Version;
        return ver?.ToString() ?? "unknown";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => Close();

    private void UninstallServiceButton_Click(object sender, RoutedEventArgs e)
    {
        // Fully qualify System.Windows.MessageBox — Wpf.Ui ships its
        // own MessageBox type that collides on simple name.
        var confirm = System.Windows.MessageBox.Show(
            this,
            "This will stop and remove the ComTek Atomic Clock Windows Service\n" +
            "and delete %ProgramData%\\ComTekAtomicClock\\.\n\n" +
            "Your per-user settings (themes, tabs, color overrides) will be\n" +
            "preserved.\n\nContinue?",
            "Uninstall the time-sync service",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        var result = ServiceLauncher.LaunchServiceUninstaller(purgeUserData: false);
        if (!result.Started)
        {
            System.Windows.MessageBox.Show(
                this,
                result.ErrorMessage ?? "Unknown error.",
                "Could not uninstall the time-sync service",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        // Helper runs asynchronously after UAC consent; the
        // MainWindow's service-state polling will pick up the change
        // and refresh the banner.
    }
}
