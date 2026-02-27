using System;
using CF_01.Models;

namespace CF_01.Services;

/// <summary>
/// Giả lập cảm biến nhiệt độ dùng profile sấy cà phê built-in (không cần file).
/// Mô phỏng chu kỳ ~2h sấy thực tế.
/// </summary>
public class BuiltInSimulatorSensor : ITemperatureSensor
{
    private readonly Random _random = new();
    private int _tickCount;

    // 2h = 7200s = 72000 ticks (0.1s/tick)
    private const int CycleTicks = 720;

    public bool IsConnected => true;
    public string SourceName => "Giả lập sấy cà phê (2h)";

    public double ReadTemperature()
    {
        _tickCount++;
        if (_tickCount >= CycleTicks)
            _tickCount = 0;

        double seconds = _tickCount * 0.1;
        double minutes = seconds / 60.0;

        double baseTemp;
        double noise;

        if (seconds < 5)
        {
            // Khởi động: 25 → 75°C (5 phút)
            double t = minutes / 5.0;
            baseTemp = 25 + 50 * t * t;
            noise = _random.NextDouble() * 2;
        }
        else if (seconds < 500)
        {
            // Sấy chính: 75-105°C (85 phút)
            double t = (minutes - 5) / 85.0;
            double peak = Math.Sin(t * Math.PI);
            baseTemp = 80 + 20 * peak;
            noise = (_random.NextDouble() * 2 - 1) * 3;
        }
        else if (seconds < 700)
        {
            // Giảm nhiệt: 90 → 50°C (20 phút)
            double t = (minutes - 90) / 20.0;
            baseTemp = 90 - 40 * t;
            noise = _random.NextDouble() * 2;
        }
        else
        {
            // Nguội: 50 → 30°C (10 phút)
            double t = Math.Min((minutes - 110) / 10.0, 1.0);
            baseTemp = 50 - 20 * t;
            noise = _random.NextDouble() * 1.5;
        }

        return Math.Clamp(baseTemp + noise, 10, 120);
    }

    public void Dispose() { }
}
