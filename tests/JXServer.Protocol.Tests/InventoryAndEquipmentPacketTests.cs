using JXServer.Adapter.LegacyJX1.Framing;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using Xunit;

namespace JXServer.Protocol.Tests;

public sealed class InventoryAndEquipmentPacketTests
{
    [Fact]
    public void SyncItemAndSyncMoneyS2CPackets_ShouldRoundTripExactWireBytes()
    {
        var syncItem = new SyncItemS2CPacket(
            RuntimeItemId: 10042u,
            Genre: ItemGenreKind.Equipment,
            Quality: ItemQualityKind.Normal,
            DetailType: 0,
            ParticularType: 1,
            Series: ElementSeries.Metal,
            Level: 5,
            Luck: 12,
            MagicLevel0: 85,
            MagicLevel1: 103,
            MagicLevel2: 121,
            MagicLevel3: 0,
            MagicLevel4: 0,
            MagicLevel5: 0,
            Version: ItemConsts.GeneratorVersion,
            RandomSeed: 0x12345678u,
            StackCount: 1,
            MaxDurability: 50,
            CurrentDurability: 48,
            ContainerPlace: ItemContainerPlace.EquipRoom,
            GridY: 3,
            GridX: 2,
            WidthCells: 2,
            HeightCells: 3);

        byte[] itemBuf = new byte[SyncItemS2CPacket.PayloadSize];
        syncItem.WritePayload(itemBuf);

        Assert.Equal(80, itemBuf.Length);
        Assert.Equal(ProtocolOpcodes.s2c_syncitem, itemBuf[0]);
        Assert.True(SyncItemS2CPacket.TryParse(itemBuf, out SyncItemS2CPacket parsedItem));
        Assert.Equal(syncItem, parsedItem);

        var syncMoney = new SyncMoneyS2CPacket(InventoryMoney: 12500, RepositoryMoney: 50000, TradeMoney: 0);
        byte[] moneyBuf = new byte[SyncMoneyS2CPacket.PayloadSize];
        syncMoney.WritePayload(moneyBuf);

        Assert.Equal(13, moneyBuf.Length);
        Assert.Equal(ProtocolOpcodes.s2c_syncmoney, moneyBuf[0]);
        Assert.True(SyncMoneyS2CPacket.TryParse(moneyBuf, out SyncMoneyS2CPacket parsedMoney));
        Assert.Equal(syncMoney, parsedMoney);
    }

    [Fact]
    public void PickupMoveRemoveAndAutoMovePackets_ShouldRoundTripExactWireBytes()
    {
        var pickup = new PlayerPickupItemC2SPacket(
            TargetGridY: 4,
            TargetGridX: 1,
            TargetPlace: ItemContainerPlace.EquipRoom,
            ObjId: 205,
            Reserved: 0u);
        byte[] pickupBuf = new byte[PlayerPickupItemC2SPacket.PayloadSize];
        pickup.WritePayload(pickupBuf);

        Assert.Equal(12, pickupBuf.Length);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(pickupBuf));
        Assert.True(PlayerPickupItemC2SPacket.TryParse(pickupBuf, out PlayerPickupItemC2SPacket parsedPickup));
        Assert.Equal(pickup, parsedPickup);

        var moveC2S = new PlayerMoveItemC2SPacket(
            DownY: 2,
            UpX: 3,
            DownPos: ItemContainerPlace.EquipRoom,
            DownX: 1,
            UpY: 0,
            UpPos: ItemContainerPlace.Equip);
        byte[] moveC2SBuf = new byte[PlayerMoveItemC2SPacket.PayloadSize];
        moveC2S.WritePayload(moveC2SBuf);

