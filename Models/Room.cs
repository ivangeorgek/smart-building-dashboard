namespace SmartBuildingDashboard.Models;

public enum HvacMode { Off, Heating, Cooling, Auto }
public enum OperationalStatus { Normal, Warning, Critical, Offline }

public sealed class Room(string id, string name, int floorNumber)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public int FloorNumber { get; } = floorNumber;
    public double Temperature { get; internal set; }
    public double Setpoint { get; internal set; } = 22;
    public double Humidity { get; internal set; }
    public double Co2 { get; internal set; }
    public bool IsOccupied { get; internal set; }
    public bool LightsOn { get; internal set; }
    public int Brightness { get; internal set; } = 75;
    public int EffectiveBrightness => LightsOn ? Brightness : 0;
    public HvacMode HvacMode { get; internal set; } = HvacMode.Auto;
    public bool IsOnline { get; internal set; } = true;
    public DateTimeOffset LastSeen { get; internal set; }
    public double PowerKw { get; internal set; }
    public double EnergyKwh { get; internal set; }
    public List<SensorReading> History { get; } = [];
    public OperationalStatus Status => !IsOnline ? OperationalStatus.Offline
        : Temperature >= 27 ? OperationalStatus.Critical
        : Co2 >= 1000 || Temperature > 25 || Temperature < 19 ? OperationalStatus.Warning
        : OperationalStatus.Normal;
}
