using System.Buffers.Binary;
using JXServer.Primitives.Chats;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_playersendchat</c> (<c>0x56</c>, variable <c>47 + SentenceLen</c> bytes): structured multi-channel chat (`FR-011`).
/// </summary>
public readonly record struct PlayerSendChatC2SPacket(
    ChatChannelKind Channel,
    byte ChatType,
    uint TargetNpcId,
    int TargetIdx,
    string SentencePrefixUtf8,
    string SentenceUtf8)
{
    public const int FixedHeaderSize = 48;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerSendChatC2SPacket packet)
    {
        packet = default;
        if (payload.Length < FixedHeaderSize ||
            payload.Length > 360 ||
            payload[0] != ProtocolOpcodes.c2s_playersendchat)
        {
            return false;
        }

        ushort lengthMinusOne = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1, 2));
        if (lengthMinusOne + 1 != payload.Length)
        {
            return false;
        }

        byte rawChannel = payload[3];
        ChatChannelKind channel = Enum.IsDefined(typeof(ChatChannelKind), rawChannel)
            ? (ChatChannelKind)rawChannel
            : ChatChannelKind.Nearby;

        byte chatType = payload[4];
        byte prefixLen = payload[5];
        ushort sentenceLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(6, 2));
        if (sentenceLen == 0 || 47 + sentenceLen > payload.Length)
        {
            return false;
        }

        uint targetId = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(8, 4));
        int targetIdx = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(12, 4));

        ReadOnlySpan<byte> prefixSpan = payload.Slice(16, Math.Min((int)prefixLen, ChatConsts.SentencePrefixBufferLength));
        string prefixUtf8 = prefixLen > 0
            ? Tcvn3Encoding.DecodeNullTerminated(prefixSpan)
            : Tcvn3Encoding.DecodeNullTerminated(payload.Slice(16, ChatConsts.SentencePrefixBufferLength));

        // In tagPLAYER_SEND_CHAT_COMMAND, szSentence starts at +0x30 (48) or +0x2F (47) depending on client build;
        // if payload.Length == 48 + sentenceLen, sentence is at +48; if 47 + sentenceLen, at +47
        int sentenceStart = payload.Length >= FixedHeaderSize + sentenceLen ? FixedHeaderSize : 47;
        ReadOnlySpan<byte> sentenceSpan = payload.Slice(sentenceStart, Math.Min((int)sentenceLen, payload.Length - sentenceStart));
        string sentenceUtf8 = Tcvn3Encoding.DecodeNullTerminated(sentenceSpan);
        if (string.IsNullOrWhiteSpace(sentenceUtf8))
        {
            return false;
        }

        packet = new PlayerSendChatC2SPacket(
            channel,
            chatType,
            targetId,
            targetIdx,
            prefixUtf8,
            sentenceUtf8);
        return true;
    }

    public byte[] ToPayloadBytes()
    {
        byte[] prefixBytes = Tcvn3Encoding.EncodeBytes(SentencePrefixUtf8 ?? string.Empty);
        int clampedPrefixLen = Math.Min(prefixBytes.Length, ChatConsts.SentencePrefixBufferLength - 1);

        byte[] sentenceBytes = Tcvn3Encoding.EncodeBytes(SentenceUtf8 ?? string.Empty);
        int clampedSentenceLen = Math.Min(sentenceBytes.Length, ChatConsts.MaxChatSentenceBytes - 1);
        ushort wireSentenceLen = (ushort)(clampedSentenceLen + 1);

        int totalLen = FixedHeaderSize + wireSentenceLen;
        byte[] buffer = new byte[totalLen];
        buffer[0] = ProtocolOpcodes.c2s_playersendchat;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(1, 2), (ushort)(totalLen - 1));
        buffer[3] = (byte)Channel;
        buffer[4] = ChatType;
        buffer[5] = (byte)clampedPrefixLen;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(6, 2), wireSentenceLen);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(8, 4), TargetNpcId);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(12, 4), TargetIdx);
        prefixBytes.AsSpan(0, clampedPrefixLen).CopyTo(buffer.AsSpan(16));
        sentenceBytes.AsSpan(0, clampedSentenceLen).CopyTo(buffer.AsSpan(FixedHeaderSize));
        buffer[totalLen - 1] = 0;
        return buffer;
    }
}