        Assert.Equal(7, moveC2SBuf.Length);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(moveC2SBuf));
        Assert.True(PlayerMoveItemC2SPacket.TryParse(moveC2SBuf, out PlayerMoveItemC2SPacket parsedMoveC2S));
        Assert.Equal(moveC2S, parsedMoveC2S);

        var moveS2C = new PlayerMoveItemS2CPacket(
            DownY: 2,
            DownPos: ItemContainerPlace.EquipRoom,
            DownX: 1,
            UpY: 0,
            UpX: 3,
            UpPos: ItemContainerPlace.Equip);
        byte[] moveS2CBuf = new byte[PlayerMoveItemS2CPacket.PayloadSize];
        moveS2C.WritePayload(moveS2CBuf);

        Assert.Equal(7, moveS2CBuf.Length);
        Assert.Equal(ProtocolOpcodes.s2c_playermoveitem, moveS2CBuf[0]);
        Assert.True(PlayerMoveItemS2CPacket.TryParse(moveS2CBuf, out PlayerMoveItemS2CPacket parsedMoveS2C));
        Assert.Equal(moveS2C, parsedMoveS2C);

        var removeS2C = new RemoveItemS2CPacket(RuntimeItemId: 10042u);
        byte[] removeBuf = new byte[RemoveItemS2CPacket.PayloadSize];
        removeS2C.WritePayload(removeBuf);

        Assert.Equal(5, removeBuf.Length);
        Assert.True(RemoveItemS2CPacket.TryParse(removeBuf, out RemoveItemS2CPacket parsedRemove));
        Assert.Equal(10042u, parsedRemove.RuntimeItemId);

        byte[] autoMoveBuf = new byte[ItemAutoMoveS2CPacket.PayloadSize];
        new ItemAutoMoveS2CPacket().WritePayload(autoMoveBuf);
        Assert.Single(autoMoveBuf);
        Assert.True(ItemAutoMoveS2CPacket.TryParse(autoMoveBuf, out _));
    }

    [Fact]
    public void EatThrowAndDivideC2SPackets_ShouldRoundTripExactWireBytes()
    {
        var eat = new PlayerEatItemC2SPacket(
            GridX: 0,
            RuntimeItemId: 777u,
            GridY: 0,
            ContainerPlace: ItemContainerPlace.Immediacy,
            AntiBotToken: 123u,
            TargetId: 0u);
        byte[] eatBuf = new byte[PlayerEatItemC2SPacket.PayloadSize];
        eat.WritePayload(eatBuf);

        Assert.Equal(16, eatBuf.Length);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(eatBuf));
        Assert.True(PlayerEatItemC2SPacket.TryParse(eatBuf, out PlayerEatItemC2SPacket parsedEat));
        Assert.Equal(eat, parsedEat);

        byte[] throwHandBuf = new byte[PlayerThrowAwayItemC2SPacket.PayloadSize];
        new PlayerThrowAwayItemC2SPacket().WritePayload(throwHandBuf);
        Assert.Single(throwHandBuf);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(throwHandBuf));
        Assert.True(PlayerThrowAwayItemC2SPacket.TryParse(throwHandBuf, out _));

        var throwAllMed = new ThrowAwayAllMedicineC2SPacket(1, 2, 3);
        byte[] throwAllBuf = new byte[ThrowAwayAllMedicineC2SPacket.PayloadSize];
        throwAllMed.WritePayload(throwAllBuf);
        Assert.Equal(4, throwAllBuf.Length);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(throwAllBuf));
        Assert.True(ThrowAwayAllMedicineC2SPacket.TryParse(throwAllBuf, out ThrowAwayAllMedicineC2SPacket parsedThrowAll));
        Assert.Equal(throwAllMed, parsedThrowAll);

        var divide = new PlayerDivideItemC2SPacket(RuntimeItemId: 888u, DivideCount: 3, ContinuouslyDivide: 1);
        byte[] divideBuf = new byte[PlayerDivideItemC2SPacket.PayloadSize];
        divide.WritePayload(divideBuf);
        Assert.Equal(10, divideBuf.Length);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(divideBuf));
        Assert.True(PlayerDivideItemC2SPacket.TryParse(divideBuf, out PlayerDivideItemC2SPacket parsedDivide));
        Assert.Equal(divide, parsedDivide);
    }
}
