using System;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Zone command enqueued when a client sends <c>0x74</c> (<c>c2s_playerrevive</c>).
/// </summary>
public sealed record RevivePlayerZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    int ReviveType) : IZoneCommand;
