using System;
using System.Buffers.Binary;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_syncitem</c> (<c>0x5F</c>, <c>80</c> bytes — <c>tagItemSync</c> / <c>ITEM_SYNC</c>):
/// synchronizes a single item instance in the player's inventory (`FR-012`).
/// </summary>
public readonly record struct SyncItemS2CPacket(
    uint RuntimeItemId,
    ItemGenreKind Genre,
    ItemQualityKind Quality,
    short DetailType,
    ushort ParticularType,
    ElementSeries Series,
    byte Level,
    byte Luck,
    short MagicLevel0,
    short MagicLevel1,
    short MagicLevel2,
    short MagicLevel3,
    short MagicLevel4,
    short MagicLevel5,
    ushort Version,
    uint RandomSeed,
    ushort StackCount,
    short MaxDurability,
    short CurrentDurability,
    ItemContainerPlace ContainerPlace,
    byte GridY,
    byte GridX,
    int WidthCells,
    int HeightCells)
{
    public const int PayloadSize = 80;

    public static SyncItemS2CPacket CreateFromSnapshot(in PlayerItemSyncSnapshot snap) =>
        new(
            RuntimeItemId: snap.RuntimeItemId,
            Genre: snap.Genre,
            Quality: snap.Quality,
            DetailType: snap.DetailType,
            ParticularType: snap.ParticularType,
            Series: snap.Series,
            Level: snap.Level,
            Luck: snap.Luck,
            MagicLevel0: snap.MagicLevel0,
            MagicLevel1: snap.MagicLevel1,
            MagicLevel2: snap.MagicLevel2,
            MagicLevel3: snap.MagicLevel3,
            MagicLevel4: snap.MagicLevel4,
            MagicLevel5: snap.MagicLevel5,
            Version: snap.Version == 0 ? ItemConsts.GeneratorVersion : snap.Version,
            RandomSeed: snap.RandomSeed,
            StackCount: snap.StackCount,
            MaxDurability: snap.MaxDurability,
            CurrentDurability: snap.CurrentDurability,
            ContainerPlace: snap.ContainerPlace,
            GridY: snap.GridY,
            GridX: snap.GridX,
            WidthCells: snap.WidthCells,
            HeightCells: snap.HeightCells);

    public static bool TryParse(ReadOnlySpan<byte> payload, out SyncItemS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_syncitem)
        {
            packet = default;
            return false;
        }

        byte pattern = payload[5];
        ItemGenreKind genre = (ItemGenreKind)(pattern & 0x0F);
        ItemQualityKind quality = (ItemQualityKind)((pattern >> 4) & 0x0F);

        packet = new SyncItemS2CPacket(
            RuntimeItemId: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4)),
            Genre: genre,
            Quality: quality,
            DetailType: BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(6, 2)),
            ParticularType: BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(8, 2)),
            Series: (ElementSeries)payload[10],
            Level: payload[11],
            Luck: payload[12],
            MagicLevel0: BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(13, 2)),
            MagicLevel1: BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(15, 2)),
            MagicLevel2: BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(17, 2)),
            MagicLevel3: BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(19, 2)),
            MagicLevel4: BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(21, 2)),
            MagicLevel5: BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(23, 2)),
            Version: BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(37, 2)),
            RandomSeed: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(39, 4)),
            StackCount: BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(43, 2)),
            MaxDurability: BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(65, 2)),
            CurrentDurability: BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(67, 2)),
            ContainerPlace: (ItemContainerPlace)payload[69],
            GridY: payload[70],
            GridX: payload[71],
            WidthCells: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(72, 4)),
            HeightCells: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(76, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination.Slice(0, PayloadSize).Clear();
        destination[0] = ProtocolOpcodes.s2c_syncitem;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), RuntimeItemId);
        destination[5] = (byte)(((byte)Genre & 0x0F) | (((byte)Quality & 0x0F) << 4));
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(6, 2), DetailType);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(8, 2), ParticularType);
        destination[10] = (byte)Series;
        destination[11] = Level;
        destination[12] = Luck;
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(13, 2), MagicLevel0);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(15, 2), MagicLevel1);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(17, 2), MagicLevel2);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(19, 2), MagicLevel3);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(21, 2), MagicLevel4);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(23, 2), MagicLevel5);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(37, 2), Version == 0 ? ItemConsts.GeneratorVersion : Version);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(39, 4), RandomSeed);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(43, 2), StackCount);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(65, 2), MaxDurability);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(67, 2), CurrentDurability);
        destination[69] = (byte)ContainerPlace;
        destination[70] = GridY;
        destination[71] = GridX;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(72, 4), WidthCells);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(76, 4), HeightCells);
    }
}
