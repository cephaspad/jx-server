# Data Model: Multi-Channel Chat Channels (Golden Reference Module)

**Feature Branch**: `016-module-chat-channels`  
**Date**: 2026-11-11  

---

## 1. Primitives & Constants (`JXServer.Primitives/Chats/`)

| Type Name | Type Kind | Description & Key Values |
| :--- | :--- | :--- |
| `ChatChannelKind` | `enum : byte` | `None = 0`, `World = 1`, `Nearby = 2`, `Whisper = 3`, `Team = 4`, `Faction = 5`, `Tong = 6`, `Screen = 7`. |
| `ChatConsts` | `static class` | `MaxTalkMessageBytes = 256`, `MaxPrefixBytes = 32`, `DefaultSubscribedMask = 0xFFFFFFFFu`, `SystemChatExtendHeader = 0x00010032u`. |

---

## 2. Plug-in Module & Commands (`JXServer.ZoneEngine/Chats/`)

### 2.1 Plug-in Registration
- **`ChatZoneModule` (`sealed class : IZoneModule`)**:
  Registers command handlers dynamically via `IZoneExtensionBuilder`:
  - `builder.AddCommandHandler<UpdateChatChannelMaskZoneCommand>(handler);`
  - `builder.AddCommandHandler<SendNearbyTalkZoneCommand>(handler);`
  - `builder.AddCommandHandler<SendStructuredChatZoneCommand>(handler);`
  - `builder.AddCommandHandler<DeliverClusterChatZoneCommand>(handler);`

### 2.2 Zone Commands
- **`SendNearbyTalkZoneCommand` (`sealed record`)**:
  - `Guid SenderCharacterId`, `long LeaseEpoch`, `string MessageUtf8`.
- **`SendStructuredChatZoneCommand` (`sealed record`)**:
  - `Guid SenderCharacterId`, `long LeaseEpoch`, `ChatChannelKind Channel`, `uint TargetId`, `string TargetNameUtf8`, `string MessageUtf8`.
- **`UpdateChatChannelMaskZoneCommand` (`sealed record`)**:
  - `Guid CharacterId`, `long LeaseEpoch`, `uint SubscribedChannelMask`.
- **`DeliverClusterChatZoneCommand` (`sealed record`)**:
  - `ChatChannelKind Channel`, `uint SenderNpcId`, `string SenderNameUtf8`, `string MessageUtf8`, `Guid? RecipientCharacterId`, `int? FactionId`.

### 2.3 Command Handler
- **`ChatCommandHandler` (`sealed class`)**:
  Processes all 4 chat commands, performing 3x3 region iteration, subscription filtering, and cluster routing.

---

## 3. Player Session Chat State (`PlayerSessions/`)

- **`PlayerZoneState` (Chat Properties)**:
  - `uint SubscribedChatChannelMask`: 32-bit bitmask determining which channels the player receives.
  - `IChatOutboundSink OutboundSink`: Resolved chat sink channel.

- **`IChatOutboundSink` (`interface`)**:
  ```csharp
  void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8);
  void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8);
  void SendSystemMessage(string messageUtf8);
  ```

---

## 4. Wire Packets (`JXServer.Adapter.LegacyJX1/Packets/`)

| Packet Name | Opcode | Direction | Framing | Layout Summary |
| :--- | :---: | :---: | :---: | :--- |
| `ChatSetChannelC2SPacket` | `0x60` | C2S | Fixed 5B | `<BI`: Opcode, SubscribedChannelMask. |
| `PlayerTalkC2SPacket` | `0x52` | C2S | Var 8..264B | Opcode, Len-1, TalkerNpcId, MessageBytes (TCVN3). |
| `PlayerTalkS2CPacket` | `0x5B` | S2C | Var 11+N | Opcode, Len-1, TalkerNpcId, Reserved, MessageBytes (TCVN3). |
| `PlayerSendChatC2SPacket` | `0x56` | C2S | Var 47+N | Opcode, Len-1, Channel, ChatType, PrefixLen, SentenceLen, TargetId, TargetIdx, Prefix[32], SentenceBytes (TCVN3). |
| `ChatGetSomeoneS2CPacket` | `0x7E` | S2C | Var 37+N | Opcode, Len-1, SenderNpcId, SenderName[32], SentenceBytes (TCVN3). |
| `SystemInfoExtendS2CPacket` | `0xFB` | S2C | Var 17+N | Opcode, Len-1, ExtendHeader (`0x00010032`), SystemPrefix (`"HÖ thèng"`), MsgLen, MessageBytes (TCVN3). |
