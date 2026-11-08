namespace JXServer.Adapter.LegacyJX1.Bishop;

/// <summary>
/// Handles a specific incoming Bishop Gateway C2S packet opcode on a <see cref="BishopClientSession"/>.
/// </summary>
public interface IBishopPacketHandler
{
    byte Opcode { get; }

    ValueTask<bool> HandleAsync(
        BishopClientSession session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default);
}
