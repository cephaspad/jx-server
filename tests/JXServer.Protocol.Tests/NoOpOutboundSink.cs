using JXServer.Contracts.Characters;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.Protocol.Tests;

internal sealed class NoOpOutboundSink : IPlayerOutboundSink
{
    public void SendInitialWorldSyncBurst(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick,
        IReadOnlyList<CharacterSkillDto> skills)
    {
    }

    public void SendActiveWorldConfirmation(PlayerZoneState playerState)
    {
    }

    public void SendServerPing(uint serverTimestamp)
    {
    }

    public void SendServerPingReply(uint echoedClientTimestamp)
    {
    }

    public void Disconnect(PlayerDetachReason reason)
    {
    }
}
