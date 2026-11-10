using JXServer.Contracts.Characters;
using JXServer.Primitives.Items;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.Tests;

internal sealed class RecordingOutboundSink : IPlayerOutboundSink
{
    public bool CountOnly { get; init; }

    public int TotalEventsSent { get; private set; }

    public int InitialSyncBurstCount { get; private set; }

    public int ActiveConfirmationCount { get; private set; }

    public List<uint> SentPings { get; } = [];

    public List<uint> SentPingReplies { get; } = [];

    public List<EntityFullSyncSnapshot> FullSyncs { get; } = [];

    public List<(uint NpcId, int MpsX, int MpsY, EntityDoingPosture Doing, byte LifePercent, byte Camp, byte IsPlayerFlag)> CompactSyncs { get; } = [];

    public List<(ushort MapId, int MpsX, int MpsY, byte FightState)> SelfPositionSyncs { get; } = [];

    public List<(int CurrentLife, int CurrentMana, int CurrentStamina)> NormalVitalSyncs { get; } = [];

    public List<(uint NpcId, int DestMpsX, int DestMpsY)> WalkBroadcasts { get; } = [];

    public List<(uint NpcId, int DestMpsX, int DestMpsY)> RunBroadcasts { get; } = [];

    public List<(uint NpcId, int DestMpsX, int DestMpsY)> JumpBroadcasts { get; } = [];

    public List<uint> SitBroadcasts { get; } = [];

    public List<(ushort MapId, int MpsX, int MpsY, string MapName)> MapTransitionSyncs { get; } = [];

    public List<(Guid TicketGuid, uint IpAddressLe, ushort Port)> ServerExchangeNotifications { get; } = [];

    public List<(uint TalkerNpcId, string Message)> PlayerTalkBroadcasts { get; } = [];

    public List<(uint SenderNpcId, string SenderName, string Message)> ChatMessages { get; } = [];

    public List<string> SystemMessages { get; } = [];

    public List<(uint CasterNpcId, uint SkillId, uint SkillLevel, int TargetMpsX, int TargetMpsYOrNpcId, bool CastDirectly)> SkillCastBroadcasts { get; } = [];

    public List<(uint NpcId, int MpsX, int MpsY, int HurtFrames)> HurtBroadcasts { get; } = [];

    public List<byte> PkNormalFlagSyncs { get; } = [];

    public List<uint> NpcDeathBroadcasts { get; } = [];

    public List<(uint NpcId, byte ReviveType)> NpcReviveBroadcasts { get; } = [];

    public List<int> PlayerExpSyncs { get; } = [];

    public List<(int NewLevel, int UnallocatedAttrPoints, int UnallocatedSkillPoints, int CurrentExp)> PlayerLevelUpSyncs { get; } = [];

    public List<(byte AttributeKind, int BaseValue, int CurrentValue, int UnallocatedAttrPoints)> PlayerAttributeSyncs { get; } = [];

    public List<(int SkillId, int SkillLevel, int SkillExp, short UnallocatedSkillPoints)> PlayerSkillLevelSyncs { get; } = [];

    public List<GroundObjectSyncSnapshot> GroundObjectAdds { get; } = [];

    public List<(int ObjId, byte Reason)> GroundObjectRemoves { get; } = [];

    public List<PlayerItemSyncSnapshot> ItemSyncs { get; } = [];

    public List<uint> ItemRemoves { get; } = [];

    public List<(int InventoryMoney, int RepositoryMoney, int TradeMoney)> MoneySyncs { get; } = [];

    public List<(byte DownY, ItemContainerPlace DownPos, byte DownX, byte UpY, byte UpX, ItemContainerPlace UpPos)> MoveItemConfirms { get; } = [];

    public int ItemAutoMoveUnlockCount { get; private set; }

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

