// NavigationService.cs
// Sprint 1.2: ViewModel-based navigation implementation.
//
// References:
// - UI_SPEC.md §4.1: Left navigation bar

using Neo.UI.ViewModels;

namespace Neo.UI.Services;

/// <summary>
/// Default implementation of <see cref="INavigationService"/>.
/// Uses <see cref="RouteRegistry"/> to resolve ViewModel factories.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly RouteRegistry _registry;
    private ViewModelBase? _currentViewModel;

    public NavigationService(RouteRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc/>
    public ViewModelBase? CurrentViewModel => _currentViewModel;

    /// <inheritdoc/>
    public event EventHandler<ViewModelBase?>? CurrentViewModelChanged;

    /// <inheritdoc/>
    public bool NavigateTo(string routeKey, object? parameter = null)
    {
        if (!_registry.TryCreate(routeKey, out var newViewModel) || newViewModel is null)
            return false;

        // Notify outgoing ViewModel
        if (_currentViewModel is INavigable outgoing)
            outgoing.OnNavigatedFrom();

        _currentViewModel = newViewModel;

        // Notify incoming ViewModel
        if (_currentViewModel is INavigable incoming)
            incoming.OnNavigatedTo(parameter);

        CurrentViewModelChanged?.Invoke(this, _currentViewModel);
        return true;
    }
}
