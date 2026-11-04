using SmartBuildingDashboard.Models;

namespace SmartBuildingDashboard.Services;

/// <summary>One instance per Blazor Server circuit. All writes run on its renderer dispatcher.</summary>
public sealed class BuildingStateService
{
    private readonly Random _random = new(42);
    private readonly List<Alarm> _alarms = [];
    private double _equipmentEnergyKwh;
    public Building Building { get; }
    public IReadOnlyList<Room> Rooms { get; }
    public IReadOnlyList<Equipment> Equipment { get; }
    public IReadOnlyList<Alarm> Alarms => _alarms;
    public IReadOnlyList<EnergyReading> HourlyEnergy { get; }
    public DateTimeOffset LastUpdated { get; private set; } = DateTimeOffset.Now;
    public bool IsPaused { get; private set; }
    public double SessionEnergyKwh { get; private set; }
    public TimeSpan SimulatedDuration { get; private set; }
    public double AverageTemperature => Rooms.Average(r => r.Temperature);
    public int OccupiedRooms => Rooms.Count(r => r.IsOccupied);
    public int OnlineDevices => Rooms.Count(r => r.IsOnline) + Equipment.Count(e => e.IsOnline);
    public int TotalDevices => Rooms.Count + Equipment.Count;
    public int ActiveAlarmCount => Alarms.Count(a => a.IsActive);
    public int UnacknowledgedCount => Alarms.Count(a => a.IsActive && !a.IsAcknowledged);
    public double PowerKw => Rooms.Sum(r => r.PowerKw) + Equipment.Sum(e => e.PowerKw);
    public double EnergyKwh => Rooms.Sum(r => r.EnergyKwh) + _equipmentEnergyKwh;
    public const double HighPowerThresholdKw = 16;
    public event Action? Changed;

    public BuildingStateService()
    {
        string[][] names = [
            ["Reception", "Meeting Room A", "Open Office A", "Conference Room"],
            ["Meeting Room B", "Open Office B", "Manager Office", "Training Room"],
            ["Meeting Room C", "Open Office C", "Server Room", "Executive Office"]
        ];
        double[] temperatures = [22.1, 21.8, 22.6, 23.4, 22.2, 22.7, 21.9, 22.3, 21.7, 22.4, 28.6, 22.1];
        var rooms = new List<Room>();
        for (var i = 0; i < 12; i++)
        {
            var room = new Room($"F{i / 4 + 1}-{i % 4 + 1:00}", names[i / 4][i % 4], i / 4 + 1)
            {
                Temperature = temperatures[i], Humidity = 43 + i % 7,
                Co2 = i == 3 ? 1245 : 510 + i * 27,
                IsOccupied = i % 3 != 1 && i != 10, LightsOn = i % 3 != 1,
                IsOnline = i != 7, LastSeen = LastUpdated.AddMinutes(i == 7 ? -14 : 0)
            };
            // Seed a deterministic half-hour of illustrative history ending at the actual reading.
            for (var j = 0; j <= 30; j++)
                room.History.Add(new(LastUpdated.AddMinutes(j - 30),
                    room.Temperature + Math.Sin((30 - j) / 6.0) * 0.3, room.Setpoint));
            room.PowerKw = CalculateRoomPower(room);
            rooms.Add(room);
        }
        Rooms = rooms.AsReadOnly();
        Building = new("Horizon Office Center", Enumerable.Range(1, 3)
            .Select(n => new Floor(n, n == 1 ? "Welcome & collaboration" : n == 2 ? "Workplace & learning" : "Leadership & infrastructure",
                rooms.Where(r => r.FloorNumber == n).ToList().AsReadOnly())).ToList().AsReadOnly());
        Equipment = new List<Equipment>
        {
            new("AHU-01", "Main Air Handling Unit", "Ventilation", true, EquipmentStatus.Running, 3.8),
            new("AHU-02", "Office Ventilation", "Ventilation", false, EquipmentStatus.Offline, 0),
            new("HVAC-F2-01", "Floor 2 HVAC Controller", "HVAC", true, EquipmentStatus.Running, 0.12),
            new("LIGHT-F1-01", "Floor 1 Lighting Controller", "Lighting", true, EquipmentStatus.Running, 0.06)
        }.AsReadOnly();
        foreach (var device in Equipment)
            device.LastUpdated = LastUpdated.AddMinutes(device.IsOnline ? 0 : -18);

        // A complete 24-hour illustrative window. Each point is energy for one hour, in kWh.
        var endHour = new DateTimeOffset(LastUpdated.Year, LastUpdated.Month, LastUpdated.Day, LastUpdated.Hour, 0, 0, LastUpdated.Offset);
        HourlyEnergy = Enumerable.Range(0, 24).Select(i =>
        {
            var timestamp = endHour.AddHours(i - 24);
            var occupiedFactor = timestamp.Hour is >= 7 and <= 19 ? 1.0 : 0.34;
            return new EnergyReading(timestamp, Math.Round((15.5 + 2.8 * Math.Sin(i * 0.6)) * occupiedFactor, 2));
        }).ToList().AsReadOnly();
        var historicalTotal = HourlyEnergy.Sum(r => r.EnergyKwh);
        foreach (var room in Rooms) room.EnergyKwh = historicalTotal * room.PowerKw / PowerKw;
        _equipmentEnergyKwh = historicalTotal * Equipment.Sum(e => e.PowerKw) / PowerKw;
        EvaluateAlarms();
    }

