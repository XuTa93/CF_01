using System;
using System.IO.Ports;
using CF_01.Models;

namespace CF_01.Services;

/// <summary>
/// Đọc nhiệt độ qua Modbus RTU Master tự viết (không dùng thư viện NModbus).
/// Gửi FC03 (Read Holding Registers) và parse response trực tiếp.
/// </summary>
public class ModbusTemperatureSensor : ITemperatureSensor
{
    private SerialPort? _port;
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly byte _slaveId;
    private readonly ushort _registerAddress;
    private readonly double _scaleFactor;
    private double _lastGoodTemp = 25.0;
    private int _errorCount;

    public bool IsConnected { get; private set; }
    public string SourceName => $"Modbus RTU: {_portName}";

    public ModbusTemperatureSensor(AppConfig config)
        : this(config.ModbusPortName, config.ModbusBaudRate,
               config.ModbusSlaveId, config.ModbusRegisterAddress,
               config.ModbusScaleFactor)
    {
    }

    public ModbusTemperatureSensor(string portName, int baudRate, byte slaveId,
                                   ushort registerAddress, double scaleFactor)
    {
        _portName = portName;
        _baudRate = baudRate;
        _slaveId = slaveId;
        _registerAddress = registerAddress;
        _scaleFactor = scaleFactor;
        Connect();
    }

    private void Connect()
    {
        try
        {
            _port = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            _port.Open();

            IsConnected = true;
            _errorCount = 0;
            Console.WriteLine($"[Modbus Master] Kết nối thành công: {_portName} (Baud: {_baudRate}, SlaveID: {_slaveId})");
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Console.WriteLine($"[Modbus Master] Lỗi kết nối {_portName}: {ex.Message}");
        }
    }

    public double ReadTemperature()
    {
        if (!IsConnected || _port == null || !_port.IsOpen)
        {
            _errorCount++;
            if (_errorCount % 100 == 0)
            {
                Console.WriteLine($"[Modbus Master] Thử kết nối lại {_portName}...");
                Reconnect();
            }
            return _lastGoodTemp;
        }

        try
        {
            ushort[] registers = ReadHoldingRegisters(_slaveId, _registerAddress, 1);
            double temp = registers[0] / _scaleFactor;
            _lastGoodTemp = temp;
            _errorCount = 0;
            return temp;
        }
        catch (TimeoutException)
        {
            _errorCount++;
            if (_errorCount <= 3 || _errorCount % 50 == 0)
                Console.WriteLine($"[Modbus Master] Timeout đọc register (lần {_errorCount})");
            return _lastGoodTemp;
        }
        catch (Exception ex)
        {
            _errorCount++;
            if (_errorCount <= 3 || _errorCount % 50 == 0)
                Console.WriteLine($"[Modbus Master] Lỗi đọc: {ex.Message} (lần {_errorCount})");

            if (_errorCount > 10)
            {
                Console.WriteLine("[Modbus Master] Quá nhiều lỗi, thử kết nối lại...");
                Reconnect();
            }
            return _lastGoodTemp;
        }
    }

    /// <summary>Gửi FC03 request và parse response.</summary>
    private ushort[] ReadHoldingRegisters(byte slaveId, ushort startAddress, ushort quantity)
    {
        // Request: [SlaveID] [03] [AddrHi] [AddrLo] [QtyHi] [QtyLo] [CRC_Lo] [CRC_Hi]
        var request = new byte[8];
        request[0] = slaveId;
        request[1] = 0x03;
        request[2] = (byte)(startAddress >> 8);
        request[3] = (byte)(startAddress & 0xFF);
        request[4] = (byte)(quantity >> 8);
        request[5] = (byte)(quantity & 0xFF);
        AppendCrc(request, 6);

        _port!.DiscardInBuffer();
        _port.Write(request, 0, request.Length);

        // Response: [SlaveID] [03] [ByteCount] [Data...] [CRC_Lo] [CRC_Hi]
        // Đọc 3 byte header trước để xác định kích thước
        var header = new byte[3];
        ReadExact(header, 0, 3);

        // Kiểm tra exception response
        if ((header[1] & 0x80) != 0)
        {
            // Đọc thêm exception code + CRC
            var errBuf = new byte[2];
            ReadExact(errBuf, 0, 2);
            throw new InvalidOperationException($"Modbus exception code: 0x{header[2]:X2}");
        }

        int dataLen = header[2]; // byte count
        var rest = new byte[dataLen + 2]; // data + CRC
        ReadExact(rest, 0, rest.Length);

        // Ghép lại để verify CRC
        var full = new byte[3 + rest.Length];
        Array.Copy(header, 0, full, 0, 3);
        Array.Copy(rest, 0, full, 3, rest.Length);

        if (!VerifyCrc(full, full.Length))
            throw new InvalidOperationException("CRC không hợp lệ");

        // Parse register values
        var registers = new ushort[quantity];
        for (int i = 0; i < quantity; i++)
        {
            registers[i] = (ushort)((full[3 + i * 2] << 8) | full[3 + i * 2 + 1]);
        }
        return registers;
    }

    private void ReadExact(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = _port!.Read(buffer, offset + totalRead, count - totalRead);
            totalRead += read;
        }
    }

    private void Reconnect()
    {
        try { _port?.Dispose(); } catch { }
        _port = null;
        IsConnected = false;
        Connect();
    }

    public void Dispose()
    {
        _port?.Dispose();
    }

    #region CRC-16/Modbus

    private static bool VerifyCrc(byte[] data, int length)
    {
        ushort calculated = CalculateCrc(data, 0, length - 2);
        ushort received = (ushort)(data[length - 2] | (data[length - 1] << 8));
        return calculated == received;
    }

    private static void AppendCrc(byte[] data, int dataLength)
    {
        ushort crc = CalculateCrc(data, 0, dataLength);
        data[dataLength] = (byte)(crc & 0xFF);
        data[dataLength + 1] = (byte)(crc >> 8);
    }

    private static ushort CalculateCrc(byte[] data, int offset, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                    crc = (ushort)((crc >> 1) ^ 0xA001);
                else
                    crc >>= 1;
            }
        }
        return crc;
    }

    #endregion
}
