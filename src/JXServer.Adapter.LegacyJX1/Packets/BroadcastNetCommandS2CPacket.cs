using System;
using System.Buffers.Binary;
using JXServer.Primitives.Combat;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_broadcastnetcommand</c> (<c>0x56</c>, <c>22</c> bytes — <c>NPC_NETCOMMAND_SYNC</c>): broadcasts entity hurt recoil (<c>CmdKind = 9</c> <c>do_hurt</c>) to the <c>3x3</c> region AOI.
/// </summary>
public readonly record struct BroadcastNetCommandS2CPacket(
    uint NpcId,
    byte CmdKind,
    int Param1MpsX,
    int Param2MpsY,
    int Param3HurtFrames,
    int Param4 = 0)
{
    public const int PayloadSize = 22;

    public static BroadcastNetCommandS2CPacket CreateHurt(uint npcId, int mpsX, int mpsY, int hurtFrames) =>
        new(npcId, CombatConsts.BroadcastNetCmdHurt, mpsX, mpsY, hurtFrames, 0);

    public static bool TryParse(ReadOnlySpan<byte> payload, out BroadcastNetCommandS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_broadcastnetcommand)
        {
            packet = default;
            return false;
        }

        packet = new BroadcastNetCommandS2CPacket(
            NpcId: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4)),
            CmdKind: payload[5],
            Param1MpsX: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(6, 4)),
            Param2MpsY: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(10, 4)),
            Param3HurtFrames: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(14, 4)),
            Param4: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(18, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_broadcastnetcommand;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), NpcId);
        destination[5] = CmdKind;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(6, 4), Param1MpsX);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(10, 4), Param2MpsY);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(14, 4), Param3HurtFrames);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(18, 4), Param4);
    }
}
