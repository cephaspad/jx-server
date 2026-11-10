using System;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Enqueued when a client sends <c>0x4A</c> (<c>c2s_requestobj</c>) or <c>0x72</c> (<c>c2s_objmouseclick</c>) (`FR-011`).
/// </summary>
public sealed record RequestGroundObjectSyncZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    int ObjId) : IZoneCommand;
