using System.Buffers.Binary;
using JXServer.Adapter.LegacyJX1.Ciphers;
using JXServer.Contracts.Characters;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// Variable-length <c>s2c_roleserver_getrolelist_result</c> (<c>0x37</c>) packet (<c>10B TProcessData + nDataLen</c>)
/// with the <c>0x3042</c> second-layer 16-bit rolling XOR cipher applied to the payload at <c>+0x0A</c>.
/// </summary>
public readonly record struct RoleListResultS2CPacket(
    uint Identity,
    IReadOnlyList<CharacterSummaryDto> Characters)
{
    public const int HeaderSize = 10;
    public const int RoleEntrySize = 35;

    public byte[] ToPayloadBytes()
    {
        IReadOnlyList<CharacterSummaryDto> roles = Characters ?? [];
        byte roleCount = (byte)Math.Min(roles.Count, CharacterConsts.MaxRolesPerAccount);

        // Use 2-byte inner header (RoleCount, RefuseCount = 0) + 35B * RoleCount so both game.exe and JX-Swarm parse identically.
        int nDataLen = 2 + (roleCount * RoleEntrySize);
        byte[] packet = new byte[HeaderSize + nDataLen];

        packet[0] = ProtocolOpcodes.s2c_roleserver_getrolelist_result;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(1, 4), (uint)nDataLen);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(5, 4), Identity);
        packet[9] = 0;

        Span<byte> roleData = packet.AsSpan(HeaderSize, nDataLen);
        roleData[0] = roleCount;
        roleData[1] = 0;

        int offset = 2;
        for (int i = 0; i < roleCount; i++)
        {
            CharacterSummaryDto c = roles[i];
            Span<byte> entry = roleData.Slice(offset, RoleEntrySize);
            Tcvn3Encoding.WriteFixedString(c.RoleNameUnicode, entry[..32]);
            entry[32] = (byte)c.Gender;
            entry[33] = (byte)c.Series;
            entry[34] = c.Level;
            offset += RoleEntrySize;
        }

        SecondLayerXorCipher.TransformRoleListPayload(roleData);
        return packet;
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out RoleListResultS2CPacket packet)
    {
        packet = default;
        if (payload.Length < 11 || payload[0] != ProtocolOpcodes.s2c_roleserver_getrolelist_result)
        {
            return false;
        }

        uint nDataLen = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4));
        uint identity = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(5, 4));
        if (nDataLen < 1 || payload.Length != HeaderSize + (int)nDataLen)
        {
            return false;
        }

        byte[] decrypted = payload.Slice(HeaderSize, (int)nDataLen).ToArray();
        SecondLayerXorCipher.TransformRoleListPayload(decrypted);

        byte roleCount = decrypted[0];
        int offset;
        if ((nDataLen - 1) == (roleCount * RoleEntrySize))
        {
            offset = 1;
        }
        else
        {
            byte refuseCount = nDataLen >= 2 ? decrypted[1] : (byte)0;
            offset = 2 + (refuseCount * 36);
            if (offset + (roleCount * RoleEntrySize) > nDataLen && 1 + (roleCount * RoleEntrySize) <= nDataLen)
            {
                offset = 1;
            }
        }

        var list = new List<CharacterSummaryDto>(roleCount);
        ReadOnlySpan<byte> span = decrypted;
        for (byte i = 0; i < roleCount && offset + RoleEntrySize <= span.Length; i++)
        {
            ReadOnlySpan<byte> entry = span.Slice(offset, RoleEntrySize);
            string name = Tcvn3Encoding.DecodeNullTerminated(entry[..32]);
            var gender = (CharacterGender)entry[32];
            var series = (ElementSeries)entry[33];
            byte level = entry[34];

            list.Add(new CharacterSummaryDto(i, Guid.Empty, name, gender, series, level));
            offset += RoleEntrySize;
        }

        packet = new RoleListResultS2CPacket(identity, list);
        return true;
    }
}
