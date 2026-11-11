# Feature Specification: Multi-Channel Chat Channels (Golden Reference Module)

**Feature Branch**: `016-module-chat-channels`  
**Created**: 2026-11-11  
**Status**: Specification Complete  
**Input**: Golden reference plug-in module (`ChatZoneModule : IZoneModule`) implementing decoupled multi-channel communication (Nearby, World, Whisper, Party, Faction, Guild), channel subscription filtering, and server system broadcasts without modifying the core zone loop.

---

## 1. Overview & Context

In an MMORPG, social communication between players across spatial, factional, and global scopes forms the cornerstone of player engagement and community building. In Võ Lâm Truyền Kỳ, communication occurs across distinct scopes:
- **Nearby Spatial Speech**: Local bubble talk delivered to observers in the speaker's immediate 3x3 region Area of Interest.
- **Structured Multi-Channel Communication**: Dedicated text tabs for World (global cross-server broadcasts), Whisper (private direct messaging between two characters), Party/Team, Faction, and Tong/Guild.
- **Subscription Filtering**: Configurable bitmasks allowing players to mute or tune into specific channels.
- **Server System Announcements**: High-priority notices broadcast by the server system to inform players of world events or announcements.

Architecturally, `016-module-chat-channels` serves as the **Golden Reference Plug-in Module** of JXServer: it implements the open/closed modular spine pattern (`IZoneModule`), registering all its command handlers dynamically via `IZoneExtensionBuilder` into `ZoneExtensionRegistry` and communicating outbound via a domain-segregated sink (`IChatOutboundSink`), touching **zero lines** of the core `MapZoneWorker` simulation loop.

---

## 2. Clarifications & Ground-Truth Context

- **Plug-in Independence**: The chat subsystem must not reside hardcoded inside the core zone loop. It registers as a standalone plug-in module (`ChatZoneModule : IZoneModule`), serving as the architectural template for all subsequent Phase 2 gameplay modules (`017` through `028`).
- **Channel Scopes**:
  - `Nearby` (Channel 2) & `Screen` (Channel 7): Restricted to the 3x3 spatial AOI surrounding the speaker.
  - `World` (Channel 1): Dispatched across all active zone shards to all online players subscribing to World chat.
  - `Whisper` (Channel 3): Routed privately to the named recipient character (or recipient runtime ID), and echoed back to the sender.
  - `Team` (Channel 4) & `Faction` (Channel 5): Routed to online members sharing the sender's party or faction.
  - `Tong` (Channel 6): Routed to online members of the sender's guild.
- **Subscription Bitmasks**: Each player session maintains a 32-bit channel mask (`SubscribedChatChannelMask`). Messages arriving for an unsubscribed channel are filtered out before wire serialization.
- **Legacy Boundary Encoding**: Chat text is authored and processed internally as standard UTF-8, and transcoded to null-terminated `TCVN3` (Vietnamese Windows-1252) at the wire protocol adapter boundary.

---

## 3. User Scenarios & Acceptance Criteria

### User Story 1 - Nearby Spatial Player Speech (Priority: P1)
A player types a speech message in the game chat. The server verifies the speaker's identity and active lease epoch, and broadcasts the speech message to all players present in the speaker's surrounding 3x3 region Area of Interest.

**Acceptance Scenarios**:
1. **Given** a connected player in a map zone, **When** the player sends a nearby talk message, **Then** the server broadcasts the speech text and the speaker's identity to all players whose spatial region is within the 3x3 neighborhood of the speaker.
2. **Given** a player located outside the 3x3 region neighborhood of the speaker, **When** the speaker sends a nearby talk message, **Then** the remote player receives no speech broadcast.
3. **Given** an empty or whitespace-only talk message, **When** submitted, **Then** the server ignores the request without broadcasting.

---

### User Story 2 - Structured Multi-Channel Chat Routing (Priority: P1)
A player sends a structured message specifying a target channel (World, Nearby, Whisper, Team, Faction, Tong). The server verifies subscription and recipient constraints, routes the message across shards or within the local zone, and delivers the message with the sender's name to all qualifying recipients.

**Acceptance Scenarios**:
1. **Given** a player sending a message on the World channel (Channel 1), **When** processed, **Then** the server delivers the message to all online players across all active zone shards who subscribe to Channel 1.
2. **Given** a player sending a private Whisper (Channel 3) targeting another player by character name, **When** processed and the target is online, **Then** the server delivers the private message to the recipient and echoes a copy back to the sender.
3. **Given** a player sending a private Whisper to an offline or non-existent character, **When** processed, **Then** the server notifies the sender that the target is unavailable.
4. **Given** a player sending a message on the Faction channel (Channel 5), **When** processed, **Then** the message is delivered to all online players belonging to the same faction who subscribe to the Faction channel.

