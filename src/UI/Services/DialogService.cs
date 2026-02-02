// DialogService.cs
// Sprint 1.2: Dialog service stub implementation.
//
// Dialogs will be implemented in Phase 4 (Sprint 4.x).
// This stub uses MessageBox for simple operations and returns Cancel
// for unimplemented dialog types.
//
// References:
// - UI_SPEC.md §9: Dialog functionality

using System.Windows;

namespace Neo.UI.Services;

/// <summary>
/// Default implementation of <see cref="IDialogService"/>.
/// Stub — full dialog implementations added in Phase 4.
/// </summary>
public sealed class DialogService : IDialogService
{
    /// <inheritdoc/>
    public DialogResult ShowDialog(string dialogKey, object? parameter = null)
    {
        // Stub: dialogs will be registered and created in Phase 4.
        return DialogResult.Cancel();
    }

    /// <inheritdoc/>
    public void ShowMessage(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <inheritdoc/>
    public bool ShowConfirmation(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        return result == MessageBoxResult.OK;
    }
}
