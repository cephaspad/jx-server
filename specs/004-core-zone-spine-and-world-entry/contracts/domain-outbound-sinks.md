# Contract 02: Domain-Segregated Outbound Sinks

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)  
**Requirements Covered**: `FR-009`, `FR-011`, `SC-001`, `SC-005`  
**Target Projects**: `src/JXServer.ZoneEngine`, `src/JXServer.Adapter.LegacyJX1`  

---

## 1. Domain Sink Architecture

All 34 outbound player notification methods are partitioned across 4 cohesive interfaces:

| Domain Interface | Method Count | Functional Responsibility |
| :--- | :---: | :--- |
| **`IWorldOutboundSink`** | 14 | Initial world sync burst (`0x49`, `0x44`, `0x45`, `0x46`, `0x4C`, `0xA4`, `0x43`), sync end completion (`0x46` + `0x4E`), AOI entity bursts, walk/run/jump/sit broadcasts, map transitions, heartbeats (`0x82`, `0x99`), and socket disconnects. |
| **`ICombatOutboundSink`** | 10 | Skill casts, aura updates, damage numbers, status ailments (poison, freeze, stun), hurt recoil, death, revival, XP gain, level up, and stat/skill point allocations. |
| **`IInventoryOutboundSink`** | 7 | Ground object placement and pickup, container item synchronization, item movement/swapping/splitting, durability abrasion, and equipment stat updates. |
| **`IChatOutboundSink`** | 3 | Nearby spatial speech (`0x5B`), structured multi-channel chat (`0x7E`), and system/GM announcements (`0xFB`). |

---

## 2. Composite Interface & Extensibility Contract

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface IPlayerOutboundSink :
    IWorldOutboundSink,
    ICombatOutboundSink,
    IInventoryOutboundSink,
    IChatOutboundSink
{
    bool TryGetOutboundSink<TSink>(out TSink? sink) where TSink : class;
}
```

- **`TryGetOutboundSink<TSink>`**: Allows modular downstream features (`005`–`020`) to attach and query custom domain sinks (e.g. `IDialogOutboundSink`, `IShopOutboundSink`, `ITradeOutboundSink`) without modifying existing core interfaces.
- **`NoOp*OutboundSink`**: Singleton null-object fallback implementations ensuring sessions that do not subscribe to optional domains never throw `NullReferenceException`.
- **`PlayerZoneState` Caching**: Exposes non-null, pre-resolved properties (`WorldSink`, `CombatSink`, `InventorySink`, `ChatSink`) to eliminate runtime casting or dictionary lookups inside the 18-Hz simulation loop.

---

## 3. Wire Transport Partial Class Mapping (`GameServerClientSession`)

In `JXServer.Adapter.LegacyJX1`, `GameServerClientSession` is decomposed into 5 partial class files that write directly into a single FIFO `_outboundChannel`:
- `GameServerClientSession.cs`: TCP socket lifecycle and packet reader.
- `GameServerClientSession.WorldSink.cs`: Encodes `IWorldOutboundSink` frames.
- `GameServerClientSession.CombatSink.cs`: Encodes `ICombatOutboundSink` frames.
- `GameServerClientSession.InventorySink.cs`: Encodes `IInventoryOutboundSink` frames.
- `GameServerClientSession.ChatSink.cs`: Encodes `IChatOutboundSink` frames.

This guarantees that all outbound packets maintain strict FIFO ordering on the TCP socket.
