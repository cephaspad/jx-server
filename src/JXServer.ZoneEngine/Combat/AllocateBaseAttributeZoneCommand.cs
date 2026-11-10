using System;
using JXServer.Primitives.Combat;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Zone command enqueued when a client sends <c>0x57</c> (<c>c2s_playeraddbaseattribute</c>).
/// </summary>
public sealed record AllocateBaseAttributeZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    BaseAttributeKind Attribute,
    int PointsToAdd) : IZoneCommand;
