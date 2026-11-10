using JXServer.Primitives.Chats;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Chats;

/// <summary>
/// Zone command routed across active <c>MapZoneWorker</c> shards via <c>IZoneClusterRouter</c>
/// for World, Private/Whisper, Team, Faction, and System chat delivery (`FR-011`).
/// </summary>
public sealed record DeliverClusterChatZoneCommand(
    uint SenderNpcId,
    string SenderNameUtf8,
    ChatChannelKind Channel,
    uint TargetNpcId,
    string TargetRoleNameUtf8,
    int SenderFactionId,
    string MessageUtf8) : IZoneCommand;
