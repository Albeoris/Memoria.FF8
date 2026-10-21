using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    public ushort GetMagicMaximumHitPoints(int physicalRow)
    {
        NativeCharacterVital character = Records[physicalRow];
        byte spell = character.Junction.MagicJunctions[0];
        int quantity = MagicJunctionPolicy.GetEffectiveCount(this, physicalRow, spell);
        return Definitions.CharacterMaximum(character.CharacterId, NativeStatDefinitions.Level(character.Experience, Definitions.Characters[character.CharacterId]), character.HitPointBonus, spell, quantity, character.Junction.Abilities);
    }

    private ushort MagicMaximumHitPoints(NativeCharacterVital character)
    {
        int row = Array.FindIndex(Records, record => record.CharacterId == character.CharacterId);
        byte spell = character.Junction.MagicJunctions[0];
        int quantity = MagicJunctionPolicy.GetEffectiveCount(this, row, spell);
        return Definitions.CharacterMaximum(character.CharacterId, NativeStatDefinitions.Level(character.Experience, Definitions.Characters[character.CharacterId]), character.HitPointBonus, spell, quantity, character.Junction.Abilities);
    }

    public int? ResolveFieldMagicCaster(int selectedRow, int spellId, Func<int, BattleNativePlayerStatRules> readRules, NativeWorldControlState? controls = null)
    {
        bool Eligible(int row) => IsMenuCharacterAvailable(row, controls);
        if (!MagicSystem.SharedMagicStock)
        {
            return Eligible(selectedRow) && MagicJunctionPolicy.CanAccess(this, selectedRow, spellId) ? selectedRow : null;
        }
        if (MagicJunctionPolicy.GetOwner(this, spellId) is int owner)
        {
            return Eligible(owner) && MagicJunctionPolicy.CanAccess(this, owner, spellId) ? owner : null;
        }
        int? selected = null;
        int maximum = -1;
        foreach (byte row in CombatParty)
        {
            if (row == 255 || !Eligible(row) || !MagicJunctionPolicy.CanAccess(this, row, spellId))
            {
                continue;
            }
            NativeCharacterVital character = Records[row];
            int magic = readRules(row).Project(GetMagicInventory(row), new(character.HitPointBonus, character.StatBonuses.ToImmutableArray())).Magic;
            if (magic > maximum)
            {
                maximum = magic;
                selected = row;
            }
        }
        return selected;
    }
}
