using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_castskilldirectly</c> (<c>0x85</c>, <c>25</c> bytes): broadcasts an instant/direct skill execution to the <c>3x3</c> region AOI.
/// </summary>
public readonly record struct CastSkillDirectlyS2CPacket(
    int TargetMpsX,
    uint SkillId,
    int TargetMpsYOrNpcId,
    uint CasterNpcId,
    uint SkillLevel,
    uint Reserved = 0)
{
    public const int PayloadSize = 25;

    public static bool TryParse(ReadOnlySpan<byte> payload, out CastSkillDirectlyS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_castskilldirectly)
        {
            packet = default;
            return false;
        }

        packet = new CastSkillDirectlyS2CPacket(
            TargetMpsX: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)),
            SkillId: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(5, 4)),
            TargetMpsYOrNpcId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(9, 4)),
            CasterNpcId: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(13, 4)),
            SkillLevel: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(17, 4)),
            Reserved: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(21, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_castskilldirectly;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), TargetMpsX);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(5, 4), SkillId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(9, 4), TargetMpsYOrNpcId);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(13, 4), CasterNpcId);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(17, 4), SkillLevel);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(21, 4), Reserved);
    }
}
