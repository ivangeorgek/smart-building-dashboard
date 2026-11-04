namespace SmartBuildingDashboard.Models;

public sealed record Building(string Name, IReadOnlyList<Floor> Floors);
