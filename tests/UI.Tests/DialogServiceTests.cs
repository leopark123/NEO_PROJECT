using Neo.UI.Services;
using Xunit;

namespace Neo.UI.Tests;

public class DialogServiceTests
{
    private sealed class FakeDialog
    {
        public object? Tag { get; set; }
    }

    [Fact]
    public void ShowDialog_KnownKey_ReturnsConfirmedResult()
    {
        var factories = new Dictionary<string, Func<object?, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Filter"] = parameter => new FakeDialog { Tag = parameter ?? "payload" }
        };

        var service = new DialogService(
            factories,
            _ => true,
            dialog => ((FakeDialog)dialog).Tag,
            (_, _) => { },
            (_, _) => { },
            (_, _) => true);

        var result = service.ShowDialog("Filter", "filter-data");

        Assert.True(result.Confirmed);
        Assert.Equal("filter-data", result.Data);
    }

    [Fact]
    public void ShowDialog_UnknownKey_ReturnsCancel()
    {
        var service = new DialogService(
            new Dictionary<string, Func<object?, object>>(StringComparer.OrdinalIgnoreCase),
            _ => true,
            _ => null,
            (_, _) => { },
            (_, _) => { },
            (_, _) => true);

        var result = service.ShowDialog("NotRegistered");

        Assert.False(result.Confirmed);
        Assert.Null(result.Data);
    }

    [Fact]
    public void ShowDialog_WhenDialogReturnsFalse_ReturnsCancel()
    {
        var factories = new Dictionary<string, Func<object?, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Display"] = _ => new FakeDialog { Tag = "display-data" }
        };

        var service = new DialogService(
            factories,
            _ => false,
            dialog => ((FakeDialog)dialog).Tag,
            (_, _) => { },
            (_, _) => { },
            (_, _) => true);

        var result = service.ShowDialog("Display");

        Assert.False(result.Confirmed);
        Assert.Null(result.Data);
    }

    [Fact]
    public void ShowDialog_BlankKey_ReturnsCancel()
    {
        var service = new DialogService(
            new Dictionary<string, Func<object?, object>>(StringComparer.OrdinalIgnoreCase),
            _ => true,
            _ => null,
            (_, _) => { },
            (_, _) => { },
            (_, _) => true);

        Assert.False(service.ShowDialog(string.Empty).Confirmed);
        Assert.False(service.ShowDialog("   ").Confirmed);
    }

    [Fact]
    public void ShowMessage_UsesInjectedHandler()
    {
        bool called = false;
        string? capturedTitle = null;
        string? capturedMessage = null;

        var service = new DialogService(
            new Dictionary<string, Func<object?, object>>(StringComparer.OrdinalIgnoreCase),
            _ => true,
            _ => null,
            (_, _) => { },
            (title, message) =>
            {
                called = true;
                capturedTitle = title;
                capturedMessage = message;
            },
            (_, _) => true);

        service.ShowMessage("Info", "hello");

        Assert.True(called);
        Assert.Equal("Info", capturedTitle);
        Assert.Equal("hello", capturedMessage);
    }

    [Fact]
    public void ShowConfirmation_UsesInjectedHandler()
    {
        var serviceTrue = new DialogService(
            new Dictionary<string, Func<object?, object>>(StringComparer.OrdinalIgnoreCase),
            _ => true,
            _ => null,
            (_, _) => { },
            (_, _) => { },
            (_, _) => true);
        var serviceFalse = new DialogService(
            new Dictionary<string, Func<object?, object>>(StringComparer.OrdinalIgnoreCase),
            _ => true,
            _ => null,
            (_, _) => { },
            (_, _) => { },
            (_, _) => false);

        Assert.True(serviceTrue.ShowConfirmation("t", "m"));
        Assert.False(serviceFalse.ShowConfirmation("t", "m"));
    }
}
