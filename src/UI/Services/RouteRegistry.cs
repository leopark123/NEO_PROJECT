// RouteRegistry.cs
// Sprint 1.2: Maps route keys to ViewModel factory functions.

using Neo.UI.ViewModels;

namespace Neo.UI.Services;

/// <summary>
/// Registry mapping route keys to ViewModel factories.
/// Routes are registered at startup via <see cref="ServiceRegistry"/>.
/// </summary>
public sealed class RouteRegistry
{
    private readonly Dictionary<string, Func<ViewModelBase>> _routes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Register a route key with a ViewModel factory.</summary>
    public void Register(string routeKey, Func<ViewModelBase> factory)
    {
        ArgumentNullException.ThrowIfNull(routeKey);
        ArgumentNullException.ThrowIfNull(factory);
        _routes[routeKey] = factory;
    }

    /// <summary>Try to create a ViewModel for the given route key.</summary>
    public bool TryCreate(string routeKey, out ViewModelBase? viewModel)
    {
        if (_routes.TryGetValue(routeKey, out var factory))
        {
            viewModel = factory();
            return true;
        }
        viewModel = null;
        return false;
    }

    /// <summary>Check if a route key is registered.</summary>
    public bool Contains(string routeKey) => _routes.ContainsKey(routeKey);
}
