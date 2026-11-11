using System.Diagnostics.CodeAnalysis;
using JXServer.Contracts.Characters;
using JXServer.Primitives.Items;
using JXServer.Primitives.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Wraps one or more core or custom-module domain sink doubles into an <see cref="IPlayerOutboundSink"/> while safely
/// discarding notifications for unsubscribed domains via singleton no-op sinks (<c>FR-011</c>, <c>SC-005</c>).
/// </summary>
public sealed class SelectivePlayerOutboundSink : IPlayerOutboundSink
{
    private readonly IWorldOutboundSink _worldSink;
    private readonly ICombatOutboundSink _combatSink;
    private readonly IInventoryOutboundSink _inventorySink;
    private readonly IChatOutboundSink _chatSink;
    private readonly Dictionary<Type, object> _customSinks = [];

    public SelectivePlayerOutboundSink(
        IWorldOutboundSink? worldSink = null,
        ICombatOutboundSink? combatSink = null,
        IInventoryOutboundSink? inventorySink = null,
        IChatOutboundSink? chatSink = null,
        params object[] customSinks)
    {
        _worldSink = worldSink ?? NoOpWorldOutboundSink.Instance;
        _combatSink = combatSink ?? NoOpCombatOutboundSink.Instance;
        _inventorySink = inventorySink ?? NoOpInventoryOutboundSink.Instance;
        _chatSink = chatSink ?? NoOpChatOutboundSink.Instance;

        if (customSinks is { Length: > 0 })
        {
            foreach (object customSink in customSinks)
            {
                ArgumentNullException.ThrowIfNull(customSink);
                _customSinks[customSink.GetType()] = customSink;
            }
        }
    }

    public static SelectivePlayerOutboundSink ForWorld(IWorldOutboundSink sink) => new(worldSink: sink);
    public static SelectivePlayerOutboundSink ForCombat(ICombatOutboundSink sink) => new(combatSink: sink);
    public static SelectivePlayerOutboundSink ForInventory(IInventoryOutboundSink sink) => new(inventorySink: sink);
    public static SelectivePlayerOutboundSink ForChat(IChatOutboundSink sink) => new(chatSink: sink);
    public static SelectivePlayerOutboundSink ForCustom<TSink>(TSink sink) where TSink : class =>
        new SelectivePlayerOutboundSink().RegisterCustomSink(sink);

    public SelectivePlayerOutboundSink RegisterCustomSink<TSink>(TSink sink)
        where TSink : class
    {
        ArgumentNullException.ThrowIfNull(sink);
        _customSinks[typeof(TSink)] = sink;
        return this;
    }

    public bool TryGetOutboundSink<TSink>([NotNullWhen(true)] out TSink? sink)
        where TSink : class
    {
        if (typeof(TSink) == typeof(IWorldOutboundSink))
        {
            sink = _worldSink as TSink;
            return sink is not null;
        }

        if (typeof(TSink) == typeof(ICombatOutboundSink))
        {
            sink = _combatSink as TSink;
            return sink is not null;
        }

        if (typeof(TSink) == typeof(IInventoryOutboundSink))
        {
            sink = _inventorySink as TSink;
            return sink is not null;
        }

        if (typeof(TSink) == typeof(IChatOutboundSink))
        {
            sink = _chatSink as TSink;
            return sink is not null;
        }

        if (_customSinks.TryGetValue(typeof(TSink), out object? exact))
        {
            sink = (TSink)exact;
            return true;
        }

        foreach (object candidate in _customSinks.Values)
        {
            if (candidate is TSink matched)
            {
                sink = matched;
                return true;
            }
        }

        sink = null;
        return false;
    }

    public void SendInitialWorldSyncBurst(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick,
        IReadOnlyList<CharacterSkillDto> skills) =>
        _worldSink.SendInitialWorldSyncBurst(playerState, mapNameUnicode, currentFrameTick, skills);

    public void SendActiveWorldConfirmation(PlayerZoneState playerState) =>
        _worldSink.SendActiveWorldConfirmation(playerState);

    public void SendNpcFullSync(in EntityFullSyncSnapshot snapshot) =>
        _worldSink.SendNpcFullSync(snapshot);

    public void SendNpcCompactSync(
        uint npcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte lifePer,
        byte camp,
        byte dir) =>
        _worldSink.SendNpcCompactSync(npcId, mpsX, mpsY, doing, lifePer, camp, dir);

    public void SendSelfPositionSync(PlayerZoneState playerState) =>
        _worldSink.SendSelfPositionSync(playerState);

