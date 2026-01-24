using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF_01.ViewModels;

public partial class FireViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();

    [ObservableProperty]
    private double _temperature = 25.0;

    [ObservableProperty]
    private bool _isLowTemperature = false;

    [ObservableProperty]
    private bool _isNormalTemperature = true;

    [ObservableProperty]
    private bool _isHighTemperature = false;

    // Ngưỡng nhiệt độ
    private const double LowTempThreshold = 20.0;
    private const double HighTempThreshold = 35.0;

    public FireViewModel()
    {
        UpdateFireDisplay();
        
        // Tạo timer thay đổi nhiệt độ mỗi 2 giây
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        // Thay đổi nhiệt độ ngẫu nhiên từ 10°C đến 45°C
        Temperature = _random.Next(10, 46);
    }

    partial void OnTemperatureChanged(double value)
    {
        UpdateFireDisplay();
    }

    private void UpdateFireDisplay()
    {
        if (Temperature < LowTempThreshold)
        {
            IsLowTemperature = true;
            IsNormalTemperature = false;
            IsHighTemperature = false;
        }
        else if (Temperature >= LowTempThreshold && Temperature < HighTempThreshold)
        {
            IsLowTemperature = false;
            IsNormalTemperature = true;
            IsHighTemperature = false;
        }
        else
        {
            IsLowTemperature = false;
            IsNormalTemperature = false;
            IsHighTemperature = true;
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
    }
}