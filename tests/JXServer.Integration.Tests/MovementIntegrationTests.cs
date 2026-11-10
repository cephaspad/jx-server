using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;
using Xunit;

namespace JXServer.Integration.Tests;

public sealed class MovementIntegrationTests
{
    [Fact]
    public async Task WalkRunJumpSit_AndAntiCheatValidation_ShouldBroadcastOverTcpWithExactWireLayouts()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        const uint tokenA1 = 0x12345678u;
        const uint tokenA2 = 0x9ABCDEF0u;

        await using SimulatedJxClientConnection gsA = await LoginAndEnterWorldAsync(
            harness,
            "moveu1",
            "Trương Tam Phong",
            tokenA1,
            tokenA2);

        await using SimulatedJxClientConnection gsB = await LoginAndEnterWorldAsync(
            harness,
            "moveu2",
            "Trương Vô Kỵ",
            0x11111111u,
            0x22222222u);

        // 1. Send forged dwCheckValue on 0x4B walk -> ignored without disconnecting
        byte[] forgedWalk = new byte[NpcWalkC2SPacket.PayloadSize];
        new NpcWalkC2SPacket(52064, 101696, CheckValue: 0xDEADBEEFu).WritePayload(forgedWalk);
        await gsA.SendEncryptedPayloadAsync(forgedWalk);

        // 2. Send valid 0x4B walk -> Observer B receives 0x50 s2c_npcwalk
        uint validWalkCheck = MovementAntiCheatConsts.CreateCheckValue(tokenA1, tokenA2, MovementAntiCheatConsts.WalkCheckKey);
        byte[] validWalk = new byte[NpcWalkC2SPacket.PayloadSize];
        new NpcWalkC2SPacket(52064, 101696, validWalkCheck).WritePayload(validWalk);
        await gsA.SendEncryptedPayloadAsync(validWalk);

        byte[] walkS2CBytes = await gsB.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_npcwalk);
        Assert.True(NpcWalkS2CPacket.TryParse(walkS2CBytes, out NpcWalkS2CPacket walkS2C));
        Assert.Equal(52064, walkS2C.DestMpsX);
        Assert.Equal(101696, walkS2C.DestMpsY);

        // 3. Send valid 0x4C run -> Observer B receives 0x51 s2c_npcrun AND 0x4D compact sync during step
        uint validRunCheck = MovementAntiCheatConsts.CreateCheckValue(tokenA1, tokenA2, MovementAntiCheatConsts.RunCheckKey);
        byte[] validRun = new byte[NpcRunC2SPacket.PayloadSize];
        new NpcRunC2SPacket(52120, 101696, validRunCheck).WritePayload(validRun);
        await gsA.SendEncryptedPayloadAsync(validRun);

        byte[] runS2CBytes = await gsB.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_npcrun);
        Assert.True(NpcRunS2CPacket.TryParse(runS2CBytes, out NpcRunS2CPacket runS2C));
        Assert.Equal(52120, runS2C.DestMpsX);
        Assert.Equal(101696, runS2C.DestMpsY);

        byte[] minSyncBytes = await gsB.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncnpcmin);
        Assert.True(SyncNpcMinS2CPacket.TryParse(minSyncBytes, out SyncNpcMinS2CPacket minSync));
        Assert.Equal(runS2C.NpcId, minSync.NpcId);

        // 4. Send 0x71 sit -> Observer B receives 0x53 s2c_npcsit
        byte[] sitC2S = new byte[NpcSitC2SPacket.PayloadSize];
        new NpcSitC2SPacket(1).WritePayload(sitC2S);
        await gsA.SendEncryptedPayloadAsync(sitC2S);

        byte[] sitS2CBytes = await gsB.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_npcsit);
        Assert.True(NpcSitS2CPacket.TryParse(sitS2CBytes, out NpcSitS2CPacket sitS2C));
        Assert.Equal(runS2C.NpcId, sitS2C.NpcId);

        // 5. Send 0x4E jump -> Observer B receives 0x54 s2c_npcjump
        byte[] jumpC2S = new byte[NpcJumpC2SPacket.PayloadSize];
        new NpcJumpC2SPacket(52032, 101696).WritePayload(jumpC2S);
        await gsA.SendEncryptedPayloadAsync(jumpC2S);

        byte[] jumpS2CBytes = await gsB.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_npcjump);
        Assert.True(NpcJumpS2CPacket.TryParse(jumpS2CBytes, out NpcJumpS2CPacket jumpS2C));
        Assert.Equal(52032, jumpS2C.DestMpsX);
        Assert.Equal(101696, jumpS2C.DestMpsY);
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
