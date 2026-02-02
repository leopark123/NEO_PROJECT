// ViewModelBase.cs
// Sprint 1.2: MVVM base class using CommunityToolkit.Mvvm.
//
// All ViewModels in Neo.UI inherit from this class.
// Provides: INotifyPropertyChanged, INotifyPropertyChanging,
//           [ObservableProperty], [RelayCommand] support via source generators.
//
// References:
// - UI_SPEC.md §3: Architecture = MVVM
// - NEO_UI_Development_Plan_WPF.md §9: ObservableObject base

using CommunityToolkit.Mvvm.ComponentModel;

namespace Neo.UI.ViewModels;

/// <summary>
/// Base class for all ViewModels in Neo.UI.
/// Extends <see cref="ObservableObject"/> from CommunityToolkit.Mvvm.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
