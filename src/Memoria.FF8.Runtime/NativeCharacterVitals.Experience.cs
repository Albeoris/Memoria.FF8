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

    public uint ReadCharacterAbilityFlags(uint equippedAbilities)
    {
        uint flags = 0;
        for (int slot = 0; slot < 4; slot++)
        {
            byte ability = unchecked((byte)(equippedAbilities >> (8 * slot)));
            if (ability is >= 58 and < 78)
            {
                flags |= CharacterAbilityFlags[ability - 58];
            }
        }
        return flags;
    }
}

public sealed partial record NativeCharacterVitals
{
    /// <summary>Native496CB0/496F30 add their ushort awards; level gains apply equipped bonuses before the ordinary saved-stat rebuild.</summary>
    public NativeCharacterVitals AwardBattleExperience(IReadOnlyDictionary<byte, ushort> characterAwards, IReadOnlyDictionary<byte, ushort> guardianForceAwards, NativeBattleExperienceDefinition definition, IReadOnlyList<byte>? battleRows = null)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(characterAwards);
        ArgumentNullException.ThrowIfNull(guardianForceAwards);
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        if (!string.Equals(KernelSha256, definition.KernelSha256, StringComparison.OrdinalIgnoreCase) || characterAwards.Keys.Any(row => row >= Records.Length || !(battleRows ?? CombatParty).Contains(row)) || guardianForceAwards.Keys.Any(id => id >= GuardianForces.Length || (GuardianForces[id].Flags & 1) == 0))
        {
            throw new InvalidDataException("Experience awards require current canonical recipients and matching kernel resources.");
        }
        NativeCharacterVital[] records = Records.ToArray();
        foreach ((byte row, ushort points) in characterAwards)
        {
            NativeCharacterVital character = records[row];
            NativeStatCurve curve = Definitions.Characters[character.CharacterId];
            int previousLevel = CharacterLevel(character);
            uint experience = unchecked(character.Experience + points);
            int level = NativeStatDefinitions.Level(experience, curve, MaximumCharacterLevel);
            if (level == MaximumCharacterLevel)
            {
                // Original496E03..496E51 indexes this cap by physical row, even for a substituted dream identity.
                NativeStatCurve capCurve = MaximumCharacterLevel == GameplayLevelRules.DefaultMaximumLevel ? Definitions.Characters[row] : curve;
                experience = NativeStatDefinitions.ExperienceForLevel(MaximumCharacterLevel, capCurve);
            }
            BattleNativeCharacterGrowth growth = character.Growth;
            int hitPointGain = 0;
            int[] statGains = new int[6];
            int levelsGained = level - previousLevel;
            if (levelsGained > 0)
            {
                uint flags = definition.ReadCharacterAbilityFlags(character.Junction.Abilities);
                if ((flags & 0x80) != 0)
                {
                    hitPointGain = 30 * levelsGained;
                }
                for (int stat = 0; stat < 4; stat++)
                {
                    if ((flags & (0x100u << stat)) != 0)
                    {
                        statGains[stat] = levelsGained;
                    }
                }
            }
            growth = growth.Add(NativeStatBonusSources.LevelUp, hitPointGain, statGains, LevelBasedStatBonuses);
            records[row] = character.WithBonusSources(growth.Sources) with { Experience = experience };
        }
        NativeGuardianForceVital[] guardianForces = GuardianForces.ToArray();
        foreach ((byte id, ushort points) in guardianForceAwards)
        {
            NativeGuardianForceVital gf = guardianForces[id];
            NativeStatCurve curve = Definitions.GuardianForces[id];
            uint experience = unchecked(gf.Experience + points);
            if (NativeStatDefinitions.Level(experience, curve) == GameplayLevelRules.DefaultMaximumLevel)
            {
                experience = NativeStatDefinitions.ExperienceForLevel(GameplayLevelRules.DefaultMaximumLevel, curve);
            }
            guardianForces[id] = gf with { Experience = experience };
        }
        return (this with { Records = records, GuardianForces = guardianForces }).RebuildParty();
    }
}
