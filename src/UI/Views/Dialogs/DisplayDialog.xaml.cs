using System.Windows;
using System.Windows.Controls;

namespace Neo.UI.Views.Dialogs;

public sealed record DisplayDialogPayload(string SweepSpeed, bool ShowScale, bool ShowEegGrid, bool ShowAeegGrid);

public partial class DisplayDialog : Window
{
    public DisplayDialog()
    {
        InitializeComponent();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        string speed = (SweepCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "15 s";
        Tag = new DisplayDialogPayload(
            speed,
            ScaleToggle.IsChecked == true,
            EegGridToggle.IsChecked == true,
            AeegGridToggle.IsChecked == true);
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
