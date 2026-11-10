using System;
using JXServer.Primitives.Combat;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Zone command enqueued when a client sends <c>0x76</c> (<c>c2s_pkapplychangenormalflag</c>).
/// </summary>
public sealed record ChangePkModeZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    PlayerPkMode RequestedPkMode) : IZoneCommand;
