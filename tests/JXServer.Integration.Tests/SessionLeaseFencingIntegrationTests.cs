using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Accounts;
using JXServer.Primitives.Characters;
using Xunit;

namespace JXServer.Integration.Tests;

/// <summary>
/// Integration tests for User Story 3 &amp; Success Criteria <c>SC-002</c> / <c>SC-003</c>:
/// <c>bKickOnline</c> session takeover, <c>LOGIN_R_ACCOUNT_EXIST</c> when <c>bKickOnline = 0</c>,
/// single-use handoff ticket replay rejection, and 100-bot concurrent world entry into Ba Lăng Huyện (<c>Map 53</c>).
/// </summary>
public sealed class SessionLeaseFencingIntegrationTests
{
    [Fact]
    public async Task SessionTakeover_ShouldRejectWhenKickOnlineFalse_AndEvictOldSessionWhenKickOnlineTrue()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        // Session 1 logs in, creates character, and enters Map 53
        Guid ticket1;
        await using (var bishop1 = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishop1.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("fencedacc", "pw123", kickOnline: true));
            _ = await bishop1.ReadNextDecryptedPayloadAsync(); // 0x41
            _ = await bishop1.ReadNextDecryptedPayloadAsync(); // 0x37

            await bishop1.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Earth, 53, "Võ Đang Kiếm").ToPayloadBytes());
            _ = await bishop1.ReadNextDecryptedPayloadAsync(); // 0x8C
            var notify1 = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishop1.ReadNextDecryptedPayloadAsync());
            ticket1 = notify1.GetTicketGuid();
        }

        await using var gs1 = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.GameServerPort);
        await gs1.SendEncryptedStructAsync(LogicLoginC2SPacket.Create(ticket1));
        for (int i = 0; i < 7; i++)
        {
            _ = await gs1.ReadNextDecryptedPayloadAsync();
        }

        await gs1.SendEncryptedStructAsync(SyncEndC2SPacket.Create(1u, 1u));
        _ = await gs1.ReadNextDecryptedPayloadAsync(); // 0x46
        _ = await gs1.ReadNextDecryptedPayloadAsync(); // 0x4E

        // Replay of single-use ticket1 on another GameServer connection must be rejected
        await using (var replayGs = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.GameServerPort))
        {
            await replayGs.SendEncryptedStructAsync(LogicLoginC2SPacket.Create(ticket1));
            await Assert.ThrowsAnyAsync<Exception>(() => replayGs.ReadNextDecryptedPayloadAsync());
        }

        // Second login with KickOnline = false -> returns AccountAlreadyOnline (0x00050004)
        await using (var bishopNoKick = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishopNoKick.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("fencedacc", "pw123", kickOnline: false));
            var noKickRes = MemoryMarshal.Read<AccountLoginResultS2CPacket>(await bishopNoKick.ReadNextDecryptedPayloadAsync());
            Assert.Equal((uint)LoginResultCode.AccountAlreadyOnline, noKickRes.Param);
        }

        // Third login with KickOnline = true -> evicts gs1 and admits new session into Map 53
        Guid ticket2;
        await using (var bishopKick = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishopKick.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("fencedacc", "pw123", kickOnline: true));
            var kickRes = MemoryMarshal.Read<AccountLoginResultS2CPacket>(await bishopKick.ReadNextDecryptedPayloadAsync());
            Assert.Equal((uint)LoginResultCode.Success, kickRes.Param);
            _ = await bishopKick.ReadNextDecryptedPayloadAsync(); // 0x37

            await bishopKick.SendEncryptedStructAsync(SelectCharacterC2SPacket.Create("Võ Đang Kiếm"));
            var notify2 = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishopKick.ReadNextDecryptedPayloadAsync());
            Assert.Equal(1, notify2.Permit);
            ticket2 = notify2.GetTicketGuid();
        }

        await using var gs2 = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.GameServerPort);
        await gs2.SendEncryptedStructAsync(LogicLoginC2SPacket.Create(ticket2));
        for (int i = 0; i < 7; i++)
        {
            _ = await gs2.ReadNextDecryptedPayloadAsync();
        }

        await gs2.SendEncryptedStructAsync(SyncEndC2SPacket.Create(2u, 2u));
        byte[] confirm = await gs2.ReadNextDecryptedPayloadAsync();
        Assert.Equal(ProtocolOpcodes.s2c_synccurplayernormal, confirm[0]);
    }

    [Fact]
    public async Task SwarmConcurrency_100ConcurrentBots_ShouldAuthenticateCreateAndEnterMap53()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        const int botCount = 100;
        Task[] botTasks = Enumerable.Range(1, botCount)
            .Select(async botIdx =>
            {
                string user = $"swarmbot{botIdx:D3}";
                string role = $"SwarmHero{botIdx:D3}";

                Guid ticket;
                await using (var bishop = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
                {
                    await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create(user, "botpass", kickOnline: true));
                    var loginRes = MemoryMarshal.Read<AccountLoginResultS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
                    Assert.Equal((uint)LoginResultCode.Success, loginRes.Param);
                    _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x37

                    var series = (ElementSeries)(botIdx % 5);
                    var gender = (CharacterGender)(botIdx % 2);
                    await bishop.SendEncryptedPayloadAsync(
                        new CreateCharacterC2SPacket((uint)botIdx, gender, series, 53, role).ToPayloadBytes());

                    var newDel = MemoryMarshal.Read<RoleNewDelResponseS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
                    Assert.Equal(1, newDel.Succeeded);

                    var notify = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
                    Assert.Equal(1, notify.Permit);
                    ticket = notify.GetTicketGuid();
                }

                await using var gs = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.GameServerPort);
                await gs.SendEncryptedStructAsync(LogicLoginC2SPacket.Create(ticket));
                for (int i = 0; i < 7; i++)
                {
                    _ = await gs.ReadNextDecryptedPayloadAsync();
                }

                await gs.SendEncryptedStructAsync(SyncEndC2SPacket.Create((uint)botIdx, 100u));
                _ = await gs.ReadNextDecryptedPayloadAsync(); // 0x46
                _ = await gs.ReadNextDecryptedPayloadAsync(); // 0x4E

                await gs.SendEncryptedStructAsync(PingReplyC2SPacket.Create(1u, (uint)botIdx));
                var pingReply = MemoryMarshal.Read<PingReplyS2CPacket>(await gs.ReadNextDecryptedPayloadAsync());
                Assert.Equal((uint)botIdx, pingReply.EchoedClientTimestamp);

                await gs.SendEncryptedStructAsync(CloseConnectionC2SPacket.Create());
            })
            .ToArray();

        await Task.WhenAll(botTasks);
    }
}
