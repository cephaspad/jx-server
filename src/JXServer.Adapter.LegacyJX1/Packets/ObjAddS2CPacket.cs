using System;
using System.Buffers.Binary;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_objadd</c> (<c>0x64</c>, variable <c>35 + NameBytes.Length</c> bytes — <c>tagOBJ_ADD_SYNC</c>):
/// synchronizes a newly dropped or AOI-entered ground object (`FR-011`).
/// </summary>
public readonly record struct ObjAddS2CPacket(
    byte Dir,
    int ObjId,
    int DataId,
    int MpsX,
    int MpsY,
    int MoneyNum,
    int ItemId,
    byte State,
    byte ItemWidth,
    ushort Genre,
    byte ItemHeight,
    byte ColorId,
    byte Flag,
    string NameUtf8)
{
    public const int HeaderSize = 35;

    public static byte[] CreatePayloadBytes(
        byte dir,
        int objId,
        int dataId,
        int mpsX,
        int mpsY,
        int moneyNum,
        int itemId,
        byte state,
        byte itemWidth,
        ushort genre,
        byte itemHeight,
        byte colorId,
        byte flag,
        string nameUtf8)
    {
        byte[] nameBytes = Tcvn3Encoding.EncodeBytes(nameUtf8 ?? string.Empty);
        int nameLen = nameBytes.Length + 1;

        int totalSize = HeaderSize + nameLen;
        byte[] buffer = new byte[totalSize];
        Span<byte> span = buffer;

        span[0] = ProtocolOpcodes.s2c_objadd;
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(1, 2), (ushort)(totalSize - 1));
        span[3] = dir;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4, 4), objId);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8, 4), dataId);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(12, 4), mpsX);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(16, 4), mpsY);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(20, 4), moneyNum);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(24, 4), itemId);
        span[28] = state;
        span[29] = itemWidth;
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(30, 2), genre);
        span[32] = itemHeight;
        span[33] = colorId;
        span[34] = flag;
        nameBytes.AsSpan().CopyTo(span.Slice(HeaderSize, nameBytes.Length));
        span[HeaderSize + nameBytes.Length] = 0;

        return buffer;
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out ObjAddS2CPacket packet)
    {
        if (payload.Length < HeaderSize + 1 || payload[0] != ProtocolOpcodes.s2c_objadd)
        {
            packet = default;
            return false;
        }

        ushort declaredLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1, 2));
        if (declaredLen != payload.Length - 1)
        {
            packet = default;
            return false;
        }

        ReadOnlySpan<byte> nameSpan = payload.Slice(HeaderSize);

        packet = new ObjAddS2CPacket(
            Dir: payload[3],
            ObjId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4, 4)),
            DataId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(8, 4)),
            MpsX: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(12, 4)),
            MpsY: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(16, 4)),
            MoneyNum: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(20, 4)),
            ItemId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(24, 4)),
            State: payload[28],
            ItemWidth: payload[29],
            Genre: BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(30, 2)),
            ItemHeight: payload[32],
            ColorId: payload[33],
            Flag: payload[34],
            NameUtf8: Tcvn3Encoding.DecodeNullTerminated(nameSpan));
        return true;
    }
}
