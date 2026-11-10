using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Contracts.Characters;
using JXServer.GameAssets.Items;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JXServer.Integration.Tests;

public sealed class InventoryAndLootIntegrationTests
{
    [Fact]
    public async Task StarterItemsGroundPickupMoveConsumeAndTownPortalPersistenceOverTcp_ShouldRoundTripAndPersist()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        const uint token1 = 0x11223344u;
        const uint token2 = 0x55667788u;

        Guid ticket;
        Guid characterId;
        await using (var bishop = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("invuser1", "pass123", kickOnline: true));
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x41
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x37

            await bishop.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Metal, 53, "Dương Quá").ToPayloadBytes());
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x8C
            var notify = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            ticket = notify.GetTicketGuid();
        }

        await using (var gs = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.GameServerPort))
        {
            await gs.SendEncryptedStructAsync(LogicLoginC2SPacket.Create(ticket));
            _ = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncend);
            await gs.SendEncryptedStructAsync(SyncEndC2SPacket.Create(token1, token2));

            // 1. Verify initial SyncMoney (0x68) and starter SyncItem (0x5F) packets arrive after SyncEnd
            byte[] moneyBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncmoney);
            Assert.True(SyncMoneyS2CPacket.TryParse(moneyBytes, out _));

            byte[] firstItemBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncitem);
            Assert.True(SyncItemS2CPacket.TryParse(firstItemBytes, out SyncItemS2CPacket firstStarterItem));
            Assert.Equal(ItemConsts.GeneratorVersion, firstStarterItem.Version);

            var router = harness.Services.GetRequiredService<ZoneClusterRouter>();
            var itemGen = harness.Services.GetRequiredService<ItemGenerator>();
            Assert.True(router.TryGetWorker(53, out MapZoneWorker? worker) && worker is not null);

            PlayerZoneState? player = null;
            foreach (SpatialRegionSlot slot in worker.RegionGrid.ActiveRegions)
            {
                foreach (PlayerZoneState p in slot.Players)
                {
                    player = p;
                }
            }

            Assert.NotNull(player);
            characterId = player!.CharacterId;
            Assert.True(player.Inventory.AllItems.Count >= 3);

            // 2. Spawn a 6-affix Blue sword on the ground next to the player and send 0x5A c2s_playerpickupitem
            GeneratedItemData blueSword = itemGen.GenerateEquipment(
                detailType: 0,
                particularType: 1,
                series: ElementSeries.Metal,
                level: 1,
                magicLevelThresholds: [5, 5, 5, 5, 5, 5],
                luck: 20);
            GroundObjectSlot groundSword = worker.GroundObjectSystem.SpawnItemGroundObject(
                53,
                null,
                worker.RegionGrid,
                PlayerItemSlot.FromGeneratedItem(70001u, blueSword),
                player.CurrentMpsX + 16,
                player.CurrentMpsY,
                player.CharacterId,
                worker.CurrentFrameTick);

            byte[] pickupC2S = new byte[PlayerPickupItemC2SPacket.PayloadSize];
            new PlayerPickupItemC2SPacket(0, 0, ItemContainerPlace.EquipRoom, groundSword.ObjId).WritePayload(pickupC2S);
            await gs.SendEncryptedPayloadAsync(pickupC2S);

            byte[] objRemBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_objremove);
            Assert.True(ObjRemoveS2CPacket.TryParse(objRemBytes, out ObjRemoveS2CPacket objRem));
            Assert.Equal(groundSword.ObjId, objRem.ObjId);

            byte[] pickedItemBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncitem);
            Assert.True(SyncItemS2CPacket.TryParse(pickedItemBytes, out SyncItemS2CPacket pickedSwordSync));
            Assert.Equal(ItemGenreKind.Equipment, pickedSwordSync.Genre);

            // 3. Move picked sword from EquipRoom to Equip Weapon slot via 0x5B -> receive 0x61 + 0x8E
            byte[] moveC2S = new byte[PlayerMoveItemC2SPacket.PayloadSize];
            new PlayerMoveItemC2SPacket(
                DownY: pickedSwordSync.GridY,
                UpX: (byte)EquipmentPartKind.Weapon,
                DownPos: pickedSwordSync.ContainerPlace,
                DownX: pickedSwordSync.GridX,
                UpY: 0,
                UpPos: ItemContainerPlace.Equip).WritePayload(moveC2S);
            await gs.SendEncryptedPayloadAsync(moveC2S);

            byte[] moveConfirmBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_playermoveitem);
            Assert.True(PlayerMoveItemS2CPacket.TryParse(moveConfirmBytes, out PlayerMoveItemS2CPacket moveConfirm));
            Assert.Equal(ItemContainerPlace.Equip, moveConfirm.UpPos);

            byte[] unlockBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_itemautomove);
            Assert.True(ItemAutoMoveS2CPacket.TryParse(unlockBytes, out _));

            // 4. Trigger emergency Town Portal via 0x87 SubCommand 0x02 -> consumes scroll (0x60)
            byte[] tpC2S = new byte[AutoAttackNpcC2SPacket.PayloadSize];
            new AutoAttackNpcC2SPacket(AutoAttackNpcC2SPacket.SubCommandEmergencyTownPortal, ReadOnlyMemory<byte>.Empty).WritePayload(tpC2S);
            await gs.SendEncryptedPayloadAsync(tpC2S);

            byte[] removeItemBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_removeitem);
            Assert.True(RemoveItemS2CPacket.TryParse(removeItemBytes, out RemoveItemS2CPacket removedScroll));
            Assert.True(removedScroll.RuntimeItemId > 0);

            // 5. Disconnect gracefully via 0x9B (c2sCloseConnection) so character inventory snapshot persists to database
            await gs.SendEncryptedPayloadAsync(new byte[] { ProtocolOpcodes.c2sCloseConnection });
        }

        // Wait briefly for detach persistence and verify database state via ICharacterRepository
        var charRepo = harness.Services.GetRequiredService<JXServer.Domain.Characters.ICharacterRepository>();
        JXServer.Domain.Characters.Character? persisted = null;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(50);
            JXServer.Domain.Characters.Character? candidate = await charRepo.FindAsync(characterId);
            if (candidate is not null &&
                candidate.Items.Any(static i => i.ContainerPlace == ItemContainerPlace.Equip))
            {
                persisted = candidate;
                break;
            }
        }

        Assert.NotNull(persisted);
        Assert.Contains(persisted!.Items, static i => i.ContainerPlace == ItemContainerPlace.Equip && i.GridX == (byte)EquipmentPartKind.Weapon);
    }
}
