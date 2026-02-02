// ViewModelBaseTests.cs
// Sprint 1.2-fix P1-3: Tests for ViewModelBase observable property support.

using CommunityToolkit.Mvvm.ComponentModel;
using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

// Concrete test ViewModel to verify ObservableProperty works through ViewModelBase
public partial class TestViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _counter;
}

public class ViewModelBaseTests
{
    [Fact]
    public void ViewModelBase_InheritsObservableObject()
    {
        var vm = new TestViewModel();
        Assert.IsAssignableFrom<ObservableObject>(vm);
    }

    [Fact]
    public void ObservableProperty_RaisesPropertyChanged()
    {
        var vm = new TestViewModel();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.Name = "Test";
        vm.Counter = 42;

        Assert.Contains("Name", raised);
        Assert.Contains("Counter", raised);
    }

    [Fact]
    public void ObservableProperty_DoesNotRaiseWhenValueUnchanged()
    {
        var vm = new TestViewModel { Name = "Same" };
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.Name = "Same"; // same value
        Assert.Empty(raised);
    }

    [Fact]
    public void HomeViewModel_IsViewModelBase()
    {
        var vm = new HomeViewModel();
        Assert.IsAssignableFrom<ViewModelBase>(vm);
    }
}
