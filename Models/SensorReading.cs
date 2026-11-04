namespace SmartBuildingDashboard.Models;

public sealed record SensorReading(DateTimeOffset Timestamp, double Temperature, double Setpoint);
public sealed record EnergyReading(DateTimeOffset Timestamp, double EnergyKwh);
