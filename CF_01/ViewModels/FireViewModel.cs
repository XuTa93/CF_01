using System;
using Avalonia.Media;
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

    [ObservableProperty]
    private double _mercuryHeight = 100.0;

    [ObservableProperty]
    private IBrush _mercuryBrush = new SolidColorBrush(Color.FromRgb(255, 204, 68));

    // Ngưỡng nhiệt độ
    private const double LowTempThreshold = 20.0;
    private const double HighTempThreshold = 35.0;
    private const double MinTemp = 10.0;
    private const double MaxTemp = 45.0;
    private const double ThermometerMaxHeight = 240.0; // Chiều cao tối đa của cột thủy ngân

    public FireViewModel()
    {
        UpdateFireDisplay();
        UpdateThermometer();
        
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

    // Tự động update khi Temperature thay đổi
    partial void OnTemperatureChanged(double value)
    {
        UpdateFireDisplay();
        UpdateThermometer();
    }

    private void UpdateFireDisplay()
    {
        if (Temperature < LowTempThreshold)
        {
            // Nhiệt độ thấp < 20°C -> FireL.gif
            IsLowTemperature = true;
            IsNormalTemperature = false;
            IsHighTemperature = false;
        }
        else if (Temperature >= LowTempThreshold && Temperature < HighTempThreshold)
        {
            // Nhiệt độ trung bình 20-35°C -> Fire.gif
            IsLowTemperature = false;
            IsNormalTemperature = true;
            IsHighTemperature = false;
        }
        else
        {
            // Nhiệt độ cao >= 35°C -> FireH.gif
            IsLowTemperature = false;
            IsNormalTemperature = false;
            IsHighTemperature = true;
        }
    }

    private void UpdateThermometer()
    {
        // Tính chiều cao cột thủy ngân (10°C = 0%, 45°C = 100%)
        var percentage = (Temperature - MinTemp) / (MaxTemp - MinTemp);
        MercuryHeight = Math.Max(20, percentage * ThermometerMaxHeight);

        // Thay đổi màu theo nhiệt độ
        Color color;
        if (Temperature < LowTempThreshold)
        {
            // Màu xanh dương (lạnh)
            color = Color.FromRgb(68, 136, 255);
        }
        else if (Temperature < 25)
        {
            // Màu xanh lá nhạt
            color = Color.FromRgb(136, 204, 255);
        }
        else if (Temperature < 30)
        {
            // Màu vàng
            color = Color.FromRgb(255, 204, 68);
        }
        else if (Temperature < HighTempThreshold)
        {
            // Màu cam
            color = Color.FromRgb(255, 136, 68);
        }
        else if (Temperature < 40)
        {
            // Màu đỏ cam
            color = Color.FromRgb(255, 102, 68);
        }
        else
        {
            // Màu đỏ (nóng)
            color = Color.FromRgb(255, 68, 68);
        }

        MercuryBrush = new SolidColorBrush(color);
    }

    // Method để cập nhật nhiệt độ từ bên ngoài
    public void UpdateTemperature(double newTemperature)
    {
        Temperature = newTemperature;
    }

    public void Dispose()
    {
        _timer?.Stop();
    }
}