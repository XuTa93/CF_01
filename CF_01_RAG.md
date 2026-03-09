# CF_01 — Hệ thống giám sát nhiệt độ lò sấy cà phê

## Tổng quan

Ứng dụng desktop giám sát nhiệt độ lò sấy cà phê realtime, gồm 2 project:
- **CF_01** — App chính (Avalonia UI), đọc nhiệt độ qua Modbus RTU Master (COM3)
- **CF_01_Simulation** — Giả lập cảm biến (Console), phát dữ liệu CSV qua Modbus RTU Slave (COM2)

Giao tiếp: `CF_01_Simulation (COM2)` ↔ virtual COM pair (com0com) ↔ `CF_01 (COM3)`

---

## Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Framework | .NET 10 (C# 14) |
| UI | Avalonia 11.3 (Fluent Theme) |
| MVVM | CommunityToolkit.Mvvm 8.4 |
| Serial | System.IO.Ports 10.0.3 |
| Modbus | Tự viết (không dùng thư viện), CRC-16/Modbus |
| Slider | RangeSlider.Avalonia 2.1 |
| GIF | Avalonia.Labs.Gif 11.3.1 |

---

## Kiến trúc Solution

```
CF_01.sln
├── CF_01/                          # App chính (Avalonia Desktop)
│   ├── Program.cs                  # Entry point
│   ├── App.axaml / App.axaml.cs    # Avalonia Application
│   ├── Models/
│   │   ├── AppConfig.cs            # Cấu hình app (config.json)
│   │   └── SessionData.cs          # Model dữ liệu phiên đo
│   ├── Services/
│   │   ├── ITemperatureSensor.cs   # Interface cảm biến
│   │   ├── ModbusTemperatureSensor.cs  # Modbus RTU Master (FC03)
│   │   └── SessionStorage.cs       # Lưu/đọc phiên đo JSON
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs        # Base class (ObservableObject)
│   │   ├── MainWindowViewModel.cs  # ViewModel cửa sổ chính
│   │   ├── FireViewModel.cs        # ViewModel hiển thị lửa + logic đo
│   │   ├── ThermometerViewModel.cs # ViewModel nhiệt kế
│   │   └── MinuteAverageEntry.cs   # Entry trung bình theo chu kỳ
│   └── Views/
│       ├── MainWindow.axaml/.cs    # Cửa sổ chính
│       ├── Fire.axaml/.cs          # UserControl lửa + stats
│       └── Thermometer.axaml/.cs   # UserControl nhiệt kế
│
└── CF_01_Simulation/               # Giả lập cảm biến (Console)
    ├── Program.cs                  # Entry + CSV loader/generator + SimConfig
    └── ModbusRtuSlave.cs           # Modbus RTU Slave tự viết (FC03, FC06)
```

---

## Giao thức Modbus RTU

### Cấu hình mặc định

| Tham số | Simulation (Slave) | App (Master) |
|---|---|---|
| COM Port | COM2 | COM3 |
| Baud Rate | 9600 | 9600 |
| Data Bits | 8 | 8 |
| Parity | None | None |
| Stop Bits | 1 | 1 |
| Slave ID | 1 | 1 |
| Register Address | 0 | 0 |
| Scale Factor | 10.0 | 10.0 |

### Frame format

```
Request FC03 (Read Holding Registers):
[SlaveID:1] [0x03:1] [StartAddr:2] [Quantity:2] [CRC:2] = 8 bytes

Response FC03:
[SlaveID:1] [0x03:1] [ByteCount:1] [Data:N*2] [CRC:2]

Request FC06 (Write Single Register):
[SlaveID:1] [0x06:1] [Addr:2] [Value:2] [CRC:2] = 8 bytes

Response FC06: Echo request (8 bytes)

Exception Response:
[SlaveID:1] [FC|0x80:1] [ExceptionCode:1] [CRC:2] = 5 bytes
```

### CRC-16/Modbus

- Polynomial: 0xA001 (reversed 0x8005)
- Initial: 0xFFFF
- Byte order: CRC_Lo trước, CRC_Hi sau

### ModbusRtuSlave (CF_01_Simulation)

- **File**: `CF_01_Simulation/ModbusRtuSlave.cs`
- **Class**: `ModbusRtuSlave : IDisposable`
- Hỗ trợ: FC03 (Read Holding Registers), FC06 (Write Single Register)
- Xử lý `TimeoutException` nội bộ — `catch (TimeoutException) { continue; }` trong vòng lặp listen
- Thread-safe holding registers bằng `lock`
- `ListenAsync(CancellationToken)` chạy trong background task
- `WriteRegister(ushort address, ushort value)` ghi giá trị từ main loop
- Mảng holding registers mặc định 100 phần tử

### ModbusTemperatureSensor (CF_01)

