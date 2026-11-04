namespace SmartBuildingDashboard.Models;

public sealed record Floor(int Number, string Description, IReadOnlyList<Room> Rooms)
{
    public double AverageTemperature => Rooms.Average(r => r.Temperature);
    public double PowerKw => Rooms.Sum(r => r.PowerKw);
    public double EnergyKwh => Rooms.Sum(r => r.EnergyKwh);
}
