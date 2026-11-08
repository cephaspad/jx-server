using System.Buffers.Binary;
using System.Runtime.InteropServices;
using JXServer.Contracts.Characters;
using JXServer.Primitives.Characters;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 483-byte <c>s2c_synccurplayerskill</c> (<c>0x45</c>) packet synchronizing up to 80 skill slots (<c>80 * 6B = 480B</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct SyncCurPlayerSkillS2CPacket
{
    public const int Size = 483;
    public const ushort ExpectedLengthField = 482;

    public byte ProtocolType;
    public ushort Length;
    public fixed byte SkillSlotsRaw[480];

    public static SyncCurPlayerSkillS2CPacket Create(IReadOnlyList<CharacterSkillDto> skills)
    {
        SyncCurPlayerSkillS2CPacket pkt = default;
        pkt.ProtocolType = ProtocolOpcodes.s2c_synccurplayerskill;
        pkt.Length = ExpectedLengthField;

        Span<byte> slotsSpan = new(pkt.SkillSlotsRaw, 480);
        slotsSpan.Clear();

        if (skills is not null)
        {
            int count = Math.Min(skills.Count, CharacterConsts.MaxSkillSlots);
            for (int i = 0; i < count; i++)
            {
                CharacterSkillDto s = skills[i];
                Span<byte> slot = slotsSpan.Slice(i * 6, 6);
                BinaryPrimitives.WriteUInt16LittleEndian(slot[..2], s.SkillId);
                slot[2] = s.BaseLevel;
                slot[3] = s.BonusLevel;
                BinaryPrimitives.WriteUInt16LittleEndian(slot.Slice(4, 2), s.ExpPercent);
            }
        }

        return pkt;
    }

    public readonly CharacterSkillDto GetSkillSlot(int slotIndex)
    {
        if ((uint)slotIndex >= CharacterConsts.MaxSkillSlots)
        {
            return new CharacterSkillDto(0, 0, 0, 0);
        }

        fixed (byte* ptr = SkillSlotsRaw)
        {
            ReadOnlySpan<byte> slot = new(ptr + (slotIndex * 6), 6);
            return new CharacterSkillDto(
                BinaryPrimitives.ReadUInt16LittleEndian(slot[..2]),
                slot[2],
                slot[3],
                BinaryPrimitives.ReadUInt16LittleEndian(slot.Slice(4, 2)));
        }
    }
}
