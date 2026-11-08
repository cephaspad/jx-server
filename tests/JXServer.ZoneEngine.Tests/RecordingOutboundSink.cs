using JXServer.Contracts.Characters;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.Tests;

internal sealed class RecordingOutboundSink : IPlayerOutboundSink
{
    public int InitialSyncBurstCount { get; private set; }

    public int ActiveConfirmationCount { get; private set; }

    public List<uint> SentPings { get; } = [];

    public List<uint> SentPingReplies { get; } = [];

    public PlayerDetachReason? LastDisconnectReason { get; private set; }

    public void SendInitialWorldSyncBurst(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick,
        IReadOnlyList<CharacterSkillDto> skills) =>
        InitialSyncBurstCount++;

    public void SendActiveWorldConfirmation(PlayerZoneState playerState) =>
        ActiveConfirmationCount++;

    public void SendServerPing(uint serverTimestamp) =>
        SentPings.Add(serverTimestamp);

    public void SendServerPingReply(uint echoedClientTimestamp) =>
        SentPingReplies.Add(echoedClientTimestamp);

    public void Disconnect(PlayerDetachReason reason) =>
        LastDisconnectReason = reason;
}