    public void SendNpcFullSync(in EntityFullSyncSnapshot snapshot)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            FullSyncs.Add(snapshot);
        }
    }

    public void SendNpcCompactSync(
        uint npcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte currentLifePercent,
        byte camp,
        byte isPlayerFlag)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            CompactSyncs.Add((npcId, mpsX, mpsY, doing, currentLifePercent, camp, isPlayerFlag));
        }
    }

    public void SendSelfPositionSync(PlayerZoneState playerState)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            SelfPositionSyncs.Add((playerState.MapId, playerState.CurrentMpsX, playerState.CurrentMpsY, playerState.FightMode));
        }
    }

    public void SendNormalVitalSync(PlayerZoneState playerState) =>
        NormalVitalSyncs.Add((playerState.CurrentLife, playerState.CurrentMana, playerState.CurrentStamina));

    public void SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY) =>
        WalkBroadcasts.Add((npcId, destMpsX, destMpsY));

    public void SendRunBroadcast(uint npcId, int destMpsX, int destMpsY) =>
        RunBroadcasts.Add((npcId, destMpsX, destMpsY));

    public void SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY) =>
        JumpBroadcasts.Add((npcId, destMpsX, destMpsY));

    public void SendSitBroadcast(uint npcId) =>
        SitBroadcasts.Add(npcId);

    public void SendWorldMapTransitionSync(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick) =>
        MapTransitionSyncs.Add((playerState.MapId, playerState.CurrentMpsX, playerState.CurrentMpsY, mapNameUnicode));

    public void SendNotifyServerExchange(Guid handoffTicketGuid, uint ipAddressLittleEndian, ushort port) =>
        ServerExchangeNotifications.Add((handoffTicketGuid, ipAddressLittleEndian, port));

    public void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8) =>
        PlayerTalkBroadcasts.Add((talkerNpcId, messageUtf8));

    public void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8) =>
        ChatMessages.Add((senderNpcId, senderNameUtf8, messageUtf8));

    public void SendSystemMessage(string messageUtf8) =>
        SystemMessages.Add(messageUtf8);

    public void SendSkillCastBroadcast(
        uint casterNpcId,
        uint skillId,
        uint skillLevel,
        int targetMpsX,
        int targetMpsYOrNpcId,
        bool castDirectly = false)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            SkillCastBroadcasts.Add((casterNpcId, skillId, skillLevel, targetMpsX, targetMpsYOrNpcId, castDirectly));
        }
    }

    public void SendHurtBroadcast(uint npcId, int mpsX, int mpsY, int hurtFrames)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            HurtBroadcasts.Add((npcId, mpsX, mpsY, hurtFrames));
        }
    }

    public void SendPkNormalFlagSync(byte pkMode) =>
        PkNormalFlagSyncs.Add(pkMode);

    public void SendNpcDeathBroadcast(uint npcId)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            NpcDeathBroadcasts.Add(npcId);
        }
    }

    public void SendNpcReviveBroadcast(uint npcId, byte reviveType = 0)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            NpcReviveBroadcasts.Add((npcId, reviveType));
        }
    }

    public void SendPlayerExpSync(int currentExp) =>
        PlayerExpSyncs.Add(currentExp);

    public void SendPlayerLevelUpSync(int newLevel, int unallocatedAttrPoints, int unallocatedSkillPoints, int currentExp) =>
        PlayerLevelUpSyncs.Add((newLevel, unallocatedAttrPoints, unallocatedSkillPoints, currentExp));

    public void SendPlayerAttributeSync(byte attributeKind, int baseValue, int currentValue, int unallocatedAttrPoints) =>
        PlayerAttributeSyncs.Add((attributeKind, baseValue, currentValue, unallocatedAttrPoints));

    public void SendPlayerSkillLevelSync(int skillId, int skillLevel, int skillExp, short unallocatedSkillPoints) =>
        PlayerSkillLevelSyncs.Add((skillId, skillLevel, skillExp, unallocatedSkillPoints));

    public void SendGroundObjectAdd(in GroundObjectSyncSnapshot snapshot)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            GroundObjectAdds.Add(snapshot);
        }
    }

    public void SendGroundObjectRemove(int objId, byte reason)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            GroundObjectRemoves.Add((objId, reason));
        }
    }

    public void SendItemSync(in PlayerItemSyncSnapshot snapshot)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            ItemSyncs.Add(snapshot);
        }
    }

    public void SendItemRemove(uint runtimeItemId)
    {
        TotalEventsSent++;
        if (!CountOnly)
        {
            ItemRemoves.Add(runtimeItemId);
        }
    }

    public void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0) =>
        MoneySyncs.Add((inventoryMoney, repositoryMoney, tradeMoney));

    public void SendPlayerMoveItemConfirm(
        byte downY,
        ItemContainerPlace downPos,
        byte downX,
        byte upY,
        byte upX,
        ItemContainerPlace upPos) =>
        MoveItemConfirms.Add((downY, downPos, downX, upY, upX, upPos));

    public void SendItemAutoMoveUnlock() =>
        ItemAutoMoveUnlockCount++;

    public void Disconnect(PlayerDetachReason reason) =>
        LastDisconnectReason = reason;
}
