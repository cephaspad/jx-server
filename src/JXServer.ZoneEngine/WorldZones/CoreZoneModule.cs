using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Core engine module configuring base world session, movement, combat, inventory,
/// and phased simulation tick systems (<c>Domains 004, 006, 008, 009, 010, 011</c>).
/// </summary>
public sealed class CoreZoneModule : IZoneModule
{
    public void Configure(IZoneExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Domain 004: World Session Lifecycle
        var worldSessionHandler = new WorldSessionCommandHandler();
        builder.AddCommandHandler<AttachPlayerZoneCommand>(worldSessionHandler);
        builder.AddCommandHandler<AttachTransferredPlayerZoneCommand>(worldSessionHandler);
        builder.AddCommandHandler<CompletePlayerSyncZoneCommand>(worldSessionHandler);
        builder.AddCommandHandler<RequestEntitySyncZoneCommand>(worldSessionHandler);
        builder.AddCommandHandler<ProcessPlayerPingZoneCommand>(worldSessionHandler);
        builder.AddCommandHandler<DetachPlayerZoneCommand>(worldSessionHandler);

        // Domain 006: Movement & Obstacles
        var movementHandler = new MovementCommandHandler();
        builder.AddCommandHandler<MovePlayerZoneCommand>(movementHandler);
        builder.AddCommandHandler<JumpPlayerZoneCommand>(movementHandler);
        builder.AddCommandHandler<TogglePlayerSitZoneCommand>(movementHandler);

        // Domains 009 & 010: Combat, PK, Skills & Progression
        var combatHandler = new CombatProgressionCommandHandler();
        builder.AddCommandHandler<CastSkillZoneCommand>(combatHandler);
        builder.AddCommandHandler<ChangeAuraSkillZoneCommand>(combatHandler);
        builder.AddCommandHandler<ChangePkModeZoneCommand>(combatHandler);
        builder.AddCommandHandler<AllocateBaseAttributeZoneCommand>(combatHandler);
        builder.AddCommandHandler<AllocateSkillPointZoneCommand>(combatHandler);
        builder.AddCommandHandler<RevivePlayerZoneCommand>(combatHandler);

        // Domains 008 & 011: Inventory, Equipment & Loot
        var inventoryHandler = new InventoryLootCommandHandler();
        builder.AddCommandHandler<RequestGroundObjectSyncZoneCommand>(inventoryHandler);
        builder.AddCommandHandler<PickupGroundObjectZoneCommand>(inventoryHandler);
        builder.AddCommandHandler<MoveInventoryItemZoneCommand>(inventoryHandler);
        builder.AddCommandHandler<UseInventoryItemZoneCommand>(inventoryHandler);
        builder.AddCommandHandler<DropInventoryItemZoneCommand>(inventoryHandler);
        builder.AddCommandHandler<DivideInventoryItemZoneCommand>(inventoryHandler);

        // Phased Simulation Tick Systems (Phases 100-600)
        builder.AddTickSystem(new StatusEffectZoneTickSystem());
        builder.AddTickSystem(new MonsterAiZoneTickSystem());
        builder.AddTickSystem(new MissileZoneTickSystem());
        builder.AddTickSystem(new GroundObjectZoneTickSystem());
        builder.AddTickSystem(new MovementAndPostureZoneTickSystem());
        builder.AddTickSystem(new SessionHeartbeatZoneTickSystem());
    }
}
