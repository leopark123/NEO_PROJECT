using System.Windows;
using System.Windows.Controls;

namespace Neo.UI.Views.Dialogs;

public sealed record FilterDialogPayload(double HpfHz, int LpfHz, int NotchHz);

public partial class FilterDialog : Window
{
    public FilterDialog()
    {
        InitializeComponent();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        double hpf = ParseDouble(HpfCombo.SelectedItem as ComboBoxItem, 0.5);
        int lpf = ParseInt(LpfCombo.SelectedItem as ComboBoxItem, 35);
        int notch = ParseInt(NotchCombo.SelectedItem as ComboBoxItem, 50);

        Tag = new FilterDialogPayload(hpf, lpf, notch);
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static int ParseInt(ComboBoxItem? item, int fallback)
    {
        if (item?.Content is null)
        {
            return fallback;
        }

        return int.TryParse(item.Content.ToString(), out int value) ? value : fallback;
    }

    private static double ParseDouble(ComboBoxItem? item, double fallback)
    {
        if (item?.Content is null)
        {
            return fallback;
        }

        return double.TryParse(item.Content.ToString(), out double value) ? value : fallback;
    }
}
