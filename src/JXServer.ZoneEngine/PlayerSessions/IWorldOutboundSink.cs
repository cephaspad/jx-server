using JXServer.Contracts.Characters;
using JXServer.Primitives.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Outbound wire port for world synchronization, spatial AOI entity visibility, movement/posture trajectories,
/// map transitions, server heartbeats, and transport disconnection (<c>FR-010</c>).
/// </summary>
public interface IWorldOutboundSink
{
    void SendInitialWorldSyncBurst(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick,
        IReadOnlyList<CharacterSkillDto> skills);

    void SendActiveWorldConfirmation(PlayerZoneState playerState);

    void SendNpcFullSync(in EntityFullSyncSnapshot snapshot) { }

    void SendNpcCompactSync(
        uint npcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte lifePer,
        byte camp,
        byte dir) { }

    void SendSelfPositionSync(PlayerZoneState playerState) { }

    void SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY) { }

    void SendRunBroadcast(uint npcId, int destMpsX, int destMpsY) { }

    void SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY) { }

    void SendSitBroadcast(uint npcId) { }

    void SendWorldMapTransitionSync(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick) { }

    void SendNotifyServerExchange(
        Guid handoffTicketGuid,
        uint ipAddressLittleEndian,
        ushort port) { }

    void SendServerPing(uint serverTimestamp);

    void SendServerPingReply(uint echoedClientTimestamp);

    void Disconnect(PlayerDetachReason reason);
}
