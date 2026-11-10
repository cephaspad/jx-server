using JXServer.Contracts.Characters;
using JXServer.Primitives.Items;
using JXServer.Primitives.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Outbound wire sink implemented by the transport adapter (<c>GameServerClientSession</c>) so the 18-Hz
/// <c>MapZoneWorker</c> can emit initial world sync bursts, AOI neighbor updates, movement broadcasts,
/// map transitions, chat messages, and periodic pings without depending on wire-framing details.
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
    /// Sends a full entity synchronization packet (`0x4C` <c>s2c_syncnpc</c>) for a player or static NPC (`FR-002`, `FR-003`).
    /// </summary>
    void SendNpcFullSync(in EntityFullSyncSnapshot snapshot)
    {
    }

    /// <summary>
    /// Sends a compact 18-byte neighbor position synchronization packet (`0x4D` <c>s2c_syncnpcmin</c>) (`FR-003`).
    /// </summary>
    void SendNpcCompactSync(
        uint npcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte lifePer,
        byte camp,
        byte dir)
    {
    }

    /// <summary>
    /// Sends a 13-byte self position synchronization packet (`0x4E` <c>s2c_syncnpcminplayer</c>) (`FR-003`).
    /// </summary>
    void SendSelfPositionSync(PlayerZoneState playerState)
    {
    }

    /// <summary>
    /// Sends a 14-byte normal vital synchronization packet (`0x46` <c>s2c_synccurplayernormal</c>) (`FR-007`).
    /// </summary>
    void SendNormalVitalSync(PlayerZoneState playerState)
    {
    }

    /// <summary>
    /// Sends a 13-byte walk trajectory broadcast (`0x50` <c>s2c_npcwalk</c>) (`FR-006`).
    /// </summary>
    void SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
    }

    /// <summary>
    /// Sends a 13-byte run trajectory broadcast (`0x51` <c>s2c_npcrun</c>) (`FR-006`).
    /// </summary>
    void SendRunBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
    }

    /// <summary>
    /// Sends a 13-byte jump trajectory broadcast (`0x54` <c>s2c_npcjump</c>) (`FR-007`).
    /// </summary>
    void SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
    }

    /// <summary>
    /// Sends a 5-byte sit posture broadcast (`0x53` <c>s2c_npcsit</c>) (`FR-007`).
    /// </summary>
    void SendSitBroadcast(uint npcId)
    {
    }

    /// <summary>
    /// Sends a same-server map transition burst (`0x49` <c>s2c_syncworld</c> + `0x4E` <c>s2c_syncnpcminplayer</c> without `0x43`) (`FR-009`).
    /// </summary>
    void SendWorldMapTransitionSync(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick)
    {
    }

    /// <summary>
    /// Sends a cross-server exchange notification (`0x35` <c>s2c_notifyplayerexchange</c>, 25 bytes) (`FR-009`).
    /// </summary>
    void SendNotifyServerExchange(
        Guid handoffTicketGuid,
        uint ipAddressLittleEndian,
        ushort port)
    {
    }

    /// <summary>
    /// Sends a nearby spatial talk broadcast (`0x5B` <c>s2c_playertalk</c>) (`FR-010`).
    /// </summary>
    void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8)
    {
    }

    /// <summary>
    /// Sends a formatted multi-channel chat packet (`0x7E` <c>s2c_chatgetsomeone</c>) (`FR-011`).
    /// </summary>
    void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8)
    {
    }

    /// <summary>
    /// Sends a server system announcement (`0xFB` <c>s2c_extend</c>) (`FR-011`).
    /// </summary>
    void SendSystemMessage(string messageUtf8)
    {
    }

    /// <summary>
    /// Sends a 25-byte skill cast broadcast (`0x5A` <c>s2c_skillcast</c> or `0x85` <c>s2c_castskilldirectly</c>) (`FR-002`).
    /// </summary>
    void SendSkillCastBroadcast(
        uint casterNpcId,
        uint skillId,
        uint skillLevel,
        int targetMpsX,
        int targetMpsYOrNpcId,
        bool castDirectly = false)
    {
    }

    /// <summary>
    /// Sends a 22-byte hurt animation broadcast (`0x56` <c>s2c_broadcastnetcommand</c> with <c>CmdKind = 9</c> <c>do_hurt</c>) (`FR-004`).
    /// </summary>
    void SendHurtBroadcast(uint npcId, int mpsX, int mpsY, int hurtFrames)
    {
    }

    /// <summary>
    /// Sends a 2-byte normal PK mode synchronization (`0x90` <c>s2c_pksyncnormalflag</c>) (`FR-005`).
    /// </summary>
    void SendPkNormalFlagSync(byte pkMode)
    {
    }

    /// <summary>
    /// Sends a 5-byte entity death broadcast (`0x57` <c>s2c_npcdeath</c>) (`FR-007`).
    /// </summary>
    void SendNpcDeathBroadcast(uint npcId)
    {
    }

    /// <summary>
    /// Sends a 6-byte entity revival broadcast (`0x89` <c>s2c_playerrevive</c>) (`FR-009`).
    /// </summary>
    void SendNpcReviveBroadcast(uint npcId, byte reviveType = 0)
    {
    }

    /// <summary>
    /// Sends a 5-byte experience total synchronization (`0x5C` <c>s2c_playerexp</c>) (`FR-007`).
    /// </summary>
    void SendPlayerExpSync(int currentExp)
    {
    }

    /// <summary>
    /// Sends a 17-byte player level-up synchronization (`0x5B` <c>s2c_playerlevelup</c>) (`FR-007`).
    /// </summary>
    void SendPlayerLevelUpSync(int newLevel, int unallocatedAttrPoints, int unallocatedSkillPoints, int currentExp)
    {
    }

    /// <summary>
    /// Sends a 14-byte player base attribute synchronization (`0x5D` <c>s2c_playerattributesync</c>) (`FR-008`).
    /// </summary>
    void SendPlayerAttributeSync(byte attributeKind, int baseValue, int currentValue, int unallocatedAttrPoints)
    {
    }

    /// <summary>
    /// Sends a 15-byte player skill level synchronization (`0x5E` <c>s2c_playerskilllevel</c>) (`FR-008`).
    /// </summary>
    void SendPlayerSkillLevelSync(int skillId, int skillLevel, int skillExp, short unallocatedSkillPoints)
    {
    }

    /// <summary>
    /// Sends a variable-length ground object addition packet (`0x64` <c>s2c_objadd</c>) (`FR-011`).
    /// </summary>
    void SendGroundObjectAdd(in GroundObjectSyncSnapshot snapshot)
    {
    }

    /// <summary>
    /// Sends a 6-byte ground object removal packet (`0x67` <c>s2c_objremove</c>) (`FR-011`).
    /// </summary>
    void SendGroundObjectRemove(int objId, byte reason)
    {
    }

    /// <summary>
    /// Sends an 80-byte inventory item synchronization packet (`0x5F` <c>s2c_syncitem</c>) (`FR-012`).
    /// </summary>
    void SendItemSync(in PlayerItemSyncSnapshot snapshot)
    {
    }

    /// <summary>
    /// Sends a 5-byte inventory item removal packet (`0x60` <c>s2c_removeitem</c>) (`FR-012`, `FR-013`).
    /// </summary>
    void SendItemRemove(uint runtimeItemId)
    {
    }

    /// <summary>
    /// Sends a 13-byte player money synchronization packet (`0x68` <c>s2c_syncmoney</c>) (`FR-012`).
    /// </summary>
    void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0)
    {
    }

    /// <summary>
    /// Sends a 7-byte inventory item move confirmation packet (`0x61` <c>s2c_playermoveitem</c>) (`FR-012`).
    /// </summary>
    void SendPlayerMoveItemConfirm(
        byte downY,
        ItemContainerPlace downPos,
        byte downX,
        byte upY,
        byte upX,
        ItemContainerPlace upPos)
    {
    }

    /// <summary>
    /// Sends a 1-byte inventory drag unlock packet (`0x8E` <c>s2c_itemautomove</c>) (`FR-012`).
    /// </summary>
    void SendItemAutoMoveUnlock()
    {
    }

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
