using System.Runtime.InteropServices;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 1-byte <c>s2c_syncend</c> (<c>0x43</c>) packet signaling completion of the initial world synchronization burst.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SyncEndS2CPacket
{
    public const int Size = 1;

    public byte ProtocolType;

    public static SyncEndS2CPacket Create() => new()
    {
        ProtocolType = ProtocolOpcodes.s2c_syncend
    };
}
