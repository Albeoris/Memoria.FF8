using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Projects the canonical saved combat party into the shared encounter and laboratory scaffold.</summary>
internal static class BattlePartyProjection
{
    public static BattleCombatantDefinition[] Create(GameState state, IReadOnlyDictionary<int, string>? names, LiveMenuCatalog catalog, BattleAssets resources)
    {
        NativeCharacterVitals vitals = state.CharacterVitals ?? throw new NotSupportedException("Interactive battle requires the actual current character records.");
        NativeCharacterAppearance appearance = state.CharacterAppearance ?? throw new NotSupportedException("Interactive battle requires the current saved costume owner.");
        vitals.Validate();
        appearance.Validate();
        List<BattleCombatantDefinition> party = [];
        foreach (byte row in vitals.CombatParty.Where(row => row != 255))
        {
            NativeCharacterVital saved = vitals.Records[row];
            if (saved.HitPoints.Minimum != saved.HitPoints.Maximum || saved.Status != 0)
            {
                throw new NotSupportedException("Interactive combat requires exact current HP and an implemented saved-status projection.");
            }
            int character = saved.CharacterId;
            int weapon = character >= 8 ? vitals.DreamWeapons[character - 8] : saved.Weapon;
            int level = NativeStatDefinitions.Level(saved.Experience, vitals.Definitions.Characters[character]);
            int costume = appearance.Records[row].Dress;
            _ = resources.LoadPlayer(character, costume);
            string name = LiveMenuData.CharacterName(state, saved.CharacterId, names, catalog);
            // TODO(TD-009): non-HP stats use explicit scaffold tuning until the native combat stat/damage contract is implemented
            BattleWeaponDefinition equipped = resources.WeaponDefinitions[weapon];
            party.Add(new BattleCombatantDefinition { Id = $"party-{row}", Name = name, IsPlayer = true, ModelId = character, Costume = costume, MaximumHp = vitals.Definitions.CharacterMaximum(saved), CurrentHp = saved.HitPoints.Minimum, Speed = 90 + level, Attack = 25 + level * 4 + equipped.AttackPower + equipped.StrengthBonus, Defense = 10 + level, WeaponId = weapon, WeaponName = equipped.Name, EquipmentIds = saved.Junction.GuardianForceAssignments.ToArray() });
        }
        if (party.Count == 0)
        {
            throw new NotSupportedException("Battle requires a nonempty canonical combat party.");
        }
        return party.ToArray();
    }

    public static BattleCombatantDefinition[] CreateNewGame(GameAssets assets, LiveMenuCatalog catalog, BattleAssets resources)
    {
        return Create(new GameState { CharacterVitals = assets.ReadNewGameCharacterVitals(), CharacterAppearance = assets.ReadNewGameCharacterAppearance() }, null, catalog, resources);
    }
}
