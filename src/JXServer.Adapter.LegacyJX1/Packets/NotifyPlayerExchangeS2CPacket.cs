using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_notifyplayerexchange</c> (<c>0x35</c>, <c>25</c> bytes): notifies a client to transfer to another GameServer
/// node using plain little-endian <c>Port</c> at <c>+0x15</c> and a 16-byte <c>HandoffGuid</c> (`FR-009`).
/// </summary>
public readonly record struct NotifyPlayerExchangeS2CPacket(
    Guid HandoffGuid,
    uint IpAddressLittleEndian,
    ushort Port)
{
    public const int PayloadSize = 25;

    public void WritePayload(Span<byte> destination)
    {
        destination.Clear();
        destination[0] = ProtocolOpcodes.s2c_notifyplayerexchange;
        HandoffGuid.TryWriteBytes(destination.Slice(1, 16));
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(17, 4), IpAddressLittleEndian);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(21, 2), Port);
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out NotifyPlayerExchangeS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_notifyplayerexchange)
        {
            packet = default;
            return false;
        }

        Guid guid = new(payload.Slice(1, 16));
        uint ip = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(17, 4));
        ushort port = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(21, 2));
        packet = new NotifyPlayerExchangeS2CPacket(guid, ip, port);
        return true;
    }
}
