using System;
using System.Collections.Generic;

namespace CF_01.Models;

/// <summary>
/// Dữ liệu 1 mẫu nhiệt độ đã lưu trữ (trung bình 10 lần đọc = 1s).
/// </summary>
public record TemperatureSample(
    DateTime Timestamp,
    double Temperature,
    double Min,
    double Max);

/// <summary>
/// Dữ liệu trung bình 1 khoảng thời gian trong phiên đo.
/// </summary>
public record IntervalAverage(
    int Index,
    DateTime StartTime,
    DateTime EndTime,
    double Average,
    double Min,
    double Max,
    int SampleCount);

/// <summary>
/// Toàn bộ dữ liệu của 1 phiên đo hoàn chỉnh (từ bắt đầu → kết thúc).
/// </summary>
public class SessionData
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double OverallAverage { get; set; }
    public double MaxTemperature { get; set; }
    public double MinTemperature { get; set; }
    public int TotalSamples { get; set; }
    public double DurationSeconds { get; set; }

    /// <summary>Danh sách mẫu 1s (trung bình 10 lần đọc).</summary>
    public List<TemperatureSample> Samples { get; set; } = [];

    /// <summary>Danh sách trung bình theo chu kỳ cài đặt.</summary>
    public List<IntervalAverage> IntervalAverages { get; set; } = [];
}
