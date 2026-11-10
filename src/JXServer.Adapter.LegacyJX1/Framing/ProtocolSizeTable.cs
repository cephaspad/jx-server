using System.Buffers.Binary;
using JXServer.Adapter.LegacyJX1.Packets;

namespace JXServer.Adapter.LegacyJX1.Framing;

/// <summary>
/// Authoritative protocol payload size validation table (<c>g_nProtocolSize</c>) for Bishop Gateway (<c>5622</c>)
/// and GameServer (<c>6666</c>) incoming C2S packets (`FR-010`, `FR-013`).
/// </summary>
public static class ProtocolSizeTable
{
    public static bool IsValidBishopC2SPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return false;
        }

        byte opcode = payload[0];
        return opcode switch
        {
            ProtocolOpcodes.c2s_login => payload.Length == 127 &&
                BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1, 2)) == 0x007E,
            ProtocolOpcodes.c2s_dbplayerselect => payload.Length == 41,
            ProtocolOpcodes.c2s_newplayer => IsValidNewPlayerPayload(payload),
            ProtocolOpcodes.c2s_roleserver_deleteplayer => payload.Length == 129,
            _ => false,
        };
    }

    public static bool IsValidGameServerC2SPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return false;
        }

        byte opcode = payload[0];
        return opcode switch
        {
            ProtocolOpcodes.c2s_logiclogin => payload.Length == 17,
            ProtocolOpcodes.c2s_syncend => payload.Length == 9,
            ProtocolOpcodes.c2s_ping => payload.Length == 9,
            ProtocolOpcodes.c2sCloseConnection => payload.Length == 1,
            ProtocolOpcodes.c2s_loadplayer => payload.Length == 5,
            ProtocolOpcodes.c2s_removeplayer => payload.Length == 17,
            ProtocolOpcodes.c2s_requestplayer => payload.Length == 5,
            ProtocolOpcodes.c2s_requestnpc => payload.Length == 5,
            ProtocolOpcodes.c2s_requestobj => payload.Length is 5 or 9,
            ProtocolOpcodes.c2s_npcwalk => payload.Length == 13,
            ProtocolOpcodes.c2s_npcrun => payload.Length == 13,
            ProtocolOpcodes.c2s_npcskill => payload.Length == 17,
            ProtocolOpcodes.c2s_npcjump => payload.Length == 13,
            ProtocolOpcodes.c2s_playertalk => IsValidPlayerTalkPayload(payload),
            ProtocolOpcodes.c2s_playersendchat => IsValidPlayerSendChatPayload(payload),
            ProtocolOpcodes.c2s_playeraddbaseattribute => payload.Length == 14,
            ProtocolOpcodes.c2s_playeraddskillpoint => payload.Length == 17,
            ProtocolOpcodes.c2s_playereatitem => payload.Length == 16,
            ProtocolOpcodes.c2s_playerpickupitem => payload.Length == 12,
            ProtocolOpcodes.c2s_playermoveitem => payload.Length == 7,
            ProtocolOpcodes.c2s_playerthrowawayitem => payload.Length == 1,
            ProtocolOpcodes.c2s_chatsetchannel => payload.Length == 5,
            ProtocolOpcodes.c2s_dialognpc => payload.Length == 9,
            ProtocolOpcodes.c2s_changeauraskill => payload.Length == 5,
            ProtocolOpcodes.c2s_npcsit => payload.Length == 2,
            ProtocolOpcodes.c2s_objmouseclick => payload.Length == 9,
            ProtocolOpcodes.c2s_npcrevive => payload.Length == 13,
            ProtocolOpcodes.c2s_pkapplychangenormalflag => payload.Length == 2,
            ProtocolOpcodes.c2s_ThrowAwayAllMedicine => payload.Length == 4,
            ProtocolOpcodes.c2s_PlayerDivideItem => payload.Length == 10,
            ProtocolOpcodes.c2s_autoattacknpc => payload.Length == 26,
            ProtocolOpcodes.c2s_welcome2server => payload.Length == 5,
            ProtocolOpcodes.c2s_daytimereq => payload.Length == 25,
            _ => false,
        };
    }

    private static bool IsValidNewPlayerPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length is < 16 or > 46)
        {
            return false;
        }

        uint nDataLen = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4));
        return nDataLen >= 6 && payload.Length == (int)(10 + nDataLen);
    }

    private static bool IsValidPlayerTalkPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length is < 8 or > 264)
        {
            return false;
        }

        ushort lengthMinusOne = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1, 2));
        return lengthMinusOne + 1 == payload.Length;
    }

    private static bool IsValidPlayerSendChatPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length is < 48 or > 360)
        {
            return false;
        }

        ushort lengthMinusOne = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1, 2));
        if (lengthMinusOne + 1 != payload.Length)
        {
            return false;
        }

        ushort sentenceLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(6, 2));
        return sentenceLen > 0 && 48 + sentenceLen - 1 <= payload.Length;
    }
}
