// ComTekAtomicClock.UI.Dialogs.HelpDialog
//
// Static help text describing how to use the app. Reachable from the
// "?" button on each tab's clock face -> Help… menu item.

using System.Runtime.Versioning;
using System.Windows;
using Wpf.Ui.Controls;

namespace ComTekAtomicClock.UI.Dialogs;

[SupportedOSPlatform("windows")]
public partial class HelpDialog : FluentWindow
{
    public HelpDialog()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => Close();
}
