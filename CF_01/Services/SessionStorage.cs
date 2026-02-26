using System;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using CF_01.Models;

namespace CF_01.Services;

/// <summary>
/// Lưu/đọc dữ liệu phiên đo nhiệt độ.
/// Mỗi phiên lưu thành 1 file JSON trong thư mục SessionData.
/// </summary>
public static class SessionStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Lưu phiên đo hoàn chỉnh ra file JSON.
    /// Tên file: session_yyyyMMdd_HHmmss.json
    /// </summary>
    public static string SaveSession(SessionData session, string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fileName = $"session_{session.StartTime:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(folder, fileName);

            var json = JsonSerializer.Serialize(session, JsonOptions);
            File.WriteAllText(filePath, json);

            Console.WriteLine($"[Storage] Đã lưu phiên: {filePath}");
            return filePath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Storage] Lỗi lưu phiên: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Đọc danh sách phiên đã lưu (chỉ metadata, không load samples).
    /// </summary>
    public static List<SessionSummary> ListSessions(string folder)
    {
        var summaries = new List<SessionSummary>();

        if (!Directory.Exists(folder))
            return summaries;

        foreach (var file in Directory.GetFiles(folder, "session_*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var session = JsonSerializer.Deserialize<SessionData>(json);
                if (session != null)
                {
                    summaries.Add(new SessionSummary
                    {
                        FilePath = file,
                        StartTime = session.StartTime,
                        DurationSeconds = session.DurationSeconds,
                        OverallAverage = session.OverallAverage,
                        MaxTemperature = session.MaxTemperature,
                        TotalSamples = session.TotalSamples
                    });
                }
            }
            catch { /* skip corrupt files */ }
        }

        summaries.Sort((a, b) => b.StartTime.CompareTo(a.StartTime)); // Newest first
        return summaries;
    }

    /// <summary>
    /// Đọc đầy đủ dữ liệu 1 phiên từ file.
    /// </summary>
    public static SessionData? LoadSession(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<SessionData>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Storage] Lỗi đọc phiên: {ex.Message}");
            return null;
        }
    }
}

public class SessionSummary
{
    public string FilePath { get; set; } = "";
    public DateTime StartTime { get; set; }
    public double DurationSeconds { get; set; }
    public double OverallAverage { get; set; }
    public double MaxTemperature { get; set; }
    public int TotalSamples { get; set; }
}
