using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF_01.ViewModels;

public partial class FireViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();

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

    // === 24h Simulation ===
    [ObservableProperty]
    private double _simulatedHour = 0;

    [ObservableProperty]
    private string _simulatedTimeText = "00:00";

    [ObservableProperty]
    private string _simulatedPeriod = "";

    // 24h nén thành CycleDurationSeconds giây (24 phút = 1 phút/giờ giả lập)
    private const double CycleDurationSeconds = 1440.0;
    private const double TickInterval = 0.1;
    private const double HoursPerTick = 24.0 / (CycleDurationSeconds / TickInterval);
    private double _simHour = 0;

    // === Averaging session properties ===
    [ObservableProperty]
    private double _averageTemperature = 0;

    [ObservableProperty]
    private bool _isSessionActive = false;

    [ObservableProperty]
    private string _sessionStatus = "Chờ khởi động...";

    [ObservableProperty]
    private int _sampleCount = 0;

    [ObservableProperty]
    private double _sessionSeconds = 0;

    [ObservableProperty]
    private double _maxSessionTemp = 0;

    [ObservableProperty]
    private double _minSessionTemp = double.MaxValue;

    [ObservableProperty]
    private double _lastAverageTemperature = 0;

    [ObservableProperty]
    private double _lastSessionSeconds = 0;

    [ObservableProperty]
    private int _lastSampleCount = 0;

    [ObservableProperty]
    private int _completedSessions = 0;

    // Averaging state machine
    private enum SessionState { Idle, PendingStart, Active, PendingEnd }
    private SessionState _sessionState = SessionState.Idle;
    private int _stateTickCount = 0;
    private double _tempSum = 0;
    private int _currentSampleCount = 0;
    private int _sessionTickCount = 0;

    private const double ActivationThreshold = 70.0;
    private const int StartDelayTicks = 10;  // 1s (0.1s × 10)
    private const int EndDelayTicks = 5;     // 0.5s (0.1s × 5)
    private const int TicksPerMinute = 600;  // 60s / 0.1s

    // Per-minute averaging
    public ObservableCollection<MinuteAverageEntry> MinuteAverages { get; } = new();
    private double _minuteTempSum = 0;
    private int _minuteSampleCount = 0;
    private double _minuteMin = double.MaxValue;
    private double _minuteMax = 0;
    private int _minuteIndex = 1;

    // Dải nhiệt cho hiệu ứng lửa text
    private const double FireMinTemp = 20.0;
    private const double FireMaxTemp = 115.0;

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
        // Advance simulated clock
        _simHour += HoursPerTick;
        if (_simHour >= 24.0)
            _simHour -= 24.0;

        SimulatedHour = _simHour;
        int hours = (int)_simHour;
        int minutes = (int)((_simHour - hours) * 60);
        SimulatedTimeText = $"{hours:D2}:{minutes:D2}";

        // Generate realistic temperature from 24h profile
        Temperature = GetTemperatureAtHour(_simHour);

        UpdateAveraging();
    }

    /// <summary>
    /// Mô phỏng nhiệt độ lò nung theo chu kỳ 24h:
    /// 0-0.25h:  Lò nguội (20-25°C)  → 15s thực
    /// 0.25-1h:  Khởi động nhanh (25→80°C)
    /// 1-16h:    Hoạt động chính (78-110°C, dao động)
    /// 16-18h:   Giảm tải (110→65°C)
    /// 18-22h:   Vận hành thấp (55-70°C)
    /// 22-24h:   Tắt lò (65→20°C)
    /// </summary>
    private double GetTemperatureAtHour(double hour)
    {
        double baseTemp;
        double noiseAmplitude;

        if (hour < 0.25)
        {
            // Lò nguội: 20-25°C, nhiễu nhẹ (15s thực)
            SimulatedPeriod = "🌙 Lò nguội";
            baseTemp = 22 + 3 * Math.Sin(hour / 0.25 * Math.PI);
            noiseAmplitude = 2;
        }
        else if (hour < 1)
        {
            // Khởi động nhanh: tăng từ 25 → 80
            SimulatedPeriod = "🔄 Khởi động";
            double t = (hour - 0.25) / 0.75;
            double eased = t * t * (3 - 2 * t);
            baseTemp = 25 + (80 - 25) * eased;
            noiseAmplitude = 3 + t * 4;
        }
        else if (hour < 16)
        {
            // Hoạt động chính: 78-110°C, nhiều dao động
            SimulatedPeriod = "🔥 Hoạt động chính";
            double t = (hour - 1) / 15.0;
            // Peak ở giữa ca (~12h)
            double peak = Math.Sin(t * Math.PI);
            baseTemp = 78 + 30 * peak;
            noiseAmplitude = 5 + peak * 6;
        }
        else if (hour < 18)
        {
            // Giảm tải: 110 → 65
            SimulatedPeriod = "📉 Giảm tải";
            double t = (hour - 16) / 2.0;
            double eased = t * t * (3 - 2 * t);
            baseTemp = 108 - (108 - 65) * eased;
            noiseAmplitude = 4 - t * 2;
        }
        else if (hour < 22)
        {
            // Vận hành thấp: 55-70°C
            SimulatedPeriod = "⚡ Vận hành thấp";
            double t = (hour - 18) / 4.0;
            baseTemp = 62 + 6 * Math.Sin(t * Math.PI * 2);
            noiseAmplitude = 3;
        }
        else
        {
            // Tắt lò: 65 → 20
            SimulatedPeriod = "🛑 Tắt lò";
            double t = (hour - 22) / 2.0;
            double eased = t * t * (3 - 2 * t);
            baseTemp = 62 - (62 - 20) * eased;
            noiseAmplitude = 2 - t;
        }

        // Thêm nhiễu ngẫu nhiên cho realistic
        double noise = (_random.NextDouble() * 2 - 1) * noiseAmplitude;

        return Math.Clamp(baseTemp + noise, 10, 120);
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

    private void UpdateAveraging()
    {
        switch (_sessionState)
        {
            case SessionState.Idle:
                if (Temperature >= ActivationThreshold)
                {
                    _sessionState = SessionState.PendingStart;
                    _stateTickCount = 1;
                    SessionStatus = $"Chờ kích hoạt ({_stateTickCount * 0.1:F1}s / 1.0s)...";
                }
                else
                {
                    SessionStatus = "Chờ khởi động...";
                }
                break;

            case SessionState.PendingStart:
                if (Temperature >= ActivationThreshold)
                {
                    _stateTickCount++;
                    SessionStatus = $"Chờ kích hoạt ({_stateTickCount * 0.1:F1}s / 1.0s)...";

                    if (_stateTickCount >= StartDelayTicks)
                    {
                        _sessionState = SessionState.Active;
                        _tempSum = 0;
                        _currentSampleCount = 0;
                        _sessionTickCount = 0;
                        MaxSessionTemp = 0;
                        MinSessionTemp = double.MaxValue;
                        IsSessionActive = true;
                        SessionStatus = "🔥 Đang ghi nhận...";

                        // Reset per-minute tracking
                        MinuteAverages.Clear();
                        _minuteTempSum = 0;
                        _minuteSampleCount = 0;
                        _minuteMin = double.MaxValue;
                        _minuteMax = 0;
                        _minuteIndex = 1;
                    }
                }
                else
                {
                    _sessionState = SessionState.Idle;
                    _stateTickCount = 0;
                    SessionStatus = "Chờ khởi động...";
                }
                break;

            case SessionState.Active:
                CollectSample();
                if (Temperature < ActivationThreshold)
                {
                    _sessionState = SessionState.PendingEnd;
                    _stateTickCount = 1;
                    SessionStatus = $"🔥 Chờ kết thúc ({_stateTickCount * 0.1:F1}s / 2.0s)...";
                }
                else
                {
                    SessionStatus = "🔥 Đang ghi nhận...";
                }
                break;

            case SessionState.PendingEnd:
                CollectSample();
                if (Temperature < ActivationThreshold)
                {
                    _stateTickCount++;
                    SessionStatus = $"🔥 Chờ kết thúc ({_stateTickCount * 0.1:F1}s / 2.0s)...";

                    if (_stateTickCount >= EndDelayTicks)
                    {
                        // Finalize phút cuối (nếu còn mẫu chưa ghi)
                        if (_minuteSampleCount > 0)
                        {
                            MinuteAverages.Add(new MinuteAverageEntry(
                                _minuteIndex,
                                SimulatedTimeText,
                                _minuteTempSum / _minuteSampleCount,
                                _minuteMin,
                                _minuteMax,
                                _minuteSampleCount));
                        }

                        // Kết thúc phiên
                        LastAverageTemperature = AverageTemperature;
                        LastSessionSeconds = SessionSeconds;
                        LastSampleCount = SampleCount;
                        CompletedSessions++;

                        _sessionState = SessionState.Idle;
                        _stateTickCount = 0;
                        IsSessionActive = false;
                        SessionStatus = "✅ Phiên hoàn tất";
                    }
                }
                else
                {
                    // Nhiệt độ tăng lại → tiếp tục phiên
                    _sessionState = SessionState.Active;
                    _stateTickCount = 0;
                    SessionStatus = "🔥 Đang ghi nhận...";
                }
                break;
        }
    }

    private void CollectSample()
    {
        _tempSum += Temperature;
        _currentSampleCount++;
        _sessionTickCount++;

        SampleCount = _currentSampleCount;
        AverageTemperature = _tempSum / _currentSampleCount;
        SessionSeconds = _sessionTickCount * 0.1;

        if (Temperature > MaxSessionTemp) MaxSessionTemp = Temperature;
        if (Temperature < MinSessionTemp) MinSessionTemp = Temperature;

        // Per-minute tracking
        _minuteTempSum += Temperature;
        _minuteSampleCount++;
        if (Temperature < _minuteMin) _minuteMin = Temperature;
        if (Temperature > _minuteMax) _minuteMax = Temperature;

        if (_sessionTickCount % TicksPerMinute == 0 && _minuteSampleCount > 0)
        {
            MinuteAverages.Add(new MinuteAverageEntry(
                _minuteIndex,
                SimulatedTimeText,
                _minuteTempSum / _minuteSampleCount,
                _minuteMin,
                _minuteMax,
                _minuteSampleCount));

            _minuteIndex++;
            _minuteTempSum = 0;
            _minuteSampleCount = 0;
            _minuteMin = double.MaxValue;
            _minuteMax = 0;
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