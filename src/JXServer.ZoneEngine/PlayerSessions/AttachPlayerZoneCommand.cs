using JXServer.Contracts.Characters;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Zone command dispatched upon valid <c>0x42</c> (<c>c2s_logiclogin</c>) ticket redemption to attach a player to a map zone.
/// </summary>
public sealed record AttachPlayerZoneCommand(
    CharacterDto Character,
    long LeaseEpoch,
    IPlayerOutboundSink OutboundSink) : IZoneCommand;
