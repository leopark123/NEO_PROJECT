// INavigable.cs
// Sprint 1.2: Interface for ViewModels that support navigation lifecycle.

namespace Neo.UI.Services;

/// <summary>
/// Implemented by ViewModels that need navigation lifecycle callbacks.
/// </summary>
public interface INavigable
{
    /// <summary>Called when the ViewModel is navigated to.</summary>
    void OnNavigatedTo(object? parameter);

    /// <summary>Called when the ViewModel is navigated away from.</summary>
    void OnNavigatedFrom();
}
