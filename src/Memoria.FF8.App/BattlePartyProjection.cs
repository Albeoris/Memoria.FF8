using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using System.Collections.Immutable;

namespace Memoria.FF8;

/// <summary>Projects the player's saved combat party into the shared encounter and laboratory scaffold.</summary>
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
            return ProjectGuardianForceAction(vitals, action);
        }).ToArray();
    }

    internal static BattleActionDefinition ProjectGuardianForceAction(NativeCharacterVitals vitals, BattleActionDefinition action)
    {
        NativeGuardianForceVital gf = vitals.GuardianForces[action.Id];
        int percent = 0;
        byte flags = 0;
        for (int bit = 19; bit < 28; bit++)
        {
            NativeStatModifier modifier = vitals.Definitions.GuardianForceModifiers[bit - 19];
            if (gf.HasLearnedAbility(bit + 64) && modifier.Selector == 0)
            {
                percent += modifier.Percentage;
            }
            if (gf.HasLearnedAbility(bit + 64))
            {
                flags |= modifier.Flags;
            }
        }
        return action with { Level = NativeStatDefinitions.Level(gf.Experience, vitals.Definitions.GuardianForces[action.Id]), DamagePercent = unchecked((byte)percent), Available = gf.HitPoints > 0 && (gf.Flags & 1) != 0, Statuses = action.Statuses?.ToArray(), NativeGuardianBoostAvailable = (flags & 1) != 0 };
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
            int weapon = vitals.GetBattleWeapon(row);
            int level = NativeStatDefinitions.Level(saved.Experience, vitals.Definitions.Characters[character]);
            int costume = row < appearance.Records.Length ? appearance.Records[row].Dress : 0;
            _ = resources.LoadPlayer(character, costume);
            string name = LiveMenuData.CharacterName(state, saved.CharacterId, names, catalog);
            BattlePlayerStats stats = resources.ReadPlayerStats(vitals, row);
            NativeBattleStatusAllocation nativeStatuses = resources.ReadPlayerNativeStatusData(vitals, row);
            BattleWeaponDefinition equipped = resources.WeaponDefinitions[weapon];
            ImmutableArray<byte> nativeCommandSlots = resources.ReadPlayerNativeCommandSlots(vitals, row);
            ImmutableArray<BattleNativeCommandBinding> nativeCommands = ReadCommandBindings(nativeCommandSlots, resources);
            party.Add(new BattleCombatantDefinition { Id = $"party-{row}", MagicRecordId = row, Name = name, IsPlayer = true, ModelId = character, Costume = costume, MaximumHp = vitals.GetMagicMaximumHitPoints(row), CurrentHp = saved.HitPoints.Minimum, Speed = Math.Max(1, stats.Speed), Attack = stats.Strength, Defense = stats.Vitality, Luck = stats.Luck, NativeKillCount = saved.KillCount, NativeDeathCount = saved.DeathCount, Hit = stats.Hit, Evasion = stats.Evasion, PhysicalPower = equipped.AttackPower, Spirit = stats.Spirit, ElementAttack = stats.ElementAttack, ElementAttackPercent = stats.ElementAttackPercent, ElementDefenses = stats.ElementDefenses, WeaponId = weapon, WeaponName = equipped.Name, EquipmentIds = saved.Junction.GuardianForceAssignments.ToArray(), Level = level, Magic = stats.Magic, NativeMagicInventory = saved.MagicInventory.ToArray(), Abilities = ReadCommandAbilities(nativeCommands), NativeCommands = nativeCommands, NativeCommandSlots = nativeCommandSlots, NativeLimit = resources.ReadNativeLimitDefinition(character), NativeAttack = resources.ReadPlayerNativeAttackData(vitals, row), NativePlayerStatRules = resources.ReadPlayerNativeStatRules(vitals, row), NativeGrowth = new BattleNativeCharacterGrowth(saved.HitPointBonus, saved.StatBonuses.ToImmutableArray()), NativeStatus0 = nativeStatuses.Saved, NativeStatus1 = nativeStatuses.Battle, NativeStatusResistances = nativeStatuses.Resistances.ToArray(), NativeRetainedStatusBits = (ushort)(saved.Status & 0x80), GuardianForceCompatibility = saved.GuardianForceCompatibility?.ToArray(), NativeCharacterFlag100 = resources.ReadCharacterNativeConditionFlag100(character) });
        }
        if (party.Count == 0)
        {
            throw new NotSupportedException("Battle requires a nonempty saved combat party.");
        }
        return party.ToArray();
    }

    internal static BattleSnapshot RestoreLegacyCommandProjection(BattleSnapshot snapshot, BattleAssets resources)
    {
        if (snapshot.Definition.MagicVitals is not { } vitals)
        {
            return snapshot;
        }
        BattleCombatantDefinition[]? corrected = null;
        BattleCombatantDefinition[] saved = snapshot.Definition.Combatants;
        for (int index = 0; index < saved.Length; index++)
        {
            BattleCombatantDefinition actor = saved[index];
            if (!actor.IsPlayer || actor.MagicRecordId is not int row || (uint)row >= vitals.Records.Length || actor.NativeCommandSlots.Length != 4 || actor.NativeCommands.IsDefaultOrEmpty)
            {
                continue;
            }
            if (actor.NativeCommandSlots[0] == 12 && actor.NativeCommandSlots.SequenceEqual(resources.ReadPlayerNativeCommandSlots(vitals, row)))
            {
                ImmutableArray<BattleNativeCommandBinding> legacyBindings = actor.NativeCommandSlots.Skip(1).Where(value => value != 0).Prepend((byte)1).Distinct().Select(resources.ReadNativeCommand).ToImmutableArray();
                if (actor.NativeCommands.SequenceEqual(legacyBindings) && actor.Abilities.SequenceEqual(ReadCommandAbilities(legacyBindings)))
                {
                    ImmutableArray<BattleNativeCommandBinding> bindings = ReadCommandBindings(actor.NativeCommandSlots, resources);
                    actor = actor with { NativeCommands = bindings, Abilities = ReadCommandAbilities(bindings) };
                    corrected ??= saved.ToArray();
                    corrected[index] = actor;
                }
            }
            byte command = vitals.GetReservedBattleCommand(row);
            if (command is 0 or 1 || actor.NativeCommandSlots.Contains(command) || actor.NativeCommands[^1].Command != command)
            {
                continue;
            }
            ImmutableArray<byte> slots = resources.ReadPlayerNativeCommandSlots(vitals, row);
            if (!actor.NativeCommandSlots.SequenceEqual(slots))
            {
                continue;
            }
            ImmutableArray<BattleNativeCommandBinding> current = ReadCommandBindings(slots, resources);
            ImmutableArray<BattleNativeCommandBinding> previous = current.Add(resources.ReadNativeCommand(command));
            if (!actor.NativeCommands.SequenceEqual(previous) || !actor.Abilities.SequenceEqual(ReadCommandAbilities(previous)))
            {
                continue;
            }
            corrected ??= saved.ToArray();
            corrected[index] = actor with { NativeCommands = current, Abilities = ReadCommandAbilities(current) };
        }
        return corrected is null ? snapshot : snapshot with { Definition = snapshot.Definition with { Combatants = corrected } };
    }

    private static ImmutableArray<BattleNativeCommandBinding> ReadCommandBindings(ImmutableArray<byte> slots, BattleAssets resources)
    {
        return slots.Where(command => command != 0).Distinct().Select(resources.ReadNativeCommand).ToImmutableArray();
    }

    private static BattleAbility[] ReadCommandAbilities(ImmutableArray<BattleNativeCommandBinding> commands)
    {
        return commands.Select(binding => BattleCommandPresentation.AbilityFor(binding.Command)).Distinct().ToArray();
    }

    public static BattleCombatantDefinition[] CreateNewGame(GameAssets assets, LiveMenuCatalog catalog, BattleAssets resources)
    {
        return Create(new GameState { CharacterVitals = assets.ReadNewGameCharacterVitals(), CharacterAppearance = assets.ReadNewGameCharacterAppearance() }, null, catalog, resources);
    }
}
