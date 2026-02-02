// IDialogService.cs
// Sprint 1.2: Dialog service interface.
//
// References:
// - UI_SPEC.md §9: Dialog functionality (Login, Patient, Filter, Display, UserMgmt, History)

namespace Neo.UI.Services;

/// <summary>
/// Provides dialog display capabilities, decoupled from Window types.
/// </summary>
public interface IDialogService
{
    /// <summary>Show a modal dialog by key and return the result.</summary>
    /// <param name="dialogKey">Dialog identifier (e.g. "Login", "Patient").</param>
    /// <param name="parameter">Optional parameter passed to the dialog ViewModel.</param>
    /// <returns>The dialog result.</returns>
    DialogResult ShowDialog(string dialogKey, object? parameter = null);

    /// <summary>Show a message box with OK button.</summary>
    void ShowMessage(string title, string message);

    /// <summary>Show a confirmation dialog with OK/Cancel.</summary>
    bool ShowConfirmation(string title, string message);
}
