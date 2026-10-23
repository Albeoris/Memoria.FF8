using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeMagicStock([property: JsonRequired] byte SpellId, [property: JsonRequired] byte Count);
public sealed record NativeStatCurve([property: JsonRequired] byte ExperienceStep, [property: JsonRequired] byte ExperienceFraction, [property: JsonRequired] byte HitPointLinear, [property: JsonRequired] byte HitPointDivisor, [property: JsonRequired] byte HitPointConstant);
public sealed record NativeStatModifier([property: JsonRequired] byte Flags, [property: JsonRequired] byte Selector, [property: JsonRequired] byte Percentage);
public sealed record NativeGuardianForceLearnedBank([property: JsonRequired] ulong Low, [property: JsonRequired] ulong High)
{
    public bool Has(int ability)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ability);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ability, 127);
        return ((ability < 64 ? Low : High) & (1UL << (ability & 63))) != 0;
    }
}
public sealed record NativeGuardianForceVital([property: JsonRequired] uint Experience, [property: JsonRequired] byte Flags, [property: JsonRequired] ushort HitPoints, [property: JsonRequired] NativeGuardianForceLearnedBank LearnedBank, [property: JsonRequired] ushort? MaximumHitPoints, [property: JsonRequired] ImmutableArray<byte> AbilityProgress, [property: JsonRequired] byte Learning, [property: JsonRequired] uint ForgottenAbilities)
{
    public GuardianForceProgression? Progression { get; init; }
    public GuardianForceCompatibilityProjection? LastProjection { get; init; }
    public bool HasLearnedAbility(int ability)
    {
        return Progression is null ? LearnedBank.Has(ability) : Progression.Abilities.Any(entry => entry.Ability == ability && entry.Learned && !entry.Forgotten && entry.Active);
    }
    public ushort? KillCount { get; init; }
    public ushort? DeathCount { get; init; }
}

/// <summary>Immutable inputs decoded from the authenticated current kernel sections, not a cache of a saved character's identity-dependent maximum.</summary>
public sealed record NativeStatDefinitions([property: JsonRequired] ImmutableArray<NativeStatCurve> Characters, [property: JsonRequired] ImmutableArray<byte> MagicHitPoints, [property: JsonRequired] ImmutableArray<NativeStatModifier> CharacterPassives, [property: JsonRequired] ImmutableArray<NativeStatCurve> GuardianForces, [property: JsonRequired] ImmutableArray<NativeStatModifier> GuardianForceModifiers)
{
    public void Validate()
    {
        if (Characters.IsDefault || Characters.Length != 11 || MagicHitPoints.IsDefault || MagicHitPoints.Length != 57 || CharacterPassives.IsDefault || CharacterPassives.Length != 19 || GuardianForces.IsDefault || GuardianForces.Length != 16 || GuardianForceModifiers.IsDefault || GuardianForceModifiers.Length != 9 || Characters.Concat(GuardianForces).Any(curve => curve is null || curve.HitPointDivisor == 0) || CharacterPassives.Concat(GuardianForceModifiers).Any(modifier => modifier is null))
        {
            throw new InvalidDataException("Native saved-stat definitions require the eleven character, magic, passive and sixteen GF kernel tables.");
        }
    }

    public static byte Level(uint experience, NativeStatCurve curve)
    {
        int level = 1;
        int accumulated = 10 * curve.ExperienceStep;
        int fractional = curve.ExperienceFraction;
        while (level < 100 && unchecked((int)experience) >= accumulated + level * fractional / 256)
        {
            level++;
            fractional += curve.ExperienceFraction;
            accumulated += 10 * curve.ExperienceStep;
        }
        return (byte)level;
    }

    public ushort CharacterMaximum(NativeCharacterVital character)
    {
        NativeStatCurve curve = Characters[character.CharacterId];
        int level = Level(character.Experience, curve);
        byte spell = character.Junction.MagicJunctions[0];
        int quantity = spell == 0 ? 0 : character.MagicInventory.FirstOrDefault(stock => stock.SpellId == spell)?.Count ?? 0;
        return CharacterMaximum(character.CharacterId, level, character.HitPointBonus, spell, quantity, character.Junction.Abilities);
    }

    public ushort CharacterMaximum(int characterId, int level, ushort hitPointBonus, byte spell, int quantity, uint abilities)
    {
        NativeStatCurve curve = Characters[characterId];
        if (spell >= MagicHitPoints.Length)
        {
            throw new NotSupportedException("Native HP junction requires its actual magic kernel definition.");
        }
        int baseMaximum = hitPointBonus + curve.HitPointConstant + level * curve.HitPointLinear + quantity * MagicHitPoints[spell] - 10 * level * level / curve.HitPointDivisor;
        int percentage = 100;
        for (int slot = 0; slot < 4; slot++)
        {
            byte ability = unchecked((byte)(abilities >> (slot * 8)));
            if (ability is >= 39 and < 58 && CharacterPassives[ability - 39].Selector == 0)
            {
                percentage += CharacterPassives[ability - 39].Percentage;
            }
        }
        int maximum = unchecked((byte)percentage * baseMaximum) / 100;
        return unchecked((ushort)Math.Min(maximum, 9999));
    }

    public ushort GuardianForceMaximum(int id, NativeGuardianForceVital guardianForce)
    {
        NativeStatCurve curve = GuardianForces[id];
        int level = Level(guardianForce.Experience, curve);
        int percentage = 100;
        for (int bit = 19; bit < 28; bit++)
        {
            NativeStatModifier modifier = GuardianForceModifiers[bit - 19];
            if (guardianForce.HasLearnedAbility(bit + 64) && modifier.Selector == 1)
            {
                percentage += modifier.Percentage;
            }
        }
        short baseMaximum = unchecked((short)(curve.HitPointConstant + level * curve.HitPointLinear + 10 * level * level / curve.HitPointDivisor));
        int maximum = unchecked((byte)percentage * baseMaximum) / 100;
        return unchecked((ushort)Math.Min(maximum, 9999));
    }
}
