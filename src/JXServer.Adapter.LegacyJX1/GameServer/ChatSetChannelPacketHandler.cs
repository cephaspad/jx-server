using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Chats;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_chatsetchannel</c> (<c>0x60</c>) and dispatches <see cref="UpdateChatChannelMaskZoneCommand"/> (`FR-013`).
/// </summary>
public sealed class ChatSetChannelPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_chatsetchannel;

    public ChatSetChannelPacketHandler(IZoneClusterRouter zoneClusterRouter)
    {
        _zoneClusterRouter = zoneClusterRouter;
    }

    public ValueTask<bool> HandleAsync(
        GameServerClientSession session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (!session.IsAttachedToWorld ||
            !session.CharacterId.HasValue ||
            !session.AttachedMapId.HasValue)
        {
            return ValueTask.FromResult(false);
        }

        if (!ChatSetChannelC2SPacket.TryParse(payload.Span, out ChatSetChannelC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new UpdateChatChannelMaskZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.SubscribedChannelMask));

        return ValueTask.FromResult(dispatched);
    }
}
