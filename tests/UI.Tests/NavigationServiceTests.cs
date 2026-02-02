// NavigationServiceTests.cs
// Sprint 1.2-fix P1-3: Tests for NavigationService + RouteRegistry.

using Neo.UI.Services;
using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

public class NavigationServiceTests
{
    private static (NavigationService nav, RouteRegistry routes) CreateService()
    {
        var routes = new RouteRegistry();
        var nav = new NavigationService(routes);
        return (nav, routes);
    }

    [Fact]
    public void NavigateTo_RegisteredRoute_ReturnsTrue()
    {
        var (nav, routes) = CreateService();
        routes.Register("Home", () => new HomeViewModel());

        Assert.True(nav.NavigateTo("Home"));
    }

    [Fact]
    public void NavigateTo_UnregisteredRoute_ReturnsFalse()
    {
        var (nav, _) = CreateService();
        Assert.False(nav.NavigateTo("NonExistent"));
    }

    [Fact]
    public void NavigateTo_SetsCurrentViewModel()
    {
        var (nav, routes) = CreateService();
        routes.Register("Home", () => new HomeViewModel());

        nav.NavigateTo("Home");

        Assert.NotNull(nav.CurrentViewModel);
        Assert.IsType<HomeViewModel>(nav.CurrentViewModel);
    }

    [Fact]
    public void NavigateTo_RaisesCurrentViewModelChanged()
    {
        var (nav, routes) = CreateService();
        routes.Register("Home", () => new HomeViewModel());

        ViewModelBase? received = null;
        nav.CurrentViewModelChanged += (_, vm) => received = vm;

        nav.NavigateTo("Home");

        Assert.NotNull(received);
        Assert.IsType<HomeViewModel>(received);
    }

    [Fact]
    public void NavigateTo_CallsINavigable_OnNavigatedTo()
    {
        var (nav, routes) = CreateService();
        var vm = new NavigableTestViewModel();
        routes.Register("Test", () => vm);

        nav.NavigateTo("Test", "param123");

        Assert.True(vm.WasNavigatedTo);
        Assert.Equal("param123", vm.ReceivedParameter);
    }

    [Fact]
    public void NavigateTo_CallsINavigable_OnNavigatedFrom_OnPreviousViewModel()
    {
        var (nav, routes) = CreateService();
        var first = new NavigableTestViewModel();
        var second = new NavigableTestViewModel();
        routes.Register("First", () => first);
        routes.Register("Second", () => second);

        nav.NavigateTo("First");
        Assert.False(first.WasNavigatedFrom);

        nav.NavigateTo("Second");
        Assert.True(first.WasNavigatedFrom);
    }

    [Fact]
    public void RouteRegistry_CaseInsensitive()
    {
        var routes = new RouteRegistry();
        routes.Register("Home", () => new HomeViewModel());

        Assert.True(routes.Contains("home"));
        Assert.True(routes.Contains("HOME"));
        Assert.True(routes.Contains("Home"));
    }

    [Fact]
    public void NavigateTo_CreatesNewInstanceEachTime()
    {
        var (nav, routes) = CreateService();
        routes.Register("Home", () => new HomeViewModel());

        nav.NavigateTo("Home");
        var first = nav.CurrentViewModel;

        nav.NavigateTo("Home");
        var second = nav.CurrentViewModel;

        Assert.NotSame(first, second);
    }
}

// Test helper
public partial class NavigableTestViewModel : ViewModelBase, INavigable
{
    public bool WasNavigatedTo { get; private set; }
    public bool WasNavigatedFrom { get; private set; }
    public object? ReceivedParameter { get; private set; }

    public void OnNavigatedTo(object? parameter)
    {
        WasNavigatedTo = true;
        ReceivedParameter = parameter;
    }

    public void OnNavigatedFrom()
    {
        WasNavigatedFrom = true;
    }
}
