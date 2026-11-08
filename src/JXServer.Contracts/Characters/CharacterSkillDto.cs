namespace JXServer.Contracts.Characters;

/// <summary>
/// DTO representing a character skill slot (<c>0x45</c> <c>s2c_synccurplayerskill</c>).
/// </summary>
public sealed record CharacterSkillDto(
    ushort SkillId,
    byte BaseLevel,
    byte BonusLevel = 0,
    ushort ExpPercent = 0);
