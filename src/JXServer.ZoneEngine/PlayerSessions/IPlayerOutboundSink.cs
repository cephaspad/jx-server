using JXServer.Contracts.Characters;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Outbound wire sink implemented by the transport adapter (<c>GameServerClientSession</c>) so the 18-Hz
/// <c>MapZoneWorker</c> can emit initial world sync bursts, active confirmations, and periodic pings
/// without depending on wire-framing details.
/// </summary>
public interface IPlayerOutboundSink
{
    /// <summary>
    /// Sends the complete initial world and character synchronization burst (`0x49`, `0x44`, `0x45`, `0x46`, `0x4C`, `0xA4`, `0x43`).
    /// </summary>
    void SendInitialWorldSyncBurst(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick,
        IReadOnlyList<CharacterSkillDto> skills);

    /// <summary>
    /// Sends the active in-world confirmation packets (`0x46` <c>SyncCurPlayerNormal</c> and `0x4E` <c>SyncNpcMinPlayer</c>)
    /// after `0x43` <c>c2s_syncend</c> completes.
    /// </summary>
    void SendActiveWorldConfirmation(PlayerZoneState playerState);

    /// <summary>
    /// Sends a periodic `0x82` <c>s2c_ping</c> packet carrying the server timestamp.
    /// </summary>
    void SendServerPing(uint serverTimestamp);

    /// <summary>
    /// Sends a `0x99` <c>s2c_pingreply</c> packet echoing the client's timestamp from `0x70`.
    /// </summary>
    void SendServerPingReply(uint echoedClientTimestamp);

    /// <summary>
    /// Closes the underlying transport session (e.g., on heartbeat timeout or lease epoch eviction).
    /// </summary>
    void Disconnect(PlayerDetachReason reason);
}
