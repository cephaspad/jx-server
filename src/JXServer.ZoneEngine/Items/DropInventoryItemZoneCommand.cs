using System;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Enqueued when a client sends <c>0x5E</c> (<c>c2s_playerthrowawayitem</c>) or <c>0x83</c> (<c>c2s_ThrowAwayAllMedicine</c>) (`FR-012`).
/// </summary>
public sealed record DropInventoryItemZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    bool ThrowAllMedicine) : IZoneCommand;
