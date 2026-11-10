namespace JXServer.Primitives.WorldZones;

/// <summary>
/// Implements the exact <c>KPlayer::IsCheckValueValid</c> (<c>0x080bf0a0</c>) anti-cheat checksum verification
/// against the two 32-bit client tokens supplied in <c>c2s_syncend</c> (<c>0x43</c>).
/// </summary>
public static class MovementAntiCheatConsts
{
    public const uint WalkCheckKey = 0x24A1u;       // 0x4B c2s_npcwalk (9377)
    public const uint RunCheckKey = 0x1D78u;        // 0x4C c2s_npcrun (7544)
    public const uint SkillCheckKey = 0x3AB9u;      // 0x4D c2s_npcskill (15033)
    public const uint TradeStartCheckKey = 0x451Fu; // 0x6B c2s_tradeapplystart (17695)
    public const uint DialogNpcCheckKey = 0x506Cu;  // 0x6E c2s_dialognpc (20588)
    public const uint TradeReplyCheckKey = 0x67CAu; // 0x75 c2s_tradereplystart (26570)

    /// <summary>
    /// Verifies <c>dwCheckValue != 0</c> and <c>((dwCheckValue ^ token1 ^ token2) % key) == 0</c>.
    /// </summary>
    public static bool IsValidCheckValue(uint checkValue, uint token1, uint token2, uint key)
    {
        if (key == 0)
        {
            return true;
        }

        if (checkValue == 0)
        {
            return false;
        }

        uint unmasked = checkValue ^ token1 ^ token2;
        return unmasked != 0 && (unmasked % key) == 0;
    }

    /// <summary>
    /// Generates a valid <c>dwCheckValue</c> for the specified <paramref name="key"/> and client tokens.
    /// </summary>
    public static uint CreateCheckValue(uint key, uint token1, uint token2, uint multiplier = 1u)
    {
        if (key == 0)
        {
            return 0;
        }

        uint clampedMultiplier = multiplier == 0 ? 1u : multiplier;
        return unchecked((clampedMultiplier * key) ^ token1 ^ token2);
    }
}
