// IAuditService.cs
// Sprint 1.2: Audit service interface for UI layer.
//
// References:
// - UI_SPEC.md §10: All user operations must be auditable
// - UI_SPEC.md §2.5: All user operations can be audited
// - CHARTER: No DB schema before Storage Sprint → memory sink

namespace Neo.UI.Services;

/// <summary>
/// UI-layer audit service interface.
/// Implementations may write to memory, file, or database.
/// </summary>
public interface IAuditService
{
    /// <summary>Log an audit event.</summary>
    /// <param name="eventType">Event type from <see cref="AuditEventTypes"/>.</param>
    /// <param name="details">Optional details string.</param>
    /// <param name="userId">Optional user ID.</param>
    void Log(string eventType, string? details = null, string? userId = null);

    /// <summary>Retrieve recent audit events (for diagnostics).</summary>
    IReadOnlyList<AuditEvent> GetRecentEvents(int count = 50);
}
