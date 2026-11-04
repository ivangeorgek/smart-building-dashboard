namespace SmartBuildingDashboard.Models;

public enum EquipmentStatus { Running, Standby, Warning, Fault, Offline }

public sealed class Equipment(string id, string name, string type, bool online, EquipmentStatus status, double powerKw)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Type { get; } = type;
    public bool IsOnline { get; } = online;
    public EquipmentStatus Status { get; } = status;
    public double PowerKw { get; } = powerKw;
    public DateTimeOffset LastUpdated { get; internal set; }
}
