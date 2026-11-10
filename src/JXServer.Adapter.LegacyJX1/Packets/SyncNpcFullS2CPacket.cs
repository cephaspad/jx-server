using System.Buffers.Binary;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Encodings;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// Variable-length <c>s2c_syncnpc</c> (<c>0x4C</c>, <c>33B header + TCVN3 Name</c>) full NPC/Player synchronization packet.
/// </summary>
public readonly record struct SyncNpcFullS2CPacket(
    byte Camp,
    ElementSeries Series,
    byte LifePercent,
    int MapMpsY,
    byte DoingState,
    byte StateInfo,
    int MapMpsX,
    byte MenuState,
    short GoldType,
    EntityKind Kind,
    uint NpcId,
    int MasterId,
    ushort TemplateId,
    ushort Level,
    byte RankFfId,
    string NameUnicode)
{
    public const int HeaderSize = 33;

    public static SyncNpcFullS2CPacket CreateFromPlayer(PlayerZoneState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte lifePercent = state.MaxLife > 0
            ? (byte)Math.Clamp((state.CurrentLife * 100) / state.MaxLife, 0, 100)
            : (byte)100;

        ushort templateId = state.Gender == CharacterGender.Male ? (ushort)1 : (ushort)2;

        return new SyncNpcFullS2CPacket(
            Camp: (byte)state.Camp,
            Series: state.Series,
            LifePercent: lifePercent,
            MapMpsY: state.CurrentMpsY,
            DoingState: (byte)state.Doing,
            StateInfo: 0,
            MapMpsX: state.CurrentMpsX,
            MenuState: 0,
            GoldType: 0,
            Kind: EntityKind.Player,
            NpcId: state.RuntimeNpcId,
            MasterId: 0,
            TemplateId: templateId,
            Level: state.Level,
            RankFfId: 0,
            NameUnicode: state.RoleNameUnicode);
    }

    public static SyncNpcFullS2CPacket CreateFromSnapshot(in EntityFullSyncSnapshot snapshot)
    {
        ushort templateId = (ushort)(snapshot.NpcSettingIdx & 0xFFFFu);
        ushort level = (ushort)(snapshot.NpcSettingIdx >> 16);
        return new SyncNpcFullS2CPacket(
            Camp: (byte)snapshot.Camp,
            Series: snapshot.Series,
            LifePercent: snapshot.LifePer,
            MapMpsY: snapshot.MpsY,
            DoingState: (byte)snapshot.Doing,
            StateInfo: 0,
            MapMpsX: snapshot.MpsX,
            MenuState: 0,
            GoldType: snapshot.IsSpecialNpc ? (short)1 : (short)0,
            Kind: snapshot.Kind,
            NpcId: snapshot.NpcId,
            MasterId: 0,
            TemplateId: templateId,
            Level: level,
            RankFfId: 0,
            NameUnicode: snapshot.NameUtf8 ?? string.Empty);
    }

    public byte[] ToPayloadBytes()
    {
        byte[] nameBytes = Tcvn3Encoding.EncodeBytes(NameUnicode);
        int totalSize = HeaderSize + nameBytes.Length + 1;
        byte[] payload = new byte[totalSize];

        payload[0] = ProtocolOpcodes.s2c_syncnpc;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1, 2), (ushort)totalSize);
        payload[3] = Camp;
        payload[4] = (byte)Series;
        payload[5] = LifePercent;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(6, 4), MapMpsY);
        payload[10] = DoingState;
        payload[11] = StateInfo;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(12, 4), MapMpsX);
        payload[16] = MenuState;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(17, 2), GoldType);
        payload[19] = (byte)Kind;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20, 4), NpcId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(24, 4), MasterId);
        uint npcSettingIdx = ((uint)Level << 16) | TemplateId;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(28, 4), npcSettingIdx);
        payload[32] = RankFfId;
        nameBytes.CopyTo(payload.AsSpan(HeaderSize));
        payload[^1] = 0;

        return payload;
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out SyncNpcFullS2CPacket packet)
    {
        if (payload.Length < HeaderSize || payload[0] != ProtocolOpcodes.s2c_syncnpc)
        {
            packet = default;
            return false;
        }

        byte camp = payload[3];
        byte series = payload[4];
        byte lifePercent = payload[5];
        int mapY = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(6, 4));
        byte doingState = payload[10];
        byte stateInfo = payload[11];
        int mapX = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(12, 4));
        byte menuState = payload[16];
        short goldType = BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(17, 2));
        byte kind = payload[19];
        uint npcId = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(20, 4));
        int masterId = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(24, 4));
        uint npcSettingIdx = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(28, 4));
        ushort templateId = (ushort)(npcSettingIdx & 0xFFFFu);
        ushort level = (ushort)(npcSettingIdx >> 16);
        byte rankFfId = payload[32];

        string name = payload.Length > HeaderSize
            ? Tcvn3Encoding.DecodeNullTerminated(payload[HeaderSize..])
            : string.Empty;

        packet = new SyncNpcFullS2CPacket(
            camp,
            (ElementSeries)(series <= 4 ? series : 0),
            lifePercent,
            mapY,
            doingState,
            stateInfo,
            mapX,
            menuState,
            goldType,
            (EntityKind)(kind <= 3 ? kind : 0),
            npcId,
            masterId,
            templateId,
            level,
            rankFfId,
            name);
        return true;
    }
}
