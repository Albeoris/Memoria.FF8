using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record BattlePlayerStats(int Strength, int Vitality, int Magic, int Spirit, int Speed, int Luck, int Hit, int Evasion, int[] ElementDefenses, int ElementAttack, int ElementAttackPercent);

/// <summary>Copies original kernel inputs for the shared native runtime player-stat projector.</summary>
internal sealed class BattlePlayerStatReader(byte[] kernel)
{
    public BattlePlayerStats Read(NativeCharacterVitals vitals, int row)
    {
        BattleNativePlayerStatRules rules = ReadRules(vitals, row);
        NativeCharacterVital character = vitals.Records[row];
        BattleNativePlayerStatistics stats = rules.Project(character.MagicInventory, new BattleNativeCharacterGrowth(character.HitPointBonus, character.StatBonuses.ToImmutableArray()));
        return new BattlePlayerStats(stats.Strength, stats.Vitality, stats.Magic, stats.Spirit, stats.Speed, stats.Luck, stats.Hit, stats.Evasion, stats.ElementDefenses.ToArray(), stats.ElementAttack, stats.ElementAttackPercent);
    }

    public BattleNativePlayerStatRules ReadRules(NativeCharacterVitals vitals, int row)
    {
        vitals.Validate();
        NativeCharacterVital character = vitals.Records[row];
        int weapon = character.CharacterId >= 8 ? vitals.DreamWeapons[character.CharacterId - 8] : character.Weapon;
        BattleNativePlayerStatRules rules = new(character.CharacterId, NativeStatDefinitions.Level(character.Experience, vitals.Definitions.Characters[character.CharacterId]), Record(6, character.CharacterId, 36).ToArray().ToImmutableArray(), Record(4, weapon, 12).ToArray().ToImmutableArray(), Enumerable.Range(0, 57).Select(index => Record(1, index, 60).ToArray().ToImmutableArray()).ToImmutableArray(), Enumerable.Range(0, 19).Select(index => Record(13, index, 8).ToArray().ToImmutableArray()).ToImmutableArray(), character.Junction.MagicJunctions.ToImmutableArray(), character.Junction.Abilities, vitals.Definitions);
        uint statusPassiveFlags = 0;
        for (int slot = 0; slot < 4; slot++)
        {
            int ability = (byte)(character.Junction.Abilities >> (8 * slot));
            if (ability is >= 58 and < 78)
            {
                ReadOnlySpan<byte> passive = Record(14, ability - 58, 8);
                statusPassiveFlags |= (uint)(passive[5] | passive[6] << 8 | passive[7] << 16);
            }
        }
        rules = rules with { NativeStatusPassiveFlags = statusPassiveFlags };
        rules.Validate();
        return rules;
    }

    private ReadOnlySpan<byte> Record(int section, int index, int size)
    {
        int start = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 4 * section));
        int end = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(8 + 4 * section));
        if (index < 0 || start < 228 || end > kernel.Length || end < start || index >= (end - start) / size)
        {
            throw new InvalidDataException("Native combat stat record exceeds its installed kernel section.");
        }
        return kernel.AsSpan(start + index * size, size);
    }
}
