using System;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Enqueued when a client sends <c>0x5B</c> (<c>c2s_playermoveitem</c>) (`FR-012`).
/// </summary>
public sealed record MoveInventoryItemZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    ItemContainerPlace SrcPlace,
    byte SrcGridX,
    byte SrcGridY,
    ItemContainerPlace DestPlace,
    byte DestGridX,
    byte DestGridY) : IZoneCommand;
