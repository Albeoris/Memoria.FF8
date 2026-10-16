namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEntryEnemy(ushort SavedStatuses, byte? Information254);
public sealed record NativeBattleEntryInput(byte FormationFlags, byte EncounterAbilityFlags);

public static class NativeBattleEntryRules
{
    public static ushort MergeFlags(ushort requestedFlags, byte formationFlags)
    {
        int flags = (formationFlags & 0xE0) != 0 ? requestedFlags & 0xFF1F : requestedFlags;
        return (ushort)(flags | formationFlags & 0xEF);
    }

    /// <summary>Original48AFD0 advantageBYTE1D28E08 after level/stat allocation and before47DAC0 entry graphics.</summary>
    public static byte Resolve(uint battleFlags, byte fieldEncounterAbilities, IReadOnlyList<NativeBattleEntryEnemy>? enemySlots, ref NativeBattleRandomState random)
    {
        if ((battleFlags & 0x800000) != 0)
        {
            return 0;
        }
        if ((battleFlags & 0x200000) != 0)
        {
            return 1;
        }
        if ((battleFlags & 0x400000) != 0)
        {
            return 2;
        }
        if (enemySlots is null || enemySlots.Count != 4 || enemySlots.Any(slot => slot is null))
        {
            throw new NotSupportedException("Native advantage requires all four physical enemy saved-status/DAT254 owners.");
        }
        NativeBattleEntryEnemy[] eligible = enemySlots.Where(slot => (slot.SavedStatuses & 1) == 0).ToArray();
        if (eligible.Any(slot => slot.Information254 is null))
        {
            throw new NotSupportedException("Native advantage reached an enemy without its original informationBYTE254.");
        }
        int offset = eligible.All(slot => (slot.Information254!.Value & 1) != 0) ? 20 : 0;
        if (eligible.All(slot => (slot.Information254!.Value & 2) != 0))
        {
            offset -= 20;
        }
        random = random.Draw(out byte roll);
        int chance = roll + offset;
        if ((fieldEncounterAbilities & 1) != 0)
        {
            chance -= 20;
        }
        int category = chance < 20 ? 0 : chance >= 236 ? 2 : 1;
        if ((fieldEncounterAbilities & 1) != 0 && category == 2)
        {
            category = 1;
        }
        if (category == 0 && eligible.Any(slot => (slot.Information254!.Value & 4) != 0))
        {
            category = 1;
        }
        if (category == 1)
        {
            return 0;
        }
        //48B2A0 ->48F0C0(128,255), including equality and its unconditional second BYTE draw.
        random = random.Draw(out byte sideRoll);
        return (byte)((category == 0 ? 4 : 2) - (sideRoll <= 128 ? 1 : 0));
    }
}
