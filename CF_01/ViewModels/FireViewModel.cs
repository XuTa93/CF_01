using System;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF_01.ViewModels;

public partial class FireViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private double _temperature = 10.0;

    [ObservableProperty]
    private bool _isLowTemperature = false;

    [ObservableProperty]
    private bool _isNormalTemperature = true;

    [ObservableProperty]
    private bool _isHighTemperature = false;

    // Fire text effect properties
    [ObservableProperty]
    private IBrush _fireTextBrush = new SolidColorBrush(Colors.White);

    [ObservableProperty]
    private Color _fireOuterGlowColor = Colors.Transparent;

    [ObservableProperty]
    private double _fireOuterGlowRadius = 0;

    [ObservableProperty]
    private Color _fireInnerGlowColor = Colors.Transparent;

    [ObservableProperty]
    private double _fireInnerGlowRadius = 0;

    [ObservableProperty]
    private double _fireGlowOpacity = 0;

    // Ngưỡng nhiệt độ (sync từ RangeSlider)
    [ObservableProperty]
    private double _lowTempThreshold = 70.0;

    [ObservableProperty]
    private double _highTempThreshold = 100.0;

    // Dải nhiệt cho hiệu ứng lửa text
    private const double FireMinTemp = 20.0;
    private const double FireMaxTemp = 120.0;

    // Reuse brush để giảm GC pressure
    private readonly LinearGradientBrush _reusableTextBrush;

    public FireViewModel()
    {
        _reusableTextBrush = new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0.5, 0, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(0.5, 1, Avalonia.RelativeUnit.Relative),
            GradientStops = { new GradientStop(), new GradientStop(), new GradientStop() }
        };

        UpdateFireDisplay();
        UpdateFireTextEffect();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(0.1)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Temperature += 1;
        if (Temperature > 110)
        {
            Temperature = 10;
        }
    }

    partial void OnTemperatureChanged(double value)
    {
        UpdateFireDisplay();
        UpdateFireTextEffect();
    }

    partial void OnLowTempThresholdChanged(double value)
    {
        UpdateFireDisplay();
    }

    partial void OnHighTempThresholdChanged(double value)
    {
        UpdateFireDisplay();
    }

    private void UpdateFireDisplay()
    {
        if (Temperature < 30)
        {
            // Dưới 30°C: không hiển thị lửa
            IsLowTemperature = false;
            IsNormalTemperature = false;
            IsHighTemperature = false;
        }
        else if (Temperature < LowTempThreshold)
        {
            IsLowTemperature = true;
            IsNormalTemperature = false;
            IsHighTemperature = false;
        }
        else if (Temperature < HighTempThreshold)
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

    private void UpdateFireTextEffect()
    {
        // t: 0.0 (20°C) → 1.0 (110°C)
        double t = Math.Clamp((Temperature - FireMinTemp) / (FireMaxTemp - FireMinTemp), 0, 1);

        // === Text foreground: cool white → warm yellow → hot orange → white-hot ===
        Color textColor;
        if (t < 0.3)
        {
            // White → Light yellow
            textColor = LerpColor(Color.FromRgb(240, 240, 255), Color.FromRgb(255, 240, 150), t / 0.3);
        }
        else if (t < 0.6)
        {
            // Light yellow → Bright orange-yellow
            textColor = LerpColor(Color.FromRgb(255, 240, 150), Color.FromRgb(255, 200, 60), (t - 0.3) / 0.3);
        }
        else if (t < 0.85)
        {
            // Bright orange-yellow → Orange
            textColor = LerpColor(Color.FromRgb(255, 200, 60), Color.FromRgb(255, 140, 30), (t - 0.6) / 0.25);
        }
        else
        {
            // Orange → White-hot (extremely hot = bright white-yellow center)
            textColor = LerpColor(Color.FromRgb(255, 140, 30), Color.FromRgb(255, 255, 220), (t - 0.85) / 0.15);
        }

        // Vertical gradient: top darker/redder (tip of flame) → bottom brighter (base)
        var topColor = LerpColor(textColor, Color.FromRgb(255, 60, 10), Math.Min(t * 0.7, 0.55));
        var bottomColor = LerpColor(textColor, Color.FromRgb(255, 255, 240), 0.25);

        _reusableTextBrush.GradientStops[0] = new GradientStop(topColor, 0);
        _reusableTextBrush.GradientStops[1] = new GradientStop(textColor, 0.45);
        _reusableTextBrush.GradientStops[2] = new GradientStop(bottomColor, 1.0);
        FireTextBrush = _reusableTextBrush;

        // === Inner glow: yellow → orange, grows with temperature ===
        FireInnerGlowColor = LerpColor(
            Color.FromRgb(255, 220, 80),
            Color.FromRgb(255, 120, 20),
            t);
        FireInnerGlowRadius = 2 + t * 30;

        // === Outer glow: orange → deep red, larger radius ===
        FireOuterGlowColor = LerpColor(
            Color.FromRgb(255, 160, 30),
            Color.FromRgb(220, 20, 0),
            t);
        FireOuterGlowRadius = 5 + t * 55;

        // === Glow opacity: subtle → intense ===
        FireGlowOpacity = 0.05 + t * 0.9;
    }

    private static Color LerpColor(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t));
    }

    public void Dispose()
    {
        _timer.Tick -= OnTimerTick;
        _timer.Stop();
    }
}