using Xm4Control.Core.Protocol;

namespace Xm4Control.Tests;

public class FrameTests
{
    [Fact]
    public void Encode_InitRequest()
    {
        var bytes = new Frame(MessageType.Command1, 0, [0x00, 0x00]).Encode();

        Assert.Equal("3E0C00000000020000" + "0E" + "3C", Convert.ToHexString(bytes));
    }

    [Fact]
    public void Encode_EscapesDelimiters()
    {
        var bytes = new Frame(MessageType.Command1, 0, [0x3C, 0x3D, 0x3E]).Encode();

        // 0x3C -> 3D 2C, 0x3D -> 3D 2D, 0x3E -> 3D 2E ; checksum 0x0C + 3 + 0xB7 = 0xC6
        Assert.Equal("3E0C00000000033D2C3D2D3D2EC63C", Convert.ToHexString(bytes));
    }

    [Theory]
    [InlineData(MessageType.Command1, 1, "6702010202010000")]
    [InlineData(MessageType.Command2, 0, "47010101")]
    [InlineData(MessageType.Ack, 1, "")]
    [InlineData(MessageType.Command1, 0, "3C3D3E00FF")]
    public void Reader_RoundTrip(MessageType type, byte sequence, string payloadHex)
    {
        var frame = new Frame(type, sequence, Convert.FromHexString(payloadHex));

        var decoded = Assert.Single(new FrameReader().Feed(frame.Encode()));

        Assert.Equal(type, decoded.Type);
        Assert.Equal(sequence, decoded.Sequence);
        Assert.Equal(payloadHex, Convert.ToHexString(decoded.Payload));
    }

    [Fact]
    public void Reader_HandlesFramesSplitAcrossReads()
    {
        var a = new Frame(MessageType.Ack, 1, []).Encode();
        var b = new Frame(MessageType.Command1, 1, [0x11, 0x00, 0x3C, 0x00]).Encode();
        var stream = a.Concat(b).ToArray();
        var reader = new FrameReader();

        var first = reader.Feed(stream.AsSpan(0, 5));
        var rest = reader.Feed(stream.AsSpan(5));

        Assert.Empty(first);
        Assert.Equal([MessageType.Ack, MessageType.Command1], rest.Select(f => f.Type));
        Assert.Equal("11003C00", Convert.ToHexString(rest[1].Payload));
    }

    [Fact]
    public void Reader_RejectsBadChecksum()
    {
        var bytes = new Frame(MessageType.Command1, 0, [0x66, 0x02]).Encode();
        bytes[^2] ^= 0xFF;
        var reader = new FrameReader();

        Assert.Empty(reader.Feed(bytes));
        Assert.Equal(1, reader.InvalidFrames);
    }
}
