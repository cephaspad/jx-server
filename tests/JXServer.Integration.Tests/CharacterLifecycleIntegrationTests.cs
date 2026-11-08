using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Accounts;
using JXServer.Primitives.Characters;
using Xunit;

namespace JXServer.Integration.Tests;

/// <summary>
/// Integration tests for User Story 2: character creation across all 5 elemental series (<c>0x45</c>),
/// prohibited/duplicate name rejection, max-3 character limit, unloaded starter village fallback (<c>FR-005</c>, <c>FR-007</c>),
/// and permanent character deletion (<c>0x3A</c>) immediately freeing the slot and character name (<c>FR-006</c>).
/// </summary>
public sealed class CharacterLifecycleIntegrationTests
{
    [Fact]
    public async Task CreateCharacters_EnforceMax3_RejectDuplicateAndProhibited_VerifyAll5Elements_AndDeleteToReuseName()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        await using var bishop1 = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort);
        await bishop1.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("lifecycle01", "secret123", kickOnline: true));

        _ = await bishop1.ReadNextDecryptedPayloadAsync(); // 0x41
        _ = await bishop1.ReadNextDecryptedPayloadAsync(); // 0x37

        // 1. Prohibited name ("GM_Master") -> rejected (bSucceeded = 0)
        await bishop1.SendEncryptedPayloadAsync(
            new CreateCharacterC2SPacket(1, CharacterGender.Male, ElementSeries.Metal, 53, "GM_Master").ToPayloadBytes());
        var badNameRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop1.ReadNextDecryptedPayloadAsync());
        Assert.Equal(0, badNameRes.Succeeded);

        // 2. Create 3 valid characters on account 1 (Metal, Wood, Water)
        (string Name, CharacterGender Gender, ElementSeries Series)[] rolesAcc1 =
        [
            ("Thiếu Lâm Tăng", CharacterGender.Male, ElementSeries.Metal),
            ("Đường Môn Hiệp", CharacterGender.Male, ElementSeries.Wood),
            ("Nga Mi Đệ Tử", CharacterGender.Female, ElementSeries.Water)
        ];

        foreach (var (name, gender, series) in rolesAcc1)
        {
            await bishop1.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, gender, series, 53, name).ToPayloadBytes());

            var createRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop1.ReadNextDecryptedPayloadAsync());
            Assert.Equal(1, createRes.Succeeded);

            var notifyRes = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishop1.ReadNextDecryptedPayloadAsync());
            Assert.Equal(1, notifyRes.Permit);
        }

        // 3. 4th character on account 1 -> rejected (Max 3 characters per account)
        await bishop1.SendEncryptedPayloadAsync(
            new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Fire, 53, "Cái Bang Đệ Tử").ToPayloadBytes());
        var fourthRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop1.ReadNextDecryptedPayloadAsync());
        Assert.Equal(0, fourthRes.Succeeded);

        // 4. Second account attempts to create duplicate name ("Thiếu Lâm Tăng") -> rejected, then creates Fire & Earth characters
        await using (var bishop2 = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishop2.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("lifecycle02", "secret456", kickOnline: true));
            _ = await bishop2.ReadNextDecryptedPayloadAsync(); // 0x41
            _ = await bishop2.ReadNextDecryptedPayloadAsync(); // 0x37

            // Duplicate name across accounts -> rejected (US2/AC2)
            await bishop2.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Fire, 53, "Thiếu Lâm Tăng").ToPayloadBytes());
            var dupRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop2.ReadNextDecryptedPayloadAsync());
            Assert.Equal(0, dupRes.Succeeded);

            // Create Fire & Earth characters and verify Earth (Series=4) initial template stats in GameServer (0x44)
            await bishop2.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Fire, 53, "Cái Bang Đệ Tử").ToPayloadBytes());
            var fireRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop2.ReadNextDecryptedPayloadAsync());
            Assert.Equal(1, fireRes.Succeeded);
            _ = await bishop2.ReadNextDecryptedPayloadAsync(); // 0x34

            await bishop2.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Earth, 53, "Võ Đang Đạo Nhân").ToPayloadBytes());
            var earthRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop2.ReadNextDecryptedPayloadAsync());
            Assert.Equal(1, earthRes.Succeeded);
            var earthNotify = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishop2.ReadNextDecryptedPayloadAsync());
            Assert.Equal(1, earthNotify.Permit);

            await using var gsEarth = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.GameServerPort);
            await gsEarth.SendEncryptedStructAsync(LogicLoginC2SPacket.Create(earthNotify.GetTicketGuid()));
            _ = await gsEarth.ReadNextDecryptedPayloadAsync(); // 0x49
            var earthPlayer = MemoryMarshal.Read<SyncCurPlayerS2CPacket>(await gsEarth.ReadNextDecryptedPayloadAsync()); // 0x44
            Assert.Equal(76, earthPlayer.LifeMax);
            Assert.Equal(163, earthPlayer.ManaMax);
            Assert.Equal(20, earthPlayer.Strength);
            Assert.Equal(40, earthPlayer.Energy);
        }

        // 5. Delete "Đường Môn Hiệp" on account 1 via 0x3A -> frees slot 1 and role name immediately (FR-006)
        await bishop1.SendEncryptedStructAsync(
            DeleteCharacterC2SPacket.Create("lifecycle01", "secret123", "Đường Môn Hiệp"));
        var delRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop1.ReadNextDecryptedPayloadAsync());
        Assert.Equal(1, delRes.Succeeded);

        // 6. Re-create "Đường Môn Hiệp" -> succeeds immediately
        await bishop1.SendEncryptedPayloadAsync(
            new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Fire, 53, "Đường Môn Hiệp").ToPayloadBytes());
        var recreateRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop1.ReadNextDecryptedPayloadAsync());
        Assert.Equal(1, recreateRes.Succeeded);
        _ = await bishop1.ReadNextDecryptedPayloadAsync(); // 0x34
    }

    [Fact]
    public async Task UnloadedStarterVillage_ShouldRedirectToMap53WhenFallbackEnabled_AndRejectWhenFallbackDisabled()
    {
        // Exclude starter village 174 (Long Tuyền Thôn) from active worldset.ini maps
        ushort[] excludedMaps = [174];

        // 1. FallbackUnloadedStarterVillage = true -> creating character in village 174 redirects to Map 53 (FR-005, FR-007)
        await using (var fallbackHarness = await IntegrationTestServerHarness.StartAsync(
            autoProvisionAccounts: true,
            fallbackUnloadedStarterVillage: true,
            excludedWorldSetMapIds: excludedMaps))
        {
            await using var bishop = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(fallbackHarness.BishopPort);
            await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("fallbackuser", "pass123", kickOnline: true));
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x41
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x37

            await bishop.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Metal, 174, "Long Tuyền Hiệp").ToPayloadBytes());

            var newDelRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            Assert.Equal(1, newDelRes.Succeeded);
            Assert.Equal(53u, newDelRes.NativePlaceId);

            var notifyRes = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            Assert.Equal(1, notifyRes.Permit);
        }

        // 2. FallbackUnloadedStarterVillage = false -> creating character in unloaded village 174 is rejected with Succeeded = 0 (FR-005)
        await using (var strictHarness = await IntegrationTestServerHarness.StartAsync(
            autoProvisionAccounts: true,
            fallbackUnloadedStarterVillage: false,
            excludedWorldSetMapIds: excludedMaps))
        {
            await using var bishopStrict = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(strictHarness.BishopPort);
            await bishopStrict.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("strictmapuser", "pass123", kickOnline: true));
            _ = await bishopStrict.ReadNextDecryptedPayloadAsync(); // 0x41
            _ = await bishopStrict.ReadNextDecryptedPayloadAsync(); // 0x37

            await bishopStrict.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Metal, 174, "Long Tuyền Khách").ToPayloadBytes());

            var strictRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishopStrict.ReadNextDecryptedPayloadAsync());
            Assert.Equal(0, strictRes.Succeeded);
        }
    }
}
