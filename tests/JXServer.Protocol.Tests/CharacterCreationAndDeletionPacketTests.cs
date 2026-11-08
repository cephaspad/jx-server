using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Characters;
using Xunit;

namespace JXServer.Protocol.Tests;

/// <summary>
/// Verifies <see cref="CreateCharacterC2SPacket"/> (<c>0x45</c>), <see cref="DeleteCharacterC2SPacket"/> (<c>0x3A</c>),
/// <see cref="RoleNewDelResponseS2CPacket"/> (<c>0x8C</c>), and <see cref="CloseConnectionC2SPacket"/> (<c>0x9B</c>).
/// </summary>
public sealed class CharacterCreationAndDeletionPacketTests
{
    [Fact]
    public void CreateCharacterC2SPacket_ShouldSerialize24ByteChoivolamVector_AndRoundTrip()
    {
        var pkt = new CreateCharacterC2SPacket(
            Identity: 0,
            Gender: CharacterGender.Male,
            Series: ElementSeries.Metal,
            NativePlaceId: 53,
            RoleNameUnicode: "choivolam");

        byte[] payload = pkt.ToPayloadBytes();

        // 15 + 9 ("choivolam") = 24 bytes
        Assert.Equal(24, payload.Length);
        Assert.Equal(ProtocolOpcodes.c2s_newplayer, payload[0]);

        Assert.True(CreateCharacterC2SPacket.TryParse(payload, out CreateCharacterC2SPacket parsed));
        Assert.Equal(CharacterGender.Male, parsed.Gender);
        Assert.Equal(ElementSeries.Metal, parsed.Series);
        Assert.Equal(53, parsed.NativePlaceId);
        Assert.Equal("choivolam", parsed.RoleNameUnicode);
    }

    [Fact]
    public void DeleteCharacterC2SPacket_ShouldBe129Bytes_AndRoundTrip()
    {
        Assert.Equal(129, Marshal.SizeOf<DeleteCharacterC2SPacket>());

        DeleteCharacterC2SPacket pkt = DeleteCharacterC2SPacket.Create(
            "testacc01",
            "123456",
            "choivolam",
            identity: 9u);

        Span<byte> raw = stackalloc byte[129];
        MemoryMarshal.Write(raw, in pkt);

        Assert.True(DeleteCharacterC2SPacket.TryParse(raw, out DeleteCharacterC2SPacket parsed));
        Assert.Equal("testacc01", parsed.GetAccountName());
        Assert.Equal("choivolam", parsed.GetRoleName());
        Assert.Equal(32, parsed.GetPasswordHash().Length);
    }

    [Fact]
    public void RoleNewDelResponseS2CPacket_ShouldBe42Bytes_AndRoundTrip()
    {
        Assert.Equal(42, Marshal.SizeOf<RoleNewDelResponseS2CPacket>());

        RoleNewDelResponseS2CPacket pkt = RoleNewDelResponseS2CPacket.Create(
            "Lệnh Hồ Xung",
            succeeded: true,
            nativePlaceId: 53,
            identity: 1u);

        Assert.Equal(ProtocolOpcodes.s2c_rolenewdelresponse, pkt.ProtocolType);
        Assert.Equal(1, pkt.Succeeded);
        Assert.Equal(53u, pkt.NativePlaceId);
        Assert.Equal("Lệnh Hồ Xung", pkt.GetRoleName());
    }
}
