using System.Buffers.Binary;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// Variable-length (<c>15 + NameByteLen</c>) <c>c2s_newplayer</c> (<c>0x45</c>) packet for creating a new character.
/// </summary>
public readonly record struct CreateCharacterC2SPacket(
    uint Identity,
    CharacterGender Gender,
    ElementSeries Series,
    ushort NativePlaceId,
    string RoleNameUnicode)
{
    public byte[] ToPayloadBytes()
    {
        byte[] tcvnNameBytes = Tcvn3Encoding.EncodeBytes(RoleNameUnicode);
        int totalSize = 15 + tcvnNameBytes.Length;
        byte[] packet = new byte[totalSize];

        packet[0] = ProtocolOpcodes.c2s_newplayer;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(1, 4), (uint)(tcvnNameBytes.Length + 5));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(5, 4), Identity);
        packet[9] = 0;
        packet[10] = (byte)Gender;
        packet[11] = (byte)Series;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12, 2), NativePlaceId);
        tcvnNameBytes.CopyTo(packet.AsSpan(14));
        packet[^1] = 0;

        return packet;
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out CreateCharacterC2SPacket packet)
    {
        packet = default;
        if (payload.Length is < 16 or > 46 || payload[0] != ProtocolOpcodes.c2s_newplayer)
        {
            return false;
        }

        uint nDataLen = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4));
        if (nDataLen < 6 || payload.Length != 10 + (int)nDataLen)
        {
            return false;
        }

        uint identity = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(5, 4));
        byte roleNo = payload[10];
        byte seriesByte = payload[11];
        ushort nativePlaceId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(12, 2));

        if (roleNo > 1 || seriesByte > 4)
        {
            return false;
        }

        string roleName = Tcvn3Encoding.DecodeNullTerminated(payload[14..]);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return false;
        }

        packet = new CreateCharacterC2SPacket(
            identity,
            (CharacterGender)roleNo,
            (ElementSeries)seriesByte,
            nativePlaceId,
            roleName);
        return true;
    }
}
