namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>Initial diagnostic level preparation through existing native experience awards, without lowering saved levels.</summary>
    public NativeCharacterVitals WithDiagnosticBattleLevel(int row, int level, NativeBattleExperienceDefinition definition)
    {
        Validate();
        definition.Validate();
        if (level is < 1 or > 100 || row is < 0 or >= 8 || !CombatParty.Contains((byte)row) || !string.Equals(KernelSha256, definition.KernelSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Diagnostic level preparation requires an actual combat-party row, level1..100 and matching installed experience resources.");
        }
        NativeCharacterVital character = Records[row];
        NativeStatCurve curve = Definitions.Characters[character.CharacterId];
        int current = NativeStatDefinitions.Level(character.Experience, curve);
        if (level < current || character.Experience > int.MaxValue)
        {
            throw new NotSupportedException("Diagnostic level preparation only awards experience; lowering a saved level or rewriting unsupported experience is unavailable.");
        }
        if (level == current)
        {
            return this;
        }
        // Search the already proved native level function, rather than reproducing
        // its curve or assigning an inferred XP value to the saved record.
        uint low = character.Experience;
        uint high = int.MaxValue;
        while (low < high)
        {
            uint middle = low + (high - low) / 2;
            if (NativeStatDefinitions.Level(middle, curve) >= level)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }
        NativeCharacterVitals next = this;
        uint remaining = low - character.Experience;
        while (remaining != 0)
        {
            ushort award = checked((ushort)Math.Min(remaining, ushort.MaxValue));
            next = next.AwardBattleExperience(new Dictionary<byte, ushort> { [checked((byte)row)] = award }, new Dictionary<byte, ushort>(), definition);
            remaining -= award;
        }
        if (NativeStatDefinitions.Level(next.Records[row].Experience, curve) != level)
        {
            throw new NotSupportedException("The original experience award/cap did not produce the requested diagnostic level for this physical character owner.");
        }
        return next;
    }
}