- **File**: `CF_01/Services/ModbusTemperatureSensor.cs`
- **Class**: `ModbusTemperatureSensor : ITemperatureSensor`
- Gửi FC03 request, đọc response theo 2 bước (header 3 bytes → data + CRC)
- Verify CRC response
- Xử lý lỗi: timeout → trả `_lastGoodTemp`, quá 10 lỗi → `Reconnect()`
- `ReadTemperature()` trả °C = register_value / scaleFactor
- `ReadExact()` đảm bảo đọc đủ N bytes từ serial port

---

## CF_01_Simulation — Chi tiết

### Luồng hoạt động

1. Load config từ `sim_config.json`
2. Tạo CSV mẫu nếu chưa có (7200 dòng ≈ 2 giờ sấy)
3. Load nhiệt độ từ CSV
4. Mở SerialPort (COM2)
5. Khởi tạo `ModbusRtuSlave` → listen trong background task
6. Main loop: đọc CSV tuần tự → `WriteRegister()` → delay → lặp
7. Khi hết CSV: cho chọn [R] chạy lại hoặc [Q] thoát

### CSV Format

```
# Comment lines start with #
25.0
25.3
30.5
...
```

- Mỗi dòng = 1 giá trị nhiệt độ (°C)
- Hỗ trợ dấu `,` và `.` cho số thập phân
- Mặc định 7200 dòng (1 dòng/giây = 2 giờ)

### 4 giai đoạn sấy mẫu

| Giai đoạn | Thời gian | Nhiệt độ | Dòng CSV |
|---|---|---|---|
| Khởi động | 0–5 phút | 25 → 75°C | 300 |
| Sấy chính | 5–90 phút | 75–105°C dao động | 5100 |
| Giảm nhiệt | 90–110 phút | 90 → 50°C | 1200 |
| Nguội | 110–120 phút | 50 → 30°C | 600 |

### SimConfig (`sim_config.json`)

```json
{
  "ComPort": "COM2",
  "BaudRate": 9600,
  "SlaveId": 1,
  "RegisterAddress": 0,
  "ScaleFactor": 10.0,
  "CsvFilePath": "temperature_data.csv",
  "UpdateIntervalMs": 1000
}
```

---

## CF_01 App — Chi tiết

### Entry Point

- `Program.cs` → `BuildAvaloniaApp()` → `App` → `MainWindow` + `MainWindowViewModel`
- `Fire.axaml.cs` tạo `FireViewModel` trực tiếp (không qua DI)

### AppConfig (`config.json`)

```json
{
  "StartThreshold": 30.0,
  "EndThreshold": 60.0,
  "StartDelaySeconds": 5.0,
  "EndDelaySeconds": 10.0,
  "SensorPollIntervalSeconds": 0.1,
  "SamplesPerStoredReading": 10,
  "AverageIntervalSeconds": 60,
  "ModbusPortName": "COM3",
  "ModbusBaudRate": 9600,
  "ModbusSlaveId": 1,
  "ModbusRegisterAddress": 0,
  "ModbusScaleFactor": 10.0,
  "DisplayLowThreshold": 70.0,
  "DisplayHighThreshold": 100.0,
  "SessionDataFolder": "SessionData"
}
```

### ITemperatureSensor

```csharp
public interface ITemperatureSensor : IDisposable
{
    double ReadTemperature();   // Đọc °C, gọi mỗi 0.1s
    bool IsConnected { get; }
    string SourceName { get; }
}
```

Hiện tại chỉ có 1 implementation: `ModbusTemperatureSensor`.

---

### FireViewModel — Logic đo nhiệt độ

**File**: `CF_01/ViewModels/FireViewModel.cs`

#### Chu kỳ đọc

- `DispatcherTimer` interval = 0.1s (cấu hình `SensorPollIntervalSeconds`)
- Mỗi tick: đọc `ReadTemperature()` → hiển thị realtime
- Mỗi 10 ticks (1 giây): tính trung bình → tạo 1 mẫu lưu trữ (`ProcessStoredSample`)

#### State Machine phiên đo

```
Idle → PendingStart → Active → PendingEnd → Idle
```

| Trạng thái | Điều kiện chuyển |
|---|---|
| Idle → PendingStart | Nhiệt độ ≥ StartThreshold (30°C) |
| PendingStart → Active | Duy trì ≥ StartDelaySeconds (5s) |
| PendingStart → Idle | Nhiệt độ < StartThreshold |
| Active → PendingEnd | Nhiệt độ < EndThreshold (60°C) |
| PendingEnd → Idle (EndSession) | Duy trì < EndDelaySeconds (10s) |
| PendingEnd → Active | Nhiệt độ ≥ EndThreshold |

#### Phiên đo (Session)

- Khi Active: tích lũy mẫu, tính TB, Min, Max
- Interval averaging: cứ mỗi `AverageIntervalSeconds` (60s) → tạo 1 `IntervalAverageEntry` hiển thị trên panel phải
- Khi EndSession: lưu `SessionData` ra JSON qua `SessionStorage`

#### Hiệu ứng lửa text

