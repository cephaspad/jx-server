using System;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Zone command enqueued when a client sends <c>0x58</c> (<c>c2s_playerapplyaddskillpoint</c>).
/// </summary>
public sealed record AllocateSkillPointZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    int SkillId,
    int PointsToAdd) : IZoneCommand;
