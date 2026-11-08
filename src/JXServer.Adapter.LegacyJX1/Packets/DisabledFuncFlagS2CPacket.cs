using System.Runtime.InteropServices;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 5-byte <c>s2c_disabledfuncflag</c> (<c>0xA4</c>) packet emitted during the initial world sync burst.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DisabledFuncFlagS2CPacket
{
    public const int Size = 5;

    public byte ProtocolType;
    public uint DisabledFlags;

    public static DisabledFuncFlagS2CPacket Create(uint disabledFlags = 0) => new()
    {
        ProtocolType = ProtocolOpcodes.s2c_disabledfuncflag,
        DisabledFlags = disabledFlags
    };
}
