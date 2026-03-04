using System.IO.Ports;

namespace CF_01_Simulation;

/// <summary>
/// Modbus RTU Slave tự viết — hỗ trợ FC03 (Read Holding Registers) và FC06 (Write Single Register).
/// Xử lý TimeoutException nội bộ, không để ngoại lệ thoát ra ngoài.
/// </summary>
internal sealed class ModbusRtuSlave : IDisposable
{
    private readonly SerialPort _port;
    private readonly byte _slaveId;
    private readonly ushort[] _holdingRegisters;
    private readonly object _lock = new();

    public ModbusRtuSlave(SerialPort port, byte slaveId, int registerCount = 100)
    {
        _port = port;
        _slaveId = slaveId;
        _holdingRegisters = new ushort[registerCount];
    }

    /// <summary>Ghi giá trị vào holding register (thread-safe).</summary>
    public void WriteRegister(ushort address, ushort value)
    {
        lock (_lock)
        {
            if (address < _holdingRegisters.Length)
                _holdingRegisters[address] = value;
        }
    }

    /// <summary>Lắng nghe request từ Master, tự xử lý timeout.</summary>
    public async Task ListenAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[256];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Đọc byte đầu tiên (slave address)
                int bytesRead;
                try
                {
                    bytesRead = _port.Read(buffer, 0, 1);
                }
                catch (TimeoutException)
                {
                    continue;
                }

                if (bytesRead == 0) continue;

                // Không phải cho slave này → bỏ qua
                if (buffer[0] != _slaveId)
                {
                    _port.DiscardInBuffer();
                    continue;
                }

                // Đọc function code
                try
                {
                    ReadExact(buffer, 1, 1);
                }
                catch (TimeoutException)
                {
                    continue;
                }

                byte functionCode = buffer[1];

                switch (functionCode)
                {
                    case 0x03:
                        HandleReadHoldingRegisters(buffer);
                        break;
                    case 0x06:
                        HandleWriteSingleRegister(buffer);
                        break;
                    default:
                        SendExceptionResponse(functionCode, 0x01);
                        _port.DiscardInBuffer();
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (TimeoutException)
            {
                // Frame không hoàn chỉnh, bỏ qua
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Modbus Slave] Lỗi: {ex.Message}");
                try { await Task.Delay(50, cancellationToken); } catch { break; }
            }
        }
    }

    private void HandleReadHoldingRegisters(byte[] buffer)
    {
        // Đọc thêm: StartAddr(2) + Quantity(2) + CRC(2) = 6 bytes
        ReadExact(buffer, 2, 6);

        if (!VerifyCrc(buffer, 8))
        {
            _port.DiscardInBuffer();
            return;
        }

        ushort startAddr = (ushort)((buffer[2] << 8) | buffer[3]);
        ushort quantity = (ushort)((buffer[4] << 8) | buffer[5]);

        if (quantity == 0 || quantity > 125)
        {
            SendExceptionResponse(0x03, 0x03);
            return;
        }

        lock (_lock)
        {
            if (startAddr + quantity > _holdingRegisters.Length)
            {
                SendExceptionResponse(0x03, 0x02);
                return;
            }

            byte byteCount = (byte)(quantity * 2);
            var response = new byte[3 + byteCount + 2];
            response[0] = _slaveId;
            response[1] = 0x03;
            response[2] = byteCount;

            for (int i = 0; i < quantity; i++)
            {
                ushort val = _holdingRegisters[startAddr + i];
                response[3 + i * 2] = (byte)(val >> 8);
                response[3 + i * 2 + 1] = (byte)(val & 0xFF);
            }

            AppendCrc(response, response.Length - 2);
            _port.Write(response, 0, response.Length);
        }
    }

    private void HandleWriteSingleRegister(byte[] buffer)
    {
        // Đọc thêm: Addr(2) + Value(2) + CRC(2) = 6 bytes
        ReadExact(buffer, 2, 6);

        if (!VerifyCrc(buffer, 8))
        {
            _port.DiscardInBuffer();
            return;
        }

        ushort addr = (ushort)((buffer[2] << 8) | buffer[3]);
        ushort value = (ushort)((buffer[4] << 8) | buffer[5]);

        lock (_lock)
        {
            if (addr >= _holdingRegisters.Length)
            {
                SendExceptionResponse(0x06, 0x02);
                return;
            }

            _holdingRegisters[addr] = value;
        }

        // Echo request as response (chuẩn Modbus FC06)
        _port.Write(buffer, 0, 8);
    }

    private void ReadExact(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = _port.Read(buffer, offset + totalRead, count - totalRead);
            totalRead += read;
        }
    }

    private void SendExceptionResponse(byte functionCode, byte exceptionCode)
    {
        var response = new byte[5];
        response[0] = _slaveId;
        response[1] = (byte)(functionCode | 0x80);
        response[2] = exceptionCode;
        AppendCrc(response, 3);
        _port.Write(response, 0, response.Length);
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

    public void Dispose()
    {
        // Port thuộc sở hữu bên ngoài, không dispose ở đây
    }
}
