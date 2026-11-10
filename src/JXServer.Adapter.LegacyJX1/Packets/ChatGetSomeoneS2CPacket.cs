using System.Buffers.Binary;
using JXServer.Primitives.Chats;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_chatgetsomeone</c> (<c>0x7E</c>, variable <c>37 + MsgLen</c> bytes): delivers a formatted chat message
/// to a client (`FR-011`).
/// </summary>
public readonly record struct ChatGetSomeoneS2CPacket(
    uint SenderNpcId,
    string SenderNameUtf8,
    string MessageUtf8)
{
    public const int FixedHeaderSize = 39; // 1B opcode + 2B len + 4B senderId + 32B senderName

    public byte[] ToPayloadBytes()
    {
        byte[] msgBytes = Tcvn3Encoding.EncodeBytes(MessageUtf8 ?? string.Empty);
        int clampedMsgLen = Math.Min(msgBytes.Length, ChatConsts.MaxChatSentenceBytes - 1);
        int totalLen = FixedHeaderSize + clampedMsgLen + 1;

        byte[] buffer = new byte[totalLen];
        buffer[0] = ProtocolOpcodes.s2c_chatgetsomeone;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(1, 2), (ushort)(totalLen - 1));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(3, 4), SenderNpcId);
        Tcvn3Encoding.WriteFixedString(SenderNameUtf8 ?? string.Empty, buffer.AsSpan(7, ChatConsts.SenderNameBufferLength));
        msgBytes.AsSpan(0, clampedMsgLen).CopyTo(buffer.AsSpan(FixedHeaderSize));
        buffer[totalLen - 1] = 0;
        return buffer;
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out ChatGetSomeoneS2CPacket packet)
    {
        packet = default;
        if (payload.Length < FixedHeaderSize + 1 || payload[0] != ProtocolOpcodes.s2c_chatgetsomeone)
        {
            return false;
        }

        ushort lengthMinusOne = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1, 2));
        if (lengthMinusOne + 1 != payload.Length)
        {
            return false;
        }

        uint senderId = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(3, 4));
        string senderName = Tcvn3Encoding.DecodeNullTerminated(payload.Slice(7, ChatConsts.SenderNameBufferLength));
        string message = Tcvn3Encoding.DecodeNullTerminated(payload[FixedHeaderSize..]);
        packet = new ChatGetSomeoneS2CPacket(senderId, senderName, message);
        return true;
    }
}
