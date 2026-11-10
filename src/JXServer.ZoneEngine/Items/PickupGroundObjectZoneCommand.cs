using System;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Enqueued when a client sends <c>0x5A</c> (<c>c2s_playerpickupitem</c>) (`FR-011`, `FR-012`).
/// </summary>
public sealed record PickupGroundObjectZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    int ObjId,
    ItemContainerPlace TargetPlace,
    byte TargetGridX,
    byte TargetGridY) : IZoneCommand;
