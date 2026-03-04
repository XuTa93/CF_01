using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CF_01_Simulation;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine("║  CF_01 Simulation — Modbus RTU Slave         ║");
Console.WriteLine("║  Giả lập nhiệt độ lò sấy cà phê             ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");
Console.WriteLine();

// === Load config ===
var config = SimConfig.Load();

Console.WriteLine($"  COM Port  : {config.ComPort}");
Console.WriteLine($"  Baud Rate : {config.BaudRate}");
Console.WriteLine($"  Slave ID  : {config.SlaveId}");
Console.WriteLine($"  Register  : {config.RegisterAddress} (Holding Register)");
Console.WriteLine($"  Scale     : {config.ScaleFactor} (VD: register 255 → 25.5°C)");
Console.WriteLine($"  CSV File  : {config.CsvFilePath}");
Console.WriteLine($"  Interval  : {config.UpdateIntervalMs}ms");
Console.WriteLine();

// === Ensure CSV exists ===
if (!File.Exists(config.CsvFilePath))
{
    Console.WriteLine($"[CSV] File không tồn tại, tạo file mẫu: {config.CsvFilePath}");
    CsvGenerator.CreateSampleCsv(config.CsvFilePath);
}

// === Load CSV ===
var temperatures = CsvLoader.Load(config.CsvFilePath);
if (temperatures.Count == 0)
{
    Console.WriteLine("[CSV] Không có dữ liệu nhiệt độ! Kiểm tra lại file CSV.");
    return;
}
Console.WriteLine($"[CSV] Đã tải {temperatures.Count} giá trị nhiệt độ");
Console.WriteLine($"      Phạm vi: {temperatures.Min():F1}°C — {temperatures.Max():F1}°C");
Console.WriteLine($"      Thời lượng: ~{temperatures.Count * config.UpdateIntervalMs / 1000 / 60} phút");
Console.WriteLine();

// === Setup Serial Port ===
using var port = new SerialPort(config.ComPort)
{
    BaudRate = config.BaudRate,
    Parity = Parity.None,
    DataBits = 8,
    StopBits = StopBits.One,
    ReadTimeout = 1000,
    WriteTimeout = 1000
};

try
{
    port.Open();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[LỖI] Không thể mở cổng {config.ComPort}: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Hướng dẫn:");
    if (OperatingSystem.IsWindows())
    {
        Console.WriteLine("  1. Cài đặt com0com (virtual COM port) để tạo cặp COM ảo");
        Console.WriteLine("     VD: COM2 ↔ COM3 (Simulation dùng COM2, App dùng COM3)");
    }
    else
    {
        Console.WriteLine("  1. Dùng socat để tạo cặp serial ảo:");
        Console.WriteLine("     socat -d -d pty,raw,echo=0 pty,raw,echo=0");
        Console.WriteLine("     VD: /dev/pts/2 ↔ /dev/pts/3");
    }
    Console.WriteLine("  2. Hoặc dùng cổng serial thực có sẵn trên máy");
    Console.WriteLine();
    Console.WriteLine("Các cổng serial hiện có:");
    foreach (var name in SerialPort.GetPortNames())
        Console.WriteLine($"    {name}");
    return;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"[Serial] Cổng {config.ComPort} đã mở thành công");
Console.ResetColor();

// === Setup Modbus RTU Slave (tự viết, không dùng thư viện) ===
using var modbusSlave = new ModbusRtuSlave(port, config.SlaveId);

// Start listening in background (Task.Run để không chặn main thread)
using var cts = new CancellationTokenSource();
_ = Task.Run(() => modbusSlave.ListenAsync(cts.Token), cts.Token);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("[Modbus] RTU Slave đang chạy, chờ Master kết nối...");
Console.ResetColor();
Console.WriteLine();

// === Main loop ===
bool running = true;
while (running)
{
    Console.WriteLine("━━━ Bắt đầu phát dữ liệu CSV ━━━");
    Console.WriteLine();

    for (int i = 0; i < temperatures.Count; i++)
    {
        double temp = temperatures[i];
        ushort regValue = (ushort)Math.Clamp(temp * config.ScaleFactor, 0, ushort.MaxValue);

        // Update holding register
        modbusSlave.WriteRegister(config.RegisterAddress, regValue);

        // Hiển thị realtime nhiệt độ
        Console.Write($"\r  [{DateTime.Now:HH:mm:ss}] {i + 1,5}/{temperatures.Count} | {temp,6:F1}°C | Reg={regValue,5} | {TemperatureBar(temp)}  ");

        try
        {
            await Task.Delay(config.UpdateIntervalMs, cts.Token);
        }
        catch (TaskCanceledException)
        {
            running = false;
            break;
        }
    }

    if (!running) break;

    Console.WriteLine();
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("═══ Đã phát hết dữ liệu CSV ═══");
    Console.ResetColor();
    Console.WriteLine("Modbus Server vẫn đang mở.");
    Console.WriteLine("  [R] Chạy lại từ đầu");
    Console.WriteLine("  [Q] Thoát");

    while (true)
    {
        var key = Console.ReadKey(true).Key;
        if (key == ConsoleKey.R)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("→ Đang chạy lại...");
            Console.ResetColor();
            Console.WriteLine();
            break;
        }
        if (key == ConsoleKey.Q)
        {
            running = false;
            break;
        }
    }
}

