using System.Windows;
using System.Windows.Controls;

namespace Neo.UI.Views.Dialogs;

public partial class HistoryDialog : Window
{
    public HistoryDialog()
    {
        InitializeComponent();
    }

    private void OnQueryClick(object sender, RoutedEventArgs e)
    {
        ResultList.Items.Clear();

        string name = NameTextBox.Text.Trim();
        string admission = AdmissionTextBox.Text.Trim();

        string key = string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(admission)
            ? "record"
            : $"{name}{admission}";

        ResultList.Items.Add(new ListBoxItem { Content = $"{key}-001 | 2026-02-01 08:10 | 2026-02-01 10:30" });
        ResultList.Items.Add(new ListBoxItem { Content = $"{key}-002 | 2026-02-03 06:40 | 2026-02-03 09:15" });
        ResultList.Items.Add(new ListBoxItem { Content = $"{key}-003 | 2026-02-04 12:00 | 2026-02-04 12:35" });
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        string? selected = (ResultList.SelectedItem as ListBoxItem)?.Content?.ToString();
        if (string.IsNullOrWhiteSpace(selected))
        {
            ErrorText.Text = "Select one history record to load.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Tag = selected;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
