# Implementation Plan: Multi-Channel Chat Channels (Golden Reference Module)

**Branch**: `016-module-chat-channels` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

## Summary

Deliver the **016-module-chat-channels** specification for `JXServer` as the **Golden Reference Plug-in Module** in strict compliance with **Constitution v1.2.0** and **JXData Standard v1**:
1. **Golden Reference Plug-in Architecture (`ChatZoneModule : IZoneModule`)**: Establish the standard plug-in pattern for all Phase 2 gameplay modules (`017`–`028`). `ChatZoneModule` implements `IZoneModule.Configure(IZoneExtensionBuilder)`, dynamically registering all chat command handlers into `ZoneExtensionRegistry`, touching **0 lines** of the core `MapZoneWorker.cs`.
2. **Modular Command Dispatch (`ChatCommandHandler`)**: Implements `IZoneCommandHandler<UpdateChatChannelMaskZoneCommand>`, `IZoneCommandHandler<SendNearbyTalkZoneCommand>`, `IZoneCommandHandler<SendStructuredChatZoneCommand>`, and `IZoneCommandHandler<DeliverClusterChatZoneCommand>`.
3. **Spatial Speech & Multi-Channel Routing**:
   - Nearby speech (`0x52` $\rightarrow$ `0x5B`): Broadcasts to the 3x3 region AOI using zero-allocation buffer enumeration.
   - Structured multi-channel chat (`0x56` $\rightarrow$ `0x7E`): Delivers World (broadcast via `IZoneClusterRouter`), Private Whisper (direct character routing with sender echo), Party, Faction, and Tong messages.
   - Subscription bitmask filtering (`0x60`): Mutes unsubscribed channels per-session via `PlayerZoneState.SubscribedChatChannelMask`.
4. **Dedicated Domain Outbound Sink (`IChatOutboundSink`)**: Isolates chat-specific wire methods (`SendPlayerTalkBroadcast`, `SendChatMessage`, `SendSystemMessage`) implemented in `GameServerClientSession.ChatSink.cs`.
5. **Boundary Transcoding**: Transcodes null-terminated `TCVN3` wire strings to standard UTF-8 strings upon entering the zone engine, and back to `TCVN3` upon serialization.

---

## Technical Context

- **Platform & Language**: .NET 10 (`net10.0`), C# 14 with `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `AllowUnsafeBlocks=true`.
- **Primary Projects**:
  - `src/JXServer.Primitives`: `ChatChannelKind`, `ChatConsts`.
  - `src/JXServer.ZoneEngine`: `ChatZoneModule`, `ChatCommandHandler`, `SendNearbyTalkZoneCommand`, `SendStructuredChatZoneCommand`, `UpdateChatChannelMaskZoneCommand`, `DeliverClusterChatZoneCommand`, `IChatOutboundSink`, `NoOpChatOutboundSink`.
  - `src/JXServer.Adapter.LegacyJX1`: Wire packets `ChatSetChannelC2SPacket` (`0x60`), `PlayerTalkC2SPacket` (`0x52`), `PlayerTalkS2CPacket` (`0x5B`), `PlayerSendChatC2SPacket` (`0x56`), `ChatGetSomeoneS2CPacket` (`0x7E`), `SystemInfoExtendS2CPacket` (`0xFB`), and packet handlers.
- **Verification Suites**:
  - `tests/JXServer.Protocol.Tests/ChatPacketTests.cs`
  - `tests/JXServer.ZoneEngine.Tests/ModularZoneDeconstructionTests.cs`
  - `tests/JXServer.Integration.Tests/MultiChannelChatIntegrationTests.cs`

---

## Constitution Check (v1.2.0)

| Principle | Status | Evaluation |
| :--- | :---: | :--- |
| **I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Loop** | **PASS** | Nearby chat evaluates within the local 18-Hz zone loop; cross-zone World and Whisper routing leverages `IZoneClusterRouter`. |
| **II. Multi-Protocol Transport Abstraction** | **PASS** | `ZoneEngine` operates on strongly-typed chat commands and `IChatOutboundSink`. Wire framing stays entirely in `Adapter.LegacyJX1`. |
| **III. Zero-Hallucination & Ground-Truth Fidelity** | **PASS** | Preserves 100% of verified wire packet layouts, channel IDs, and system header constants (`0x00010032`, `"HÖ thèng"`). |
| **IV. Zero-Dupe Economy & Lease-Fenced Integrity** | **PASS** | Chat commands validate active `LeaseEpoch` against current player session before dispatching messages. |
| **V. UTF-8 Domain Core & Boundary-Only Transcoding** | **PASS** | All zone chat strings and commands use standard UTF-8; transcoding to `TCVN3` occurs strictly at the wire adapter edge. |
| **VI. Clean Modular Boundaries, ABP DDD & C# 14** | **PASS** | Standalone `IZoneModule` plug-in pattern, 1 type per `.cs` file, sealed concrete classes, pluralized folders (`Chats/`). |

---

## Project Structure

```text
specs/016-module-chat-channels/
├── spec.md
├── checklists/
│   └── requirements.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── chat-protocol.md
└── tasks.md
```

### Actual Codebase Mapping

```text
src/
├── JXServer.Primitives/
│   └── Chats/
│       ├── ChatChannelKind.cs
│       └── ChatConsts.cs
├── JXServer.ZoneEngine/
│   ├── Chats/
│   │   ├── ChatZoneModule.cs
│   │   ├── ChatCommandHandler.cs
│   │   ├── DeliverClusterChatZoneCommand.cs
│   │   ├── SendNearbyTalkZoneCommand.cs
│   │   ├── SendStructuredChatZoneCommand.cs
│   │   └── UpdateChatChannelMaskZoneCommand.cs
│   └── PlayerSessions/
│       ├── IChatOutboundSink.cs
│       └── NoOpChatOutboundSink.cs
└── JXServer.Adapter.LegacyJX1/
    ├── Packets/
    │   ├── ChatSetChannelC2SPacket.cs
    │   ├── PlayerTalkC2SPacket.cs
    │   ├── PlayerTalkS2CPacket.cs
    │   ├── PlayerSendChatC2SPacket.cs
    │   ├── ChatGetSomeoneS2CPacket.cs
    │   └── SystemInfoExtendS2CPacket.cs
    └── GameServer/
        ├── GameServerClientSession.ChatSink.cs
        ├── ChatSetChannelPacketHandler.cs
        ├── PlayerTalkPacketHandler.cs
        └── PlayerSendChatPacketHandler.cs

tests/
├── JXServer.Protocol.Tests/
│   └── ChatPacketTests.cs
├── JXServer.ZoneEngine.Tests/
│   └── ModularZoneDeconstructionTests.cs
└── JXServer.Integration.Tests/
    └── MultiChannelChatIntegrationTests.cs
```
