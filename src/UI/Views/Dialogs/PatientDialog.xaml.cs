using System.Windows;

namespace Neo.UI.Views.Dialogs;

public sealed record PatientDialogPayload(string PatientName, string AdmissionId, string BedNumber, string Gender);

public partial class PatientDialog : Window
{
    public PatientDialog()
    {
        InitializeComponent();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        string admissionId = AdmissionIdTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(admissionId))
        {
            ErrorText.Text = "Admission ID is required.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        string patientName = PatientNameTextBox.Text.Trim();
        string bedNumber = BedTextBox.Text.Trim();
        string gender = (GenderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Unknown";

        Tag = new PatientDialogPayload(patientName, admissionId, bedNumber, gender);
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
