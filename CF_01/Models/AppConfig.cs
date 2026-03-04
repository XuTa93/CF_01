using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System;

namespace CF_01.Models;

/// <summary>
/// Cấu hình ứng dụng đo nhiệt độ lò sấy cà phê.
/// Lưu/đọc từ config.json.
/// </summary>
public class AppConfig
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");

    // === Ngưỡng phiên đo ===

    /// <summary>Nhiệt độ bắt đầu đo (°C). Mặc định 70°C.</summary>
    public double StartThreshold { get; set; } = 30.0;

    /// <summary>Nhiệt độ kết thúc đo (°C). Mặc định 60°C.</summary>
    public double EndThreshold { get; set; } = 60.0;

    /// <summary>Thời gian duy trì trên ngưỡng bắt đầu để kích hoạt (giây).</summary>
    public double StartDelaySeconds { get; set; } = 5.0;

    /// <summary>Thời gian duy trì dưới ngưỡng kết thúc để dừng (giây).</summary>
    public double EndDelaySeconds { get; set; } = 10.0;

    // === Cảm biến ===

    /// <summary>Chu kỳ đọc cảm biến (giây). Mặc định 0.1s.</summary>
    public double SensorPollIntervalSeconds { get; set; } = 0.1;

    /// <summary>Số lần đọc để tính trung bình thành 1 mẫu lưu trữ. Mặc định 10 (= 1s).</summary>
    public int SamplesPerStoredReading { get; set; } = 10;

    /// <summary>Chu kỳ ghi nhiệt độ trung bình (giây). Mặc định 600 (10 phút).</summary>
    public int AverageIntervalSeconds { get; set; } = 60;

    // === Modbus RTU ===

    /// <summary>Cổng COM cho Modbus RTU. VD: "COM3".</summary>
    public string ModbusPortName { get; set; } = "COM3";

    /// <summary>Baud rate Modbus RTU.</summary>
    public int ModbusBaudRate { get; set; } = 9600;

    /// <summary>Slave ID của cảm biến nhiệt độ.</summary>
    public byte ModbusSlaveId { get; set; } = 1;

    /// <summary>Register address để đọc nhiệt độ.</summary>
    public ushort ModbusRegisterAddress { get; set; } = 0;

    /// <summary>Hệ số chia giá trị register (VD: register=245 ÷ 10 = 24.5°C).</summary>
    public double ModbusScaleFactor { get; set; } = 10.0;

    // === Hiển thị ===

    /// <summary>Ngưỡng dưới mặc định cho RangeSlider (hiển thị lửa xanh).</summary>
    public double DisplayLowThreshold { get; set; } = 70.0;

    /// <summary>Ngưỡng trên mặc định cho RangeSlider.</summary>
    public double DisplayHighThreshold { get; set; } = 100.0;

    // === Lưu trữ ===

    /// <summary>Thư mục lưu dữ liệu phiên đo.</summary>
    public string SessionDataFolder { get; set; } = "SessionData";

    // === Load / Save ===

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] Lỗi đọc config: {ex.Message}. Dùng mặc định.");
        }

        var config = new AppConfig();
        config.Save();
        return config;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] Lỗi lưu config: {ex.Message}");
        }
    }
}
