using System;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Enqueued when a client sends <c>0x85</c> (<c>c2s_PlayerDivideItem</c>) (`FR-012`).
/// </summary>
public sealed record DivideInventoryItemZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    uint RuntimeItemId,
    ushort DivideCount) : IZoneCommand;
