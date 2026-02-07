using System.Windows;

namespace Neo.UI.Views.Dialogs;

public partial class LoginDialog : Window
{
    public LoginDialog()
    {
        InitializeComponent();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        string user = UserNameTextBox.Text.Trim();
        string password = PasswordInput.Password;

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            ErrorText.Text = "User name and password are required.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Tag = user;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
