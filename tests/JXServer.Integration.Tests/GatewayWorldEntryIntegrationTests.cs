using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Domain.Accounts;
using JXServer.Primitives.Accounts;
using JXServer.Primitives.Characters;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JXServer.Integration.Tests;

/// <summary>
/// End-to-end integration tests for User Story 1: Bishop Gateway authentication (<c>0x41</c>),
/// strict vs. auto-provisioned credentials (<c>FR-002</c>, <c>FR-003</c>, <c>US1/AC4</c>),
/// character roster (<c>0x37</c>), character selection (<c>0x64</c>), handoff ticket (<c>0x34</c>),
/// GameServer world entry (<c>0x42</c>), initial world sync burst (<c>0x49..0x43</c>), and heartbeat pings (<c>0x70</c>/<c>0x99</c>).
/// </summary>
public sealed class GatewayWorldEntryIntegrationTests
{
    [Fact]
    public async Task EndToEnd_AutoProvision_CreateOrSelectCharacter_AndEnterWorldMap53()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        // 1. Connect to Bishop, auto-provision account "hero01", and create character "Lệnh Hồ Xung"
        Guid handoffTicket;
        await using (var bishop = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("hero01", "pass123", kickOnline: true));

            byte[] loginResBytes = await bishop.ReadNextDecryptedPayloadAsync();
            var loginRes = MemoryMarshal.Read<AccountLoginResultS2CPacket>(loginResBytes);
            Assert.Equal((uint)LoginResultCode.Success, loginRes.Param);

            byte[] roleListBytes = await bishop.ReadNextDecryptedPayloadAsync();
            Assert.True(RoleListResultS2CPacket.TryParse(roleListBytes, out RoleListResultS2CPacket emptyRoster));
            Assert.Empty(emptyRoster.Characters);

