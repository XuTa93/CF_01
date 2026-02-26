using System;

namespace CF_01.ViewModels;

/// <summary>
/// Entry hiển thị trung bình theo chu kỳ cài đặt trên UI.
/// </summary>
public class IntervalAverageEntry
{
    public int Index { get; }
    public string StartTimeText { get; }
    public string EndTimeText { get; }
    public double Average { get; }
    public double Min { get; }
    public double Max { get; }
    public int Samples { get; }

    public IntervalAverageEntry(int index, DateTime startTime, DateTime endTime,
        double average, double min, double max, int samples)
    {
        Index = index;
        StartTimeText = startTime.ToString("HH:mm:ss");
        EndTimeText = endTime.ToString("HH:mm:ss");
        Average = average;
        Min = min;
        Max = max;
        Samples = samples;
    }
}
