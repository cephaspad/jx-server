# Contract: GameServer Multi-Channel Chat & System Message Wire Protocol (Port 6666)

**Feature Branch**: `016-module-chat-channels`  
**Date**: 2026-11-11  
**Ground-Truth Sources**:
- `jx-swarm/docs/02-game-systems/npc-dialog-inventory-and-economy.md`
- `jx-win-src/Sources/Core/Src/KProtocol.h`, `KPlayer.cpp`

All text fields on the wire (`szTalkMsg`, `szSender`, `szSentencePrefix`, `szSentence`, `szMessage`) are encoded in **TCVN3 (Vietnamese Windows-1252)** at the `JXServer.Adapter.LegacyJX1` boundary and transcoded immediately to **UTF-8 (`string`)** before entering `JXServer.ZoneEngine`.

---

## 1. Chat Channel Subscription (`c2s_chatsetchannel` `0x60` / Decimal `96`)
- **Direction**: `Client -> Server (C2S)`
- **Fixed Payload Size**: `5 bytes` (`wPacketLen = 0x0005`)
- **C# Struct**: `ChatSetChannelC2SPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x60` (`c2s_chatsetchannel`) |
| `0x01` | `4` | `uint` | `SubscribedChannelMask` | Bitmask of subscribed channels (`(1u << channelId)`). Default on world entry: `0xFFFFFFFFu` |

---

## 2. Nearby Spatial Speech (`c2s_playertalk` `0x52` $\rightarrow$ `s2c_playertalk` `0x5B`)

### 2.1 `c2s_playertalk` (`0x52` / Decimal `82`)
- **Direction**: `Client -> Server (C2S)`
- **Framing Type**: `Variable` (`g_nProtocolSize[0x11] = -1`, validated by `wLengthMinusOne + 1 == payload.Length`, `8 <= payload.Length <= 264`)
- **C# Struct**: `PlayerTalkC2SPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x52` (`c2s_playertalk`) |
| `0x01` | `2` | `ushort` | `LengthMinusOne` | `TotalPayloadSize - 1` (`6 + MsgLen`) |
| `0x03` | `4` | `uint` | `TalkerNpcId` | Sender `Npc.m_dwID` (or `0`; server overrides with authoritative `PlayerZoneState.PlayerNpcId`) |
| `0x07` | `MsgLen` | `byte[]` | `TalkMessageBytes` | Null-terminated `TCVN3` message string |

### 2.2 `s2c_playertalk` (`0x5B` / Decimal `91`)
- **Direction**: `Server -> Client (S2C)` (Broadcast to `3x3` region AOI)
- **Framing Type**: `Variable` (`11 + MsgLen` bytes, including trailing `0x00`)
- **C# Struct**: `PlayerTalkS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x5B` (`s2c_playertalk`) |
| `0x01` | `2` | `ushort` | `LengthMinusOne` | `TotalPayloadSize - 1` (`10 + MsgLen`) |
| `0x03` | `4` | `uint` | `TalkerNpcId` | Speaker's runtime `PlayerNpcId` |
| `0x07` | `4` | `uint` | `Reserved` | `0x00000000` |
| `0x0B` | `MsgLen` | `byte[]` | `TalkMessageBytes` | Null-terminated `TCVN3` message string |

---

## 3. Structured Multi-Channel Chat (`c2s_playersendchat` `0x56` $\rightarrow$ `s2c_chatgetsomeone` `0x7E`)

### 3.1 `c2s_playersendchat` (`0x56` / Decimal `86`)
- **Direction**: `Client -> Server (C2S)`
- **Framing Type**: `Variable` (`g_nProtocolSize[0x15] = -1`, validated by `wLengthMinusOne + 1 == payload.Length` and `47 + wSentenceLen <= payload.Length`)
- **C# Struct**: `PlayerSendChatC2SPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x56` (`c2s_playersendchat`) |
| `0x01` | `2` | `ushort` | `LengthMinusOne` | `TotalPayloadSize - 1` (`46 + SentenceLen`) |
| `0x03` | `1` | `byte` | `CurrChannel` | `ChatChannelKind` (`1` = World, `2` = Nearby, `3` = Whisper, `4` = Team, `5` = Faction, `6` = Tong, `7` = Screen) |
| `0x04` | `1` | `byte` | `ChatType` | `0` = Normal chat (`MSG_G_CHAT`), `1` = Command |
| `0x05` | `1` | `byte` | `ChatPrefixLen` | Length of prefix inside `SentencePrefix` (`<= 32`) |
| `0x06` | `2` | `ushort` | `SentenceLen` | Byte length of `Sentence` at `+0x30` |
| `0x08` | `4` | `uint` | `TargetId` | Target player `NpcId` (`0` for public channels) |
| `0x0C` | `4` | `int` | `TargetIdx` | Target index (`-1` or `0`) |
| `0x10` | `32` | `byte[32]` | `SentencePrefix` | Null-terminated `TCVN3` target character name (for Whisper `Channel 3`) or channel prefix |
| `0x30` | `SentenceLen` | `byte[]` | `Sentence` | `TCVN3` chat text |

### 3.2 `s2c_chatgetsomeone` (`0x7E` / Decimal `126`)
- **Direction**: `Server -> Client (S2C)`
- **Framing Type**: `Variable` (`37 + MsgLen` bytes, where `MsgLen` includes trailing `0x00`)
- **C# Struct**: `ChatGetSomeoneS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x7E` (`s2c_chatgetsomeone`) |
| `0x01` | `2` | `ushort` | `LengthMinusOne` | `TotalPayloadSize - 1` (`36 + MsgLen`) |
| `0x03` | `4` | `uint` | `SenderNpcId` | Sender's runtime `PlayerNpcId` |
| `0x07` | `32` | `byte[32]` | `SenderName` | Null-terminated `TCVN3` character name of sender |
| `0x27` | `MsgLen` | `byte[]` | `Sentence` | Null-terminated `TCVN3` message content |

---

## 4. Server System Message (`s2c_extend` `0xFB` / Decimal `251`)
- **Direction**: `Server -> Client (S2C)`
- **Framing Type**: `Variable` (`17 + MsgLen` bytes)
- **C# Struct**: `SystemInfoExtendS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0xFB` (`s2c_extend`) |
| `0x01` | `2` | `ushort` | `LengthMinusOne` | `TotalPayloadSize - 1` (`16 + MsgLen`) |
| `0x03` | `4` | `uint` | `ExtendHeader` | `0x00010032` (`s2c_extendchat` system header) |
| `0x07` | `8` | `byte[8]` | `SystemPrefix` | `TCVN3` `"HÖ thèng"` (`48 D6 20 74 68 E8 6E 67`) |
| `0x0F` | `2` | `ushort` | `MessageLength` | Byte length of `MessageBytes` |
| `0x11` | `MsgLen` | `byte[]` | `MessageBytes` | Null-terminated `TCVN3` system message |

---

## 5. Domain Outbound Port: `IChatOutboundSink`

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface IChatOutboundSink
{
    void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8);
    void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8);
    void SendSystemMessage(string messageUtf8);
}
```