- 5 layer `TextBlock` chồng nhau: 2 shadow đen + outer glow + inner glow + text gradient
- Màu gradient thay đổi theo nhiệt độ (20–115°C)
- Vùng xanh (green zone): nhiệt độ trong khoảng [LowThreshold, HighThreshold]
- Ngoài vùng xanh: gradient từ trắng → vàng → cam → trắng sáng

#### Hiệu ứng lửa GIF

- 3 GIF: `FireL.gif` (thấp), `Fire.gif` (bình thường), `FireH.gif` (cao)
- Hiển thị theo ngưỡng: < 30°C (tắt), < LowThreshold (thấp), < HighThreshold (bình thường), ≥ HighThreshold (cao)

### ThermometerViewModel

**File**: `CF_01/ViewModels/ThermometerViewModel.cs`

- Hiển thị nhiệt kế đứng với cột thủy ngân
- Dải: 30–120°C, chiều cao cột 590px
- Màu thủy ngân thay đổi theo nhiệt độ (xanh dương → xanh lá → vàng → cam → đỏ)
- `RangeSlider` cho phép chỉnh `LowerTemperature` / `UpperTemperature` → sync ngược về `FireViewModel`
- Bo tròn đầu cột thủy ngân khi > 90%

### Views

#### MainWindow.axaml

- Grid 2 cột: `Fire` (stretch) + Panel trung bình chu kỳ (160px)
- Panel phải hiển thị `IntervalAverages` (ItemsControl + ScrollViewer)

#### Fire.axaml

- Grid 2 cột: Fire area + Thermometer
- Fire area: Viewbox chứa 3 GIF + 5-layer text effect
- Overlay trái-trên: đồng hồ, trạng thái, tổng phiên
- Overlay trái-dưới: đồng hồ thực (HH:mm:ss)
- Overlay phải: phiên hiện tại (TB, thời gian, max, min, chu kỳ) + phiên trước

#### Fire.axaml.cs

- Tạo `FireViewModel` trong code-behind
- Sync `FireViewModel.Temperature` → `ThermometerControl.SetTemperature()`
- Sync `ThermometerViewModel.LowerTemperature/UpperTemperature` → `FireViewModel.LowTempThreshold/HighTempThreshold`

#### Thermometer.axaml

- Viewbox chứa Canvas (nhiệt kế) + RangeSlider (dọc) + Labels
- Tick marks vẽ trong code-behind (`DrawTickMarks`)
- Mercury column + bulb với hiệu ứng gradient và glow

### SessionData & Storage

#### SessionData (Model)

```csharp
record TemperatureSample(DateTime Timestamp, double Temperature, double Min, double Max);
record IntervalAverage(int Index, DateTime StartTime, DateTime EndTime,
                       double Average, double Min, double Max, int SampleCount);

class SessionData {
    DateTime StartTime, EndTime;
    double OverallAverage, MaxTemperature, MinTemperature, DurationSeconds;
    int TotalSamples;
    List<TemperatureSample> Samples;
    List<IntervalAverage> IntervalAverages;
}
```

#### SessionStorage

- Lưu JSON: `SessionData/{session_yyyyMMdd_HHmmss.json}`
- `SaveSession()`, `LoadSession()`, `ListSessions()` (newest first)

---

## Luồng dữ liệu End-to-End

```
┌─────────────────────────────────────────────────────┐
│                CF_01_Simulation (Console)            │
│                                                     │
│  CSV File ──→ temperatures[] ──→ WriteRegister()    │
│                                    ↓                │
│  ModbusRtuSlave ←── holdingRegisters[0] = regValue  │
│       ↕ SerialPort (COM2)                           │
└──────────────────────┬──────────────────────────────┘
                       │ Virtual COM Pair (com0com)
                       │ COM2 ↔ COM3
┌──────────────────────┴──────────────────────────────┐
│                CF_01 (Avalonia Desktop)              │
│                                                     │
│  ModbusTemperatureSensor ←── SerialPort (COM3)      │
│       ↓ ReadTemperature() mỗi 0.1s                 │
│  FireViewModel                                      │
│       ↓ Temperature (realtime)                      │
│       ↓ ProcessStoredSample (mỗi 1s)               │
│       ↓ IntervalAverage (mỗi 60s)                  │
│       ↓ SessionData → SessionStorage (JSON)         │
│                                                     │
│  UI: Fire GIF + Text Effect + Thermometer + Stats   │
└─────────────────────────────────────────────────────┘
```

---

## Giá trị nhiệt độ qua Modbus

- Register value = nhiệt độ × ScaleFactor (mặc định 10)
- VD: 25.5°C → register = 255, 100.0°C → register = 1000
- Kiểu: `ushort` (0–65535), clamp từ `double`

---


## Git

- Repository: `https://github.com/XuTa93/CF_01`
- Branch: `master`
- Local: `D:\CSharp\IoT_Temp\IOTCF\CF_01`
