// DialogResult.cs
// Sprint 1.2: Dialog result wrapper.

namespace Neo.UI.Services;

/// <summary>
/// Result returned from a dialog interaction.
/// </summary>
public sealed class DialogResult
{
    /// <summary>True if the user confirmed (OK/Save/etc.).</summary>
    public bool Confirmed { get; init; }

    /// <summary>Optional data returned by the dialog.</summary>
    public object? Data { get; init; }

    public static DialogResult Ok(object? data = null) => new() { Confirmed = true, Data = data };
    public static DialogResult Cancel() => new() { Confirmed = false };
}
