using DjiRcJoystick.Protocol;
using Xunit;

namespace DjiRcJoystick.Protocol.Tests;

public sealed class DjiProtocolTests
{
    [Fact]
    public void StickScalingMatchesObservedRange()
    {
        Assert.Equal(0, ControllerState.StickValue(0x6c, 0x01));
        Assert.Equal(16_384, ControllerState.StickValue(0x00, 0x04));
        Assert.Equal(32_767, ControllerState.StickValue(0x94, 0x06));
    }

    [Fact]
    public void StickPacketMapsAllAxes()
    {
        var packet = Response(38, 0x01, bytes =>
        {
            bytes[13] = 0x00; bytes[14] = 0x04;
            bytes[16] = 0x6c; bytes[17] = 0x01;
            bytes[19] = 0x94; bytes[20] = 0x06;
            bytes[22] = 0x00; bytes[23] = 0x04;
        });

        var state = new ControllerState();
        Assert.True(state.Apply(packet));
        Assert.Equal(16_384, state.RightHorizontal);
        Assert.Equal(0, state.RightVertical);
        Assert.Equal(32_767, state.LeftVertical);
        Assert.Equal(16_384, state.LeftHorizontal);
    }

    [Fact]
    public void ButtonPacketMapsButtonsAndSwitch()
    {
        var packet = Response(58, 0x27, bytes =>
        {
            bytes[28] = 0x10;
            bytes[29] = 0x66;
        });

        var state = new ControllerState();
        Assert.True(state.Apply(packet));
        Assert.Equal(0b1101, state.Buttons);
        Assert.Equal(0, state.CenterSwitch);
    }

    [Fact]
    public void DumlPacketHasExpectedLayoutAndChecksums()
    {
        var packet = DjiProtocol.Packet(0x24, 0x34eb, [0x01]);
        Assert.Equal(
            new byte[] { 0x55, 0x0e, 0x04, 0x66, 0x0a, 0x06, 0xeb, 0x34, 0x40, 0x06, 0x24, 0x01, 0xd9, 0xec },
            packet);
        Assert.True(DjiProtocol.IsValid(packet));
    }

    [Fact]
    public void CorruptAndUnexpectedPacketsAreRejected()
    {
        var corrupt = Response(38, 0x01, _ => { });
        corrupt[13] ^= 0xff;

        var state = new ControllerState();
        Assert.False(state.Apply(corrupt));
        Assert.False(state.Apply(Response(38, 0x27, _ => { })));
    }

    private static byte[] Response(int length, byte commandId, Action<byte[]> mutate)
    {
        var packet = DjiProtocol.Packet(commandId, 0x34eb, new byte[length - 13]);
        mutate(packet);
        var checksum = DjiProtocol.Checksum16(packet.AsSpan(0, packet.Length - 2));
        packet[^2] = (byte)(checksum & 0xff);
        packet[^1] = (byte)(checksum >> 8);
        return packet;
    }
}
