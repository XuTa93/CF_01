using System;

namespace CF_01.Services;

/// <summary>
/// Interface cho nguồn đọc nhiệt độ.
/// Hỗ trợ: Modbus RTU, CSV simulator, Built-in simulator.
/// Thiết kế cho 1 cảm biến, mở rộng dễ cho 2-4 cảm biến.
/// </summary>
public interface ITemperatureSensor : IDisposable
{
    /// <summary>Đọc nhiệt độ hiện tại (°C). Gọi mỗi 0.1s.</summary>
    double ReadTemperature();

    /// <summary>Kiểm tra kết nối cảm biến.</summary>
    bool IsConnected { get; }

    /// <summary>Tên nguồn dữ liệu (để hiển thị trên UI).</summary>
    string SourceName { get; }
}
