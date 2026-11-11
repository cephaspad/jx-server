# Tasks: Multi-Channel Chat Channels (Golden Reference Module)

**Feature Branch**: `016-module-chat-channels`  
**Status**: Completed & Verified  

---

## Task List

- [X] **T01 — Primitive Chat Definitions** `src/JXServer.Primitives/Chats/`
  - [X] Define channel enum (`World`, `Nearby`, `Whisper`, `Team`, `Faction`, `Tong`, `Screen`): `ChatChannelKind.cs`
  - [X] Define chat constants (lengths, defaults, system headers): `ChatConsts.cs`

- [X] **T02 — Wire Packet Serialization & Transcoding** `src/JXServer.Adapter.LegacyJX1/Packets/`
  - [X] Define channel subscription packet struct (`0x60`): `ChatSetChannelC2SPacket.cs`
  - [X] Define spatial talk packet structs (`0x52` / `0x5B`): `PlayerTalkC2SPacket.cs`, `PlayerTalkS2CPacket.cs`
  - [X] Define multi-channel chat packet structs (`0x56` / `0x7E`): `PlayerSendChatC2SPacket.cs`, `ChatGetSomeoneS2CPacket.cs`
  - [X] Define server system announcement packet struct (`0xFB`): `SystemInfoExtendS2CPacket.cs`
  - [X] Packet test verification: `tests/JXServer.Protocol.Tests/ChatPacketTests.cs`

- [X] **T03 — Plug-in Module & Dynamic Registration** `src/JXServer.ZoneEngine/Chats/`
  - [X] Implement `ChatZoneModule : IZoneModule`: `ChatZoneModule.cs`
  - [X] Register `UpdateChatChannelMaskZoneCommand`, `SendNearbyTalkZoneCommand`, `SendStructuredChatZoneCommand`, and `DeliverClusterChatZoneCommand` via `IZoneExtensionBuilder`
  - [X] Verify zero modifications to `src/JXServer.ZoneEngine/WorldZones/MapZoneWorker.cs`
  - [X] Modular zone deconstruction test verification: `tests/JXServer.ZoneEngine.Tests/ModularZoneDeconstructionTests.cs`

- [X] **T04 — Command Handlers & Channel Routing** `src/JXServer.ZoneEngine/Chats/`
  - [X] Implement domain command handler: `ChatCommandHandler.cs`
  - [X] Implement 3x3 region iteration for nearby talk: `SendNearbyTalkZoneCommand.cs`
  - [X] Implement multi-channel routing (World, Whisper with echo, Faction): `SendStructuredChatZoneCommand.cs`, `DeliverClusterChatZoneCommand.cs`
  - [X] Implement channel subscription mask filtering: `UpdateChatChannelMaskZoneCommand.cs`

- [X] **T05 — Outbound Chat Sink & Packet Handlers**
  - [X] Implement dedicated chat outbound sink: `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.ChatSink.cs`
  - [X] Implement packet handlers: `ChatSetChannelPacketHandler.cs`, `PlayerTalkPacketHandler.cs`, `PlayerSendChatPacketHandler.cs`
  - [X] Integration test suite verification: `tests/JXServer.Integration.Tests/MultiChannelChatIntegrationTests.cs`
