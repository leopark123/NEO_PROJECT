// AuditServiceTests.cs
// Sprint 1.2-fix P1-3: Tests for AuditServiceAdapter.

using Neo.UI.Services;
using Xunit;

namespace Neo.UI.Tests;

public class AuditServiceTests
{
    [Fact]
    public void Log_StoresEvent()
    {
        var audit = new AuditServiceAdapter();
        audit.Log(AuditEventTypes.GainChange, "100 -> 200", "user1");

        var events = audit.GetRecentEvents(10);
        Assert.Single(events);
        Assert.Equal(AuditEventTypes.GainChange, events[0].EventType);
        Assert.Equal("100 -> 200", events[0].Details);
        Assert.Equal("user1", events[0].UserId);
    }

    [Fact]
    public void Log_TimestampUs_IsPositiveInt64()
    {
        var audit = new AuditServiceAdapter();
        audit.Log(AuditEventTypes.Screenshot);

        var events = audit.GetRecentEvents(1);
        Assert.True(events[0].TimestampUs > 0, "Timestamp should be positive int64 microseconds");
    }

    [Fact]
    public void Log_AllEventTypes_Accepted()
    {
        var audit = new AuditServiceAdapter();

        // All 11 event types (10 from UI_SPEC §10 + LEAD_CHANGE added 2026-02-08)
        audit.Log(AuditEventTypes.MonitoringStart);
        audit.Log(AuditEventTypes.MonitoringStop);
        audit.Log(AuditEventTypes.FilterChange);
        audit.Log(AuditEventTypes.GainChange);
        audit.Log(AuditEventTypes.LeadChange);
        audit.Log(AuditEventTypes.Seek);
        audit.Log(AuditEventTypes.Annotation);
        audit.Log(AuditEventTypes.Screenshot);
        audit.Log(AuditEventTypes.UserLogin);
        audit.Log(AuditEventTypes.UserLogout);
        audit.Log(AuditEventTypes.DeviceDisconnect);

        var events = audit.GetRecentEvents(20);
        Assert.Equal(11, events.Count);
    }

    [Fact]
    public void GetRecentEvents_ReturnsLastN()
    {
        var audit = new AuditServiceAdapter();
        for (int i = 0; i < 10; i++)
            audit.Log("EVENT", $"detail-{i}");

        var last3 = audit.GetRecentEvents(3);
        Assert.Equal(3, last3.Count);
        Assert.Equal("detail-7", last3[0].Details);
        Assert.Equal("detail-8", last3[1].Details);
        Assert.Equal("detail-9", last3[2].Details);
    }

    [Fact]
    public void Log_BoundedAt10K_EvictsOldest()
    {
        var audit = new AuditServiceAdapter();
        for (int i = 0; i < AuditServiceAdapter.MaxEvents + 100; i++)
            audit.Log("EVENT", $"detail-{i}");

        var all = audit.GetRecentEvents(AuditServiceAdapter.MaxEvents + 100);
        Assert.Equal(AuditServiceAdapter.MaxEvents, all.Count);
        // Oldest should be evicted (first 100 gone)
        Assert.Equal("detail-100", all[0].Details);
    }

    [Fact]
    public void Log_NullEventType_Throws()
    {
        var audit = new AuditServiceAdapter();
        Assert.Throws<ArgumentNullException>(() => audit.Log(null!));
    }

    [Fact]
    public async Task Log_ThreadSafe_NoCrash()
    {
        var audit = new AuditServiceAdapter();
        var tasks = new List<Task>();
        for (int t = 0; t < 4; t++)
        {
            int threadId = t;
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                    audit.Log("THREAD_TEST", $"thread-{threadId}-{i}");
            }));
        }
        await Task.WhenAll(tasks);

        var events = audit.GetRecentEvents(3000);
        Assert.True(events.Count > 0);
        Assert.True(events.Count <= AuditServiceAdapter.MaxEvents);
    }

    [Fact]
    public void GetRecentEvents_EmptyAdapter_ReturnsEmpty()
    {
        var audit = new AuditServiceAdapter();
        var events = audit.GetRecentEvents();
        Assert.Empty(events);
    }

    [Fact]
    public void AuditEventTypes_ContainsAllDefinedTypes()
    {
        // Verify all defined event types (10 from UI_SPEC §10 + LEAD_CHANGE added 2026-02-08)
        Assert.Equal("MONITORING_START", AuditEventTypes.MonitoringStart);
        Assert.Equal("MONITORING_STOP", AuditEventTypes.MonitoringStop);
        Assert.Equal("FILTER_CHANGE", AuditEventTypes.FilterChange);
        Assert.Equal("GAIN_CHANGE", AuditEventTypes.GainChange);
        Assert.Equal("LEAD_CHANGE", AuditEventTypes.LeadChange);
        Assert.Equal("SEEK", AuditEventTypes.Seek);
        Assert.Equal("ANNOTATION", AuditEventTypes.Annotation);
        Assert.Equal("SCREENSHOT", AuditEventTypes.Screenshot);
        Assert.Equal("USER_LOGIN", AuditEventTypes.UserLogin);
        Assert.Equal("USER_LOGOUT", AuditEventTypes.UserLogout);
        Assert.Equal("DEVICE_DISCONNECT", AuditEventTypes.DeviceDisconnect);
    }
}
