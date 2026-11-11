# Research & Technical Rationale: Multi-Channel Chat Channels (Golden Reference Module)

**Feature Branch**: `016-module-chat-channels`  
**Date**: 2026-11-11  

---

## 1. Architectural Role: The Golden Reference Plug-in Module

In the JXServer modular roadmap, specifications `001` through `015` represent the core engine and character capabilities, while specifications `016` through `028` represent decoupled gameplay, social, and economic modules.

`016-module-chat-channels` serves as the **Golden Reference Plug-in Module** demonstrating the strict architectural standards required for all Phase 2 modules:
1. **Self-Contained Module Definition**:
   Implements `IZoneModule`, registering its command handlers, tick systems (if any), and feature slots via `IZoneExtensionBuilder`.
2. **Zero Core Loop Edits**:
   `MapZoneWorker.cs` contains zero hardcoded references or `switch` cases for chat commands; all chat routing occurs through the dynamic `ZoneExtensionRegistry`.
3. **Dedicated Domain Outbound Sink**:
   Outbound chat packets route through `IChatOutboundSink`, decoupled from world entry, combat, or inventory sinks.
4. **Isolated Fault Containment**:
   Any exception occurring within a chat command handler is safely caught, logged, and isolated to the invoking session without destabilizing the 18-Hz simulation shard.

---

## 2. Channel Scopes & Routing Topology

Chat channels follow the authentic Kingsoft wire specifications:

| Channel ID | Name | Routing Scope | Delivery Mechanism |
| :---: | :--- | :--- | :--- |
| `1` | World (`Thế giới`) | All online players across all map shards | Broadcast via `IZoneClusterRouter` |
| `2` | Nearby (`Lân cận`) | Observers in the speaker's 3x3 region AOI | Direct region iteration via `SpatialRegionSlot` |
| `3` | Whisper (`Mật đàm`) | Specific recipient player + Sender echo | Point-to-point via `IZoneClusterRouter` |
| `4` | Team / Party (`Đội ngũ`) | Active party members | Filtered by party membership |
| `5` | Faction (`Môn phái`) | Same faction members across shards | Filtered by `FactionId` via cluster router |
| `6` | Tong / Guild (`Bang hội`) | Same guild members across shards | Filtered by `GuildId` via cluster router |
| `7` | Screen (`Màn hình`) | Same screen / 3x3 region AOI | Direct region iteration |

---

## 3. Channel Subscription Bitmask Mechanics

Clients manage channel muting via `c2s_chatsetchannel` (`0x60`, 5 bytes):
- The payload transmits a 32-bit unsigned integer `SubscribedChannelMask`.
- Each bit $k$ corresponds to channel $k$: `(1u << channelId)`.
- Default on world entry: `0xFFFFFFFFu` (all channels enabled).
- When a message is prepared for a player session, the zone engine checks:
  $$\text{IsSubscribed} = (\text{SubscribedChatChannelMask} \ \& \ (1u \ll \text{ChannelId})) \neq 0$$
- If the bit is cleared, the message is dropped immediately, saving serialization and socket transmission bandwidth.

---

## 4. Boundary Encoding Transcoding (TCVN3 $\leftrightarrow$ UTF-8)

Legacy game clients (`game.exe`) expect Vietnamese text encoded in **TCVN3 (ABC)** with null-termination:
- In `JXServer.ZoneEngine`, all string properties, commands, and logs use standard **UTF-8** (`string`).
- At the `JXServer.Adapter.LegacyJX1` boundary:
  - Inbound C2S packets (`0x52`, `0x56`) read raw bytes from the network pipe and transcode them to UTF-8 using `Encoding.GetEncoding("windows-1252")` mapping or custom TCVN3 lookup tables before instantiating zone commands.
  - Outbound S2C packets (`0x5B`, `0x7E`, `0xFB`) encode UTF-8 text into `TCVN3` byte arrays and append a trailing null byte (`0x00`).
- This guarantees zero encoding contamination inside domain and zone layers while providing 100% text fidelity on legacy client displays.

---

## 5. System Announcements (`0xFB` `s2c_extend`)

Server announcements use the legacy extension packet `s2c_extend` (`0xFB`):
- Header: `0x00010032` (identifies system chat extension).
- Prefix: 8-byte TCVN3 string `"HÖ thèng"` (`48 D6 20 74 68 E8 6E 67`).
- Message: Variable-length TCVN3 encoded announcement text.
