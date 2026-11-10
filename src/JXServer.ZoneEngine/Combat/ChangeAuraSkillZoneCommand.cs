using System;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Zone command enqueued when a client sends <c>0x6F</c> (<c>c2s_changeauraskill</c>).
/// </summary>
public sealed record ChangeAuraSkillZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    int AuraSkillId) : IZoneCommand;
