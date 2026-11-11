using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Domain command handler executing combat, PK mode, aura skills, attribute allocation,
/// skill progression, and revival commands (<c>Domain 009</c> &amp; <c>Domain 010</c>).
/// </summary>
public sealed class CombatProgressionCommandHandler :
    IZoneCommandHandler<CastSkillZoneCommand>,
    IZoneCommandHandler<ChangeAuraSkillZoneCommand>,
    IZoneCommandHandler<ChangePkModeZoneCommand>,
    IZoneCommandHandler<AllocateBaseAttributeZoneCommand>,
    IZoneCommandHandler<AllocateSkillPointZoneCommand>,
    IZoneCommandHandler<RevivePlayerZoneCommand>
{
    public ValueTask HandleAsync(
        CastSkillZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleCastSkill(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        ChangeAuraSkillZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleChangeAuraSkill(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        ChangePkModeZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleChangePkMode(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        AllocateBaseAttributeZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleAllocateBaseAttribute(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        AllocateSkillPointZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleAllocateSkillPoint(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        RevivePlayerZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return context.Worker.HandleRevivePlayerAsync(command, context.CurrentFrame, cancellationToken);
    }
}
