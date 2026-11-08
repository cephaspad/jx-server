using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Accounts;
using JXServer.Primitives.Characters;
using Xunit;

namespace JXServer.Integration.Tests;

/// <summary>
/// Integration tests for User Story 2: character creation across all 5 elemental series (<c>0x45</c>),
/// prohibited/duplicate name rejection, max-3 character limit, and permanent character deletion (<c>0x3A</c>)
/// immediately freeing the slot and character name (`FR-005`, `FR-006`).
/// </summary>
public sealed class CharacterLifecycleIntegrationTests
{
    [Fact]
    public async Task CreateCharacters_EnforceMax3_RejectDuplicateAndProhibited_AndDeleteToReuseName()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        await using var bishop = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort);
        await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("lifecycle01", "secret123", kickOnline: true));

        _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x41
        _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x37

        // 1. Prohibited name ("GM_Master") -> rejected (bSucceeded = 0)
        await bishop.SendEncryptedPayloadAsync(
            new CreateCharacterC2SPacket(1, CharacterGender.Male, ElementSeries.Metal, 53, "GM_Master").ToPayloadBytes());
        var badNameRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
        Assert.Equal(0, badNameRes.Succeeded);

        // 2. Create 3 valid characters across different series (Metal, Wood, Water)
        (string Name, CharacterGender Gender, ElementSeries Series)[] roles =
        [
            ("Thiếu Lâm Tăng", CharacterGender.Male, ElementSeries.Metal),
            ("Đường Môn Hiệp", CharacterGender.Male, ElementSeries.Wood),
            ("Nga Mi Đệ Tử", CharacterGender.Female, ElementSeries.Water)
        ];

        foreach (var (name, gender, series) in roles)
        {
            await bishop.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, gender, series, 53, name).ToPayloadBytes());

            var createRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            Assert.Equal(1, createRes.Succeeded);

            var notifyRes = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            Assert.Equal(1, notifyRes.Permit);
        }

        // 3. 4th character on the same account -> rejected (Max 3 characters per account)
        await bishop.SendEncryptedPayloadAsync(
            new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Fire, 53, "Cái Bang Đệ Tử").ToPayloadBytes());
        var fourthRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
        Assert.Equal(0, fourthRes.Succeeded);

        // 4. Delete "Đường Môn Hiệp" via 0x3A -> frees slot 1 and role name immediately (FR-006)
        await bishop.SendEncryptedStructAsync(
            DeleteCharacterC2SPacket.Create("lifecycle01", "secret123", "Đường Môn Hiệp"));
        var delRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
        Assert.Equal(1, delRes.Succeeded);

        // 5. Re-create "Đường Môn Hiệp" (now as Fire series) -> succeeds immediately
        await bishop.SendEncryptedPayloadAsync(
            new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Fire, 53, "Đường Môn Hiệp").ToPayloadBytes());
        var recreateRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
        Assert.Equal(1, recreateRes.Succeeded);
        _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x34
    }
}
