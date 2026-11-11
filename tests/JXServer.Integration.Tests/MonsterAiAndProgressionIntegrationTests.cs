using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JXServer.Integration.Tests;

public sealed class MonsterAiAndProgressionIntegrationTests
{
    [Fact]
    public async Task MonsterKillLevelUpStatSkillAllocationAndReviveOverTcp_ShouldRoundTripPackets()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        const uint token1 = 0x2468ACE0u;
        const uint token2 = 0x13579BDFu;

        await using SimulatedJxClientConnection gs = await LoginAndEnterWorldAsync(
            harness,
            "progu2",
            "Trương Vô Kỵ",
            token1,
            token2);

        var router = harness.Services.GetRequiredService<ZoneClusterRouter>();
        Assert.True(router.TryGetWorker(53, out MapZoneWorker? worker) && worker is not null);

        PlayerZoneState? player = null;
        foreach (SpatialRegionSlot slot in worker.RegionGrid.ActiveRegions)
        {
            foreach (PlayerZoneState p in slot.Players)
            {
                p.FightMode = 1;
                p.Experience = 95;
                p.WeaponMinDamage = 500;
                p.WeaponMaxDamage = 600;
                p.BonusAttackRating = 1000;
                player = p;
            }
        }

        Assert.NotNull(player);

        // Locate a monster near the player or move one next to the player
        NpcZoneState monster = worker.StaticNpcs.Values.First(static n => n.Kind == EntityKind.Monster);
        worker.RegionGrid.RemoveNpc(monster, monster.RegionX, monster.RegionY);
        monster.MpsX = player!.CurrentMpsX + 32;
        monster.MpsY = player.CurrentMpsY;
        monster.TargetMpsX = monster.MpsX;
        monster.TargetMpsY = monster.MpsY;
        monster.Doing = EntityDoingPosture.Stand;
        monster.AiState = MonsterAiStateKind.IdlePatrol;
        monster.NextAiActionTick = long.MaxValue;
        worker.RegionGrid.AddNpc(monster);
        monster.CurLife = 10;
        monster.ExpReward = 50;

        // 1. Attack monster with skill 53 -> monster dies (0x57), player gains XP (0x5C) and levels up (0x5B)
        uint checkVal = MovementAntiCheatConsts.CreateCheckValue(MovementAntiCheatConsts.SkillCheckKey, token1, token2);
        byte[] skillC2S = new byte[NpcSkillC2SPacket.PayloadSize];
        new NpcSkillC2SPacket(TargetMpsYOrNpcId: (int)monster.NpcId, SkillId: 53, TargetMpsX: -1, CheckValue: checkVal).WritePayload(skillC2S);
        await gs.SendEncryptedPayloadAsync(skillC2S);

        byte[] deathBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_npcdeath);
        Assert.True(NpcDeathS2CPacket.TryParse(deathBytes, out NpcDeathS2CPacket deathPkt));
        Assert.Equal(monster.NpcId, deathPkt.NpcId);

        byte[] lvlUpBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_playerlevelup);
        Assert.True(PlayerLevelUpS2CPacket.TryParse(lvlUpBytes, out PlayerLevelUpS2CPacket lvlPkt));
        Assert.Equal(2, lvlPkt.NewLevel);
        Assert.Equal(5, lvlPkt.UnallocatedAttrPoints);
        Assert.Equal(1, lvlPkt.UnallocatedSkillPoints);

        // 2. Allocate +2 Strength via 0x57 -> receive 0x5D s2c_playerattributesync
        byte[] attrC2S = new byte[PlayerAddBaseAttributeC2SPacket.PayloadSize];
        new PlayerAddBaseAttributeC2SPacket(BaseAttributeKind.Strength, 2).WritePayload(attrC2S);
        await gs.SendEncryptedPayloadAsync(attrC2S);

        byte[] attrS2CBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_playerattributesync);
        Assert.True(PlayerAttributeS2CPacket.TryParse(attrS2CBytes, out PlayerAttributeS2CPacket attrS2C));
        Assert.Equal(BaseAttributeKind.Strength, attrS2C.Attribute);
        Assert.Equal(3, attrS2C.UnallocatedAttrPoints);

        // 3. Allocate +1 Skill point to skill 53 via 0x58 -> receive 0x5E s2c_playerskilllevel
        byte[] skillPtC2S = new byte[PlayerApplyAddSkillPointC2SPacket.PayloadSize];
        new PlayerApplyAddSkillPointC2SPacket(SkillId: 53, PointsToAdd: 1).WritePayload(skillPtC2S);
        await gs.SendEncryptedPayloadAsync(skillPtC2S);

        byte[] skillPtS2CBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_playerskilllevel);
        Assert.True(PlayerSkillLevelS2CPacket.TryParse(skillPtS2CBytes, out PlayerSkillLevelS2CPacket skillPtS2C));
        Assert.Equal(53, skillPtS2C.SkillId);
        Assert.Equal(2, skillPtS2C.SkillLevel);
        Assert.Equal(0, skillPtS2C.UnallocatedSkillPoints);

        // 4. Mark player defeated and send 0x74 c2s_npcrevive -> receive 0x89 s2c_playerrevive
        player.CurrentLife = 0;
        player.Doing = EntityDoingPosture.Death;

        byte[] reviveC2S = new byte[PlayerReviveC2SPacket.PayloadSize];
        new PlayerReviveC2SPacket(0).WritePayload(reviveC2S);
        await gs.SendEncryptedPayloadAsync(reviveC2S);

        byte[] reviveS2CBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_playerrevive);
        Assert.True(NpcReviveS2CPacket.TryParse(reviveS2CBytes, out NpcReviveS2CPacket reviveS2C));
        Assert.Equal(player.RuntimeNpcId, reviveS2C.NpcId);
        Assert.Equal(0, reviveS2C.ReviveType);
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
