using System.Diagnostics.CodeAnalysis;
using JXServer.Contracts.Characters;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Chats;
using JXServer.Primitives.Items;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

/// <summary>
/// Thread-safe or single-threaded outbound event recorder implementing <see cref="IPlayerOutboundSink"/> (<c>FR-009</c>, <c>FR-011</c>, <c>SC-005</c>).
/// Automatically stamps recorded events with the current harness simulation frame and functional domain.
/// </summary>
public sealed class PlayerOutboundEventRecorder : IPlayerOutboundSink
{
    private readonly Func<long> _currentFrameProvider;
    private readonly List<RecordedOutboundEvent> _events = [];
    private readonly Dictionary<Type, object> _customSinks = [];
    private readonly object _lock = new();

    public IReadOnlyList<RecordedOutboundEvent> Events
    {
        get
        {
            lock (_lock)
            {
                return _events.ToArray();
            }
        }
    }

    public PlayerOutboundEventRecorder(Func<long>? currentFrameProvider = null)
    {
        _currentFrameProvider = currentFrameProvider ?? (() => 0);
    }

    public IReadOnlyList<RecordedOutboundEvent> GetEventsByDomain(OutboundNotificationDomain domain)
    {
        lock (_lock)
        {
            var list = new List<RecordedOutboundEvent>();
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Domain == domain)
                {
                    list.Add(_events[i]);
                }
            }
            return list;
        }
    }

    public PlayerOutboundEventRecorder RegisterCustomSink<TSink>(TSink customSink)
        where TSink : class
    {
        ArgumentNullException.ThrowIfNull(customSink);
        lock (_lock)
        {
            _customSinks[typeof(TSink)] = customSink;
        }
        return this;
    }

    public void RecordCustomEvent(string methodName, params object?[] arguments)
    {
        Record(OutboundNotificationDomain.CustomModule, methodName, arguments);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }

    public bool TryGetOutboundSink<TSink>([NotNullWhen(true)] out TSink? sink)
        where TSink : class
    {
        if (this is TSink typedSelf)
        {
            sink = typedSelf;
            return true;
        }

        lock (_lock)
        {
            if (_customSinks.TryGetValue(typeof(TSink), out object? custom))
            {
                sink = (TSink)custom;
                return true;
            }
        }

        sink = null;
        return false;
    }

    private void Record(OutboundNotificationDomain domain, string methodName, params object?[] arguments)
    {
        long frame = _currentFrameProvider();
        lock (_lock)
        {
            _events.Add(new RecordedOutboundEvent(frame, domain, methodName, arguments));
        }
    }

    #region IWorldOutboundSink
    public void SendInitialWorldSyncBurst(PlayerZoneState playerState, string mapNameUnicode, uint currentFrameTick, IReadOnlyList<CharacterSkillDto> skills) =>
        Record(OutboundNotificationDomain.World, nameof(SendInitialWorldSyncBurst), playerState, mapNameUnicode, currentFrameTick, skills);

    public void SendActiveWorldConfirmation(PlayerZoneState playerState) =>
        Record(OutboundNotificationDomain.World, nameof(SendActiveWorldConfirmation), playerState);

    public void SendNpcFullSync(in EntityFullSyncSnapshot snapshot) =>
        Record(OutboundNotificationDomain.World, nameof(SendNpcFullSync), snapshot);

    public void SendNpcCompactSync(uint npcId, int mpsX, int mpsY, EntityDoingPosture doing, byte lifePer, byte camp, byte dir) =>
        Record(OutboundNotificationDomain.World, nameof(SendNpcCompactSync), npcId, mpsX, mpsY, doing, lifePer, camp, dir);

    public void SendSelfPositionSync(PlayerZoneState playerState) =>
        Record(OutboundNotificationDomain.World, nameof(SendSelfPositionSync), playerState);

    public void SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY) =>
        Record(OutboundNotificationDomain.World, nameof(SendWalkBroadcast), npcId, destMpsX, destMpsY);

    public void SendRunBroadcast(uint npcId, int destMpsX, int destMpsY) =>
        Record(OutboundNotificationDomain.World, nameof(SendRunBroadcast), npcId, destMpsX, destMpsY);

    public void SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY) =>
        Record(OutboundNotificationDomain.World, nameof(SendJumpBroadcast), npcId, destMpsX, destMpsY);

    public void SendSitBroadcast(uint npcId) =>
        Record(OutboundNotificationDomain.World, nameof(SendSitBroadcast), npcId);

    public void SendWorldMapTransitionSync(PlayerZoneState playerState, string mapNameUnicode, uint currentFrameTick) =>
        Record(OutboundNotificationDomain.World, nameof(SendWorldMapTransitionSync), playerState, mapNameUnicode, currentFrameTick);

    public void SendNotifyServerExchange(Guid handoffTicketGuid, uint ipAddressLittleEndian, ushort port) =>
        Record(OutboundNotificationDomain.World, nameof(SendNotifyServerExchange), handoffTicketGuid, ipAddressLittleEndian, port);

    public void SendServerPing(uint serverTimestamp) =>
        Record(OutboundNotificationDomain.World, nameof(SendServerPing), serverTimestamp);

    public void SendServerPingReply(uint echoedClientTimestamp) =>
        Record(OutboundNotificationDomain.World, nameof(SendServerPingReply), echoedClientTimestamp);

    public void Disconnect(PlayerDetachReason reason) =>
        Record(OutboundNotificationDomain.World, nameof(Disconnect), reason);
    #endregion

    #region ICombatOutboundSink
    public void SendNormalVitalSync(PlayerZoneState playerState) =>
        Record(OutboundNotificationDomain.Combat, nameof(SendNormalVitalSync), playerState);

    public void SendSkillCastBroadcast(uint casterNpcId, uint skillId, uint skillLevel, int targetMpsX, int targetMpsYOrNpcId, bool castDirectly = false) =>
        Record(OutboundNotificationDomain.Combat, nameof(SendSkillCastBroadcast), casterNpcId, skillId, skillLevel, targetMpsX, targetMpsYOrNpcId, castDirectly);

    public void SendHurtBroadcast(uint npcId, int mpsX, int mpsY, int hurtFrames) =>
        Record(OutboundNotificationDomain.Combat, nameof(SendHurtBroadcast), npcId, mpsX, mpsY, hurtFrames);

    public void SendPkNormalFlagSync(byte pkMode) =>
        Record(OutboundNotificationDomain.Combat, nameof(SendPkNormalFlagSync), pkMode);

    public void SendNpcDeathBroadcast(uint npcId) =>
        Record(OutboundNotificationDomain.Combat, nameof(SendNpcDeathBroadcast), npcId);

    public void SendNpcReviveBroadcast(uint npcId, byte reviveType = 0) =>
        Record(OutboundNotificationDomain.Combat, nameof(SendNpcReviveBroadcast), npcId, reviveType);

    public void SendPlayerExpSync(int currentExp) =>
        Record(OutboundNotificationDomain.Combat, nameof(SendPlayerExpSync), currentExp);

    public void SendPlayerLevelUpSync(int newLevel, int unallocatedAttrPoints, int unallocatedSkillPoints, int currentExp) =>
        Record(OutboundNotificationDomain.Combat, nameof(SendPlayerLevelUpSync), newLevel, unallocatedAttrPoints, unallocatedSkillPoints, currentExp);

    public void SendPlayerAttributeSync(byte attributeKind, int baseValue, int currentValue, int unallocatedAttrPoints) =>
        Record(OutboundNotificationDomain.Combat, nameof(SendPlayerAttributeSync), attributeKind, baseValue, currentValue, unallocatedAttrPoints);

    public void SendPlayerSkillLevelSync(int skillId, int skillLevel, int skillExp, short unallocatedSkillPoints) =>
        Record(OutboundNotificationDomain.Combat, nameof(SendPlayerSkillLevelSync), skillId, skillLevel, skillExp, unallocatedSkillPoints);
    #endregion

    #region IInventoryOutboundSink
    public void SendGroundObjectAdd(in GroundObjectSyncSnapshot snapshot) =>
        Record(OutboundNotificationDomain.Inventory, nameof(SendGroundObjectAdd), snapshot);

    public void SendGroundObjectRemove(int objId, byte reason) =>
        Record(OutboundNotificationDomain.Inventory, nameof(SendGroundObjectRemove), objId, reason);

    public void SendItemSync(in PlayerItemSyncSnapshot snapshot) =>
        Record(OutboundNotificationDomain.Inventory, nameof(SendItemSync), snapshot);

    public void SendItemRemove(uint runtimeItemId) =>
        Record(OutboundNotificationDomain.Inventory, nameof(SendItemRemove), runtimeItemId);

    public void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0) =>
        Record(OutboundNotificationDomain.Inventory, nameof(SendMoneySync), inventoryMoney, repositoryMoney, tradeMoney);

    public void SendPlayerMoveItemConfirm(byte downY, ItemContainerPlace downPos, byte downX, byte upY, byte upX, ItemContainerPlace upPos) =>
        Record(OutboundNotificationDomain.Inventory, nameof(SendPlayerMoveItemConfirm), downY, downPos, downX, upY, upX, upPos);

    public void SendItemAutoMoveUnlock() =>
        Record(OutboundNotificationDomain.Inventory, nameof(SendItemAutoMoveUnlock));
    #endregion

    #region IChatOutboundSink
    public void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8) =>
        Record(OutboundNotificationDomain.Chat, nameof(SendPlayerTalkBroadcast), talkerNpcId, messageUtf8);

    public void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8) =>
        Record(OutboundNotificationDomain.Chat, nameof(SendChatMessage), senderNpcId, senderNameUtf8, messageUtf8);

    public void SendSystemMessage(string messageUtf8) =>
        Record(OutboundNotificationDomain.Chat, nameof(SendSystemMessage), messageUtf8);
    #endregion
}
