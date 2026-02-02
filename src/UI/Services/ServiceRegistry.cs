// ServiceRegistry.cs
// Sprint 1.2: Simple DI bootstrap for Neo.UI services.
//
// This is a lightweight service locator used during startup.
// All services are singletons created here and injected into ViewModels.
//
// References:
// - UI_SPEC.md §3: MVVM architecture

namespace Neo.UI.Services;

/// <summary>
/// Bootstraps and provides access to all UI services.
/// Created once at application startup.
/// </summary>
public sealed class ServiceRegistry
{
    public RouteRegistry Routes { get; }
    public INavigationService Navigation { get; }
    public IDialogService Dialog { get; }
    public IAuditService Audit { get; }

    public ServiceRegistry()
    {
        Routes = new RouteRegistry();
        Navigation = new NavigationService(Routes);
        Dialog = new DialogService();
        Audit = new AuditServiceAdapter();
    }
}