await cts.CancelAsync();
Console.WriteLine();
Console.WriteLine("Đã dừng Modbus Slave. Tạm biệt!");

// === Helper: Temperature bar ===
static string TemperatureBar(double temp)
{
    int barLen = (int)Math.Clamp((temp - 20) / 100 * 30, 0, 30);
    return new string('█', barLen) + new string('░', 30 - barLen);
}

// === CSV Loader ===
static class CsvLoader
{
    public static List<double> Load(string path)
    {
        var temps = new List<double>();
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var normalized = trimmed.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var temp))
                temps.Add(temp);
        }
        return temps;
    }
}

// === CSV Generator ===
static class CsvGenerator
{
    /// <summary>
    /// Tạo file CSV mẫu mô phỏng chu kỳ sấy cà phê (~2 giờ).
    /// Mỗi dòng = 1 giây → 7200 dòng = 2 giờ.
    /// </summary>
    public static void CreateSampleCsv(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var writer = new StreamWriter(path);
        writer.WriteLine("# Nhiệt độ mô phỏng sấy cà phê (°C)");
        writer.WriteLine("# Mỗi dòng = 1 giây");
        writer.WriteLine("# Tổng: ~2 giờ sấy (7200 dòng)");
        writer.WriteLine("# Giai đoạn: Khởi động → Sấy chính → Giảm nhiệt → Nguội");

        var rng = new Random(42);

        // Giai đoạn 1: Khởi động (0–5 phút, 300 dòng): 25 → 75°C
        for (int i = 0; i < 300; i++)
        {
            double t = i / 300.0;
            double temp = 25 + 50 * t * t + rng.NextDouble() * 3;
            writer.WriteLine(temp.ToString("F1", CultureInfo.InvariantCulture));
        }

        // Giai đoạn 2: Sấy chính (5–90 phút, 5100 dòng): 75–105°C dao động
        for (int i = 0; i < 5100; i++)
        {
            double t = i / 5100.0;
            double baseTemp = 85 + 15 * Math.Sin(t * Math.PI);
            double temp = baseTemp + (rng.NextDouble() * 2 - 1) * 5;
            writer.WriteLine(Math.Clamp(temp, 70, 110).ToString("F1", CultureInfo.InvariantCulture));
        }

        // Giai đoạn 3: Giảm nhiệt (90–110 phút, 1200 dòng): 90 → 50°C
        for (int i = 0; i < 1200; i++)
        {
            double t = i / 1200.0;
            double temp = 90 - 40 * t + rng.NextDouble() * 3;
            writer.WriteLine(temp.ToString("F1", CultureInfo.InvariantCulture));
        }

        // Giai đoạn 4: Nguội (110–120 phút, 600 dòng): 50 → 30°C
        for (int i = 0; i < 600; i++)
        {
            double t = i / 600.0;
            double temp = 50 - 20 * t + rng.NextDouble() * 2;
            writer.WriteLine(temp.ToString("F1", CultureInfo.InvariantCulture));
        }

        Console.WriteLine($"[CSV] Đã tạo file mẫu: {path} (7200 dòng ≈ 2 giờ)");
    }
}

// === Simulation Config ===
class SimConfig
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "sim_config.json");

    public string ComPort { get; set; } = OperatingSystem.IsWindows() ? "COM2" : "/dev/ttyUSB0";
    public int BaudRate { get; set; } = 9600;
    public byte SlaveId { get; set; } = 1;
    public ushort RegisterAddress { get; set; } = 0;
    public double ScaleFactor { get; set; } = 10.0;
    public string CsvFilePath { get; set; } = "temperature_data.csv";
    public int UpdateIntervalMs { get; set; } = 1000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static SimConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<SimConfig>(json, JsonOptions);
                if (config != null)
                {
                    Console.WriteLine($"[Config] Đã tải từ {ConfigPath}");
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] Lỗi đọc: {ex.Message}");
        }

        // Save default config
        var defaultConfig = new SimConfig();
        defaultConfig.Save();
        Console.WriteLine($"[Config] Đã tạo file mặc định: {ConfigPath}");
        return defaultConfig;
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
            Console.WriteLine($"[Config] Lỗi lưu: {ex.Message}");
        }
    }
}

