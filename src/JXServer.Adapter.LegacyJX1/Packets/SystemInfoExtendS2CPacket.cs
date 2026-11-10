using System.Buffers.Binary;
using JXServer.Primitives.Chats;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_extend</c> (<c>0xFB</c>, variable <c>17 + MsgLen</c> bytes): server system announcement
/// (<c>SendSystemInfo</c> with <c>0x00010032</c> header and <c>"HÖ thèng"</c> prefix) (`FR-011`).
/// </summary>
public readonly record struct SystemInfoExtendS2CPacket(string MessageUtf8)
{
    public const int FixedHeaderSize = 17;

    public byte[] ToPayloadBytes()
    {
        byte[] msgBytes = Tcvn3Encoding.EncodeBytes(MessageUtf8 ?? string.Empty);
        int clampedMsgLen = Math.Min(msgBytes.Length, ChatConsts.MaxChatSentenceBytes - 1);
        ushort wireMsgLen = (ushort)(clampedMsgLen + 1);
        int totalLen = FixedHeaderSize + wireMsgLen;

        byte[] buffer = new byte[totalLen];
        buffer[0] = ProtocolOpcodes.s2c_extend;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(1, 2), (ushort)(totalLen - 1));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(3, 4), ChatConsts.SystemExtendHeader);
        Tcvn3Encoding.WriteFixedString(ChatConsts.DefaultSystemPrefixUtf8, buffer.AsSpan(7, 8));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(15, 2), wireMsgLen);
        msgBytes.AsSpan(0, clampedMsgLen).CopyTo(buffer.AsSpan(FixedHeaderSize));
        buffer[totalLen - 1] = 0;
        return buffer;
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out SystemInfoExtendS2CPacket packet)
    {
        packet = default;
        if (payload.Length < FixedHeaderSize + 1 || payload[0] != ProtocolOpcodes.s2c_extend)
        {
            return false;
        }

        ushort lengthMinusOne = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1, 2));
        if (lengthMinusOne + 1 != payload.Length)
        {
            return false;
        }

        uint header = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(3, 4));
        if (header != ChatConsts.SystemExtendHeader)
        {
            return false;
        }

        ushort msgLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(15, 2));
        if (FixedHeaderSize + msgLen > payload.Length)
        {
            return false;
        }

        string msg = Tcvn3Encoding.DecodeNullTerminated(payload.Slice(FixedHeaderSize, msgLen));
        packet = new SystemInfoExtendS2CPacket(msg);
        return true;
    }
}
