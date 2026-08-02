using DjiRcJoystick.Protocol;
using System.Diagnostics;

namespace DjiRcJoystick.App;

internal sealed class ControllerSession : IDisposable
{
    private readonly SerialConnection serial;
    private ushort sequence = 0x34eb;
    private int incompletePolls;

    public ControllerSession(string portName)
    {
        serial = new SerialConnection(portName);
        try
        {
            Send(0x24, [0x01]);
        }
        catch
        {
            serial.Dispose();
            throw;
        }
    }

    public ControllerState State { get; } = new();

    public bool Poll(CancellationToken cancellationToken)
    {
        Send(0x01);
        Send(0x27);

        var receivedSticks = false;
        var receivedButtons = false;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromMilliseconds(600) && !(receivedSticks && receivedButtons))
        {
            var remaining = TimeSpan.FromMilliseconds(600) - stopwatch.Elapsed;
            var packet = serial.ReadPacket(
                remaining < TimeSpan.FromMilliseconds(150) ? remaining : TimeSpan.FromMilliseconds(150),
                cancellationToken);
            if (packet is null || !State.Apply(packet)) continue;
            receivedSticks |= packet.Length == 38 && packet[10] == 0x01;
            receivedButtons |= packet.Length == 58 && packet[10] == 0x27;
        }

        if (receivedSticks && receivedButtons)
        {
            incompletePolls = 0;
            return true;
        }

        incompletePolls++;
        if (incompletePolls >= 3)
        {
            throw new IOException("DJI RC stopped returning complete control reports");
        }
        return false;
    }

    private void Send(byte commandId, byte[]? payload = null)
    {
        serial.Write(DjiProtocol.Packet(commandId, sequence, payload));
        sequence++;
    }

    public void Dispose()
    {
        serial.Dispose();
    }
}
