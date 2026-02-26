using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CF_01.Models;
using CF_01.Services;

namespace CF_01.ViewModels;

public partial class FireViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly AppConfig _config;
    private ITemperatureSensor _sensor;

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

    // Dynamic shadow properties
    [ObservableProperty]
    private double _shadowBlurStrong = 20;

    [ObservableProperty]
    private double _shadowOpacityStrong = 1.0;

    [ObservableProperty]
    private double _shadowBlurMedium = 8;

    [ObservableProperty]
    private double _shadowOpacityMedium = 0.95;

    [ObservableProperty]
    private double _shadowBlurMain = 6;

    [ObservableProperty]
    private double _shadowOpacityMain = 0.85;

    // Ngưỡng nhiệt độ hiển thị (sync từ RangeSlider)
    [ObservableProperty]
    private double _lowTempThreshold = 70.0;

    [ObservableProperty]
    private double _highTempThreshold = 100.0;

    // === Thông tin nguồn cảm biến ===
    [ObservableProperty]
    private string _sensorSourceName = "";

    // === Thời gian thực ===
    [ObservableProperty]
    private string _currentTimeText = "00:00:00";

    [ObservableProperty]
    private string _sessionElapsedText = "";

    [ObservableProperty]
    private string _sessionStartTimeText = "";

    [ObservableProperty]
    private string _programElapsedText = "00:00:00";

    [ObservableProperty]
    private string _intervalProgressText = "";

    [ObservableProperty]
    private string _intervalPanelTitle = "";

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
    private string _lastSessionElapsedText = "";

    [ObservableProperty]
    private int _lastSampleCount = 0;

    [ObservableProperty]
    private int _completedSessions = 0;

    // === State machine ===
    private enum SessionState { Idle, PendingStart, Active, PendingEnd }
    private SessionState _sessionState = SessionState.Idle;
    private int _stateTickCount = 0;

    // Config-driven thresholds (ticks = seconds / 0.1)
    private int _startDelayTicks;
    private int _endDelayTicks;

    // === Sampling: 0.1s đọc, trung bình 10 lần → 1 mẫu lưu trữ ===
    private double _pollSum = 0;
    private double _pollMin = double.MaxValue;
    private double _pollMax = 0;
    private int _pollCount = 0;

    // Session accumulation (trên mẫu lưu trữ 1s)
    private double _sessionTempSum = 0;
    private int _storedSampleCount = 0;
    private DateTime _sessionStartTime;

    // Interval averaging (chu kỳ cài đặt)
    public ObservableCollection<IntervalAverageEntry> IntervalAverages { get; } = new();
    private double _intervalTempSum = 0;
    private int _intervalSampleCount = 0;
    private double _intervalMin = double.MaxValue;
    private double _intervalMax = 0;
    private int _intervalIndex = 1;
    private DateTime _intervalStartTime;
    private int _samplesPerInterval;

    // Session data for persistence
    private SessionData? _currentSessionData;

    // Program start time
    private readonly DateTime _programStartTime = DateTime.Now;

    // Dải nhiệt cho hiệu ứng lửa text
    private const double FireMinTemp = 20.0;
    private const double FireMaxTemp = 115.0;

    // Reuse brush để giảm GC pressure
    private readonly LinearGradientBrush _reusableTextBrush;

    public FireViewModel()
    {
        _config = AppConfig.Load();
        _sensor = CreateSensor(_config);
        SensorSourceName = _sensor.SourceName;

        // Tính ticks từ config
        _startDelayTicks = (int)(_config.StartDelaySeconds / _config.SensorPollIntervalSeconds);
        _endDelayTicks = (int)(_config.EndDelaySeconds / _config.SensorPollIntervalSeconds);
        double secondsPerSample = _config.SensorPollIntervalSeconds * _config.SamplesPerStoredReading;
        _samplesPerInterval = Math.Max(1, (int)(_config.AverageIntervalSeconds / secondsPerSample));
        int intervalMinutes = _config.AverageIntervalSeconds / 60;
        IntervalPanelTitle = intervalMinutes >= 60
            ? $"\ud83d\udcc8 TB \u0110\u1ecaNH K\u1ef2 ({intervalMinutes / 60}h)"
            : $"\ud83d\udcc8 TB \u0110\u1ecaNH K\u1ef2 ({intervalMinutes}ph)";

        LowTempThreshold = _config.DisplayLowThreshold;
        HighTempThreshold = _config.DisplayHighThreshold;

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
            Interval = TimeSpan.FromSeconds(_config.SensorPollIntervalSeconds)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private static ITemperatureSensor CreateSensor(AppConfig config)
    {
        return config.TemperatureSource.ToLowerInvariant() switch
        {
            "csv" => new CsvTemperatureSensor(config.CsvFilePath),
            // "modbus" => new ModbusTemperatureSensor(config), // Bước tương lai
            _ => new BuiltInSimulatorSensor()
        };
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        // Cập nhật đồng hồ thực
        CurrentTimeText = DateTime.Now.ToString("HH:mm:ss");
        ProgramElapsedText = (DateTime.Now - _programStartTime).ToString(@"hh\:mm\:ss");

        // Đọc nhiệt độ từ cảm biến (0.1s/lần)
        double rawTemp = _sensor.ReadTemperature();

        // Tích lũy cho trung bình mẫu lưu trữ
        _pollSum += rawTemp;
        _pollCount++;
        if (rawTemp < _pollMin) _pollMin = rawTemp;
        if (rawTemp > _pollMax) _pollMax = rawTemp;

        // Hiển thị nhiệt độ realtime (mỗi 0.1s)
        Temperature = rawTemp;

        // Mỗi 10 lần đọc (1s) → tạo 1 mẫu lưu trữ
        if (_pollCount >= _config.SamplesPerStoredReading)
        {
            double storedTemp = _pollSum / _pollCount;
            ProcessStoredSample(storedTemp, _pollMin, _pollMax);

            // Reset bộ tích lũy
            _pollSum = 0;
            _pollCount = 0;
            _pollMin = double.MaxValue;
            _pollMax = 0;
        }

        UpdateAveraging();
    }

    /// <summary>
    /// Xử lý 1 mẫu lưu trữ (trung bình 10 lần đọc = 1 giây).
    /// </summary>
    private void ProcessStoredSample(double avgTemp, double minTemp, double maxTemp)
    {
        if (_sessionState != SessionState.Active && _sessionState != SessionState.PendingEnd)
            return;

        _sessionTempSum += avgTemp;
        _storedSampleCount++;

        SampleCount = _storedSampleCount;
        AverageTemperature = _sessionTempSum / _storedSampleCount;
        SessionSeconds = (DateTime.Now - _sessionStartTime).TotalSeconds;
        SessionElapsedText = TimeSpan.FromSeconds(SessionSeconds).ToString(@"hh\:mm\:ss");

        if (avgTemp > MaxSessionTemp) MaxSessionTemp = avgTemp;
        if (avgTemp < MinSessionTemp) MinSessionTemp = avgTemp;

        // Lưu mẫu vào session data
        _currentSessionData?.Samples.Add(new TemperatureSample(
            DateTime.Now, avgTemp, minTemp, maxTemp));

        // Interval tracking
        _intervalTempSum += avgTemp;
        _intervalSampleCount++;
        if (avgTemp < _intervalMin) _intervalMin = avgTemp;
        if (avgTemp > _intervalMax) _intervalMax = avgTemp;

        // Cập nhật tiến trình
        int elapsedMin = _intervalSampleCount / 60;
        int totalMin = _samplesPerInterval / 60;
        IntervalProgressText = $"{elapsedMin}/{totalMin} phút";

        // Kiểm tra đủ mẫu cho 1 chu kỳ
        if (_intervalSampleCount >= _samplesPerInterval)
        {
            var intervalEnd = _intervalStartTime.AddSeconds(_config.AverageIntervalSeconds);
            var intervalAvg = _intervalTempSum / _intervalSampleCount;

            IntervalAverages.Add(new IntervalAverageEntry(
                _intervalIndex, _intervalStartTime, intervalEnd,
                intervalAvg, _intervalMin, _intervalMax, _intervalSampleCount));

            _currentSessionData?.IntervalAverages.Add(new IntervalAverage(
                _intervalIndex, _intervalStartTime, intervalEnd,
                intervalAvg, _intervalMin, _intervalMax, _intervalSampleCount));

            _intervalIndex++;
            _intervalTempSum = 0;
            _intervalSampleCount = 0;
            _intervalMin = double.MaxValue;
            _intervalMax = 0;
            _intervalStartTime = intervalEnd;
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
        UpdateFireTextEffect();
    }

    partial void OnHighTempThresholdChanged(double value)
    {
        UpdateFireDisplay();
        UpdateFireTextEffect();
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
        double startThreshold = _config.StartThreshold;
        double endThreshold = _config.EndThreshold;
        double startDelaySec = _config.StartDelaySeconds;
        double endDelaySec = _config.EndDelaySeconds;

        switch (_sessionState)
        {
            case SessionState.Idle:
                if (Temperature >= startThreshold)
                {
                    _sessionState = SessionState.PendingStart;
                    _stateTickCount = 1;
                    SessionStatus = $"Chờ kích hoạt ({_stateTickCount * 0.1:F1}s / {startDelaySec:F0}s)...";
                }
                else
                {
                    SessionStatus = "Chờ khởi động...";
                }
                break;

            case SessionState.PendingStart:
                if (Temperature >= startThreshold)
                {
                    _stateTickCount++;
                    SessionStatus = $"Chờ kích hoạt ({_stateTickCount * 0.1:F1}s / {startDelaySec:F0}s)...";

                    if (_stateTickCount >= _startDelayTicks)
                    {
                        StartSession();
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
                if (Temperature < endThreshold)
                {
                    _sessionState = SessionState.PendingEnd;
                    _stateTickCount = 1;
                    SessionStatus = $"🔥 Chờ kết thúc ({_stateTickCount * 0.1:F1}s / {endDelaySec:F0}s)...";
                }
                else
                {
                    SessionStatus = "🔥 Đang ghi nhận...";
                }
                break;

            case SessionState.PendingEnd:
                if (Temperature < endThreshold)
                {
                    _stateTickCount++;
                    SessionStatus = $"🔥 Chờ kết thúc ({_stateTickCount * 0.1:F1}s / {endDelaySec:F0}s)...";

                    if (_stateTickCount >= _endDelayTicks)
                    {
                        EndSession();
                    }
                }
                else
                {
                    _sessionState = SessionState.Active;
                    _stateTickCount = 0;
                    SessionStatus = "🔥 Đang ghi nhận...";
                }
                break;
        }
    }

    private void StartSession()
    {
        _sessionState = SessionState.Active;
        _sessionTempSum = 0;
        _storedSampleCount = 0;
        _sessionStartTime = DateTime.Now;
        MaxSessionTemp = 0;
        MinSessionTemp = double.MaxValue;
        IsSessionActive = true;
        SessionStartTimeText = DateTime.Now.ToString("HH:mm:ss");
        SessionStatus = "🔥 Đang ghi nhận...";

        // Reset interval tracking
        IntervalAverages.Clear();
        _intervalTempSum = 0;
        _intervalSampleCount = 0;
        _intervalMin = double.MaxValue;
        _intervalMax = 0;
        _intervalIndex = 1;
        _intervalStartTime = DateTime.Now;

        // Tạo session data mới
        _currentSessionData = new SessionData
        {
            StartTime = _sessionStartTime
        };
    }

    private void EndSession()
    {
        // Finalize chu kỳ cuối (nếu còn mẫu chưa ghi)
        if (_intervalSampleCount > 0)
        {
            var intervalEnd = _intervalStartTime.AddSeconds(_config.AverageIntervalSeconds);
            var intervalAvg = _intervalTempSum / _intervalSampleCount;

            IntervalAverages.Add(new IntervalAverageEntry(
                _intervalIndex, _intervalStartTime, intervalEnd,
                intervalAvg, _intervalMin, _intervalMax, _intervalSampleCount));

            _currentSessionData?.IntervalAverages.Add(new IntervalAverage(
                _intervalIndex, _intervalStartTime, intervalEnd,
                intervalAvg, _intervalMin, _intervalMax, _intervalSampleCount));
        }

        // Cập nhật thông tin phiên trước
        LastAverageTemperature = AverageTemperature;
        LastSessionSeconds = SessionSeconds;
        LastSessionElapsedText = SessionElapsedText;
        LastSampleCount = SampleCount;
        CompletedSessions++;

        // Lưu session data
        if (_currentSessionData != null)
        {
            _currentSessionData.EndTime = DateTime.Now;
            _currentSessionData.OverallAverage = AverageTemperature;
            _currentSessionData.MaxTemperature = MaxSessionTemp;
            _currentSessionData.MinTemperature = MinSessionTemp;
            _currentSessionData.TotalSamples = SampleCount;
            _currentSessionData.DurationSeconds = SessionSeconds;

            SessionStorage.SaveSession(_currentSessionData, _config.SessionDataFolder);
            _currentSessionData = null;
        }

        _sessionState = SessionState.Idle;
        _stateTickCount = 0;
        IsSessionActive = false;
        SessionStatus = "✅ Phiên hoàn tất";
    }

    private void UpdateFireTextEffect()
    {
        double t = Math.Clamp((Temperature - FireMinTemp) / (FireMaxTemp - FireMinTemp), 0, 1);

        bool isInGreenZone = Temperature >= LowTempThreshold && Temperature < HighTempThreshold;

        Color textColor;
        Color topColor;
        Color bottomColor;

        if (isInGreenZone)
        {
            double g = Math.Clamp(
                (Temperature - LowTempThreshold) / (HighTempThreshold - LowTempThreshold), 0, 1);

            textColor = LerpColor(
                Color.FromRgb(100, 255, 150),
                Color.FromRgb(50, 220, 80), g);

            topColor = LerpColor(textColor, Color.FromRgb(30, 180, 60), 0.3);
            bottomColor = LerpColor(textColor, Color.FromRgb(180, 255, 200), 0.25);

            FireInnerGlowColor = LerpColor(
                Color.FromRgb(100, 255, 120),
                Color.FromRgb(50, 200, 80), g);
            FireInnerGlowRadius = 5 + g * 20;

            FireOuterGlowColor = LerpColor(
                Color.FromRgb(60, 220, 100),
                Color.FromRgb(30, 160, 60), g);
            FireOuterGlowRadius = 10 + g * 35;

            FireGlowOpacity = 0.3 + g * 0.5;
        }
        else
        {
            if (t < 0.3)
            {
                textColor = LerpColor(Color.FromRgb(240, 240, 255), Color.FromRgb(255, 240, 150), t / 0.3);
            }
            else if (t < 0.6)
            {
                textColor = LerpColor(Color.FromRgb(255, 240, 150), Color.FromRgb(255, 200, 60), (t - 0.3) / 0.3);
            }
            else if (t < 0.85)
            {
                textColor = LerpColor(Color.FromRgb(255, 200, 60), Color.FromRgb(255, 140, 30), (t - 0.6) / 0.25);
            }
            else
            {
                textColor = LerpColor(Color.FromRgb(255, 140, 30), Color.FromRgb(255, 255, 220), (t - 0.85) / 0.15);
            }

            topColor = LerpColor(textColor, Color.FromRgb(255, 60, 10), Math.Min(t * 0.7, 0.55));
            bottomColor = LerpColor(textColor, Color.FromRgb(255, 255, 240), 0.25);

            FireInnerGlowColor = LerpColor(
                Color.FromRgb(255, 220, 80),
                Color.FromRgb(255, 120, 20), t);
            FireInnerGlowRadius = 2 + t * 30;

            FireOuterGlowColor = LerpColor(
                Color.FromRgb(255, 160, 30),
                Color.FromRgb(220, 20, 0), t);
            FireOuterGlowRadius = 5 + t * 55;

            FireGlowOpacity = 0.05 + t * 0.9;
        }

        _reusableTextBrush.GradientStops[0] = new GradientStop(topColor, 0);
        _reusableTextBrush.GradientStops[1] = new GradientStop(textColor, 0.45);
        _reusableTextBrush.GradientStops[2] = new GradientStop(bottomColor, 1.0);
        FireTextBrush = _reusableTextBrush;

        ShadowBlurStrong = 20 + t * 20;
        ShadowOpacityStrong = 1.0;
        ShadowBlurMedium = 8 + t * 8;
        ShadowOpacityMedium = 0.95 + t * 0.05;
        ShadowBlurMain = 6 + t * 9;
        ShadowOpacityMain = 0.85 + t * 0.15;
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
        _sensor.Dispose();
    }
}