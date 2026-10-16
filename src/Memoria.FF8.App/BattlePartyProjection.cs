using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using System.Collections.Immutable;

namespace Memoria.FF8;

/// <summary>Projects the canonical saved combat party into the shared encounter and laboratory scaffold.</summary>
internal static class BattlePartyProjection
{
    public static BattleActionDefinition[] CreateActions(NativeCharacterVitals vitals, IReadOnlyList<BattleActionDefinition> installed)
    {
        vitals.Validate();
        return installed.Select(action =>
        {
            if (action.Ability != BattleAbility.GuardianForce)
            {
                return action with { Statuses = action.Statuses?.ToArray() };
            }
            NativeGuardianForceVital gf = vitals.GuardianForces[action.Id];
            int percent = 0;
            for (int bit = 19; bit < 28; bit++)
            {
                NativeStatModifier modifier = vitals.Definitions.GuardianForceModifiers[bit - 19];
                if ((gf.LearnedBank.High & (1UL << bit)) != 0 && modifier.Selector == 0)
                {
                    percent += modifier.Percentage;
                }
            }
            return action with { Level = NativeStatDefinitions.Level(gf.Experience, vitals.Definitions.GuardianForces[action.Id]), DamagePercent = unchecked((byte)percent), Available = gf.HitPoints > 0 && (gf.Flags & 1) != 0, Statuses = action.Statuses?.ToArray() };
        }).ToArray();
    }

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
            if (saved.HitPoints.Minimum != saved.HitPoints.Maximum)
            {
                throw new NotSupportedException("Interactive combat requires exact current HP.");
            }
            int character = saved.CharacterId;
            int weapon = character >= 8 ? vitals.DreamWeapons[character - 8] : saved.Weapon;
            int level = NativeStatDefinitions.Level(saved.Experience, vitals.Definitions.Characters[character]);
            int costume = appearance.Records[row].Dress;
            _ = resources.LoadPlayer(character, costume);
            string name = LiveMenuData.CharacterName(state, saved.CharacterId, names, catalog);
            BattlePlayerStats stats = resources.ReadPlayerStats(vitals, row);
            NativeBattleStatusAllocation nativeStatuses = resources.ReadPlayerNativeStatusData(vitals, row);
            BattleWeaponDefinition equipped = resources.WeaponDefinitions[weapon];
            party.Add(new BattleCombatantDefinition { Id = $"party-{row}", Name = name, IsPlayer = true, ModelId = character, Costume = costume, MaximumHp = vitals.Definitions.CharacterMaximum(saved), CurrentHp = saved.HitPoints.Minimum, Speed = Math.Max(1, stats.Speed), Attack = stats.Strength, Defense = stats.Vitality, Luck = stats.Luck, NativeKillCount = saved.KillCount, Hit = stats.Hit, Evasion = stats.Evasion, PhysicalPower = equipped.AttackPower, Spirit = stats.Spirit, ElementAttack = stats.ElementAttack, ElementAttackPercent = stats.ElementAttackPercent, ElementDefenses = stats.ElementDefenses, WeaponId = weapon, WeaponName = equipped.Name, EquipmentIds = saved.Junction.GuardianForceAssignments.ToArray(), Level = level, Magic = stats.Magic, NativeMagicInventory = saved.MagicInventory.ToArray(), Abilities = Commands(saved, vitals), NativeCommands = NativeCommands(saved, vitals, resources), NativeCommandSlots = resources.ReadPlayerNativeCommandSlots(vitals, row), NativeLimit = resources.ReadNativeLimitDefinition(character), NativeStatus0 = nativeStatuses.Saved, NativeStatus1 = nativeStatuses.Battle, NativeStatusResistances = nativeStatuses.Resistances.ToArray(), NativeRetainedStatusBits = (ushort)(saved.Status & 0x80), GuardianForceCompatibility = saved.GuardianForceCompatibility?.ToArray(), NativeCharacterFlag100 = resources.ReadCharacterNativeConditionFlag100(character) });
        }
        if (party.Count == 0)
        {
            throw new NotSupportedException("Battle requires a nonempty canonical combat party.");
        }
        return party.ToArray();
    }

    private static BattleAbility[] Commands(NativeCharacterVital character, NativeCharacterVitals vitals)
    {
        List<BattleAbility> result = [BattleAbility.Attack];
        for (int slot = 0; slot < 4; slot++)
        {
            int ability = (byte)(character.Junction.Commands >> (8 * slot));
            if (ability == 0)
            {
                continue;
            }
            if (ability is < 20 or >= 39)
            {
                throw new NotSupportedException($"Saved command ability {ability} has no native command mapping.");
            }
            BattleAbility command = vitals.MenuDefinitions.CommandAbilityCommands[ability - 20] switch
            {
                2 => BattleAbility.Magic,
                3 => BattleAbility.GuardianForce,
                4 => BattleAbility.Item,
                6 => BattleAbility.Draw,
                _ => BattleAbility.NativeCommand
            };
            if (!result.Contains(command))
            {
                result.Add(command);
            }
        }
        return result.ToArray();
    }

    private static ImmutableArray<BattleNativeCommandBinding> NativeCommands(NativeCharacterVital character, NativeCharacterVitals vitals, BattleAssets resources)
    {
        List<byte> commands = [1];
        for (int slot = 0; slot < 4; slot++)
        {
            int ability = (byte)(character.Junction.Commands >> (8 * slot));
            if (ability == 0)
            {
                continue;
            }
            if (ability is < 20 or >= 39)
            {
                throw new NotSupportedException($"Saved command ability {ability} has no native command mapping.");
            }
            byte command = vitals.MenuDefinitions.CommandAbilityCommands[ability - 20];
            if (!commands.Contains(command))
            {
                commands.Add(command);
            }
        }
        return commands.Select(resources.ReadNativeCommand).ToImmutableArray();
    }
    public static BattleCombatantDefinition[] CreateNewGame(GameAssets assets, LiveMenuCatalog catalog, BattleAssets resources)
    {
        return Create(new GameState { CharacterVitals = assets.ReadNewGameCharacterVitals(), CharacterAppearance = assets.ReadNewGameCharacterAppearance() }, null, catalog, resources);
    }
}
