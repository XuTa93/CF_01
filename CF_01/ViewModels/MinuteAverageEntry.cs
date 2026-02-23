namespace CF_01.ViewModels;

public class MinuteAverageEntry
{
    public int Minute { get; }
    public string TimeLabel { get; }
    public double Average { get; }
    public double Min { get; }
    public double Max { get; }
    public int Samples { get; }

    public MinuteAverageEntry(int minute, string timeLabel, double average, double min, double max, int samples)
    {
        Minute = minute;
        TimeLabel = timeLabel;
        Average = average;
        Min = min;
        Max = max;
        Samples = samples;
    }
}
