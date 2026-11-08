using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 60-byte <c>s2c_notifyplayerlogin</c> (<c>0x34</c>) packet containing the single-use 16-byte <c>GUID</c> at <c>+0x05</c>
/// and the GameServer IPv4 address (<c>+0x36</c>) and TCP port (<c>+0x3A</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct NotifyPlayerLoginS2CPacket
{
    public const int Size = 60;

    public byte ProtocolType;
    public uint Identity;
    public fixed byte Guid[16];
    public fixed byte szRoleName[32];
    public byte Permit;
    public uint IpAddressNetworkOrder;
    public ushort Port;

    public static NotifyPlayerLoginS2CPacket Create(
        Guid ticketGuid,
        string roleNameUnicode,
        bool permitted,
        string gameServerHost,
        ushort gameServerPort,
        uint identity = 0)
    {
        NotifyPlayerLoginS2CPacket pkt = default;
        pkt.ProtocolType = ProtocolOpcodes.s2c_notifyplayerlogin;
        pkt.Identity = identity;
        pkt.Permit = permitted ? (byte)1 : (byte)0;
        pkt.Port = gameServerPort;

        ticketGuid.TryWriteBytes(new Span<byte>(pkt.Guid, 16));
        Tcvn3Encoding.WriteFixedString(roleNameUnicode, new Span<byte>(pkt.szRoleName, 32));

        IPAddress ip = IPAddress.TryParse(gameServerHost, out IPAddress? parsed) &&
                       parsed.AddressFamily == AddressFamily.InterNetwork
            ? parsed
            : IPAddress.Loopback;

        Span<byte> ipBytes = stackalloc byte[4];
        ip.TryWriteBytes(ipBytes, out _);
        pkt.IpAddressNetworkOrder = BinaryPrimitives.ReadUInt32LittleEndian(ipBytes);
        return pkt;
    }

    public readonly Guid GetTicketGuid()
    {
        fixed (byte* ptr = Guid)
        {
            return new Guid(new ReadOnlySpan<byte>(ptr, 16));
        }
    }

    public readonly string GetRoleName()
    {
        fixed (byte* ptr = szRoleName)
        {
            return Tcvn3Encoding.DecodeNullTerminated(new ReadOnlySpan<byte>(ptr, 32));
        }
    }

    public readonly IPAddress GetIpAddress()
    {
        Span<byte> ipBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(ipBytes, IpAddressNetworkOrder);
        return new IPAddress(ipBytes);
    }
}
