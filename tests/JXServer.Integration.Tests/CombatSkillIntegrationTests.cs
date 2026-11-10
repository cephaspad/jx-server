using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JXServer.Integration.Tests;

public sealed class CombatSkillIntegrationTests
{
    [Fact]
    public async Task CastSkillAndChangePkModeOverTcp_ShouldBroadcastSkillCastAndSyncPkFlag()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        const uint token1 = 0x13572468u;
        const uint token2 = 0x24681357u;

        await using SimulatedJxClientConnection gs = await LoginAndEnterWorldAsync(
            harness,
            "combatu1",
            "Kiều Phong",
            token1,
            token2);

        // Move player into FightMode = 1 in zone memory so offensive skill cast is permitted
        var router = harness.Services.GetRequiredService<ZoneClusterRouter>();
        Assert.True(router.TryGetWorker(53, out MapZoneWorker? worker) && worker is not null);
        foreach (SpatialRegionSlot slot in worker.RegionGrid.ActiveRegions)
        {
            foreach (PlayerZoneState p in slot.Players)
            {
                p.FightMode = 1;
            }
        }

        // 1. Change PK mode via 0x76 -> receive 0x90 s2c_pksyncnormalflag
        byte[] pkC2S = new byte[PkApplyChangeNormalFlagC2SPacket.PayloadSize];
        new PkApplyChangeNormalFlagC2SPacket(PlayerPkMode.Fight).WritePayload(pkC2S);
        await gs.SendEncryptedPayloadAsync(pkC2S);

        byte[] pkS2CBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_pksyncnormalflag);
        Assert.True(PkSyncNormalFlagS2CPacket.TryParse(pkS2CBytes, out PkSyncNormalFlagS2CPacket pkSync));
        Assert.Equal(PlayerPkMode.Fight, pkSync.PkMode);

        // 2. Cast starter skill 53 via 0x4D with valid SkillCheckKey (0x3AB9) -> receive 0x5A s2c_skillcast
        uint validSkillCheck = MovementAntiCheatConsts.CreateCheckValue(MovementAntiCheatConsts.SkillCheckKey, token1, token2);
        byte[] skillC2S = new byte[NpcSkillC2SPacket.PayloadSize];
        new NpcSkillC2SPacket(TargetMpsYOrNpcId: 101696, SkillId: 53, TargetMpsX: 52032, CheckValue: validSkillCheck).WritePayload(skillC2S);
        await gs.SendEncryptedPayloadAsync(skillC2S);

        byte[] castS2CBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_skillcast);
        Assert.True(SkillCastS2CPacket.TryParse(castS2CBytes, out SkillCastS2CPacket castS2C));
        Assert.Equal(53u, castS2C.SkillId);
        Assert.Equal(52032, castS2C.TargetMpsX);
        Assert.Equal(101696, castS2C.TargetMpsYOrNpcId);
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
}
