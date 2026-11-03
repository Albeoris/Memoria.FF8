namespace Memoria.FF8.Runtime;

public static class EncounterActorProperties
{
    public const string ReflectResistanceMultiplier = "statusResistances.reflectMultiplier";

    public static BattleCombatantDefinition ScaleReflectResistance(BattleCombatantDefinition actor, double multiplier)
    {
        if (!double.IsFinite(multiplier) || multiplier is < 0 or > 100)
        {
            throw new InvalidDataException("Reflect resistance multiplier must be finite and between 0 and 100.");
        }
        byte[] resistances;
        if (actor.NativeStatusResistances.Length == 40)
        {
            resistances = actor.NativeStatusResistances.ToArray();
        }
        else if (actor.NativeTemplate?.NativeStatusAllocation is { } native && !native.Resistances.IsDefault && native.Resistances.Length == 40)
        {
            resistances = native.Resistances.ToArray();
        }
        else
        {
            throw new InvalidDataException("Reflect resistance requires the selected actor's native forty-byte status resistance bank.");
        }
        // The resistance bank contains eight saved-status entries, then the battle-status bits; Reflect is bit7.
        const int index = 8 + 7;
        resistances[index] = (byte)Math.Clamp(Math.Floor(resistances[index] * multiplier), 0, byte.MaxValue);
        NativeBattleEnemyTemplate? template = actor.NativeTemplate;
        if (template?.NativeStatusAllocation is { } allocation)
        {
            template = template with { NativeStatusAllocation = allocation with { Resistances = System.Collections.Immutable.ImmutableArray.CreateRange(resistances) } };
        }
        return actor with { NativeStatusResistances = resistances, NativeTemplate = template };
    }

    /// <summary>Truncates once after multiplication and retains a positive signed HP owner.</summary>
    public static int ScaleMaximumHp(int baseline, double multiplier)
    {
        if (baseline <= 0 || !double.IsFinite(multiplier) || multiplier <= 0 || multiplier > 100)
        {
            throw new InvalidDataException("Maximum HP requires a positive baseline and a finite multiplier in (0, 100].");
        }
        return (int)Math.Clamp(Math.Floor(baseline * multiplier), 1, int.MaxValue);
    }
}
