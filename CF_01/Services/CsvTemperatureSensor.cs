using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CF_01.Services;

/// <summary>
/// Đọc nhiệt độ từ file CSV để mô phỏng cảm biến.
/// File CSV format: mỗi dòng là 1 giá trị nhiệt độ (°C),
/// đọc tuần tự mỗi 0.1s, lặp lại khi hết file.
/// 
/// Ví dụ file temperature_data.csv:
/// 25.0
/// 25.3
/// 30.5
/// 45.2
/// ...
/// </summary>
public class CsvTemperatureSensor : ITemperatureSensor
{
    private readonly List<double> _temperatures = [];
    private int _currentIndex;
    private readonly string _filePath;

    public bool IsConnected => _temperatures.Count > 0;
    public string SourceName => $"CSV: {Path.GetFileName(_filePath)}";

    public CsvTemperatureSensor(string csvFilePath)
    {
        _filePath = csvFilePath;
        CreateSampleCsv(csvFilePath); // Always overwrite file on each run
        LoadCsv(csvFilePath);
    }

    private void LoadCsv(string path)
    {
        _temperatures.Clear();
        _currentIndex = 0;

        if (!File.Exists(path))
        {
            Console.WriteLine($"[CSV Sensor] File không tồn tại: {path}. Tạo file mẫu.");
            CreateSampleCsv(path);
        }

        try
        {
            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;

                // Hỗ trợ cả dấu , và . cho số thập phân
                var normalized = trimmed.Replace(',', '.');
                if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var temp))
                {
                    _temperatures.Add(temp);
                }
            }

            Console.WriteLine($"[CSV Sensor] Đã tải {_temperatures.Count} giá trị từ {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CSV Sensor] Lỗi đọc CSV: {ex.Message}");
        }
    }

    public double ReadTemperature()
    {
        if (_temperatures.Count == 0)
            return 25.0; // Giá trị mặc định nếu không có dữ liệu

        var temp = _temperatures[_currentIndex];
        _currentIndex = (_currentIndex + 1) % _temperatures.Count;
        return temp;
    }

    /// <summary>
    /// Tạo file CSV mẫu mô phỏng chu kỳ sấy cà phê (~2h).
    /// Mỗi dòng = 0.1s → 72000 dòng = 2 giờ.
    /// </summary>
    private static void CreateSampleCsv(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var writer = new StreamWriter(path);
            writer.WriteLine("# Nhiệt độ mô phỏng sấy cà phê (°C)");
            writer.WriteLine("# Mỗi dòng = 1 lần đọc (0.1s)");
            writer.WriteLine("# Tổng: ~2 giờ sấy");

            var rng = new Random(42);

            // Giai đoạn 1: Khởi động (0-5 phút, 3000 dòng): 25 → 75°C
            for (int i = 0; i < 3000; i++)
            {
                double t = i / 3000.0;
                double temp = 25 + 50 * t * t + rng.NextDouble() * 3;
                writer.WriteLine(temp.ToString("F1", CultureInfo.InvariantCulture));
            }

            // Giai đoạn 2: Sấy chính (5-90 phút, 51000 dòng): 75-105°C dao động
            for (int i = 0; i < 51000; i++)
            {
                double t = i / 51000.0;
                double baseTemp = 85 + 15 * Math.Sin(t * Math.PI);
                double temp = baseTemp + (rng.NextDouble() * 2 - 1) * 5;
                writer.WriteLine(Math.Clamp(temp, 70, 110).ToString("F1", CultureInfo.InvariantCulture));
            }

            // Giai đoạn 3: Giảm nhiệt (90-110 phút, 12000 dòng): 90 → 50°C
            for (int i = 0; i < 12000; i++)
            {
                double t = i / 12000.0;
                double temp = 90 - 40 * t + rng.NextDouble() * 3;
                writer.WriteLine(temp.ToString("F1", CultureInfo.InvariantCulture));
            }

            // Giai đoạn 4: Nguội (110-120 phút, 6000 dòng): 50 → 30°C
            for (int i = 0; i < 6000; i++)
            {
                double t = i / 6000.0;
                double temp = 50 - 20 * t + rng.NextDouble() * 2;
                writer.WriteLine(temp.ToString("F1", CultureInfo.InvariantCulture));
            }

            Console.WriteLine($"[CSV Sensor] Đã tạo file mẫu: {path} (72000 dòng = 2h)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CSV Sensor] Lỗi tạo file mẫu: {ex.Message}");
        }
    }

    public void Dispose() { }
}
