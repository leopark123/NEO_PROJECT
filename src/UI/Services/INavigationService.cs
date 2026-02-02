// INavigationService.cs
// Sprint 1.2: Navigation service interface.
//
// References:
// - UI_SPEC.md §4.1: Left navigation bar (首页/历史/显示/滤波/用户/导出/关机)

using Neo.UI.ViewModels;

namespace Neo.UI.Services;

/// <summary>
/// Provides ViewModel-based navigation within the main content area.
/// </summary>
public interface INavigationService
{
    /// <summary>The currently active ViewModel.</summary>
    ViewModelBase? CurrentViewModel { get; }

    /// <summary>Navigate to a registered route by key.</summary>
    /// <param name="routeKey">Route key (e.g. "Home", "History").</param>
    /// <param name="parameter">Optional parameter passed to the target ViewModel.</param>
    /// <returns>True if navigation succeeded.</returns>
    bool NavigateTo(string routeKey, object? parameter = null);

    /// <summary>Raised when CurrentViewModel changes.</summary>
    event EventHandler<ViewModelBase?>? CurrentViewModelChanged;
}
