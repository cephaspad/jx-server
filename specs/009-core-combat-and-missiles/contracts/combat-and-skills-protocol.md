# Wire Protocol Contract: Combat, Skills & 64-Direction Missiles (Port 6666)

**Feature Branch**: `009-core-combat-and-missiles`  
**Date**: 2026-11-11  
**Ground-Truth Sources**:
- `jx-swarm/docs/02-game-systems/movement-and-combat.md`
- `jx-win-src/Sources/Core/Src/KProtocol.h`, `KPlayer.cpp`, `KNpc.cpp`, `KSkills.cpp`, `KMissle.cpp`

All packets are little-endian and encrypted on TCP Port `6666` via the session's `KsgStreamCipher` + `SecondLayerXorCipher`.

---

## 1. Skill Casting, Auto-Attack & Aura Selection (`C2S` & `S2C`)

### 1.1 `c2s_npcskill` (`0x4D`, Fixed `17` Bytes — `NPC_SKILL_COMMAND`)
Sent when the player casts a martial arts skill or basic attack.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x4D` (`c2s_npcskill`) |
| `+0x01` | `4` | `int` | `TargetMpsYOrNpcId` | When `TargetMpsX == -1`, holds locked `TargetNpcId` (`1..9999` or player runtime ID); otherwise holds ground target `MpsY`. |
| `+0x05` | `4` | `int` | `SkillId` | ID of the skill to cast. |
| `+0x09` | `4` | `int` | `TargetMpsX` | Ground target `MpsX`, or `-1` (`0xFFFFFFFF`) when locked onto an entity. |
| `+0x0D` | `4` | `uint` | `CheckValue` | Anti-cheat token verified against `MovementAntiCheatConsts.SkillCheckKey = 0x3AB9u` (`KPlayer::IsCheckValueValid`). |

### 1.2 `c2s_autoattacknpc` (`0x87`, Fixed `26` Bytes — `PLAYER_AUTO_ATTACK_COMMAND`)
Client auto-play / emergency envelope packet.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x87` (`c2s_autoattacknpc`) |
| `+0x01` | `1` | `byte` | `SubCommand` | Sub-command type: `0x02` = Emergency Town Portal; `0x03` = Wrapped command at `+0x02`. |
| `+0x02..+0x19` | `24` | `byte[]` | `Payload` | If `buffer[2] == 0x4D`: 17-byte `c2s_npcskill` frame at `+0x02..+0x12` followed by zero padding. |

### 1.3 `c2s_changeauraskill` (`0x6F`, Fixed `5` Bytes — `PLAYER_CHANGE_AURA_SKILL`)
Sent when toggling the active right-hand aura skill.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x6F` (`c2s_changeauraskill`) |
| `+0x01` | `4` | `int` | `AuraSkillId` | Learned aura skill ID to activate on the right-hand slot. |

### 1.4 `c2s_pkapplychangenormalflag` (`0x76`, Fixed `2` Bytes — `PLAYER_CHANGE_PK_FLAG`)
Sent to change normal PK stance.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x76` (`c2s_pkapplychangenormalflag`) |
| `+0x01` | `1` | `byte` | `PkFlag` | `0` = Peace (Luyện công), `1` = Fight (Chiến đấu), `2` = Slaughter (Đồ sát). Switching back to `0` enforces a `3240`-frame (3-minute) cooldown. |

---

## 2. Server Broadcasts: Skill Casting, Hurt Recoil & Vital Updates (`S2C`)

### 2.1 `s2c_skillcast` (`0x5A`, Fixed `25` Bytes — `NPC_SKILL_SYNC`) & `s2c_castskilldirectly` (`0x85`, Fixed `25` Bytes)
Broadcast to all players in the `3x3` region AOI when a skill cast is accepted (`0x5A` for standard casting animation, `0x85` for direct/instant casts).

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x5A` (or `0x85`) |
| `+0x01` | `4` | `int` | `TargetMpsX` | Ground target `MpsX` (`-1` when entity-locked) |
| `+0x05` | `4` | `uint` | `SkillId` | ID of the cast skill |
| `+0x09` | `4` | `int` | `TargetMpsYOrNpcId` | Ground target `MpsY` or target entity runtime ID |
| `+0x0D` | `4` | `uint` | `CasterNpcId` | Runtime ID of the caster |
| `+0x11` | `4` | `uint` | `SkillLevel` | Effective level of the cast skill |
| `+0x15` | `4` | `uint` | `Reserved` | `0` |

### 2.2 `s2c_broadcasenetcommand` (`0x56`, Fixed `22` Bytes — `NPC_NETCOMMAND_SYNC`)
Broadcast to the `3x3` region AOI when an entity takes a hit that triggers hurt recoil (`CmdKind = 9`).

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x56` (`s2c_broadcasenetcommand`) |
| `+0x01` | `4` | `uint` | `NpcId` | Target entity taking damage |
| `+0x05` | `1` | `byte` | `CmdKind` | `9` = `do_hurt` |
| `+0x06` | `4` | `int` | `Param1` | `CurrentMpsX` |
| `+0x0A` | `4` | `int` | `Param2` | `CurrentMpsY` |
| `+0x0E` | `4` | `int` | `Param3` | `HurtRecoverFrames` |
| `+0x12` | `4` | `int` | `Param4` | `0` |

---

## 3. Outbound Notification Port: `ICombatOutboundSink`

The zone simulation loop invokes domain-segregated sink methods on `ICombatOutboundSink`:

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface ICombatOutboundSink
{
    void SendNormalVitalSync(PlayerZoneState playerState);

    void SendSkillCastBroadcast(
        uint casterNpcId,
        uint skillId,
        uint skillLevel,
        int targetMpsX,
        int targetMpsYOrNpcId,
        bool castDirectly = false);

    void SendHurtBroadcast(uint npcId, int mpsX, int mpsY, int hurtFrames);

    void SendPkNormalFlagSync(byte pkMode);
}
```