    public void SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY) =>
        _worldSink.SendWalkBroadcast(npcId, destMpsX, destMpsY);

    public void SendRunBroadcast(uint npcId, int destMpsX, int destMpsY) =>
        _worldSink.SendRunBroadcast(npcId, destMpsX, destMpsY);

    public void SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY) =>
        _worldSink.SendJumpBroadcast(npcId, destMpsX, destMpsY);

    public void SendSitBroadcast(uint npcId) =>
        _worldSink.SendSitBroadcast(npcId);

    public void SendWorldMapTransitionSync(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick) =>
        _worldSink.SendWorldMapTransitionSync(playerState, mapNameUnicode, currentFrameTick);

    public void SendNotifyServerExchange(
        Guid handoffTicketGuid,
        uint ipAddressLittleEndian,
        ushort port) =>
        _worldSink.SendNotifyServerExchange(handoffTicketGuid, ipAddressLittleEndian, port);

    public void SendServerPing(uint serverTimestamp) =>
        _worldSink.SendServerPing(serverTimestamp);

    public void SendServerPingReply(uint echoedClientTimestamp) =>
        _worldSink.SendServerPingReply(echoedClientTimestamp);

    public void Disconnect(PlayerDetachReason reason) =>
        _worldSink.Disconnect(reason);

    public void SendNormalVitalSync(PlayerZoneState playerState) =>
        _combatSink.SendNormalVitalSync(playerState);

    public void SendSkillCastBroadcast(
        uint casterNpcId,
        uint skillId,
        uint skillLevel,
        int targetMpsX,
        int targetMpsYOrNpcId,
        bool castDirectly = false) =>
        _combatSink.SendSkillCastBroadcast(casterNpcId, skillId, skillLevel, targetMpsX, targetMpsYOrNpcId, castDirectly);

    public void SendHurtBroadcast(uint npcId, int mpsX, int mpsY, int hurtFrames) =>
        _combatSink.SendHurtBroadcast(npcId, mpsX, mpsY, hurtFrames);

    public void SendPkNormalFlagSync(byte pkMode) =>
        _combatSink.SendPkNormalFlagSync(pkMode);

    public void SendNpcDeathBroadcast(uint npcId) =>
        _combatSink.SendNpcDeathBroadcast(npcId);

    public void SendNpcReviveBroadcast(uint npcId, byte reviveType = 0) =>
        _combatSink.SendNpcReviveBroadcast(npcId, reviveType);

    public void SendPlayerExpSync(int currentExp) =>
        _combatSink.SendPlayerExpSync(currentExp);

    public void SendPlayerLevelUpSync(
        int newLevel,
        int unallocatedAttrPoints,
        int unallocatedSkillPoints,
        int currentExp) =>
        _combatSink.SendPlayerLevelUpSync(newLevel, unallocatedAttrPoints, unallocatedSkillPoints, currentExp);

    public void SendPlayerAttributeSync(
        byte attributeKind,
        int baseValue,
        int currentValue,
        int unallocatedAttrPoints) =>
        _combatSink.SendPlayerAttributeSync(attributeKind, baseValue, currentValue, unallocatedAttrPoints);

    public void SendPlayerSkillLevelSync(
        int skillId,
        int skillLevel,
        int skillExp,
        short unallocatedSkillPoints) =>
        _combatSink.SendPlayerSkillLevelSync(skillId, skillLevel, skillExp, unallocatedSkillPoints);

    public void SendGroundObjectAdd(in GroundObjectSyncSnapshot snapshot) =>
        _inventorySink.SendGroundObjectAdd(snapshot);

    public void SendGroundObjectRemove(int objId, byte reason) =>
        _inventorySink.SendGroundObjectRemove(objId, reason);

    public void SendItemSync(in PlayerItemSyncSnapshot snapshot) =>
        _inventorySink.SendItemSync(snapshot);

    public void SendItemRemove(uint runtimeItemId) =>
        _inventorySink.SendItemRemove(runtimeItemId);

    public void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0) =>
        _inventorySink.SendMoneySync(inventoryMoney, repositoryMoney, tradeMoney);

    public void SendPlayerMoveItemConfirm(
        byte downY,
        ItemContainerPlace downPos,
        byte downX,
        byte upY,
        byte upX,
        ItemContainerPlace upPos) =>
        _inventorySink.SendPlayerMoveItemConfirm(downY, downPos, downX, upY, upX, upPos);

    public void SendItemAutoMoveUnlock() =>
        _inventorySink.SendItemAutoMoveUnlock();

    public void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8) =>
        _chatSink.SendPlayerTalkBroadcast(talkerNpcId, messageUtf8);

    public void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8) =>
        _chatSink.SendChatMessage(senderNpcId, senderNameUtf8, messageUtf8);

    public void SendSystemMessage(string messageUtf8) =>
        _chatSink.SendSystemMessage(messageUtf8);
}
