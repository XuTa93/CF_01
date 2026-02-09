using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace CF_01.ViewModels;

public partial class ThermometerViewModel : ObservableObject
{
    [ObservableProperty]
    private double _temperature = 25.0;

    [ObservableProperty]
    private double _mercuryHeight = 100.0;

    [ObservableProperty]
    private IBrush _mercuryBrush = new SolidColorBrush(Color.FromRgb(255, 204, 68));

    [ObservableProperty]
    private ObservableCollection<TemperatureScale> _temperatureScales = new();

    // Ngưỡng nhiệt độ
    private const double MinTemp = 0.0;
    private const double MaxTemp = 120.0;
    private const double ThermometerMaxHeight = 590.0; // Chiều cao thực tế của ống (620 - padding)
    private const double LowTempThreshold = 20.0;
    private const double HighTempThreshold = 80.0;

    public ThermometerViewModel()
    {
        GenerateTemperatureScales();
        UpdateThermometer();
    }

    partial void OnTemperatureChanged(double value)
    {
        UpdateThermometer();
    }

    private void GenerateTemperatureScales()
    {
        TemperatureScales.Clear();
        
        // Tạo các mốc nhiệt độ từ 0 đến 120, mỗi 10 độ
        for (int temp = (int)MaxTemp; temp >= (int)MinTemp; temp -= 10)
        {
            var percentage = (temp - MinTemp) / (MaxTemp - MinTemp);
            var yPosition = 15 + (1 - percentage) * (ThermometerMaxHeight - 30); // 15 = top padding
            
            TemperatureScales.Add(new TemperatureScale
            {
                Value = temp,
                Label = $"{temp}°",
                YPosition = yPosition,
                Color = GetScaleColor(temp),
                FontSize = (temp == 0 || temp == 120) ? 16 : 14,
                FontWeight = (temp == 0 || temp == 120) ? "Bold" : "Normal"
            });
        }
    }

    private string GetScaleColor(int temp)
    {
        if (temp >= 100) return "#FF0000";
        if (temp >= 90) return "#FF2222";
        if (temp >= 80) return "#FF4444";
        if (temp >= 70) return "#FF6644";
        if (temp >= 60) return "#FF8844";
        if (temp >= 50) return "#FFAA44";
        if (temp >= 40) return "#FFCC44";
        if (temp >= 30) return "#FFEE44";
        if (temp >= 20) return "#CCFF44";
        if (temp >= 10) return "#88FF88";
        return "#4488FF";
    }

    private void UpdateThermometer()
    {
        // Tính chiều cao cột thủy ngân (0°C = 0%, 120°C = 100%)
        var percentage = Math.Clamp((Temperature - MinTemp) / (MaxTemp - MinTemp), 0, 1);
        MercuryHeight = Math.Max(20, percentage * ThermometerMaxHeight);

        // Thay đổi màu theo nhiệt độ
        Color color;
        if (Temperature < LowTempThreshold)
        {
            // Màu xanh dương (lạnh)
            color = Color.FromRgb(68, 136, 255);
        }
        else if (Temperature < 40)
        {
            // Màu xanh lục nhạt
            color = Color.FromRgb(136, 255, 136);
        }
        else if (Temperature < 60)
        {
            // Màu vàng
            color = Color.FromRgb(255, 238, 68);
        }
        else if (Temperature < HighTempThreshold)
        {
            // Màu cam
            color = Color.FromRgb(255, 136, 68);
        }
        else if (Temperature < 100)
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

public class TemperatureScale
{
    public int Value { get; set; }
    public string Label { get; set; } = string.Empty;
    public double YPosition { get; set; }
    public string Color { get; set; } = "#FFFFFF";
    public int FontSize { get; set; }
    public string FontWeight { get; set; } = "Normal";
}