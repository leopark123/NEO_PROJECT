using System.Windows;
using Neo.UI.Views.Dialogs;

namespace Neo.UI.Services;

public sealed class DialogService : IDialogService
{
    private readonly IReadOnlyDictionary<string, Func<object?, object>> _dialogFactories;
    private readonly Func<object, bool?> _showDialog;
    private readonly Func<object, object?> _getDialogData;
    private readonly Action<object, Window> _setOwner;
    private readonly Action<string, string> _showMessage;
    private readonly Func<string, string, bool> _showConfirmation;

    public DialogService(
        IReadOnlyDictionary<string, Func<object?, object>>? dialogFactories = null,
        Func<object, bool?>? showDialog = null,
        Func<object, object?>? getDialogData = null,
        Action<object, Window>? setOwner = null,
        Action<string, string>? showMessage = null,
        Func<string, string, bool>? showConfirmation = null)
    {
        _dialogFactories = dialogFactories ?? CreateDefaultDialogFactories();
        _showDialog = showDialog ?? (dialog => ((Window)dialog).ShowDialog());
        _getDialogData = getDialogData ?? (dialog => ((Window)dialog).Tag);
        _setOwner = setOwner ?? ((dialog, owner) =>
        {
            if (dialog is Window window && window.Owner is null && owner != window)
            {
                window.Owner = owner;
            }
        });
        _showMessage = showMessage ?? ((title, message) =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
        _showConfirmation = showConfirmation ?? ((title, message) =>
            MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK);
    }

    public DialogResult ShowDialog(string dialogKey, object? parameter = null)
    {
        if (string.IsNullOrWhiteSpace(dialogKey))
        {
            return DialogResult.Cancel();
        }

        if (!_dialogFactories.TryGetValue(dialogKey, out var factory))
        {
            return DialogResult.Cancel();
        }

        object dialog = factory(parameter);
        if (Application.Current?.MainWindow is Window owner)
        {
            _setOwner(dialog, owner);
        }

        bool? confirmed = _showDialog(dialog);
        return confirmed == true
            ? DialogResult.Ok(_getDialogData(dialog))
            : DialogResult.Cancel();
    }

    public void ShowMessage(string title, string message)
    {
        _showMessage(title, message);
    }

    public bool ShowConfirmation(string title, string message)
    {
        return _showConfirmation(title, message);
    }

    private static IReadOnlyDictionary<string, Func<object?, object>> CreateDefaultDialogFactories()
    {
        var factories = new Dictionary<string, Func<object?, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Login"] = parameter => CreateDialog<LoginDialog>(parameter),
            ["Patient"] = parameter => CreateDialog<PatientDialog>(parameter),
            ["Filter"] = parameter => CreateDialog<FilterDialog>(parameter),
            ["Display"] = parameter => CreateDialog<DisplayDialog>(parameter),
            ["UserManagement"] = parameter => CreateDialog<UserManagementDialog>(parameter),
            ["History"] = parameter => CreateDialog<HistoryDialog>(parameter),
            ["Password"] = parameter => CreateDialog<PasswordDialog>(parameter)
        };

        return factories;
    }

    private static Window CreateDialog<TDialog>(object? parameter) where TDialog : Window, new()
    {
        var dialog = new TDialog();
        if (parameter is not null)
        {
            dialog.DataContext = parameter;
        }

        return dialog;
    }
}
