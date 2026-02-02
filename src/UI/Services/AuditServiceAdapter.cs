// AuditServiceAdapter.cs
// Sprint 1.2: In-memory audit service implementation.
//
// Uses a bounded in-memory list as the audit sink.
// Will be replaced with a persistent backend (via Neo.Storage)
// when the Storage sprint is completed.
//
// References:
// - UI_SPEC.md §10: Audit requirements
// - CHARTER: No DB schema before Storage Sprint

using System.Diagnostics;

namespace Neo.UI.Services;

/// <summary>
/// In-memory audit service adapter.
/// Thread-safe, bounded to <see cref="MaxEvents"/> entries (FIFO eviction).
/// </summary>
public sealed class AuditServiceAdapter : IAuditService
{
    public const int MaxEvents = 10_000;

    private readonly object _lock = new();
    private readonly List<AuditEvent> _events = new();

    /// <inheritdoc/>
    public void Log(string eventType, string? details = null, string? userId = null)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        var evt = new AuditEvent
        {
            TimestampUs = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1_000_000),
            EventType = eventType,
            UserId = userId,
            Details = details
        };

        lock (_lock)
        {
            if (_events.Count >= MaxEvents)
                _events.RemoveAt(0);
            _events.Add(evt);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<AuditEvent> GetRecentEvents(int count = 50)
    {
        lock (_lock)
        {
            int start = Math.Max(0, _events.Count - count);
            return _events.GetRange(start, _events.Count - start).AsReadOnly();
        }
    }
}
