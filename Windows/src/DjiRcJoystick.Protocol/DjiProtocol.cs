namespace DjiRcJoystick.Protocol;

public static class DjiProtocol
{
    public static byte[] Packet(
        byte commandId,
        ushort sequence,
        byte[]? payload = null,
        byte source = 0x0a,
        byte target = 0x06,
        byte commandType = 0x40,
        byte commandSet = 0x06)
    {
        payload ??= [];
        var length = 13 + payload.Length;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, 0x3ff);

        var result = new byte[length];
        result[0] = 0x55;
        result[1] = (byte)(length & 0xff);
        result[2] = (byte)((length >> 8) | 0x04);
        result[3] = HeaderChecksum(0x77, result.AsSpan(0, 3));
        result[4] = source;
        result[5] = target;
        result[6] = (byte)(sequence & 0xff);
        result[7] = (byte)(sequence >> 8);
        result[8] = commandType;
        result[9] = commandSet;
        result[10] = commandId;
        payload.CopyTo(result, 11);

        var checksum = Checksum16(result.AsSpan(0, length - 2));
        result[^2] = (byte)(checksum & 0xff);
        result[^1] = (byte)(checksum >> 8);
        return result;
    }

    public static ushort Checksum16(ReadOnlySpan<byte> bytes, ushort seed = 0x3692)
    {
        var checksum = seed;
        foreach (var value in bytes)
        {
            checksum ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                checksum = (ushort)((checksum >> 1) ^ ((checksum & 1) == 1 ? 0x8408 : 0));
            }
        }

        return checksum;
    }

    public static byte HeaderChecksum(byte seed, ReadOnlySpan<byte> bytes)
    {
        var checksum = seed;
        foreach (var value in bytes)
        {
            checksum ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                checksum = (byte)((checksum >> 1) ^ ((checksum & 1) == 1 ? 0x8c : 0));
            }
        }

        return checksum;
    }

    public static bool IsValid(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 13 || packet[0] != 0x55)
        {
            return false;
        }

        var declaredLength = ((packet[2] << 8) | packet[1]) & 0x03ff;
        if (declaredLength != packet.Length || (packet[2] & 0xfc) != 0x04)
        {
            return false;
        }

        if (packet[3] != HeaderChecksum(0x77, packet[..3]))
        {
            return false;
        }

        var expected = (ushort)(packet[^2] | (packet[^1] << 8));
        return expected == Checksum16(packet[..^2]);
    }
}
