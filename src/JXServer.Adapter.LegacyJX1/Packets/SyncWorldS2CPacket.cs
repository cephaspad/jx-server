using System.Runtime.InteropServices;
using JXServer.Primitives.Encodings;
using JXServer.Primitives.WorldZones;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 34-byte <c>s2c_syncworld</c> (<c>0x49</c>, <c>tagWORLD_SYNC</c>) packet.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct SyncWorldS2CPacket
{
    public const int Size = 34;

    public byte ProtocolType;
    public int SubWorldId;
    public int MapCopyIndex;
    public int Region;
    public byte Weather;
    public uint Frame;
    public fixed byte MapName[16];

    public static SyncWorldS2CPacket Create(
        ushort mapId,
        int mpsX,
        int mpsY,
        uint frameTick,
        string mapNameUnicode,
        byte weather = 0)
    {
        var pos = new WorldPosition(mapId, mpsX, mpsY);
        SyncWorldS2CPacket pkt = default;
        pkt.ProtocolType = ProtocolOpcodes.s2c_syncworld;
        pkt.SubWorldId = mapId;
        pkt.MapCopyIndex = 0;
        pkt.Region = pos.PackedRegionId;
        pkt.Weather = weather;
        pkt.Frame = frameTick;
        Tcvn3Encoding.WriteFixedString(mapNameUnicode, new Span<byte>(pkt.MapName, 16));
        return pkt;
    }

    public readonly string GetMapName()
    {
        fixed (byte* ptr = MapName)
        {
            return Tcvn3Encoding.DecodeNullTerminated(new ReadOnlySpan<byte>(ptr, 16));
        }
    }
}
