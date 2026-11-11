# Wire Protocol Contract: Monster AI, Death, Progression & Revival (Port 6666)

**Feature Branch**: `010-core-monster-ai-and-progression`  
**Date**: 2026-11-11  
**Ground-Truth Sources**:
- `jx-swarm/docs/02-game-systems/movement-and-combat.md`
- `jx-win-src/Sources/Core/Src/KProtocol.h`, `KPlayer.cpp`, `KNpc.cpp`, `KNpcSet.cpp`

All packets are little-endian and encrypted on TCP Port `6666` via the session's `KsgStreamCipher` + `SecondLayerXorCipher`.

---

## 1. Death & Revival Wire Packets (`C2S` & `S2C`)

### 1.1 `s2c_npcdeath` (`0x57`, Fixed `5` Bytes — `NPC_DEATH_SYNC`)
Broadcast to the `3x3` region AOI when a monster or player's `CurrentLife` reaches `0`.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x57` (`s2c_npcdeath`) |
| `+0x01` | `4` | `uint` | `NpcId` | Runtime ID of the deceased entity |

### 1.2 `c2s_playerrevive` (`0x74`, Fixed `13` Bytes — `PLAYER_REVIVE_COMMAND`)
Sent by a dead player to request revival.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x74` (`c2s_playerrevive`) |
| `+0x01` | `4` | `int` | `ReviveType` | `0` = Town revival, `1` = In-place revival |
| `+0x05` | `4` | `uint` | `Reserved1` | `0` |
| `+0x09` | `4` | `uint` | `Reserved2` | `0` |

### 1.3 `s2c_npcrevive` (`0x89`, Fixed `6` Bytes — `NPC_REVIVE_SYNC`)
Broadcast when an entity revives.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x89` (`s2c_npcrevive`) |
| `+0x01` | `4` | `uint` | `NpcId` | Runtime ID of the revived entity |
| `+0x05` | `1` | `byte` | `ReviveType` | `0` = Town revival, `1` = In-place revival |

---

## 2. Experience, Level-Up & Point Allocation (`C2S` & `S2C`)

### 2.1 `s2c_playerexp` (`0x5C`, Fixed `5` Bytes — `PLAYER_EXP_SYNC`)
Sent to update current experience balance.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x5C` (`s2c_playerexp`) |
| `+0x01` | `4` | `int` | `CurrentExp` | Cumulative experience within current level |

### 2.2 `s2c_playerlevelup` (`0x5B`, Fixed `17` Bytes — `PLAYER_LEVEL_UP_SYNC`)
Sent upon character level advancement.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x5B` (`s2c_playerlevelup`) |
| `+0x01` | `4` | `int` | `BaseAttributePoints` | Remaining unallocated attribute points |
| `+0x05` | `4` | `int` | `SkillPoints` | Remaining unallocated skill points |
| `+0x09` | `4` | `int` | `NewLevel` | Advanced character level |
| `+0x0D` | `4` | `int` | `MaxExpForNextLevel` | Experience needed to reach next level |

### 2.3 `c2s_playeraddbaseattribute` (`0x57`, Fixed `14` Bytes) $\rightarrow$ `s2c_playerattribute` (`0x5D`, Fixed `14` Bytes)
Base attribute point allocation request and response.

**Request (`0x57`, `14B`)**:
| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x57` |
| `+0x01` | `1` | `byte` | `Attribute` | `0` = Strength, `1` = Dexterity, `2` = Vitality, `3` = Energy |
| `+0x02` | `4` | `int` | `AddNo` | Points to allocate ($> 0$) |
| `+0x06` | `8` | `uint[2]` | `Reserved` | `0` |

**Response (`0x5D`, `14B`)**:
| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x5D` |
| `+0x01` | `1` | `byte` | `Attribute` | `0..3` |
| `+0x02` | `4` | `int` | `BasePoint` | New base attribute value |
| `+0x06` | `4` | `int` | `CurPoint` | New effective attribute value including equipment |
| `+0x0A` | `4` | `int` | `LeavePoint` | Remaining unallocated attribute points |

### 2.4 `c2s_playerapplyaddskillpoint` (`0x58`, Fixed `17` Bytes) $\rightarrow$ `s2c_playerskilllevel` (`0x5E`, Fixed `15` Bytes)
Skill point allocation request and response.

**Request (`0x58`, `17B`)**:
| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x58` |
| `+0x01` | `4` | `int` | `SkillId` | Target skill ID |
| `+0x05` | `4` | `int` | `AddPoint` | Points to allocate ($> 0$) |
| `+0x09` | `8` | `uint[2]` | `Reserved` | `0` |

**Response (`0x5E`, `15B`)**:
| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x5E` |
| `+0x01` | `4` | `int` | `SkillId` | Target skill ID |
| `+0x05` | `4` | `int` | `SkillLevel` | Effective skill level |
| `+0x09` | `4` | `int` | `LeavePoint` | Remaining unallocated skill points |
| `+0x0D` | `2` | `short` | `SkillExpPercent` | `0..100` |

---

## 3. Outbound Notification Sink Extensions (`ICombatOutboundSink`)

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface ICombatOutboundSink
{
    void SendNpcDeathBroadcast(uint npcId);
    void SendNpcReviveBroadcast(uint npcId, byte reviveType = 0);
    void SendPlayerExpSync(int currentExp);
    void SendPlayerLevelUpSync(int newLevel, int unallocatedAttrPoints, int unallocatedSkillPoints, int currentExp);
    void SendPlayerAttributeSync(byte attributeKind, int baseValue, int currentValue, int unallocatedAttrPoints);
    void SendPlayerSkillLevelSync(int skillId, int skillLevel, int skillExp, short unallocatedSkillPoints);
}
```
