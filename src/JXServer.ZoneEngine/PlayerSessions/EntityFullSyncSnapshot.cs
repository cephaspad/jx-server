using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Immutable snapshot of a player or NPC entity passed to <see cref="IPlayerOutboundSink.SendNpcFullSync"/>
/// to serialize <c>s2c_syncnpc</c> (<c>0x4C</c>, <c>61</c> bytes) (`FR-002`, `FR-003`).
/// </summary>
public readonly record struct EntityFullSyncSnapshot(
    uint NpcId,
    int MpsX,
    int MpsY,
    uint NpcSettingIdx,
    ElementSeries Series,
    byte LifePer,
    EntityDoingPosture Doing,
    EntityCamp Camp,
    byte Dir,
    EntityKind Kind,
    bool IsSpecialNpc,
    string NameUtf8);