            // Create character
            var createPkt = new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Metal, 53, "Lệnh Hồ Xung");
            await bishop.SendEncryptedPayloadAsync(createPkt.ToPayloadBytes());

            byte[] newDelBytes = await bishop.ReadNextDecryptedPayloadAsync();
            var newDelRes = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(newDelBytes);
            Assert.Equal(1, newDelRes.Succeeded);
            Assert.Equal("Lệnh Hồ Xung", newDelRes.GetRoleName());

            byte[] notifyBytes = await bishop.ReadNextDecryptedPayloadAsync();
            var notifyRes = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(notifyBytes);
            Assert.Equal(1, notifyRes.Permit);
            Assert.Equal(harness.GameServerPort, notifyRes.Port);
            handoffTicket = notifyRes.GetTicketGuid();
            Assert.NotEqual(Guid.Empty, handoffTicket);
        }

        // 2. Connect to GameServer, present 0x42 handoff ticket, verify initial sync burst & 0x43 sync-end
        await using (var gs = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.GameServerPort))
        {
            await gs.SendEncryptedStructAsync(LogicLoginC2SPacket.Create(handoffTicket));

            byte[] p1 = await gs.ReadNextDecryptedPayloadAsync(); // 0x49 SyncWorld
            byte[] p2 = await gs.ReadNextDecryptedPayloadAsync(); // 0x44 SyncCurPlayer
            byte[] p3 = await gs.ReadNextDecryptedPayloadAsync(); // 0x45 SyncCurPlayerSkill
            byte[] p4 = await gs.ReadNextDecryptedPayloadAsync(); // 0x46 SyncCurPlayerNormal
            byte[] p5 = await gs.ReadNextDecryptedPayloadAsync(); // 0x4C SyncNpcFull
            byte[] p6 = await gs.ReadNextDecryptedPayloadAsync(); // 0xA4 DisabledFuncFlag
            byte[] p7 = await gs.ReadNextDecryptedPayloadAsync(); // 0x43 SyncEnd (1B)

            Assert.Equal(ProtocolOpcodes.s2c_syncworld, p1[0]);
            var worldSync = MemoryMarshal.Read<SyncWorldS2CPacket>(p1);
            Assert.Equal(53, worldSync.SubWorldId);

            Assert.Equal(ProtocolOpcodes.s2c_synccurplayer, p2[0]);
            var curPlayer = MemoryMarshal.Read<SyncCurPlayerS2CPacket>(p2);
            Assert.Equal(204, curPlayer.LifeMax);
            Assert.Equal(16, curPlayer.ManaMax);
            Assert.Equal(35, curPlayer.Strength);

            Assert.Equal(ProtocolOpcodes.s2c_synccurplayerskill, p3[0]);
            Assert.Equal(483, p3.Length);

            Assert.Equal(ProtocolOpcodes.s2c_synccurplayernormal, p4[0]);
            Assert.Equal(ProtocolOpcodes.s2c_syncnpc, p5[0]);
            Assert.True(SyncNpcFullS2CPacket.TryParse(p5, out SyncNpcFullS2CPacket selfNpc));
            Assert.Equal("Lệnh Hồ Xung", selfNpc.NameUnicode);
            Assert.Equal(52032, selfNpc.MapMpsX);
            Assert.Equal(101696, selfNpc.MapMpsY);

            Assert.Equal(ProtocolOpcodes.s2c_disabledfuncflag, p6[0]);
            Assert.Equal(ProtocolOpcodes.s2c_syncend, p7[0]);
            Assert.Single(p7);

            // Send 9-byte 0x43 c2s_syncend reply
            await gs.SendEncryptedStructAsync(SyncEndC2SPacket.Create(0x12345678u, 0x9ABCDEF0u));

            byte[] active1 = await gs.ReadNextDecryptedPayloadAsync(); // 0x46
            byte[] active2 = await gs.ReadNextDecryptedPayloadAsync(); // 0x4E
            Assert.Equal(ProtocolOpcodes.s2c_synccurplayernormal, active1[0]);
            Assert.Equal(ProtocolOpcodes.s2c_syncnpcminplayer, active2[0]);

            // Send 9-byte 0x70 c2s_ping and verify 5-byte 0x99 s2c_pingreply
            await gs.SendEncryptedStructAsync(PingReplyC2SPacket.Create(worldSync.Frame, 0xCAFEBABEu));
            byte[] pingReplyBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_pingreply);
            var pingReply = MemoryMarshal.Read<PingReplyS2CPacket>(pingReplyBytes);
            Assert.Equal(ProtocolOpcodes.s2c_pingreply, pingReply.ProtocolType);
            Assert.Equal(0xCAFEBABEu, pingReply.EchoedClientTimestamp);

            // Clean logout (0x9B) and wait for server-side detach completion
            await gs.SendEncryptedStructAsync(CloseConnectionC2SPacket.Create());
            await gs.WaitForRemoteDisconnectAsync();
        }

        // 3. Reconnect to Bishop, verify character appears in 0x37 roster, and select via 0x64
        await using (var bishop2 = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishop2.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("hero01", "pass123", kickOnline: false));

            byte[] loginResBytes = await bishop2.ReadNextDecryptedPayloadAsync();
            var loginRes = MemoryMarshal.Read<AccountLoginResultS2CPacket>(loginResBytes);
            Assert.Equal((uint)LoginResultCode.Success, loginRes.Param);

            byte[] roleListBytes = await bishop2.ReadNextDecryptedPayloadAsync();
            Assert.True(RoleListResultS2CPacket.TryParse(roleListBytes, out RoleListResultS2CPacket roster));
            Assert.Single(roster.Characters);
            Assert.Equal("Lệnh Hồ Xung", roster.Characters[0].RoleNameUnicode);

            await bishop2.SendEncryptedStructAsync(SelectCharacterC2SPacket.Create("Lệnh Hồ Xung"));
            byte[] notifyBytes = await bishop2.ReadNextDecryptedPayloadAsync();
            var notifyRes = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(notifyBytes);
            Assert.Equal(1, notifyRes.Permit);
            Assert.NotEqual(Guid.Empty, notifyRes.GetTicketGuid());
        }
    }

    [Fact]
    public async Task AuthenticationRejections_ShouldEnforceStrictMode_WrongPassword_ProtocolVersion_AndFrozenStatus()
    {
        // 1. When AutoProvisionAccounts = false, unknown username is rejected with AccountOrPasswordError (0x00050003) (FR-003)
        await using (var strictHarness = await IntegrationTestServerHarness.StartAsync(autoProvisionAccounts: false))
        {
            await using var bishopStrict = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(strictHarness.BishopPort);
            await bishopStrict.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("unregistered01", "pass123", kickOnline: true));

            var unknownRes = MemoryMarshal.Read<AccountLoginResultS2CPacket>(await bishopStrict.ReadNextDecryptedPayloadAsync());
            Assert.Equal((uint)LoginResultCode.AccountOrPasswordError, unknownRes.Param);
        }

        // 2. When AutoProvisionAccounts = true, first login creates account; subsequent wrong password, bad version, and frozen status are rejected (FR-002, US1/AC4)
        await using var harness = await IntegrationTestServerHarness.StartAsync(autoProvisionAccounts: true);

        await using (var bishop = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            // First login registers "guard01" with "correctpass"
            await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("guard01", "correctpass", kickOnline: true));
            var okRes = MemoryMarshal.Read<AccountLoginResultS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            Assert.Equal((uint)LoginResultCode.Success, okRes.Param);
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x37

            // Wrong password on subsequent login -> 0x00050003
            await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("guard01", "wrongpass", kickOnline: true));
            var badPassRes = MemoryMarshal.Read<AccountLoginResultS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            Assert.Equal((uint)LoginResultCode.AccountOrPasswordError, badPassRes.Param);

            // Incompatible protocol version -> 0x0005000A
            await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("guard01", "correctpass", kickOnline: true, protocolVersion: 0x00000001u));
            var badVerRes = MemoryMarshal.Read<AccountLoginResultS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            Assert.Equal((uint)LoginResultCode.InvalidProtocolVersion, badVerRes.Param);
        }

        // Freeze "guard01" in repository and verify AccountFrozen (0x0005000B)
        var accountRepo = harness.Services.GetRequiredService<IAccountRepository>();
        Account? account = await accountRepo.FindByNormalizedUserNameAsync(Account.NormalizeUserName("guard01"));
        Assert.NotNull(account);
        account.Freeze();
        await accountRepo.UpdateAsync(account, autoSave: true);

        await using (var bishopFrozen = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishopFrozen.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("guard01", "correctpass", kickOnline: true));
            var frozenRes = MemoryMarshal.Read<AccountLoginResultS2CPacket>(await bishopFrozen.ReadNextDecryptedPayloadAsync());
            Assert.Equal((uint)LoginResultCode.AccountFrozen, frozenRes.Param);
        }
    }
}
