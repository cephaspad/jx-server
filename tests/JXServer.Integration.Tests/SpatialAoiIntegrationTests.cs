using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Characters;
using Xunit;

namespace JXServer.Integration.Tests;

public sealed class SpatialAoiIntegrationTests
{
    [Fact]
    public async Task TwoPlayersInMap53_ShouldReceive3x3StaticNpcs_Mutual0x4C_AndRespondTo0x48_0x49()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        await using SimulatedJxClientConnection gsA = await LoginAndEnterWorldAsync(
            harness,
            "aoiu1",
            "Kiều Phong",
            token1: 0x11111111u,
            token2: 0x22222222u);

        // Player A should receive at least one 0x4C static NPC in the 3x3 region around Ba Lăng Huyện spawn (52032, 101696)
        SyncNpcFullS2CPacket staticNpc = await ReadSyncNpcMatchingAsync(gsA, p => p.Kind != EntityKind.Player);
        Assert.True(staticNpc.NpcId is >= 1 and < 10000);
        Assert.NotEmpty(staticNpc.NameUnicode);

        // Player B enters the same region
        await using SimulatedJxClientConnection gsB = await LoginAndEnterWorldAsync(
            harness,
            "aoiu2",
            "Đoàn Dự",
            token1: 0x33333333u,
            token2: 0x44444444u);

        // Player A receives 0x4C for Player B ("Đoàn Dự"), Player B receives 0x4C for Player A ("Kiều Phong")
        SyncNpcFullS2CPacket bSeenByA = await ReadSyncNpcMatchingAsync(gsA, p => p.Kind == EntityKind.Player && p.NameUnicode == "Đoàn Dự");
        SyncNpcFullS2CPacket aSeenByB = await ReadSyncNpcMatchingAsync(gsB, p => p.Kind == EntityKind.Player && p.NameUnicode == "Kiều Phong");

        Assert.True(bSeenByA.NpcId >= 10000);
        Assert.True(aSeenByB.NpcId >= 10000);

        // Player A sends 0x48 c2s_requestplayer for Player B and 0x49 c2s_requestnpc for the static NPC
        byte[] reqPlayerBuf = new byte[RequestPlayerC2SPacket.PayloadSize];
        new RequestPlayerC2SPacket(bSeenByA.NpcId).WritePayload(reqPlayerBuf);
        await gsA.SendEncryptedPayloadAsync(reqPlayerBuf);

        SyncNpcFullS2CPacket queriedPlayer = await ReadSyncNpcMatchingAsync(gsA, p => p.NpcId == bSeenByA.NpcId);
        Assert.Equal("Đoàn Dự", queriedPlayer.NameUnicode);

        byte[] reqNpcBuf = new byte[RequestNpcC2SPacket.PayloadSize];
        new RequestNpcC2SPacket(staticNpc.NpcId).WritePayload(reqNpcBuf);
        await gsA.SendEncryptedPayloadAsync(reqNpcBuf);

        SyncNpcFullS2CPacket queriedNpc = await ReadSyncNpcMatchingAsync(gsA, p => p.NpcId == staticNpc.NpcId);
        Assert.Equal(staticNpc.NameUnicode, queriedNpc.NameUnicode);
    }

    private static async Task<SimulatedJxClientConnection> LoginAndEnterWorldAsync(
        IntegrationTestServerHarness harness,
        string username,
        string roleName,
        uint token1,
        uint token2)
    {
        Guid ticket;
        await using (var bishop = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create(username, "pass123", kickOnline: true));
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x41
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x37

            await bishop.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Metal, 53, roleName).ToPayloadBytes());
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x8C
            var notify = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            ticket = notify.GetTicketGuid();
        }

        var gs = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.GameServerPort);
        await gs.SendEncryptedStructAsync(LogicLoginC2SPacket.Create(ticket));
        _ = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncend);
        await gs.SendEncryptedStructAsync(SyncEndC2SPacket.Create(token1, token2));
        _ = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncnpcminplayer);
        return gs;
    }

    private static async Task<SyncNpcFullS2CPacket> ReadSyncNpcMatchingAsync(
        SimulatedJxClientConnection gs,
        Func<SyncNpcFullS2CPacket, bool> predicate)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            byte[] payload = await gs.ReadNextDecryptedPayloadAsync(cts.Token);
            if (payload.Length > 0 &&
                payload[0] == ProtocolOpcodes.s2c_syncnpc &&
                SyncNpcFullS2CPacket.TryParse(payload, out SyncNpcFullS2CPacket packet) &&
                predicate(packet))
            {
                return packet;
            }
        }
    }
}
