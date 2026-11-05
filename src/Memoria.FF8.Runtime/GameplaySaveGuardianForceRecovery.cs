using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Pending historical progression kept separate from current active abilities and installed definitions.</summary>
public sealed record GameplaySaveGuardianForceRecovery([property: JsonRequired] int Version, [property: JsonRequired] GameplaySaveGuardianForceRecoveryRow?[] GuardianForces)
{
    internal static GameplaySaveGuardianForceRecovery? Capture(NativeCharacterVitals source)
    {
        ImmutableArray<GuardianForceProgression?> history = source.GuardianForceRecoveryProgression;
        if (history.IsDefaultOrEmpty)
        {
            return null;
        }
        if (history.Length != 16)
        {
            throw new InvalidDataException("Gameplay GF recovery requires sixteen historical progression rows.");
        }
        GameplaySaveGuardianForceRecovery result = new(1, history.Select(row => row is null ? null : CaptureRow(row)).ToArray());
        result.Validate();
        result.ValidateAbilities(source.MenuDefinitions.AbilityPoints.Length);
        return result.GuardianForces.Any(row => row?.Abilities.Any(entry => entry.ItemTaught) == true) ? result : null;
    }

    private static GameplaySaveGuardianForceRecoveryRow CaptureRow(GuardianForceProgression row)
    {
        if (row.Abilities.IsDefault)
        {
            throw new InvalidDataException("Gameplay GF recovery requires an explicit historical ability list.");
        }
        return new(row.Initialized, row.Abilities.Select(entry => entry is null ? null! : new GameplaySaveGuardianForceRecoveryAbility(entry.Ability, entry.Learned, entry.EarnedPoints, entry.Forgotten, entry.ItemTaught, entry.Active)).ToArray());
    }

    public void Validate()
    {
        if (Version != 1 || GuardianForces is not { Length: 16 } || GuardianForces.Any(row => row is not null && (row.Abilities is null || row.Abilities.Any(entry => entry is null || entry.Ability is <= 0 or >= 128 || entry.EarnedPoints is < 0 or > 255 || entry.Learned && entry.Forgotten) || row.Abilities.Select(entry => entry.Ability).Distinct().Count() != row.Abilities.Length)))
        {
            throw new InvalidDataException("Gameplay GF recovery requires distinct bounded historical abilities in sixteen nullable rows.");
        }
    }

    internal ImmutableArray<GuardianForceProgression?> Restore(int abilityCount)
    {
        Validate();
        ValidateAbilities(abilityCount);
        return GuardianForces.Select(row => row is null ? null : new GuardianForceProgression(row.Initialized, row.Abilities.Select(entry => new GuardianForceAbilityProgress(entry.Ability, entry.Learned, entry.EarnedPoints, entry.Forgotten, entry.ItemTaught, entry.Active)).ToImmutableArray())).ToImmutableArray();
    }

    private void ValidateAbilities(int abilityCount)
    {
        if (GuardianForces.Any(row => row?.Abilities.Any(entry => entry.Ability >= abilityCount) == true))
        {
            throw new NotSupportedException("Gameplay GF recovery contains an ability outside the selected gameplay definition contract.");
        }
    }
}

public sealed record GameplaySaveGuardianForceRecoveryRow([property: JsonRequired] bool Initialized, [property: JsonRequired] GameplaySaveGuardianForceRecoveryAbility[] Abilities);
public sealed record GameplaySaveGuardianForceRecoveryAbility([property: JsonRequired] int Ability, [property: JsonRequired] bool Learned, [property: JsonRequired] int EarnedPoints, [property: JsonRequired] bool Forgotten, [property: JsonRequired] bool ItemTaught, [property: JsonRequired] bool Active);
