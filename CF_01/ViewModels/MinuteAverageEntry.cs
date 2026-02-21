namespace CF_01.ViewModels;

public class MinuteAverageEntry
{
    public int Minute { get; }
    public double Average { get; }
    public double Min { get; }
    public double Max { get; }
    public int Samples { get; }

    public MinuteAverageEntry(int minute, double average, double min, double max, int samples)
    {
        Minute = minute;
        Average = average;
        Min = min;
        Max = max;
        Samples = samples;
    }

    public string DisplayText => $"Phút {Minute}: {Average:F1}°C  (↓{Min:F0} ↑{Max:F0})";
}
