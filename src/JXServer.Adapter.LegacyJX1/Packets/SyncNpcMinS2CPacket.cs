using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_syncnpcmin</c> (<c>0x4D</c>, <c>18</c> bytes): compact neighbor position/posture sync (`FR-003`).
/// </summary>
public readonly record struct SyncNpcMinS2CPacket(
    uint NpcId,
    int MpsX,
    int MpsY,
    byte Doing,
    byte LifePer,
    byte State,
    byte Camp,
    byte Dir)
{
    public const int PayloadSize = 18;

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_syncnpcmin;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), NpcId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), MpsX);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(9, 4), MpsY);
        destination[13] = Doing;
        destination[14] = LifePer;
        destination[15] = State;
        destination[16] = Camp;
        destination[17] = Dir;
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out SyncNpcMinS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_syncnpcmin)
        {
            packet = default;
            return false;
        }

        packet = new SyncNpcMinS2CPacket(
            NpcId: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4)),
            MpsX: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4)),
            MpsY: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(9, 4)),
            Doing: payload[13],
            LifePer: payload[14],
            State: payload[15],
            Camp: payload[16],
            Dir: payload[17]);
        return true;
    }
}
