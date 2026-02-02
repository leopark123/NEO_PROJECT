# Handoff: UI MVVM Infrastructure (Sprint 1.2)

> **Created**: 2026-01-30
> **Sprint**: 1.2
> **Status**: Complete

---

## Delivered Files

| File | Purpose |
|------|---------|
| `ViewModels/ViewModelBase.cs` | Abstract base class extending `ObservableObject` |
| `ViewModels/MainWindowViewModel.cs` | MainWindow ViewModel (partial, extends ViewModelBase) |
| `Services/INavigable.cs` | Navigation lifecycle interface (OnNavigatedTo/From) |
| `Services/INavigationService.cs` | Navigation service interface |
| `Services/NavigationService.cs` | ViewModel navigation via RouteRegistry |
| `Services/RouteRegistry.cs` | Route key → ViewModel factory mapping |
| `Services/IDialogService.cs` | Dialog service interface |
| `Services/DialogService.cs` | Stub implementation (Phase 4) |
| `Services/DialogResult.cs` | Dialog result wrapper (Confirmed + Data) |
| `Services/IAuditService.cs` | Audit service interface |
| `Services/AuditEvent.cs` | Audit event record + AuditEventTypes constants |
| `Services/AuditServiceAdapter.cs` | In-memory audit sink (bounded 10K, thread-safe) |
| `Services/ServiceRegistry.cs` | DI bootstrap (creates all services) |

## Modified Files

| File | Change |
|------|--------|
| `Neo.UI.csproj` | Added CommunityToolkit.Mvvm 8.2.2 + Neo.Core ProjectReference |
| `App.xaml` | Removed StartupUri (manual window creation) |
| `App.xaml.cs` | ServiceRegistry bootstrap + MainWindow DataContext binding |

---

## API Surface

### ViewModelBase

```csharp
public abstract class ViewModelBase : ObservableObject { }
```

All ViewModels inherit from this. Use `[ObservableProperty]` and `[RelayCommand]` source generators.

### Navigation

```csharp
// Register routes at startup
services.Routes.Register("Home", () => new HomeViewModel());

// Navigate
services.Navigation.NavigateTo("Home", optionalParam);

// Listen for changes
services.Navigation.CurrentViewModelChanged += (s, vm) => { ... };
```

ViewModels implementing `INavigable` receive `OnNavigatedTo`/`OnNavigatedFrom` callbacks.

### Dialog

```csharp
// Show modal dialog (stub — returns Cancel until Phase 4)
var result = services.Dialog.ShowDialog("Login");

// Simple message/confirmation
services.Dialog.ShowMessage("Title", "Message");
bool confirmed = services.Dialog.ShowConfirmation("Title", "Question?");
```

### Audit

```csharp
// Log an audit event
services.Audit.Log(AuditEventTypes.GainChange, "100 -> 200 uV/cm", userId: "admin");

// Read recent events
var events = services.Audit.GetRecentEvents(10);
```

10 event types defined in `AuditEventTypes`:
MONITORING_START, MONITORING_STOP, FILTER_CHANGE, GAIN_CHANGE, SEEK, ANNOTATION, SCREENSHOT, USER_LOGIN, USER_LOGOUT, DEVICE_DISCONNECT

---

## Constraints

- Audit uses in-memory sink (no DB per CHARTER — Storage Sprint not complete)
- DialogService is a stub returning Cancel for all dialog keys
- No medical computation in any ViewModel
- All timestamps use int64 microseconds (Stopwatch-based)

## Next Steps

- **Sprint 1.3**: Style system (Colors.xaml, Fonts.xaml, Buttons.xaml, etc.)
- **Sprint 1.4**: D3DImage renderer validation
- **Phase 2**: Wire navigation buttons to actual ViewModels
- **Phase 4**: Implement dialog ViewModels and register with DialogService