    public Room GetRoom(string id) => Rooms.First(r => r.Id == id);
    public Floor GetFloor(int number) => Building.Floors.First(f => f.Number == number);

    public void SetTemperature(string id, double value)
    {
        if (!double.IsFinite(value)) return;
        UpdateRoom(id, room => room.Setpoint = Math.Clamp(Math.Round(value * 2) / 2, 18, 28));
    }
    public void SetHvacMode(string id, HvacMode mode)
    {
        if (Enum.IsDefined(mode)) UpdateRoom(id, room => room.HvacMode = mode);
    }
    public void SetLights(string id, bool enabled) => UpdateRoom(id, room => room.LightsOn = enabled);
    public void SetBrightness(string id, int brightness) => UpdateRoom(id, room => room.Brightness = Math.Clamp(brightness, 0, 100));
    private void UpdateRoom(string id, Action<Room> update)
    {
        var room = GetRoom(id);
        if (!room.IsOnline) return;
        update(room);
        room.PowerKw = CalculateRoomPower(room);
        EvaluateAlarms();
        Changed?.Invoke();
    }

    public void TogglePause() { IsPaused = !IsPaused; Changed?.Invoke(); }
    public void Acknowledge(string id)
    {
        var alarm = _alarms.FirstOrDefault(a => a.Id == id);
        if (alarm is null || alarm.IsAcknowledged) return;
        // Acknowledgement records operator awareness; only sensor conditions can clear a fault.
        alarm.IsAcknowledged = true;
        alarm.AcknowledgedAt = DateTimeOffset.Now;
        Changed?.Invoke();
    }

    public void Advance(TimeSpan elapsed)
    {
        if (IsPaused || elapsed <= TimeSpan.Zero) return;
        var timestamp = DateTimeOffset.Now;
        var previousPower = PowerKw;
        foreach (var room in Rooms)
        {
            var previousRoomPower = room.PowerKw;
            if (room.IsOnline)
            {
                // Slow thermal inertia: a control setpoint never overwrites a measured sensor value.
                // The server room retains an injected heat load so its cooling fault remains demonstrable.
                var target = room.Id == "F3-03" ? 28.2 : room.HvacMode switch
                {
                    HvacMode.Auto => room.Setpoint,
                    HvacMode.Cooling => Math.Min(room.Setpoint, room.Temperature),
                    HvacMode.Heating => Math.Max(room.Setpoint, room.Temperature),
                    _ => 24.0
                };
                var response = 1 - Math.Exp(-elapsed.TotalSeconds / 240);
                room.Temperature = Math.Clamp(room.Temperature + (target - room.Temperature) * response + (_random.NextDouble() - 0.5) * 0.025, 18, 32);
                room.Humidity = Math.Clamp(room.Humidity + (_random.NextDouble() - 0.5) * 0.25, 38, 57);
                var co2Target = room.Id == "F1-04" ? 1220 : room.IsOccupied ? 760 : 480;
                room.Co2 = Math.Clamp(room.Co2 + (co2Target - room.Co2) * response + (_random.NextDouble() - 0.5) * 6, 420, 1500);
                room.LastSeen = timestamp;
                room.History.Add(new(timestamp, room.Temperature, room.Setpoint));
                // Keep bounded history; the offline controller's history stays frozen, honestly showing stale data.
                if (room.History.Count > 240) room.History.RemoveAt(0);
            }
            room.PowerKw = CalculateRoomPower(room);
            // Trapezoidal integration: average kW × elapsed hours = kWh.
            room.EnergyKwh += (previousRoomPower + room.PowerKw) / 2 * elapsed.TotalHours;
        }
        foreach (var device in Equipment.Where(e => e.IsOnline)) device.LastUpdated = timestamp;
        _equipmentEnergyKwh += Equipment.Sum(e => e.PowerKw) * elapsed.TotalHours;
        SessionEnergyKwh += (previousPower + PowerKw) / 2 * elapsed.TotalHours;
        SimulatedDuration += elapsed;
        LastUpdated = timestamp;
        EvaluateAlarms();
        Changed?.Invoke();
    }

