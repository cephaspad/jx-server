using System.Buffers.Binary;
using JXServer.Primitives.Chats;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_playertalk</c> (<c>0x52</c>, variable <c>7 + MsgLen</c> bytes): nearby spatial speech (`FR-010`).
/// </summary>
public readonly record struct PlayerTalkC2SPacket(
    uint TalkerNpcId,
    string MessageUtf8)
{
    public const int HeaderSize = 7;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerTalkC2SPacket packet)
    {
        packet = default;
        if (payload.Length < HeaderSize + 1 ||
            payload.Length > HeaderSize + ChatConsts.MaxTalkMessageBytes + 1 ||
            payload[0] != ProtocolOpcodes.c2s_playertalk)
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
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        packet = new PlayerTalkC2SPacket(talkerId, message);
        return true;
    }

    public byte[] ToPayloadBytes()
    {
        byte[] tcvnBytes = Tcvn3Encoding.EncodeBytes(MessageUtf8 ?? string.Empty);
        int msgLen = Math.Min(tcvnBytes.Length, ChatConsts.MaxTalkMessageBytes - 1);
        int totalLen = HeaderSize + msgLen + 1;
        byte[] buffer = new byte[totalLen];
        buffer[0] = ProtocolOpcodes.c2s_playertalk;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(1, 2), (ushort)(totalLen - 1));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(3, 4), TalkerNpcId);
        tcvnBytes.AsSpan(0, msgLen).CopyTo(buffer.AsSpan(HeaderSize));
        buffer[totalLen - 1] = 0;
        return buffer;
    }
}