---

### User Story 3 - Chat Channel Subscription Filtering (Priority: P2)
A player customizes their chat settings by enabling or disabling specific chat channels. The server updates the player's subscription bitmask, immediately silencing muted channels for that player while maintaining deliveries for subscribed channels.

**Acceptance Scenarios**:
1. **Given** a player who has muted the World channel by clearing bit 1 in their subscription mask, **When** another player broadcasts a World chat message, **Then** the server suppresses delivery of that World message to the muted player.
2. **Given** a player re-enabling a previously muted channel, **When** the subscription update is processed, **Then** subsequent messages on that channel resume normal delivery.

---

### User Story 4 - Server System Announcements (Priority: P2)
The server broadcasts high-priority system announcements or operational notices to all connected players. Connected clients receive the system message formatted with official system headers.

**Acceptance Scenarios**:
1. **Given** a system maintenance or global announcement triggered by the server, **When** dispatched, **Then** all connected players receive the system message packet with the standard system announcement prefix.

---

### User Story 5 - Golden Reference Plug-in Module Independence (Priority: P1)
The chat system is deployed as an isolated plug-in module implementing `IZoneModule`. It registers all command handlers via `IZoneExtensionBuilder` into `ZoneExtensionRegistry`, requiring zero modifications to the core zone tick loop.

**Acceptance Scenarios**:
1. **Given** the zone engine initializing map shards, **When** extension modules are registered, **Then** `ChatZoneModule` registers `UpdateChatChannelMaskZoneCommand`, `SendNearbyTalkZoneCommand`, `SendStructuredChatZoneCommand`, and `DeliverClusterChatZoneCommand` without modifying `MapZoneWorker.cs`.
2. **Given** an invalid or throwing chat command, **When** handled, **Then** the error is contained within the session context without halting or corrupting the map zone simulation loop.

---

## 4. Edge Cases

- **Speaker Sends Message Right As Recipient Disconnects**: If a target player disconnects while a private Whisper is in transit across the cluster router, the message terminates safely without throwing unhandled exceptions.
- **Message Exceeds Maximum Wire Length**: If a client sends a message exceeding the maximum permitted length (256 characters), the server truncates the message cleanly at the boundary or rejects the oversized frame.
- **Spam Burst from Single Client**: If a client floods chat messages faster than the permitted rate, the server drops surplus messages, protecting outbound socket channels from buffer bloat.
- **Non-ASCII Characters & Diacritics**: Vietnamese diacritics in UTF-8 format are safely converted to TCVN3 at the legacy protocol boundary without corrupting string length or packet offsets.

---

## 5. Functional Requirements

- **FR-001**: The chat subsystem MUST be implemented as an independent plug-in module (`ChatZoneModule : IZoneModule`), registering its command handlers dynamically through `IZoneExtensionBuilder`.
- **FR-002**: The chat module MUST NOT modify `MapZoneWorker.cs` directly, adhering to the Open/Closed modular spine architecture.
- **FR-003**: The system MUST process nearby spatial talk requests, broadcasting speech to all players present in the speaker's 3x3 region Area of Interest.
- **FR-004**: The system MUST process structured multi-channel chat requests across World, Nearby, Whisper, Team, Faction, Tong, and Screen channels.
- **FR-005**: World channel messages MUST be routed to all online players across all active map zones via the cluster router.
- **FR-006**: Private Whisper messages MUST be routed to the specific target player by character name or ID, with an echo copy returned to the sender.
- **FR-007**: Faction channel messages MUST be routed exclusively to online players matching the sender's faction.
- **FR-008**: The system MUST maintain and enforce a 32-bit channel subscription bitmask per player session, suppressing delivery of messages from muted channels.
- **FR-009**: The system MUST support broadcasting server system notices to all online players with standard system message headers.
- **FR-010**: All chat text MUST be handled internally as standard UTF-8 strings and transcoded to TCVN3 at the legacy wire protocol boundary.
- **FR-011**: Outbound notifications MUST route through a dedicated domain-segregated sink (`IChatOutboundSink`).

---

## 6. Measurable Success Criteria

- **SC-001**: 100% of nearby spatial talk messages are received exclusively by observers within the speaker's 3x3 region neighborhood.
- **SC-002**: 100% of private whispers reach the exact intended online recipient and echo back to the sender with zero cross-talk leakage.
- **SC-003**: 100% of messages sent on muted channels are suppressed according to the player's subscription bitmask.
- **SC-004**: `ChatZoneModule` registers 100% of its command routes into `ZoneExtensionRegistry` with zero edits to `MapZoneWorker.cs`.
- **SC-005**: Zero unhandled exceptions or zone loop halts occur when handling malformed, oversized, or unsubscribed chat messages.
