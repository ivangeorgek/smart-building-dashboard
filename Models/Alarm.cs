namespace SmartBuildingDashboard.Models;

public enum AlarmSeverity { Critical, Warning, Information }

public sealed class Alarm(string id, string sourceId, string source, string description, AlarmSeverity severity, DateTimeOffset triggeredAt)
{
    public string Id { get; } = id;
    public string SourceId { get; } = sourceId;
    public string Source { get; } = source;
    public string Description { get; } = description;
    public AlarmSeverity Severity { get; } = severity;
    public DateTimeOffset TriggeredAt { get; internal set; } = triggeredAt;
    public bool IsActive { get; internal set; } = true;
    public bool IsAcknowledged { get; internal set; }
    public DateTimeOffset? AcknowledgedAt { get; internal set; }
}
