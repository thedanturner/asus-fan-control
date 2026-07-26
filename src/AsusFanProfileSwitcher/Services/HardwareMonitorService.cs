using System.Text.RegularExpressions;
using LibreHardwareMonitor.Hardware;

namespace AsusFanProfileSwitcher.Services;

internal sealed record FanReading(
    string Id,
    string Hardware,
    string DefaultName,
    float Rpm,
    float? Percentage);

internal sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsMotherboardEnabled = true,
        IsControllerEnabled = true
    };
    private bool _isOpen;

    public string? Error { get; private set; }

    public void Open()
    {
        try
        {
            _computer.Open();
            _isOpen = true;
            Error = null;
        }
        catch (Exception exception)
        {
            Error = exception.Message;
        }
    }

    public IReadOnlyList<FanReading> Read()
    {
        if (!_isOpen)
        {
            return [];
        }

        try
        {
            var readings = new List<FanReading>();
            foreach (var hardware in _computer.Hardware)
            {
                ReadHardware(hardware, readings);
            }

            Error = null;
            return readings
                .OrderBy(reading => reading.Hardware)
                .ThenBy(reading => reading.DefaultName)
                .ToArray();
        }
        catch (Exception exception)
        {
            Error = exception.Message;
            return [];
        }
    }

    private static void ReadHardware(IHardware hardware, ICollection<FanReading> readings)
    {
        hardware.Update();
        var fans = hardware.Sensors.Where(sensor => sensor.SensorType == SensorType.Fan).ToArray();
        var controls = hardware.Sensors
            .Where(sensor => sensor.SensorType == SensorType.Control)
            .ToArray();

        for (var index = 0; index < fans.Length; index++)
        {
            var fan = fans[index];
            if (fan.Value is null)
            {
                continue;
            }

            var number = TrailingNumber(fan.Name);
            var control = controls.FirstOrDefault(sensor =>
                    number is not null && TrailingNumber(sensor.Name) == number)
                ?? controls.ElementAtOrDefault(index);

            readings.Add(new FanReading(
                fan.Identifier.ToString(),
                hardware.Name,
                fan.Name,
                Math.Max(0, fan.Value.Value),
                control?.Value is { } value ? Math.Clamp(value, 0, 100) : null));
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            ReadHardware(subHardware, readings);
        }
    }

    private static int? TrailingNumber(string value)
    {
        var match = Regex.Match(value, @"(\d+)\s*$");
        return match.Success && int.TryParse(match.Groups[1].Value, out var number)
            ? number
            : null;
    }

    public void Dispose()
    {
        if (_isOpen)
        {
            _computer.Close();
            _isOpen = false;
        }
    }
}
