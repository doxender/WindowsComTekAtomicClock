// ComTekAtomicClock.UI.Dialogs.ThemesDialog
//
// Modal gallery of all 12 clock-face themes. Reachable from the "?"
// overlay button on each tab's clock face -> Themes… menu item. The
// content mirrors design/themes/index.html but trimmed down to just
// names + images per our spec.
//
// Click a tile -> apply that theme to the tab the dialog was opened
// for, set DialogResult=true (so MainWindow can persist), and close.
// The Close button discards (DialogResult=false).

using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using ComTekAtomicClock.UI.ViewModels;
using Wpf.Ui.Controls;

namespace ComTekAtomicClock.UI.Dialogs;

[SupportedOSPlatform("windows")]
public partial class ThemesDialog : FluentWindow
{
    private readonly TabViewModel _tabVm;

    public ThemesDialog(TabViewModel tabVm)
    {
        _tabVm = tabVm ?? throw new ArgumentNullException(nameof(tabVm));
        InitializeComponent();

        EditingTabLabel.Text = $"Pick a theme for: {tabVm.Label}";
    }

    /// <summary>
    /// Click handler on a single theme tile. The tile's DataContext is
    /// the bound <see cref="ThemePreview"/>; copy its
    /// <see cref="ThemePreview.Theme"/> onto the tab and close the
    /// dialog. MainWindow's caller persists settings on
    /// DialogResult==true.
    /// </summary>
    private void ThemeTile_Click(object sender, RoutedEventArgs e)
    {
        // Fully-qualified System.Windows.Controls.Button — the XAML
        // uses the plain WPF Button element (not Wpf.Ui's themed
        // Button), so we disambiguate against Wpf.Ui.Controls.Button
        // which is also pulled in by the ui: namespace.
        if (sender is System.Windows.Controls.Button btn &&
            btn.DataContext is ThemePreview preview)
        {
            _tabVm.Theme = preview.Theme;
            DialogResult = true;
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
