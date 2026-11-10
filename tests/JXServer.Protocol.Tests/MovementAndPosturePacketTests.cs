using System.Buffers.Binary;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.WorldZones;
using Xunit;

namespace JXServer.Protocol.Tests;

public sealed class MovementAndPosturePacketTests
{
    [Fact]
    public void MovementAntiCheatConsts_ShouldValidateWalkAndRunCheckValues()
    {
        const uint token1 = 0x12345678u;
        const uint token2 = 0x9ABCDEF0u;

        uint walkCheck = MovementAntiCheatConsts.CreateCheckValue(token1, token2, MovementAntiCheatConsts.WalkCheckKey);
        uint runCheck = MovementAntiCheatConsts.CreateCheckValue(token1, token2, MovementAntiCheatConsts.RunCheckKey);

        Assert.True(MovementAntiCheatConsts.IsValidCheckValue(walkCheck, token1, token2, MovementAntiCheatConsts.WalkCheckKey));
        Assert.False(MovementAntiCheatConsts.IsValidCheckValue(walkCheck, token1, token2, MovementAntiCheatConsts.RunCheckKey));
        Assert.True(MovementAntiCheatConsts.IsValidCheckValue(runCheck, token1, token2, MovementAntiCheatConsts.RunCheckKey));
        Assert.False(MovementAntiCheatConsts.IsValidCheckValue(runCheck + 1, token1, token2, MovementAntiCheatConsts.RunCheckKey));
    }

    [Fact]
    public void NpcWalkS2CPacket_And_NpcRunS2CPacket_ShouldHaveDistinctAsymmetricWireLayouts()
    {
        const uint npcId = 0x11223344u;
        const int destMpsX = 51500;
        const int destMpsY = 102800;

        var walk = new NpcWalkS2CPacket(npcId, destMpsX, destMpsY);
        Span<byte> walkBytes = stackalloc byte[NpcWalkS2CPacket.PayloadSize];
        walk.WritePayload(walkBytes);

        // s2c_npcwalk (0x50): <BIii -> NpcId at +1, DestMpsY at +5, DestMpsX at +9
        Assert.Equal(ProtocolOpcodes.s2c_npcwalk, walkBytes[0]);
        Assert.Equal(npcId, BinaryPrimitives.ReadUInt32LittleEndian(walkBytes.Slice(1, 4)));
        Assert.Equal(destMpsY, BinaryPrimitives.ReadInt32LittleEndian(walkBytes.Slice(5, 4)));
        Assert.Equal(destMpsX, BinaryPrimitives.ReadInt32LittleEndian(walkBytes.Slice(9, 4)));
        Assert.True(NpcWalkS2CPacket.TryParse(walkBytes, out NpcWalkS2CPacket parsedWalk));
        Assert.Equal(walk, parsedWalk);

        var run = new NpcRunS2CPacket(npcId, destMpsX, destMpsY);
        Span<byte> runBytes = stackalloc byte[NpcRunS2CPacket.PayloadSize];
        run.WritePayload(runBytes);

        // s2c_npcrun (0x51): <BiIi -> DestMpsY at +1, NpcId at +5, DestMpsX at +9
        Assert.Equal(ProtocolOpcodes.s2c_npcrun, runBytes[0]);
        Assert.Equal(destMpsY, BinaryPrimitives.ReadInt32LittleEndian(runBytes.Slice(1, 4)));
        Assert.Equal(npcId, BinaryPrimitives.ReadUInt32LittleEndian(runBytes.Slice(5, 4)));
        Assert.Equal(destMpsX, BinaryPrimitives.ReadInt32LittleEndian(runBytes.Slice(9, 4)));
        Assert.True(NpcRunS2CPacket.TryParse(runBytes, out NpcRunS2CPacket parsedRun));
        Assert.Equal(run, parsedRun);
    }

    [Fact]
    public void NpcJump_And_NpcSit_And_NotifyPlayerExchange_ShouldRoundTrip()
    {
        var jumpS2C = new NpcJumpS2CPacket(10005u, 52000, 103000);
        Span<byte> jumpBuf = stackalloc byte[NpcJumpS2CPacket.PayloadSize];
        jumpS2C.WritePayload(jumpBuf);
        Assert.Equal(103000, BinaryPrimitives.ReadInt32LittleEndian(jumpBuf.Slice(1, 4)));
        Assert.Equal(52000, BinaryPrimitives.ReadInt32LittleEndian(jumpBuf.Slice(5, 4)));
        Assert.Equal(10005u, BinaryPrimitives.ReadUInt32LittleEndian(jumpBuf.Slice(9, 4)));
        Assert.True(NpcJumpS2CPacket.TryParse(jumpBuf, out NpcJumpS2CPacket parsedJump));
        Assert.Equal(jumpS2C, parsedJump);

        var sitC2S = new NpcSitC2SPacket(1);
        Span<byte> sitC2SBuf = stackalloc byte[NpcSitC2SPacket.PayloadSize];
        sitC2S.WritePayload(sitC2SBuf);
        Assert.True(NpcSitC2SPacket.TryParse(sitC2SBuf, out NpcSitC2SPacket parsedSitC2S));
        Assert.True(parsedSitC2S.IsSitting);

        var sitS2C = new NpcSitS2CPacket(10005u);
        Span<byte> sitS2CBuf = stackalloc byte[NpcSitS2CPacket.PayloadSize];
        sitS2C.WritePayload(sitS2CBuf);
        Assert.True(NpcSitS2CPacket.TryParse(sitS2CBuf, out NpcSitS2CPacket parsedSitS2C));
        Assert.Equal(10005u, parsedSitS2C.NpcId);

        Guid handoffGuid = Guid.NewGuid();
        var exchange = new NotifyPlayerExchangeS2CPacket(handoffGuid, 0x0100007Fu, 6666);
        Span<byte> exchangeBuf = stackalloc byte[NotifyPlayerExchangeS2CPacket.PayloadSize];
        exchange.WritePayload(exchangeBuf);
        Assert.Equal(6666, BinaryPrimitives.ReadUInt16LittleEndian(exchangeBuf.Slice(21, 2)));
        Assert.True(NotifyPlayerExchangeS2CPacket.TryParse(exchangeBuf, out NotifyPlayerExchangeS2CPacket parsedExchange));
        Assert.Equal(exchange, parsedExchange);
    }
}
