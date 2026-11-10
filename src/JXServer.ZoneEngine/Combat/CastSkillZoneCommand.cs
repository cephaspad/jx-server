using System;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Zone command enqueued when a client sends <c>0x4D</c> (<c>c2s_npcskill</c>) or <c>0x87</c> (<c>SubCommand = 0x03</c>).
/// </summary>
public sealed record CastSkillZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    int SkillId,
    int TargetParam1,
    int TargetParam2) : IZoneCommand;
