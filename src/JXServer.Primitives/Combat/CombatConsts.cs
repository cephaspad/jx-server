namespace JXServer.Primitives.Combat;

/// <summary>
/// Combat, hit-rating, resistance, and timing constants verified from <c>jx-win-src</c>.
/// </summary>
public static class CombatConsts
{
    public const int MinHitPercent = 5;
    public const int MaxHitPercent = 95;
    public const int DefaultMaxResistPercent = 75;
    public const int HardCapResistPercent = 95;
    public const int UnarmedStrengthDivisor = 5;
    public const int DefaultPkDamageRatePercent = 20;
    public const int SwitchToPeaceCooldownFrames = 3240;
    public const byte BroadcastNetCmdHurt = 9;
    public const int ConqueredSeriesDamageBonusPercent = 20;
    public const int ConqueredSeriesDamagePenaltyPercent = -20;
    public const int MaxActiveMissilesPerZone = 2048;
}
