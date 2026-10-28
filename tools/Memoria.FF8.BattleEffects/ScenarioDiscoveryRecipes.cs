using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static (string ActorId, int PhysicalIndex) ResolveRecipeRecipient(JsonObject template, bool party, string partyActorId)
    {
        if (party)
        {
            return (partyActorId, 0);
        }
        JsonNode target = template["target"] ?? throw new InvalidDataException("A hostile recipe requires a declared initialized opponent.");
        string actorId = target["actor_id"]?.GetValue<string>() ?? throw new InvalidDataException("The declared opponent requires its actor identity.");
        int physicalIndex = target["physical_index"]?.GetValue<int>() ?? throw new InvalidDataException("The declared opponent requires its physical index.");
        if (string.IsNullOrWhiteSpace(actorId) || physicalIndex is < 3 or > 6)
        {
            throw new InvalidDataException("The declared opponent requires an enemy actor and physical index3..6.");
        }
        return (actorId, physicalIndex);
    }

    public static bool TryPrepareBlueMagicRecipe(BattleAssets assets, NativeCharacterVitals initial, BattleNativeAction native, string templatePath, out JsonObject? recipe, out string? blocker)
    {
        recipe = null;
        blocker = null;
        if (native.CharacterId is not int owner || native.CrisisLevel is not byte crisis || native.KernelIndex is < 0 or >= 16 || native.TargetFlags is not byte flags || native.MagicId is 0 or > 400)
        {
            blocker = "BLUE_MAGIC_MAPPING_PRECONDITION: original owner, crisis supplemental row, learned bit or target/effect mapping is absent.";
            return false;
        }
        if (native.AttackType is 5 or 6)
        {
            blocker = "BLUE_MAGIC_REVIVAL_PRECONDITION: legitimate KO recipient and between-execution KO rearm are required.";
            return false;
        }
        BattleNativeLimitDefinition limit = assets.ReadNativeLimitDefinition(owner);
        int ownerRow = Array.FindIndex(initial.Records, record => record.CharacterId == owner);
        if (ownerRow < 0 || initial.Records.Count(record => record.CharacterId == owner) != 1)
        {
            blocker = "BLUE_MAGIC_OWNER_PRECONDITION: the original command owner must have one actual initialized saved character record.";
            return false;
        }
        ushort maximum = initial.GetMagicMaximumHitPoints(ownerRow);
        int hp = ResolveLimitPreparationHitPoints(limit, maximum, initial.Records[ownerRow].Status, crisis);
        if (hp == 0)
        {
            blocker = "BLUE_MAGIC_CRISIS_PRECONDITION: no legal positive initial HP with actual native status/party state can reach this crisis row through ordinary selection RNG.";
            return false;
        }
        JsonObject result = JsonNode.Parse(File.ReadAllText(templatePath))!.AsObject();
        JsonObject character = result["fixture"]!["characters"]![0]!.AsObject();
        character["row"] = ownerRow;
        character["character_id"] = owner;
        character["stocks"] = new JsonArray();
        character["hit_points"] = hp;
        result["fixture"]!["combat_party"] = new JsonArray(ownerRow, 255, 255);
        result["fixture"]!["limits"] = new JsonObject { ["learn_blue_magic"] = 1 << native.KernelIndex };
        string actorId = $"party-{ownerRow}";
        bool party = (flags & 0x40) == 0 || NativeBattleLimitRules.IsFixedAllyTarget(flags);
        (string recipient, int recipientIndex) = ResolveRecipeRecipient(result, party, actorId);
        _ = NativeBattleLimitRules.PlayerTargetMask(flags, recipientIndex);
        result["driver"]!["input"] = new JsonObject { ["kind"] = "action", ["actor_id"] = actorId, ["target_id"] = recipient, ["ability"] = nameof(BattleAbility.LimitBreak), ["action_id"] = native.Command << 16 | native.ActionId };
        result["driver"]!["expected_effect"] = native.MagicId;
        result["driver"]!["expected_native_action"] = native.ActionId;
        result["driver"]!["expected_native_command"] = native.Command;
        result["driver"]!["limit_selection"] = new JsonObject { ["minimum_crisis_level"] = crisis, ["maximum_crisis_level"] = crisis, ["maximum_selections"] = 256 };
        if (party)
        {
            ushort saved = 0;
            // Curse blocks Limit admission; Regen changes the declared HP while
            // ordinary selection callbacks run between the two invocations.
            uint battle = native.Statuses1 & 0x210;
            for (int bit = 0; bit < 8; bit++)
            {
                if (limit.StatusWeights[bit] != 0)
                {
                    saved |= (ushort)(native.Statuses0 & (1 << bit));
                }
            }
            for (int bit = 0; bit < 24; bit++)
            {
                if (limit.StatusWeights[bit + 8] != 0)
                {
                    battle |= native.Statuses1 & (1u << bit);
                }
            }
            result["driver"]!["repairs"] = new JsonArray(new JsonObject { ["actor_id"] = actorId, ["hit_points"] = hp, ["revive"] = false, ["remove_statuses0"] = saved, ["remove_statuses1"] = battle });
        }
        recipe = result;
        return true;
    }

    public static int ResolveLimitPreparationHitPoints(BattleNativeLimitDefinition definition, ushort maximumHitPoints, ushort savedStatuses, byte requiredCrisis)
    {
        int chosen = 0;
        int bestCount = 0;
        for (int hp = maximumHitPoints; hp > 0; hp--)
        {
            int count = 0;
            for (int random = 0; random <= byte.MaxValue; random++)
            {
                if (NativeBattleLimitRules.CrisisLevel(definition, hp, maximumHitPoints, savedStatuses, 0, 0, checked((byte)random)) == requiredCrisis)
                {
                    count++;
                }
            }
            if (count > bestCount)
            {
                chosen = hp;
                bestCount = count;
            }
        }
        return chosen;
    }

    public static bool TryPrepareEnemyRecipe(BattleAssets assets, NativeCharacterVitals initial, NativeBattleExperienceDefinition experienceDefinition, EnemyScenarioActionOwner owner, EnemyScenarioScriptedAction[] scripted, string templatePath, out JsonObject? recipe, out EnemyScenarioFormation? chosen, out EnemyScenarioScriptedAction? selectorProof, out string? blocker, EnemyScenarioStartup[]? startups = null)
    {
        return TryPrepareEnemyRecipe(assets, initial, experienceDefinition, owner, scripted, [], [], templatePath, out recipe, out chosen, out selectorProof, out blocker, startups: startups);
    }

    private static bool TryPrepareEnemyRecipe(BattleAssets assets, NativeCharacterVitals initial, NativeBattleExperienceDefinition experienceDefinition, EnemyScenarioActionOwner owner, EnemyScenarioScriptedAction[] scripted, Dictionary<(int ModelId, int SceneLevel, int PartyLevel), int[]> possibleBands, Dictionary<int, NativeCharacterVitals> preparedLevels, string templatePath, out JsonObject? recipe, out EnemyScenarioFormation? chosen, out EnemyScenarioScriptedAction? selectorProof, out string? blocker, bool useTurnScript = false, EnemyScenarioStartup[]? startups = null)
    {
        recipe = null;
        chosen = null;
        selectorProof = null;
        blocker = null;
        if (owner.NativeAction is not { MagicId: >= 1 and <= 400 } native || owner.ActionId == 0)
        {
            blocker = "ENEMY_MAPPING_PRECONDITION: the actual ability row is null or does not have a unique installed nonzero native effect mapping.";
            return false;
        }
        selectorProof = scripted.Where(action => action.ModelId == owner.ModelId && action.LevelBand == owner.LevelBand && action.Command == owner.Command && action.ActionId == owner.ActionId && action.Sequence == owner.Sequence && action.Callback == 1 && action.TargetSelector is 0 or 200 or 201 or 204 or 205).OrderBy(action => action.Offset).FirstOrDefault();
        if (selectorProof is null)
        {
            blocker = "ENEMY_TRIGGER_OR_TARGET_PRECONDITION: no actual turn-callback dispatch for this exact row/band/sequence has a proved self/party/group target selector. Counter/death/initialization-only, dynamic target and last-attacker paths require their original trigger recipe.";
            return false;
        }
        if (!TrySelectEnemyRemovalRepeatParty(native, selectorProof.TargetSelector!.Value, initial.Records, initial.CombatParty, out byte[]? removalParty, out blocker))
        {
            return false;
        }
        byte[]? shootParty = null;
        if (native.MagicId == 42 && !TrySelectShootParty(initial.Records, initial.CombatParty, out shootParty, out blocker))
        {
            return false;
        }
        if (ScenarioEnemyDiscovery.InitializationUnconditionallyDisablesTargeting(assets.ReadEnemyAi(owner.ModelId, 0).Scripts[0]))
        {
            blocker = "ENEMY_ACTIVATION_PRECONDITION: the original initialization callback unconditionally disables this participant's targeting; a genuine encounter phase transition is required before the declared targetable participant contract.";
            return false;
        }
        byte[]? preparedParty = shootParty;
        int initialLevel = NativeStatDefinitions.Level(initial.Records[0].Experience, initial.Definitions.Characters[initial.Records[0].CharacterId]);
        if (removalParty is not null)
        {
            initialLevel = removalParty.Max(row => NativeStatDefinitions.Level(initial.Records[row].Experience, initial.Definitions.Characters[initial.Records[row].CharacterId]));
        }
        int selectedLevel = initialLevel;
        int physical = -1;
        foreach (EnemyScenarioFormation context in owner.Formations.Where(context => context.Enabled && !context.Hidden && !context.Suspended && !context.Untargetable).OrderBy(context => context.FormationId).ThenBy(context => context.SceneSlot))
        {
            BattleEncounter formation = assets.LoadEncounter(context.FormationId);
            if (!TryValidateFreshEnemyEntry(formation, scripted, out string? entryBlocker, startups))
            {
                blocker = entryBlocker;
                continue;
            }
            BattleEnemyPlacement[] active = formation.Slots.Where(slot => slot.Enabled).ToArray();
            if (active.Length is < 1 or > 4 || active.Any(slot => !slot.IsEnemyModel))
            {
                continue;
            }
            if (native.MagicId == 258 && (owner.ModelId != 63 || owner.Command != 8 || owner.ActionId != 213 || owner.Sequence != 14 || formation.Id != 190 || formation.StageId != 50 || context.SceneSlot != 1 || active.Length != 2 || active[0].Slot != 0 || active[0].ModelId != 62 || active[1].Slot != 1 || active[1].ModelId != 63 || active.Any(slot => slot.Hidden || slot.InitiallySuspended || slot.Untargetable)))
            {
                //The original programme anchors physical3 and4 at bone38; its actual turn producer is the second actor.
                continue;
            }
            if (!TrySelectStageEntryParty(formation.StageId, formation.Id, initial.Records, initial.CombatParty, out byte[]? stageParty, out blocker))
            {
                continue;
            }
            byte[]? candidateParty = removalParty ?? shootParty ?? stageParty;
            bool bandMatches = false;
            for (int partyLevel = initialLevel; partyLevel <= 100; partyLevel++)
            {
                (int, int, int) key = (owner.ModelId, context.SceneLevel, partyLevel);
                int[]? bands = null;
                if (candidateParty is not null || !possibleBands.TryGetValue(key, out bands))
                {
                    byte[] partyLevels = (candidateParty ?? initial.CombatParty).Where(row => row != 255).Select(row => row == 0 || removalParty is not null ? checked((byte)partyLevel) : NativeStatDefinitions.Level(initial.Records[row].Experience, initial.Definitions.Characters[initial.Records[row].CharacterId])).ToArray();
                    bands = ResolvePossibleEnemyLevels(checked((byte)context.SceneLevel), partyLevels).Select(level => assets.ReadEnemyAi(owner.ModelId, level).LevelBand).Distinct().Order().ToArray();
                    if (candidateParty is null)
                    {
                        possibleBands.Add(key, bands);
                    }
                }
                if (bands.Length == 1 && bands[0] == owner.LevelBand)
                {
                    selectedLevel = partyLevel;
                    bandMatches = true;
                    break;
                }
            }
            if (!bandMatches)
            {
                continue;
            }
            int index = Array.FindIndex(active, slot => slot.Slot == context.SceneSlot && slot.ModelId == owner.ModelId);
            if (index < 0)
            {
                continue;
            }
            chosen = context;
            preparedParty = candidateParty;
            physical = index + 3;
            break;
        }
        if (chosen is null)
        {
            blocker ??= "ENEMY_PHASE_OR_LEVEL_PRECONDITION: no original enabled, visible, unsuspended, targetable scene slot preserves this exact ability level band for every native entry RNG roll at any legitimate increased Squall level through100. Hidden reinforcements and phase actors require genuine activation; a different level/formation is not fabricated.";
            return false;
        }
        blocker = null;
        string actor = $"enemy-{chosen.SceneSlot}";
        string target = selectorProof.TargetSelector is 200 or 205 ? actor : "party-0";
        JsonObject result = JsonNode.Parse(File.ReadAllText(templatePath))!.AsObject();
        if (preparedParty is not null)
        {
            result["fixture"]!["combat_party"] = new JsonArray(preparedParty.Select(row => (JsonNode)JsonValue.Create(row)!).ToArray());
        }
        result["encounter"] = chosen.FormationId;
        result["target"] = new JsonObject { ["actor_id"] = actor, ["stage"] = chosen.StageId, ["model"] = owner.ModelId, ["scene_slot"] = chosen.SceneSlot, ["physical_index"] = physical, ["flags_mask"] = 195, ["flags_value"] = 1, ["opening_action"] = null, ["opening_effect"] = null };
        JsonObject character = result["fixture"]!["characters"]![0]!.AsObject();
        character["stocks"] = new JsonArray();
        if (!preparedLevels.TryGetValue(selectedLevel, out NativeCharacterVitals? prepared))
        {
            prepared = initial.WithDiagnosticBattleLevel(0, selectedLevel, experienceDefinition);
            preparedLevels.Add(selectedLevel, prepared);
        }
        character["level"] = selectedLevel;
        character["hit_points"] = prepared.GetMagicMaximumHitPoints(0);
        result["driver"]!["input"] = new JsonObject { ["kind"] = "action", ["actor_id"] = actor, ["target_id"] = target, ["ability"] = nameof(BattleAbility.EnemyAbility), ["action_id"] = owner.ActionId };
        result["driver"]!["expected_effect"] = native.MagicId;
        result["driver"]!["expected_native_action"] = native.ActionId;
        result["driver"]!["expected_native_command"] = native.Command;
        result["driver"]!["enemy_ability"] = new JsonObject { ["model"] = owner.ModelId, ["scene_slot"] = chosen.SceneSlot, ["level_band"] = owner.LevelBand, ["ability_slot"] = owner.AbilitySlot, ["command"] = owner.Command, ["action_id"] = owner.ActionId, ["sequence"] = owner.Sequence, ["target_selector"] = selectorProof.TargetSelector!.Value };
        if (useTurnScript)
        {
            result["driver"]!["enemy_ability"]!["use_turn_script"] = true;
            result["driver"]!["supporting_effects"] = new JsonArray(scripted.Where(action => action.ModelId == owner.ModelId && action.Callback == 1 && action.NativeEffectId is >= 1 and <= 400 && action.NativeEffectId != native.MagicId).Select(action => action.NativeEffectId!.Value).Distinct().Order().Select(effect => (JsonNode)JsonValue.Create(effect)!).ToArray());
        }
        if (selectorProof.TargetSelector is 0 or 201 or 204)
        {
            result["driver"]!["repairs"] = new JsonArray(new JsonObject { ["actor_id"] = "party-0", ["hit_points"] = prepared.GetMagicMaximumHitPoints(0), ["revive"] = false, ["remove_statuses0"] = 0, ["remove_statuses1"] = 0 });
        }
        DeclareEnemySupportingReactions(result, assets.LoadEncounter(chosen.FormationId), actor, native.MagicId, scripted);
        if (removalParty is not null)
        {
            prepared = PrepareEnemyRemovalRepeatParty(result, initial, experienceDefinition, removalParty, selectedLevel);
        }
        DeclareEnemyPartyKnockoutRearm(result, native, selectorProof.TargetSelector!.Value, preparedParty ?? initial.CombatParty, row => prepared.GetMagicMaximumHitPoints(row));
        recipe = result;
        return true;
    }

    public static int[] ResolvePossibleEnemyLevels(byte sceneLevel, byte[] presentPartyLevels)
    {
        int draws = NativeBattleEnemyLevelRules.RandomByteCount(sceneLevel);
        HashSet<int> levels = [];
        int firstCount = draws == 0 ? 1 : 256;
        int secondCount = draws == 2 ? 256 : 1;
        for (int first = 0; first < firstCount; first++)
        {
            for (int second = 0; second < secondCount; second++)
            {
                levels.Add(NativeBattleEnemyLevelRules.Resolve(sceneLevel, presentPartyLevels, checked((byte)first), draws == 2 ? checked((byte)second) : null));
            }
        }
        return levels.Order().ToArray();
    }

    private static bool TryPrepareOrdinaryPlayerRecipe(BattleAssets assets, NativeCharacterVitals initial, BattleNativeAction native, string templatePath, out JsonObject? recipe, out string? blocker)
    {
        recipe = null;
        blocker = null;
        BattleAbility ability = native.KernelSection == 2 ? BattleAbility.GuardianForce : native.KernelSection == 10 ? BattleAbility.NativeCommand : BattleAbility.Item;
        BattleActionDefinition? definition = ability == BattleAbility.NativeCommand && native.TargetFlags is byte commandFlags ? new(BattleAbility.NativeCommand, native.Command << 16 | native.ActionId, native.Name ?? $"Command {native.Command}", BattleActionEffect.Native, native.Power, (commandFlags & 0x40) == 0, (commandFlags & 0x30) != 0x10 && (commandFlags & 2) == 0) { NativeAction = native } : assets.ActionDefinitions.SingleOrDefault(action => action.Ability == ability && action.NativeAction?.Command == native.Command && action.NativeAction.KernelIndex == native.KernelIndex && action.NativeAction.KernelSection == native.KernelSection);
        int? expectedEffect = ResolveCatalogEffect(native, assets.NativeActions);
        if (definition is null || ability != BattleAbility.NativeCommand && native.Command is not (4 or 254) || definition.Id <= 0 && ability == BattleAbility.Item || expectedEffect is null or 0 or > 400)
        {
            blocker = native.AttackType == 14 && native.ActionId == 30 ? "POCKETSTATION_PRECONDITION: the original saved Chocobo availability/away flags, attack variant and level determine the actual kernel9/command244 child; no fixed effect is invented from the item's index word." : "NO_ORDINARY_PLAYER_MAPPING: this row is null, a command variant or lacks an exact installed ordinary semantic action/effect mapping.";
            return false;
        }
        BattleNativeAction focal = native;
        if (native.AttackType == 14)
        {
            focal = assets.NativeActions.Single(child => child.Command == 244 && child.ActionId == native.MagicId);
        }
        if (native.AttackType is 5 or 6)
        {
            blocker = "REVIVAL_REPEAT_PRECONDITION: a legal KO recipient and typed between-execution KO rearm are not available; no living target is substituted.";
            return false;
        }
        if (native.AttackType == 18)
        {
            blocker = "REPEAT_PRECONDITION_UNSATISFIED: this original Kamikaze action removes the only current player combatant; no terminal hold, additional authentic party member and legitimate revival/rearm recipe is provided. Two reconstructed battles would not satisfy repetition.";
            return false;
        }
        byte command = ability == BattleAbility.GuardianForce ? (byte)3 : ability == BattleAbility.NativeCommand ? native.Command : (byte)4;
        int? summon = ability == BattleAbility.GuardianForce ? definition.Id : null;
        if (!TryPrepareCommand(initial, command, summon, out int commandAbility, out int[] guardians, out JsonObject[] guardianRecipes, out blocker))
        {
            return false;
        }
        JsonObject result = JsonNode.Parse(File.ReadAllText(templatePath))!.AsObject();
        JsonObject fixture = result["fixture"]!.AsObject();
        JsonObject character = fixture["characters"]![0]!.AsObject();
        fixture["guardian_forces"] = new JsonArray(guardianRecipes.Cast<JsonNode>().ToArray());
        character["guardian_forces"] = new JsonArray(guardians.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
        character["stocks"] = new JsonArray();
        character.Remove("magic_command");
        character["commands"] = new JsonArray(new JsonObject { ["slot"] = 0, ["ability"] = commandAbility });
        bool party = definition.Allies || native.TargetFlags is byte flags && NativeBattleLimitRules.IsFixedAllyTarget(flags);
        (string recipient, int recipientIndex) = ResolveRecipeRecipient(result, party, "party-0");
        _ = NativeBattleLimitRules.PlayerTargetMask(native.TargetFlags!.Value, recipientIndex);
        character["hit_points"] = party && native.AttackType is 3 or 4 or 32 ? 1 : initial.GetMagicMaximumHitPoints(0);
        JsonObject input = result["driver"]!["input"]!.AsObject();
        input["ability"] = ability.ToString();
        input["action_id"] = definition.Id;
        input["target_id"] = recipient;
        result["driver"]!["expected_effect"] = expectedEffect.Value;
        result["driver"]!["expected_native_action"] = focal.ActionId;
        result["driver"]!["expected_native_command"] = focal.Command;
        if (ability == BattleAbility.Item)
        {
            fixture["items"] = new JsonArray(new JsonObject { ["id"] = definition.Id, ["count"] = 2 });
        }
        recipe = result;
        return true;
    }

    private static bool TryPrepareCommand(NativeCharacterVitals initial, byte command, int? summonedGuardian, out int commandAbility, out int[] guardians, out JsonObject[] guardianRecipes, out string? blocker)
    {
        commandAbility = -1;
        guardians = [];
        guardianRecipes = [];
        blocker = null;
        int[] abilities = initial.MenuDefinitions.CommandAbilityCommands.Select((mapped, index) => (mapped, index)).Where(row => row.mapped == command).Select(row => row.index + 20).ToArray();
        foreach (int ability in abilities)
        {
            for (int provider = 0; provider < initial.GuardianForces.Length; provider++)
            {
                NativeCharacterVitals candidate = initial.AcquireGuardianForce(provider);
                int learnedPoints = 0;
                if (!candidate.GuardianForces[provider].HasLearnedAbility(ability))
                {
                    NativeGuardianForceAbility? available = candidate.GuardianForceAbilities(provider).SingleOrDefault(entry => entry.Ability == ability && !entry.Learned);
                    if (available is null)
                    {
                        continue;
                    }
                    learnedPoints = Math.Max(1, available.RequiredPoints - available.Progress);
                    candidate = candidate.SelectGuardianForceLearning(provider, ability).AwardGuardianForceAbilityPoints(provider, checked((ushort)learnedPoints));
                    if (!candidate.GuardianForces[provider].HasLearnedAbility(ability))
                    {
                        continue;
                    }
                }
                int[] selected = summonedGuardian is int summon ? new[] { summon, provider }.Distinct().ToArray() : [provider];
                foreach (int guardian in selected)
                {
                    if ((candidate.GuardianForces[guardian].Flags & 1) == 0)
                    {
                        candidate = candidate.AcquireGuardianForce(guardian);
                    }
                    if ((candidate.Records[0].Junction.GuardianForces & (1 << guardian)) == 0)
                    {
                        candidate = candidate.ToggleGuardianForceJunction(0, guardian);
                    }
                }
                _ = candidate.SetJunctionAbility(0, 0, ability, command: true);
                if (summonedGuardian is int focal && candidate.GuardianForces[focal].HitPoints == 0)
                {
                    blocker = "GF_HP_PRECONDITION: the actual acquired GF has zero HP and the fixture cannot fabricate saved GF HP; an ordinary GF recovery prerequisite is required.";
                    return false;
                }
                commandAbility = ability;
                guardians = selected;
                guardianRecipes = selected.Select(guardian =>
                {
                    JsonObject preparation = new() { ["id"] = guardian, ["acquire"] = true };
                    if (guardian == provider && learnedPoints != 0)
                    {
                        preparation["learning_ability"] = ability;
                        preparation["ability_points"] = learnedPoints;
                    }
                    return preparation;
                }).ToArray();
                return true;
            }
        }
        blocker = $"COMMAND_LEARNING_PRECONDITION: installed command {command} has no initially learned provider after ordinary GF acquisition; a real supported learning/AP recipe is required.";
        return false;
    }
}
