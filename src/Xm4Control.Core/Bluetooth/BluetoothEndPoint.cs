using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Xm4Control.Core.Bluetooth;

/// <summary>Adresse Winsock Bluetooth (SOCKADDR_BTH) pour ouvrir un socket RFCOMM.</summary>
internal sealed class BluetoothEndPoint(ulong address, Guid service) : EndPoint
{
    public const AddressFamily Family = (AddressFamily)32; // AF_BTH
    public const ProtocolType Rfcomm = (ProtocolType)3;    // BTHPROTO_RFCOMM
    private const int Size = 30;                            // sizeof(SOCKADDR_BTH), structure packée

    public ulong Address => address;
    public Guid Service => service;

    public override AddressFamily AddressFamily => Family;

    public override SocketAddress Serialize()
    {
        // famille (2 octets, déjà écrite) | adresse (8) | GUID du service (16) | port (4)
        // Le port reste à 0 : Windows retrouve le canal RFCOMM par une requête SDP sur le GUID.
        var socketAddress = new SocketAddress(Family, Size);
        var buffer = socketAddress.Buffer.Span;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer[2..], address);
        service.TryWriteBytes(buffer[10..]);
        return socketAddress;
    }

    public override EndPoint Create(SocketAddress socketAddress)
    {
        var buffer = socketAddress.Buffer.Span;
        return new BluetoothEndPoint(BinaryPrimitives.ReadUInt64LittleEndian(buffer[2..]), new Guid(buffer.Slice(10, 16)));
    }
}
