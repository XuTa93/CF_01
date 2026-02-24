using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace CF_01.ViewModels;

public partial class ThermometerViewModel : ObservableObject
{
    [ObservableProperty]
    private double _upperTemperature = 100.0;

    [ObservableProperty]
    private double _lowerTemperature = 25.0;

    [ObservableProperty]
    private double _temperature = 25.0;

    [ObservableProperty]
    private double _mercuryHeight = 100.0;

    [ObservableProperty]
    private IBrush _mercuryBrush = new SolidColorBrush(Color.FromRgb(255, 204, 68));

    [ObservableProperty]
    private Color _mercuryColor = Color.FromRgb(255, 204, 68);

    [ObservableProperty]
    private Avalonia.CornerRadius _mercuryCornerRadius = new(0, 0, 8, 8);

    // Vị trí Y của label Lower/Upper (tính từ top Canvas)
    [ObservableProperty]
    private double _lowerLabelY = 0;

    [ObservableProperty]
    private double _upperLabelY = 0;

    private const double MinTemp = 30.0;
    private const double MaxTemp = 120.0;
    private const double ColumnHeight = 590.0;
    private const double LowTempThreshold = 30.0;
    private const double HighTempThreshold = 80.0;

    // Layout constants
    private const double TopPadding = 15.0;
    private const double UsableHeight = 590.0;

    // Slider thumb offset — thumb center không chạm biên slider,
    // bị co vào bởi nửa chiều cao thumb ở mỗi đầu
    private const double SliderTopMargin = 15.0;
    private const double SliderBottomMargin = 45.0;
    private const double CanvasHeight = 650.0;
    private const double ThumbHalf = 10.0;
    private const double TrackTop = SliderTopMargin + ThumbHalf;
    private const double TrackBottom = CanvasHeight - SliderBottomMargin - ThumbHalf;
    private const double TrackRange = TrackBottom - TrackTop;

    public ThermometerViewModel()
    {
        UpdateThermometer();
        UpdateLabelPositions();
    }

    partial void OnTemperatureChanged(double value)
    {
        UpdateThermometer();
    }

    partial void OnLowerTemperatureChanged(double value)
    {
        UpdateLabelPositions();
    }

    partial void OnUpperTemperatureChanged(double value)
    {
        UpdateLabelPositions();
    }

    private void UpdateLabelPositions()
    {
        double lowerPct = Math.Clamp((LowerTemperature - MinTemp) / (MaxTemp - MinTemp), 0, 1);
        LowerLabelY = TrackTop + (1 - lowerPct) * TrackRange - 10;

        double upperPct = Math.Clamp((UpperTemperature - MinTemp) / (MaxTemp - MinTemp), 0, 1);
        UpperLabelY = TrackTop + (1 - upperPct) * TrackRange - 10;
    }

    private void UpdateThermometer()
    {
        var percentage = Math.Clamp((Temperature - MinTemp) / (MaxTemp - MinTemp), 0, 1);
        MercuryHeight = Math.Max(10, percentage * ColumnHeight);

        // Bo tròn đầu ống chỉ khi gần đầy (>90%)
        double topRadius = percentage > 0.9
            ? 23.0 * ((percentage - 0.9) / 0.1)
            : 0;
        MercuryCornerRadius = new Avalonia.CornerRadius(topRadius, topRadius, 8, 8);

        Color color;
        if (Temperature < LowTempThreshold)
            color = Color.FromRgb(80, 160, 255);
        else if (Temperature < 40)
            color = Color.FromRgb(50, 220, 140);
        else if (Temperature < 60)
            color = Color.FromRgb(255, 220, 60);
        else if (Temperature < HighTempThreshold)
            color = Color.FromRgb(255, 160, 40);
        else if (Temperature < 100)
            color = Color.FromRgb(255, 80, 40);
        else
            color = Color.FromRgb(240, 40, 40);

        MercuryColor = color;
        MercuryBrush = new SolidColorBrush(color);
    }
}