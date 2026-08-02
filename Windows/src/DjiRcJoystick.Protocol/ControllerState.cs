namespace DjiRcJoystick.Protocol;

public sealed class ControllerState
{
    public ushort LeftHorizontal { get; private set; } = 16_384;
    public ushort LeftVertical { get; private set; } = 16_384;
    public ushort RightHorizontal { get; private set; } = 16_384;
    public ushort RightVertical { get; private set; } = 16_384;
    public byte Buttons { get; private set; }
    public sbyte CenterSwitch { get; private set; }

    public bool Apply(ReadOnlySpan<byte> packet)
    {
        if (!DjiProtocol.IsValid(packet) || packet[9] != 0x06)
        {
            return false;
        }

        switch (packet.Length, packet[10])
        {
            case (38, 0x01):
                RightHorizontal = StickValue(packet[13], packet[14]);
                RightVertical = StickValue(packet[16], packet[17]);
                LeftVertical = StickValue(packet[19], packet[20]);
                LeftHorizontal = StickValue(packet[22], packet[23]);
                return true;

            case (58, 0x27):
                var buttonBits = (ushort)((packet[28] << 8) | packet[29]);
                Buttons = 0;
                if ((buttonBits & 0x1060) == 0x1060) Buttons |= 1 << 0;
                if ((buttonBits & 0x1080) == 0x1080) Buttons |= 1 << 1;
                if ((buttonBits & 0x1004) == 0x1004) Buttons |= 1 << 2;
                if ((buttonBits & 0x1002) == 0x1002) Buttons |= 1 << 3;

                var switchBits = (ushort)((packet[27] << 8) | packet[28]);
                CenterSwitch = switchBits == 0 ? (sbyte)1 : (switchBits & 0x20) == 0x20 ? (sbyte)-1 : (sbyte)0;
                return true;

            default:
                return false;
        }
    }

    public static ushort StickValue(byte lowByte, byte highByte)
    {
        var raw = lowByte | (highByte << 8);
        var scaled = (raw - 364) * 4_096 / 165;
        return (ushort)Math.Clamp(scaled, 0, 32_767);
    }
}
