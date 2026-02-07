using System.Windows;
using System.Windows.Controls;

namespace Neo.UI.Views.Dialogs;

public sealed record UserManagementDialogPayload(string VerifiedBy, string? SelectedUser);

public partial class UserManagementDialog : Window
{
    private bool _verified;

    public UserManagementDialog()
    {
        InitializeComponent();
    }

    private void OnVerifyClick(object sender, RoutedEventArgs e)
    {
        _verified = !string.IsNullOrWhiteSpace(AdminPasswordBox.Password);
        ErrorText.Visibility = _verified ? Visibility.Collapsed : Visibility.Visible;
        ErrorText.Text = _verified ? string.Empty : "Admin password is required.";
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (!_verified)
        {
            ErrorText.Text = "Verify admin password before confirm.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        string? selectedUser = (UserList.SelectedItem as ListBoxItem)?.Content?.ToString();
        Tag = new UserManagementDialogPayload("admin", selectedUser);
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