    private static double CalculateRoomPower(Room room)
    {
        var hvac = room.HvacMode == HvacMode.Off ? 0.08
            : room.HvacMode == HvacMode.Heating && room.Temperature >= room.Setpoint ? 0.2
            : room.HvacMode == HvacMode.Cooling && room.Temperature <= room.Setpoint ? 0.2
            : 0.42 + Math.Min(Math.Abs(room.Temperature - room.Setpoint) * 0.25, 1.8);
        var baseLoad = room.Id == "F3-03" ? 3.9 : room.IsOccupied ? 0.5 : 0.16;
        // An offline controller retains its last commanded loads; loss of telemetry does not imply zero use.
        return baseLoad + hvac + room.EffectiveBrightness / 100.0 * 0.35;
    }

    private void EvaluateAlarms()
    {
        SetAlarm("ALM-001", "F3-03", "Server Room", "Room temperature exceeds the 27 °C limit", AlarmSeverity.Critical, GetRoom("F3-03").Temperature >= 27, 7);
        SetAlarm("ALM-002", "AHU-02", "AHU-02 · Office Ventilation", "Communication lost; ventilation device is offline", AlarmSeverity.Critical, !Equipment.First(e => e.Id == "AHU-02").IsOnline, 18);
        SetAlarm("ALM-003", "F1-04", "Conference Room", "CO₂ concentration exceeds the 1,000 ppm limit", AlarmSeverity.Warning, GetRoom("F1-04").Co2 >= 1000, 12);
        SetAlarm("ALM-004", "BUILDING", "Building energy", $"Estimated demand exceeds the {HighPowerThresholdKw:0} kW advisory limit", AlarmSeverity.Warning, PowerKw > HighPowerThresholdKw, 4);
        SetAlarm("ALM-005", "F2-04", "Training Room", "Room controller offline; last known readings shown", AlarmSeverity.Warning, !GetRoom("F2-04").IsOnline, 14);
        foreach (var room in Rooms.Where(r => r.IsOnline))
        {
            if (room.Id != "F3-03")
                SetAlarm($"TEMP-{room.Id}", room.Id, room.Name, "Room temperature exceeds the 27 °C limit", AlarmSeverity.Critical, room.Temperature >= 27, 0);
            if (room.Id != "F1-04")
                SetAlarm($"AIR-{room.Id}", room.Id, room.Name, "CO₂ concentration exceeds the 1,000 ppm limit", AlarmSeverity.Warning, room.Co2 >= 1000, 0);
        }
    }

    private void SetAlarm(string id, string sourceId, string source, string description, AlarmSeverity severity, bool active, int ageMinutes)
    {
        var alarm = _alarms.FirstOrDefault(a => a.Id == id);
        if (alarm is null)
        {
            if (active) _alarms.Add(new(id, sourceId, source, description, severity, LastUpdated.AddMinutes(-ageMinutes)));
            return;
        }
        if (active && !alarm.IsActive)
        {
            alarm.TriggeredAt = LastUpdated;
            alarm.IsAcknowledged = false;
            alarm.AcknowledgedAt = null;
        }
        alarm.IsActive = active;
    }
}
