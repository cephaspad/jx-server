namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles a specific incoming GameServer C2S packet opcode on a <see cref="GameServerClientSession"/>.
/// </summary>
public interface IGameServerPacketHandler
{
    byte Opcode { get; }

    ValueTask<bool> HandleAsync(
        GameServerClientSession session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default);
}
