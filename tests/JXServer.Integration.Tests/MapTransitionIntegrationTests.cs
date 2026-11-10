using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JXServer.Integration.Tests;

public sealed class MapTransitionIntegrationTests
{
    [Fact]
    public async Task TownGateFightToggle_AndCrossMapLevelGateBounceBack_ShouldExecuteOverTcp()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        var mapCatalog = harness.Services.GetRequiredService<IMapCatalog>();
        var trapCatalog = harness.Services.GetRequiredService<ITrapScriptCatalog>();

        Assert.True(mapCatalog.TryGetMapDescriptor(53, out MapDescriptor? map53));

        // Locate one TownGate trap cell and one CrossMapPortal trap cell with MinRequiredLevel > 1 in Map 53
        int gateMpsX = -1, gateMpsY = -1;
        int portalMpsX = -1, portalMpsY = -1;

        for (int ry = map53!.RegionBeginY; ry < map53.RegionBeginY + map53.RegionHeight; ry++)
        {
            for (int rx = map53.RegionBeginX; rx < map53.RegionBeginX + map53.RegionWidth; rx++)
            {
                if (!mapCatalog.TryGetRegionTrapGrid(53, rx, ry, out RegionTrapGrid? trapGrid) || trapGrid is null)
                {
                    continue;
                }

                for (int cx = 0; cx < WorldCoordinateConsts.RegionWidthCells; cx++)
                {
                    for (int cy = 0; cy < WorldCoordinateConsts.RegionHeightCells; cy++)
                    {
                        uint tid = trapGrid.GetTrapId(cx, cy);
                        if (tid == 0 || !trapCatalog.TryGetTrapBinding(tid, out PortalTrapBindingTemplate? b) || b is null)
                        {
                            continue;
                        }

                        if (gateMpsX < 0 && b.ActionKind == PortalTrapActionKind.ToggleFightStateAndSetPos)
                        {
                            gateMpsX = (rx * WorldCoordinateConsts.RegionWidthMps) + (cx * 32) + 16;
                            gateMpsY = (ry * WorldCoordinateConsts.RegionHeightMps) + (cy * 32) + 16;
                        }
                        else if (portalMpsX < 0 &&
                                 b.ActionKind == PortalTrapActionKind.ChangeMapPortal &&
                                 b.MinRequiredLevel > 1 &&
                                 !string.IsNullOrWhiteSpace(b.DeniedMessage))
                        {
                            portalMpsX = (rx * WorldCoordinateConsts.RegionWidthMps) + (cx * 32) + 16;
                            portalMpsY = (ry * WorldCoordinateConsts.RegionHeightMps) + (cy * 32) + 16;
                        }
                    }
                }
            }
        }

        Assert.True(gateMpsX > 0);
        Assert.True(portalMpsX > 0);

        // Login as Level 1 character in Map 53
        Guid ticket;
        await using (var bishop = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create("trapu1", "pass123", kickOnline: true));
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x41
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x37

            await bishop.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Metal, 53, "Tân Thủ Cấp 1").ToPayloadBytes());
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x8C
            var notify = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            ticket = notify.GetTicketGuid();
        }

        await using var gs = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.GameServerPort);
        await gs.SendEncryptedStructAsync(LogicLoginC2SPacket.Create(ticket));
        _ = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncend);
        await gs.SendEncryptedStructAsync(SyncEndC2SPacket.Create(1u, 1u));
        _ = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncnpcminplayer);

        // 1. Step onto Town Gate trap -> receives 0x4E position sync at nudged gate coordinate
        byte[] jumpGate = new byte[NpcJumpC2SPacket.PayloadSize];
        new NpcJumpC2SPacket(gateMpsX, gateMpsY).WritePayload(jumpGate);
        await gs.SendEncryptedPayloadAsync(jumpGate);

        byte[] posSync = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncnpcminplayer);
        Assert.Equal(ProtocolOpcodes.s2c_syncnpcminplayer, posSync[0]);

        // 2. Step onto Cross-Map Portal as Level 1 (< MinRequiredLevel) -> receives 0xFB system message and bounces back
        byte[] jumpPortal = new byte[NpcJumpC2SPacket.PayloadSize];
        new NpcJumpC2SPacket(portalMpsX, portalMpsY).WritePayload(jumpPortal);
        await gs.SendEncryptedPayloadAsync(jumpPortal);

        byte[] sysExtendBytes = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_extend);
        Assert.True(SystemInfoExtendS2CPacket.TryParse(sysExtendBytes, out SystemInfoExtendS2CPacket sysExtend));
        Assert.NotEmpty(sysExtend.MessageUtf8);
    }
}
