using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Chats;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_playersendchat</c> (<c>0x56</c>) and dispatches <see cref="SendStructuredChatZoneCommand"/> (`FR-012`, `FR-014`).
/// </summary>
public sealed class PlayerSendChatPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_playersendchat;

    public PlayerSendChatPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!PlayerSendChatC2SPacket.TryParse(payload.Span, out PlayerSendChatC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new SendStructuredChatZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.Channel,
                request.TargetNpcId,
                request.SentencePrefixUtf8,
                request.SentenceUtf8));

        return ValueTask.FromResult(dispatched);
    }
}
