using System.Runtime.InteropServices;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 9-byte <c>c2s_syncend</c> (<c>0x43</c>) packet sent by the client in reply to 1-byte <see cref="SyncEndS2CPacket"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SyncEndC2SPacket
{
    public const int Size = 9;

    public byte ProtocolType;
    public uint ClientToken;
    public uint ClientTick;

    public static SyncEndC2SPacket Create(uint clientToken, uint clientTick) => new()
    {
        ProtocolType = ProtocolOpcodes.c2s_syncend,
        ClientToken = clientToken,
        ClientTick = clientTick
    };

    public static bool TryParse(ReadOnlySpan<byte> payload, out SyncEndC2SPacket packet)
    {
        if (payload.Length != Size || payload[0] != ProtocolOpcodes.c2s_syncend)
        {
            packet = default;
            return false;
        }

        packet = MemoryMarshal.Read<SyncEndC2SPacket>(payload);
        return true;
    }
}
