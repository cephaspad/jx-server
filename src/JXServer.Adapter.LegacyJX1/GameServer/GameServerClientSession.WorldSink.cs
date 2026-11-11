using System.Buffers;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Contracts.Characters;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.Adapter.LegacyJX1.GameServer;

public sealed partial class GameServerClientSession : IWorldOutboundSink
{
    public void SendInitialWorldSyncBurst(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick,
        IReadOnlyList<CharacterSkillDto> skills)
    {
        var writer = new ArrayBufferWriter<byte>(1024);

        SyncWorldS2CPacket worldSync = SyncWorldS2CPacket.Create(
            playerState.MapId,
            playerState.CurrentMpsX,
            playerState.CurrentMpsY,
            currentFrameTick,
            mapNameUnicode);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in worldSync);

        SyncCurPlayerS2CPacket curPlayer = SyncCurPlayerS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in curPlayer);

        SyncCurPlayerSkillS2CPacket skillSync = SyncCurPlayerSkillS2CPacket.Create(skills);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in skillSync);

        SyncCurPlayerNormalS2CPacket normalSync = SyncCurPlayerNormalS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in normalSync);

        SyncNpcFullS2CPacket selfNpc = SyncNpcFullS2CPacket.CreateFromPlayer(playerState);
        _frameProcessor.WriteEncryptedS2CFrame(writer, selfNpc.ToPayloadBytes());

        DisabledFuncFlagS2CPacket disabledFlags = DisabledFuncFlagS2CPacket.Create(0);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in disabledFlags);

        SyncEndS2CPacket syncEnd = SyncEndS2CPacket.Create();
        _frameProcessor.WriteEncryptedS2CStruct(writer, in syncEnd);

        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendActiveWorldConfirmation(PlayerZoneState playerState)
    {
        var writer = new ArrayBufferWriter<byte>(64);

        SyncCurPlayerNormalS2CPacket normalSync = SyncCurPlayerNormalS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in normalSync);

        SyncNpcMinPlayerS2CPacket minPlayer = SyncNpcMinPlayerS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in minPlayer);

        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendServerPing(uint serverTimestamp)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        PingS2CPacket ping = PingS2CPacket.Create(serverTimestamp);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in ping);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendServerPingReply(uint echoedClientTimestamp)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        PingReplyS2CPacket reply = PingReplyS2CPacket.Create(echoedClientTimestamp);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in reply);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNpcFullSync(in EntityFullSyncSnapshot snapshot)
    {
        var writer = new ArrayBufferWriter<byte>(256);
        SyncNpcFullS2CPacket fullSync = SyncNpcFullS2CPacket.CreateFromSnapshot(in snapshot);
        _frameProcessor.WriteEncryptedS2CFrame(writer, fullSync.ToPayloadBytes());
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNpcCompactSync(
        uint npcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte lifePer,
        byte camp,
        byte dir)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var minSync = new SyncNpcMinS2CPacket(
            npcId,
            mpsX,
            mpsY,
            (byte)doing,
            lifePer,
            State: 0,
            camp,
            dir);
        Span<byte> payload = stackalloc byte[SyncNpcMinS2CPacket.PayloadSize];
        minSync.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendSelfPositionSync(PlayerZoneState playerState)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        SyncNpcMinPlayerS2CPacket minPlayer = SyncNpcMinPlayerS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in minPlayer);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var walk = new NpcWalkS2CPacket(npcId, destMpsX, destMpsY);
        Span<byte> payload = stackalloc byte[NpcWalkS2CPacket.PayloadSize];
        walk.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendRunBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var run = new NpcRunS2CPacket(npcId, destMpsX, destMpsY);
        Span<byte> payload = stackalloc byte[NpcRunS2CPacket.PayloadSize];
        run.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var jump = new NpcJumpS2CPacket(npcId, destMpsX, destMpsY);
        Span<byte> payload = stackalloc byte[NpcJumpS2CPacket.PayloadSize];
        jump.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendSitBroadcast(uint npcId)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var sit = new NpcSitS2CPacket(npcId);
        Span<byte> payload = stackalloc byte[NpcSitS2CPacket.PayloadSize];
        sit.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendWorldMapTransitionSync(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick)
    {
        AttachedMapId = playerState.MapId;

        var writer = new ArrayBufferWriter<byte>(256);
        SyncWorldS2CPacket worldSync = SyncWorldS2CPacket.Create(
            playerState.MapId,
            playerState.CurrentMpsX,
            playerState.CurrentMpsY,
            currentFrameTick,
            mapNameUnicode);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in worldSync);

        SyncNpcMinPlayerS2CPacket minPlayer = SyncNpcMinPlayerS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in minPlayer);

        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNotifyServerExchange(Guid handoffTicketGuid, uint ipAddressLittleEndian, ushort port)
    {
        var writer = new ArrayBufferWriter<byte>(64);
        var exchange = new NotifyPlayerExchangeS2CPacket(
            handoffTicketGuid,
            ipAddressLittleEndian,
            port);
        Span<byte> payload = stackalloc byte[NotifyPlayerExchangeS2CPacket.PayloadSize];
        exchange.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void Disconnect(PlayerDetachReason reason)
    {
        try
        {
            _cts.Cancel();
            _tcpClient.Close();
        }
        catch
        {
        }
    }
}
