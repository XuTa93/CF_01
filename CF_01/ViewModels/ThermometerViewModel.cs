using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace CF_01.ViewModels;

public partial class ThermometerViewModel : ObservableObject
{
    [ObservableProperty]
    private double _temperature = 25.0;

    [ObservableProperty]
    private double _mercuryHeight = 100.0;

    [ObservableProperty]
    private IBrush _mercuryBrush = new SolidColorBrush(Color.FromRgb(255, 204, 68));

    // Ngưỡng nhiệt độ
    private const double MinTemp = 10.0;
    private const double MaxTemp = 45.0;
    private const double ThermometerMaxHeight = 240.0;
    private const double LowTempThreshold = 20.0;
    private const double HighTempThreshold = 35.0;

    public ThermometerViewModel()
    {
        UpdateThermometer();
    }

    partial void OnTemperatureChanged(double value)
    {
        UpdateThermometer();
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
}