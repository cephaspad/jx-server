using System.Buffers.Binary;
using JXServer.Primitives.Chats;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_playertalk</c> (<c>0x5B</c>, variable <c>11 + MsgLen</c> bytes): broadcasts a player's overhead speech bubble
/// to the <c>3x3</c> region AOI (`FR-010`).
/// </summary>
public readonly record struct PlayerTalkS2CPacket(
    uint TalkerNpcId,
    string MessageUtf8)
{
    public const int HeaderSize = 11;

    public byte[] ToPayloadBytes()
    {
        byte[] tcvnBytes = Tcvn3Encoding.EncodeBytes(MessageUtf8 ?? string.Empty);
        int msgLen = Math.Min(tcvnBytes.Length, ChatConsts.MaxTalkMessageBytes - 1);
        int totalLen = HeaderSize + msgLen + 1;
        byte[] buffer = new byte[totalLen];
        buffer[0] = ProtocolOpcodes.s2c_playertalk;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(1, 2), (ushort)(totalLen - 1));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(3, 4), TalkerNpcId);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(7, 4), 0u);
        tcvnBytes.AsSpan(0, msgLen).CopyTo(buffer.AsSpan(HeaderSize));
        buffer[totalLen - 1] = 0;
        return buffer;
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerTalkS2CPacket packet)
    {
        packet = default;
        if (payload.Length < HeaderSize + 1 || payload[0] != ProtocolOpcodes.s2c_playertalk)
        {
            return false;
        }

        ushort lengthMinusOne = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1, 2));
        if (lengthMinusOne + 1 != payload.Length)
        {
            return false;
        }

        uint talkerId = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(3, 4));
        string message = Tcvn3Encoding.DecodeNullTerminated(payload[HeaderSize..]);
        packet = new PlayerTalkS2CPacket(talkerId, message);
        return true;
    }
}
