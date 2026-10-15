using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Current kernel14 byte5..7 flags for character abilities58..77, assembled by native495530.</summary>
public sealed record NativeBattleExperienceDefinition(string KernelSha256, ImmutableArray<uint> CharacterAbilityFlags)
{
    public void Validate()
    {
        if (KernelSha256 is not { Length: 64 } || !KernelSha256.All(Uri.IsHexDigit) || CharacterAbilityFlags.IsDefault || CharacterAbilityFlags.Length != 20 || CharacterAbilityFlags.Any(flags => flags > 0xFFFFFF))
        {
            throw new InvalidDataException("Battle experience requires the actual current character ability kernel flags.");
        }
    }
}

public sealed partial record NativeCharacterVitals
{
    /// <summary>Native496CB0/496F30 add their ushort awards; level gains apply equipped bonuses before the ordinary saved-stat rebuild.</summary>
    public NativeCharacterVitals AwardBattleExperience(IReadOnlyDictionary<byte, ushort> characterAwards, IReadOnlyDictionary<byte, ushort> guardianForceAwards, NativeBattleExperienceDefinition definition)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(characterAwards);
        ArgumentNullException.ThrowIfNull(guardianForceAwards);
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        if (!string.Equals(KernelSha256, definition.KernelSha256, StringComparison.OrdinalIgnoreCase) || characterAwards.Keys.Any(row => row >= Records.Length || !CombatParty.Contains(row)) || guardianForceAwards.Keys.Any(id => id >= GuardianForces.Length || (GuardianForces[id].Flags & 1) == 0))
        {
            throw new InvalidDataException("Experience awards require current canonical recipients and matching kernel resources.");
        }
        NativeCharacterVital[] records = Records.ToArray();
        foreach ((byte row, ushort points) in characterAwards)
        {
            NativeCharacterVital character = records[row];
            NativeStatCurve curve = Definitions.Characters[character.CharacterId];
            int previousLevel = NativeStatDefinitions.Level(character.Experience, curve);
            uint experience = unchecked(character.Experience + points);
            int level = NativeStatDefinitions.Level(experience, curve);
            if (level == 100)
            {
                // Original496E03..496E51 indexes this cap by physical row, even for a substituted dream identity.
                experience = MaximumExperience(Definitions.Characters[row]);
            }
            ushort hitPointBonus = character.HitPointBonus;
            byte[] statBonuses = character.StatBonuses.ToArray();
            int levelsGained = level - previousLevel;
            if (previousLevel != 100 && levelsGained > 0)
            {
                uint flags = 0;
                for (int slot = 0; slot < 4; slot++)
                {
                    byte ability = unchecked((byte)(character.Junction.Abilities >> (8 * slot)));
                    if (ability is >= 58 and < 78)
                    {
                        flags |= definition.CharacterAbilityFlags[ability - 58];
                    }
                }
                if ((flags & 0x80) != 0)
                {
                    hitPointBonus = (ushort)Math.Min(hitPointBonus + 30 * levelsGained, 9999);
                }
                for (int stat = 0; stat < 4; stat++)
                {
                    if ((flags & (0x100u << stat)) != 0)
                    {
                        statBonuses[stat] = (byte)Math.Min(statBonuses[stat] + levelsGained, 255);
                    }
                }
            }
            records[row] = character with { Experience = experience, HitPointBonus = hitPointBonus, StatBonuses = statBonuses };
        }
        NativeGuardianForceVital[] guardianForces = GuardianForces.ToArray();
        foreach ((byte id, ushort points) in guardianForceAwards)
        {
            NativeGuardianForceVital gf = guardianForces[id];
            NativeStatCurve curve = Definitions.GuardianForces[id];
            uint experience = unchecked(gf.Experience + points);
            if (NativeStatDefinitions.Level(experience, curve) == 100)
            {
                experience = MaximumExperience(curve);
            }
            guardianForces[id] = gf with { Experience = experience };
        }
        return (this with { Records = records, GuardianForces = guardianForces }).RebuildParty();
    }

    private static uint MaximumExperience(NativeStatCurve curve)
    {
        return (uint)(9801 * curve.ExperienceFraction / 256 + 990 * curve.ExperienceStep);
    }
}
