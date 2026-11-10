using System;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Enqueued when a client sends <c>0x59</c> (<c>c2s_playereatitem</c>) or <c>0x87</c> sub-command <c>0x02</c> (`FR-013`).
/// </summary>
public sealed record UseInventoryItemZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    uint RuntimeItemId,
    ItemContainerPlace Place,
    byte GridX,
    byte GridY,
    bool IsEmergencyTownPortal = false) : IZoneCommand;
