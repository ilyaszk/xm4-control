using System.Buffers.Binary;

namespace Xm4Control.Core.Protocol;

public enum MessageType : byte
{
    Ack = 0x01,
    Command1 = 0x0C,
    Command2 = 0x0E,
}

/// <summary>
/// Trame du protocole Sony "MDR" v1 :
/// 0x3E | type | séquence | longueur (int32 big endian) | payload | checksum | 0x3C.
/// Tout ce qui est entre les deux délimiteurs est échappé : 0x3C, 0x3D et 0x3E
/// deviennent 0x3D suivi de l'octet masqué par 0xEF.
/// Le checksum est la somme (modulo 256) des octets de type à la fin du payload.
/// </summary>
public sealed record Frame(MessageType Type, byte Sequence, byte[] Payload)
{
    public const byte Start = 0x3E;
    public const byte End = 0x3C;
    public const byte Escape = 0x3D;
    private const byte EscapeMask = 0xEF;

    public byte[] Encode()
    {
        var body = new byte[6 + Payload.Length + 1];
        body[0] = (byte)Type;
        body[1] = Sequence;
        BinaryPrimitives.WriteInt32BigEndian(body.AsSpan(2), Payload.Length);
        Payload.CopyTo(body, 6);
        body[^1] = Checksum(body.AsSpan(0, body.Length - 1));

        var output = new List<byte>(body.Length + 4) { Start };
        foreach (var b in body)
        {
            if (b is Start or End or Escape)
            {
                output.Add(Escape);
                output.Add((byte)(b & EscapeMask));
            }
            else
            {
                output.Add(b);
            }
        }
        output.Add(End);
        return output.ToArray();
    }

    /// <summary>Décode le contenu d'une trame, délimiteurs exclus.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> escaped, out Frame? frame)
    {
        frame = null;
        var raw = new List<byte>(escaped.Length);
        for (var i = 0; i < escaped.Length; i++)
        {
            if (escaped[i] == Escape)
            {
                if (++i >= escaped.Length) return false;
                raw.Add((byte)(escaped[i] | ~EscapeMask));
            }
            else
            {
                raw.Add(escaped[i]);
            }
        }

        // type + séquence + longueur + checksum
        if (raw.Count < 7) return false;

        var bytes = raw.ToArray();
        if (Checksum(bytes.AsSpan(0, bytes.Length - 1)) != bytes[^1]) return false;

        var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(2));
        if (length != bytes.Length - 7) return false;

        frame = new Frame((MessageType)bytes[0], bytes[1], bytes.AsSpan(6, length).ToArray());
        return true;
    }

    public static byte Checksum(ReadOnlySpan<byte> data)
    {
        byte sum = 0;
        foreach (var b in data) sum += b;
        return sum;
    }

    public override string ToString() => $"{Type} seq={Sequence} [{Convert.ToHexString(Payload)}]";
}

/// <summary>Découpe un flux d'octets RFCOMM en trames.</summary>
public sealed class FrameReader
{
    private readonly List<byte> _buffer = [];
    private bool _inFrame;

    /// <summary>Nombre de trames invalides (checksum ou longueur) ignorées.</summary>
    public int InvalidFrames { get; private set; }

    public List<Frame> Feed(ReadOnlySpan<byte> data)
    {
        var frames = new List<Frame>();
        foreach (var b in data)
        {
            if (b == Frame.Start)
            {
                _buffer.Clear();
                _inFrame = true;
            }
            else if (b == Frame.End && _inFrame)
            {
                if (Frame.TryDecode(_buffer.ToArray(), out var frame)) frames.Add(frame!);
                else InvalidFrames++;
                _buffer.Clear();
                _inFrame = false;
            }
            else if (_inFrame)
            {
                _buffer.Add(b);
            }
        }
        return frames;
    }
}
