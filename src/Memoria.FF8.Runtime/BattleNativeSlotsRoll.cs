namespace Memoria.FF8.Runtime;

public sealed record BattleNativeSlotsRoll(byte MagicId, byte Repetitions);

public static class NativeBattleSlotsRules
{
    /// <summary>Native48CBB0 consumes exactly four48F020 bytes, including the final repetition draw.</summary>
    public static BattleNativeSlotsRoll Roll(BattleNativeLimitDefinition rules, int level, byte crisis, byte levelRoll, byte bucketRoll, byte spellRoll, byte repeatRoll)
    {
        if (level is < 1 or > 100 || crisis is < 1 or > 4 || rules.SlotsSelectionBytes.IsDefaultOrEmpty || rules.SlotsSpellBytes.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native Slots requires the actual level, crisis and contiguous kernel26/27 bytes.");
        }
        int bucket = bucketRoll < 39 ? 0 : bucketRoll < 159 ? 1 : bucketRoll < 209 ? 2 : bucketRoll < 249 ? 3 : 4;
        int selectionOffset = 12 * bucket + levelRoll % 5 + level / 10 + crisis - 1;
        if (selectionOffset >= rules.SlotsSelectionBytes.Length)
        {
            throw new InvalidDataException("Native Slots selection lies outside the preserved contiguous kernel tables.");
        }
        int spellOffset = 2 * ((spellRoll & 7) + 8 * rules.SlotsSelectionBytes[selectionOffset]);
        if (spellOffset + 1 >= rules.SlotsSpellBytes.Length || rules.SlotsSpellBytes[spellOffset + 1] == 0)
        {
            throw new InvalidDataException("Native Slots pair has no preserved spell or nonzero repetition bound.");
        }
        return new BattleNativeSlotsRoll(rules.SlotsSpellBytes[spellOffset], (byte)(repeatRoll % rules.SlotsSpellBytes[spellOffset + 1] + 1));
    }
}
