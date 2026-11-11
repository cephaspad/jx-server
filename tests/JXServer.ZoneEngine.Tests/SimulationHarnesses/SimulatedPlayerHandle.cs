using JXServer.Contracts.Characters;
using JXServer.ZoneEngine.Chats;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

/// <summary>
/// Convenient simulation handle for an attached player within <see cref="ZoneSimulationHarness"/> (<c>FR-008</c>).
/// Encapsulates the character profile, the outbound event recorder, active map, and high-level command dispatch helpers.
/// </summary>
public sealed class SimulatedPlayerHandle
{
    public CharacterDto Character { get; }

    public Guid CharacterId => Character.Id;

    public PlayerOutboundEventRecorder Recorder { get; }

    public long LeaseEpoch { get; }

    public ushort MapId { get; internal set; }

    public ZoneSimulationHarness Harness { get; }

    public SimulatedPlayerHandle(
        CharacterDto character,
        PlayerOutboundEventRecorder recorder,
        long leaseEpoch,
        ushort mapId,
        ZoneSimulationHarness harness)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(harness);

        Character = character;
        Recorder = recorder;
        LeaseEpoch = leaseEpoch;
        MapId = mapId;
        Harness = harness;
    }

    public PlayerZoneState? GetZoneState()
    {
        if (Harness.TryGetZone(MapId, out var worker) && worker.TryGetPlayer(CharacterId, out var player))
        {
            return player;
        }

        return null;
    }

    public void Move(int targetMpsX, int targetMpsY, bool isRun = false)
    {
        Harness.EnqueueCommand(MapId, new MovePlayerZoneCommand(CharacterId, LeaseEpoch, targetMpsX, targetMpsY, isRun));
    }

    public void Jump(int targetMpsX, int targetMpsY)
    {
        Harness.EnqueueCommand(MapId, new JumpPlayerZoneCommand(CharacterId, LeaseEpoch, targetMpsX, targetMpsY));
    }

    public void ToggleSit(bool isSitting)
    {
        Harness.EnqueueCommand(MapId, new TogglePlayerSitZoneCommand(CharacterId, LeaseEpoch, isSitting));
    }

    public void CastSkill(ushort skillId, int targetMpsX, int targetMpsY)
    {
        Harness.EnqueueCommand(MapId, new CastSkillZoneCommand(CharacterId, LeaseEpoch, skillId, targetMpsX, targetMpsY));
    }

    public void TalkNearby(string messageUtf8)
    {
        Harness.EnqueueCommand(MapId, new SendNearbyTalkZoneCommand(CharacterId, LeaseEpoch, messageUtf8));
    }

    public void Detach(PlayerDetachReason reason = PlayerDetachReason.CleanLogout)
    {
        Harness.EnqueueCommand(MapId, new DetachPlayerZoneCommand(CharacterId, LeaseEpoch, reason));
    }
}
