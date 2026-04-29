// ComTekAtomicClock.UI.Dialogs.AboutDialog
//
// Modal About dialog reachable from MainWindow's Help -> About menu.
// Shows the current version (read from the assembly's
// AssemblyInformationalVersion or fallback to AssemblyVersion),
// copyright, MIT license text, NIST attribution, and source-repo URL
// per requirements.txt § 2.9.

using System.Reflection;
using System.Windows;
using Wpf.Ui.Controls;

namespace ComTekAtomicClock.UI.Dialogs;

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
}
