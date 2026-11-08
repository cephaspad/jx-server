using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Contracts.Characters;
using JXServer.Primitives.Accounts;
using JXServer.Primitives.Characters;
using JXServer.ZoneEngine.PlayerSessions;
using Xunit;

namespace JXServer.Protocol.Tests;

/// <summary>
/// Verifies symmetric serialization and parsing of all US1 Bishop Gateway (<c>0x41</c>, <c>0x37</c>, <c>0x64</c>, <c>0x34</c>)
/// and GameServer (<c>0x42</c>, <c>0x49</c>, <c>0x44</c>, <c>0x45</c>, <c>0x46</c>, <c>0x4C</c>, <c>0x4E</c>, <c>0xA4</c>, <c>0x43</c>, <c>0x82</c>, <c>0x70</c>, <c>0x99</c>) packets.
/// </summary>
public sealed class GatewayAndWorldEntryPacketTests
{
    [Fact]
    public void AccountLoginC2S_And_ResultS2C_ShouldBe127Bytes_AndRoundTrip()
    {
        Assert.Equal(127, Marshal.SizeOf<AccountLoginC2SPacket>());
        Assert.Equal(127, Marshal.SizeOf<AccountLoginResultS2CPacket>());

        AccountLoginC2SPacket req = AccountLoginC2SPacket.Create("testbot01", "123456", kickOnline: true);
        Span<byte> raw = stackalloc byte[127];
        MemoryMarshal.Write(raw, in req);

        Assert.True(AccountLoginC2SPacket.TryParse(raw, out AccountLoginC2SPacket parsedReq));
        Assert.Equal("testbot01", parsedReq.GetUserName());
        Assert.Equal(32, parsedReq.GetPasswordHash().Length);
        Assert.Equal(1u, parsedReq.KickOnline);

        AccountLoginResultS2CPacket res = AccountLoginResultS2CPacket.Create(in parsedReq, LoginResultCode.Success, 86_400u);
        Assert.Equal((uint)LoginResultCode.Success, res.Param);
        Assert.Equal(86_400u, res.LeftTime);
        Assert.Equal("testbot01", res.GetUserName());
    }

    [Fact]
    public void RoleListResultS2CPacket_ShouldEncryptWith0x3042_AndRoundTripAllSlots()
    {
        var roster = new List<CharacterSummaryDto>
        {
            new(0, Guid.NewGuid(), "Kiều Phong", CharacterGender.Male, ElementSeries.Fire, 1),
            new(1, Guid.NewGuid(), "Tiểu Long Nữ", CharacterGender.Female, ElementSeries.Water, 10)
        };

        var packet = new RoleListResultS2CPacket(0xABCDu, roster);
        byte[] payload = packet.ToPayloadBytes();

        // 10B TProcessData + 2B (RoleCount, RefuseCount) + 2 * 35B = 82 bytes
        Assert.Equal(82, payload.Length);
        Assert.Equal(ProtocolOpcodes.s2c_roleserver_getrolelist_result, payload[0]);

        Assert.True(RoleListResultS2CPacket.TryParse(payload, out RoleListResultS2CPacket decoded));
        Assert.Equal(0xABCDu, decoded.Identity);
        Assert.Equal(2, decoded.Characters.Count);
        Assert.Equal("Kiều Phong", decoded.Characters[0].RoleNameUnicode);
        Assert.Equal(CharacterGender.Male, decoded.Characters[0].Gender);
        Assert.Equal(ElementSeries.Fire, decoded.Characters[0].Series);
        Assert.Equal("Tiểu Long Nữ", decoded.Characters[1].RoleNameUnicode);
    }

    [Fact]
    public void SelectCharacterC2SPacket_ShouldEncryptWith0x38BC_AndDecodeRoleName()
    {
        Assert.Equal(41, Marshal.SizeOf<SelectCharacterC2SPacket>());

        SelectCharacterC2SPacket pkt = SelectCharacterC2SPacket.Create("Độc Cô Cầu Bại", 42u);
        Span<byte> raw = stackalloc byte[41];
        MemoryMarshal.Write(raw, in pkt);

        Assert.True(SelectCharacterC2SPacket.TryParse(raw, out SelectCharacterC2SPacket parsed));
        Assert.True(parsed.TryDecodeRoleName(out string decodedName));
        Assert.Equal("Độc Cô Cầu Bại", decodedName);
    }

