namespace JXServer.Domain.Characters;

/// <summary>
/// Owned value object representing a character skill slot entry (<c>uint16 SkillId</c>, <c>byte BaseLevel</c>, <c>byte BonusLevel</c>, <c>uint16 ExpPercent</c>).
/// </summary>
public sealed record CharacterSkillValue(
    ushort SkillId,
    byte BaseLevel,
    byte BonusLevel = 0,
    ushort ExpPercent = 0);
