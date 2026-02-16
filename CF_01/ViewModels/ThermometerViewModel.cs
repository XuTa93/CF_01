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

    private const double MinTemp = 20.0;
    private const double MaxTemp = 120.0;
    private const double ColumnHeight = 590.0; // usable height for mercury column (above bulb)
    private const double LowTempThreshold = 20.0;
    private const double HighTempThreshold = 80.0;

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
        var percentage = Math.Clamp((Temperature - MinTemp) / (MaxTemp - MinTemp), 0, 1);
        MercuryHeight = Math.Max(10, percentage * ColumnHeight);

        Color color;
        if (Temperature < LowTempThreshold)
            color = Color.FromRgb(68, 136, 255);
        else if (Temperature < 40)
            color = Color.FromRgb(100, 210, 160);
        else if (Temperature < 60)
            color = Color.FromRgb(255, 200, 50);
        else if (Temperature < HighTempThreshold)
            color = Color.FromRgb(255, 140, 50);
        else if (Temperature < 100)
            color = Color.FromRgb(255, 80, 50);
        else
            color = Color.FromRgb(230, 30, 30);

        MercuryColor = color;
        MercuryBrush = new SolidColorBrush(color);
    }
}