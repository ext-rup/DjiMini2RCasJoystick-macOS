using System.Diagnostics;
using System.IO.Ports;
using DjiRcJoystick.Protocol;

namespace DjiRcJoystick.App;

internal sealed class SerialConnection : IDisposable
{
    private readonly SerialPort port;

    public SerialConnection(string portName)
    {
        var serialPort = new SerialPort(portName, 115_200, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = false,
            RtsEnable = false,
            ReadTimeout = 20,
            WriteTimeout = 1_000,
        };
        try
        {
            serialPort.Open();
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
            port = serialPort;
        }
        catch
        {
            serialPort.Dispose();
            throw;
        }
    }

    public void Write(byte[] bytes)
    {
        port.Write(bytes, 0, bytes.Length);
    }

    public byte[]? ReadPacket(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ReadByte() != 0x55) continue;

            var header = new byte[4];
            header[0] = 0x55;
            if (!ReadExact(header, 1, 3, timeout - stopwatch.Elapsed, cancellationToken)) return null;

            var packetLength = ((header[2] << 8) | header[1]) & 0x03ff;
            if (packetLength < 13 || (header[2] & 0xfc) != 0x04 ||
                header[3] != DjiProtocol.HeaderChecksum(0x77, header.AsSpan(0, 3)))
            {
                continue;
            }

            var packet = new byte[packetLength];
            header.CopyTo(packet, 0);
            if (!ReadExact(packet, 4, packetLength - 4, timeout - stopwatch.Elapsed, cancellationToken)) return null;
            if (DjiProtocol.IsValid(packet)) return packet;
        }

        return null;
    }

    private int? ReadByte()
    {
        try
        {
            return port.ReadByte();
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private bool ReadExact(byte[] destination, int offset, int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (count > 0 && stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = ReadByte();
            if (value is null) continue;
            destination[offset++] = (byte)value.Value;
            count--;
        }

        return count == 0;
    }

    public void Dispose()
    {
        port.Dispose();
    }
}