    [Fact]
    public void NotifyPlayerLoginS2CPacket_ShouldBe60Bytes_AndPreserveGuidIpAndPort()
    {
        Assert.Equal(60, Marshal.SizeOf<NotifyPlayerLoginS2CPacket>());

        Guid ticket = Guid.NewGuid();
        NotifyPlayerLoginS2CPacket pkt = NotifyPlayerLoginS2CPacket.Create(
            ticket,
            "Trương Vô Kỵ",
            permitted: true,
            "127.0.0.1",
            6666,
            identity: 7u);

        Assert.Equal(ticket, pkt.GetTicketGuid());
        Assert.Equal("Trương Vô Kỵ", pkt.GetRoleName());
        Assert.Equal("127.0.0.1", pkt.GetIpAddress().ToString());
        Assert.Equal(6666, pkt.Port);
        Assert.Equal(1, pkt.Permit);
    }

    [Fact]
    public void GameServerWorldSyncAndHeartbeatPackets_ShouldMatchExactWireSizes()
    {
        Assert.Equal(17, Marshal.SizeOf<LogicLoginC2SPacket>());
        Assert.Equal(34, Marshal.SizeOf<SyncWorldS2CPacket>());
        Assert.Equal(68, Marshal.SizeOf<SyncCurPlayerS2CPacket>());
        Assert.Equal(483, Marshal.SizeOf<SyncCurPlayerSkillS2CPacket>());
        Assert.Equal(14, Marshal.SizeOf<SyncCurPlayerNormalS2CPacket>());
        Assert.Equal(13, Marshal.SizeOf<SyncNpcMinPlayerS2CPacket>());
        Assert.Equal(5, Marshal.SizeOf<DisabledFuncFlagS2CPacket>());
        Assert.Equal(1, Marshal.SizeOf<SyncEndS2CPacket>());
        Assert.Equal(9, Marshal.SizeOf<SyncEndC2SPacket>());
        Assert.Equal(5, Marshal.SizeOf<PingS2CPacket>());
        Assert.Equal(9, Marshal.SizeOf<PingReplyC2SPacket>());
        Assert.Equal(5, Marshal.SizeOf<PingReplyS2CPacket>());

        var dummySink = new NoOpOutboundSink();
        var state = new PlayerZoneState(
            runtimeNpcId: 1001,
            accountId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            leaseEpoch: 1,
            roleNameUnicode: "Võ Đang Đạo Sĩ",
            gender: CharacterGender.Male,
            series: ElementSeries.Earth,
            level: 1,
            experience: 0,
            leadExperience: 0,
            mapId: 53,
            currentMpsX: 52032,
            currentMpsY: 101696,
            currentLife: 76,
            maxLife: 76,
            currentMana: 163,
            maxMana: 163,
            currentStamina: 100,
            maxStamina: 100,
            strength: 20,
            dexterity: 15,
            vitality: 25,
            energy: 40,
            lucky: 0,
            unallocatedAttributePoints: 0,
            unallocatedSkillPoints: 0,
            inventoryMoney: 0,
            repositoryMoney: 0,
            currentFactionId: 0xFF,
            firstFactionId: 0xFF,
            factionJoinCount: 0,
            initialTick: 10,
            outboundSink: dummySink);

        SyncWorldS2CPacket worldSync = SyncWorldS2CPacket.Create(53, 52032, 101696, 180u, "Ba Lăng huyện");
        Assert.Equal(53, worldSync.SubWorldId);
        Assert.Equal((99 << 16) | 101, worldSync.Region);
        Assert.Equal("Ba Lăng huyện", worldSync.GetMapName());

        SyncNpcFullS2CPacket selfNpc = SyncNpcFullS2CPacket.CreateFromPlayer(state);
        byte[] npcBytes = selfNpc.ToPayloadBytes();
        Assert.True(SyncNpcFullS2CPacket.TryParse(npcBytes, out SyncNpcFullS2CPacket parsedNpc));
        Assert.Equal(1001u, parsedNpc.NpcId);
        Assert.Equal(52032, parsedNpc.MapMpsX);
        Assert.Equal(101696, parsedNpc.MapMpsY);
        Assert.Equal("Võ Đang Đạo Sĩ", parsedNpc.NameUnicode);
    }

    private sealed class NoOpOutboundSink : IPlayerOutboundSink
    {
        public void SendInitialWorldSyncBurst(PlayerZoneState playerState, string mapNameUnicode, uint currentFrameTick, IReadOnlyList<CharacterSkillDto> skills) { }
        public void SendActiveWorldConfirmation(PlayerZoneState playerState) { }
        public void SendServerPing(uint serverTimestamp) { }
        public void SendServerPingReply(uint echoedClientTimestamp) { }
        public void Disconnect(PlayerDetachReason reason) { }
    }
}
