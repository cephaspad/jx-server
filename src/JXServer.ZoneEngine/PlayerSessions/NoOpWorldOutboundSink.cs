using JXServer.Contracts.Characters;
using JXServer.Primitives.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Allocation-free singleton fallback when <see cref="IWorldOutboundSink"/> is unsubscribed (<c>FR-011</c>).
/// </summary>
public sealed class NoOpWorldOutboundSink : IWorldOutboundSink
{
    public static NoOpWorldOutboundSink Instance { get; } = new();

    private NoOpWorldOutboundSink()
    {
    }

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

    public void SendNpcFullSync(in EntityFullSyncSnapshot snapshot)
    {
    }

    public void SendNpcCompactSync(
        uint npcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte lifePer,
        byte camp,
        byte dir)
    {
    }

    public void SendSelfPositionSync(PlayerZoneState playerState)
    {
    }

    public void SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
    }

    public void SendRunBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
    }

    public void SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
    }

    public void SendSitBroadcast(uint npcId)
    {
    }

    public void SendWorldMapTransitionSync(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick)
    {
    }

    public void SendNotifyServerExchange(
        Guid handoffTicketGuid,
        uint ipAddressLittleEndian,
        ushort port)
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
