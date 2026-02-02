// AuditEvent.cs
// Sprint 1.2: Audit event data structure.
//
// References:
// - UI_SPEC.md §10: Audit requirements
// - UI_SPEC.md §10: timestamp_us (int64) + event_type + user_id + details

namespace Neo.UI.Services;

/// <summary>
/// Represents a single audit event logged by the UI.
/// All timestamps use int64 microseconds per CHARTER/UI_SPEC.
/// </summary>
public sealed record AuditEvent
{
    /// <summary>Monotonic timestamp in microseconds.</summary>
    public required long TimestampUs { get; init; }

    /// <summary>Event type string per UI_SPEC §10.</summary>
    public required string EventType { get; init; }

    /// <summary>User ID that triggered the event (null if system-generated).</summary>
    public string? UserId { get; init; }

    /// <summary>Free-form details string.</summary>
    public string? Details { get; init; }
}

/// <summary>
/// Well-known audit event types — exhaustive list from UI_SPEC §10.
/// Navigation is NOT in §10; §2.5 ("所有用户操作可审计") is a general principle
/// but §10's specific enumeration takes precedence. Page navigation does not
/// affect patient data, monitoring state, or clinical outcome, so it is not
/// a "关键操作" requiring audit. No audit call is emitted for navigation.
/// </summary>
public static class AuditEventTypes
{
    public const string MonitoringStart = "MONITORING_START";
    public const string MonitoringStop = "MONITORING_STOP";
    public const string FilterChange = "FILTER_CHANGE";
    public const string GainChange = "GAIN_CHANGE";
    public const string Seek = "SEEK";
    public const string Annotation = "ANNOTATION";
    public const string Screenshot = "SCREENSHOT";
    public const string UserLogin = "USER_LOGIN";
    public const string UserLogout = "USER_LOGOUT";
    public const string DeviceDisconnect = "DEVICE_DISCONNECT";
}
