using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private BattleAssets? battleAssets;
    private LiveMenuCatalog? battleCatalog;
    private BattleSession? battle;

    public BattleSession? Battle => battle;
    public BattleRewardSummary? LastBattleRewards { get; private set; }
    public BattleAssets BattleResources => battleAssets ??= new BattleAssets(assets.GameDirectory, resources);

    public BattleRewardSummary PreviewBattleRewards()
    {
        BattleSession active = battle ?? throw new InvalidOperationException("Battle rewards require an active battle owner.");
        if (active.Outcome != BattleOutcome.Victory)
        {
            throw new InvalidOperationException("Battle rewards are available after victory.");
        }
        BattleEncounterOccurrence encounter = session.State.PendingBattleEncounter ?? throw new InvalidDataException("Battle rewards require the observed encounter occurrence.");
        return ProjectBattleResult(CaptureBattleOutcome(active, encounter)).Rewards;
    }

    private void ConfigureBattleHost()
    {
        battle = session.State.Battle is null ? null : BattleSession.Restore(session.State.Battle);
        Events.HostAutomaticBattles = true;
        Events.UseInteractiveBattle = (_, _) => session.State.FullBattlePlaythrough == true;
        Events.ValidateInteractiveBattle = (encounter, flags) =>
        {
            try
            {
                BattleDefinition definition = CreateEncounterBattle(encounter, activeFlags: flags);
                _ = new BattleSession(definition, BattleAtbMode.Normal);
                return null;
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or FileNotFoundException or ArgumentException)
            {
                return "Interactive battle cannot enter: " + exception.Message;
            }
        };
    }

    public BattleDefinition CreateEncounterBattle(int encounterId, byte nativeSeed = 0, ushort activeFlags = 0)
    {
        BattleEncounter encounter = BattleResources.LoadEncounter(encounterId);
        _ = BattleResources.LoadStage(encounter.StageId);
        List<BattleCombatantDefinition> combatants = [.. CreateBattleParty()];
        NativeBattleRandomState random = NativeBattleRandomState.Initialize(nativeSeed);
        byte[] partyLevels = combatants.Select(actor => checked((byte)actor.Level)).ToArray();
        foreach (BattleEnemyPlacement enemy in encounter.Slots.Where(slot => slot.NativeModelId != 255 && (slot.Enabled || slot.NativeModelId != 0)))
        {
            if (!enemy.IsEnemyModel)
            {
                throw new NotSupportedException($"Scene slot {enemy.Slot} requires the native character participant statistics contract for model {enemy.NativeModelId}.");
            }
            _ = BattleResources.LoadEnemy(enemy.ModelId);
            byte roll = 0;
            byte? secondRoll = null;
            int randomBytes = enemy.Enabled ? BattleEnemyNativeStats.LevelRandomByteCount(checked((byte)enemy.Level)) : 0;
            if (randomBytes > 0)
            {
                random = random.Draw(out roll);
            }
            if (randomBytes == 2)
            {
                random = random.Draw(out byte second);
                secondRoll = second;
            }
            int level = enemy.Enabled ? BattleEnemyNativeStats.ResolveLevel(checked((byte)enemy.Level), partyLevels, roll, secondRoll) : 0;
            BattleEnemyNativeProjection stats = BattleResources.ReadEnemyNativeStats(enemy.ModelId).Project(level);
            int power = 0;
            int drainPercent = 0;
            combatants.Add(new BattleCombatantDefinition { Id = $"enemy-{enemy.Slot}", SceneSlot = enemy.Slot, InitialActive = enemy.Enabled, InitialHidden = enemy.Hidden, InitialUntargetable = enemy.Untargetable, InitialSuspended = enemy.InitiallySuspended, NativeAi = BattleResources.ReadEnemyAi(enemy.ModelId, level), NativeTemplate = BattleResources.ReadEnemyTemplate(enemy.ModelId, checked((byte)enemy.Level)), Name = BattleResources.ReadEnemyName(enemy.ModelId), ModelId = enemy.ModelId, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Speed = Math.Max(1, stats.Speed), Attack = stats.Strength, Defense = stats.Vitality, Level = level, Magic = stats.Magic, Spirit = stats.Spirit, Evasion = stats.Evasion, PhysicalPower = power, DrainPercent = drainPercent, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), DrawSpells = BattleResources.ReadEnemyDrawSpells(enemy.ModelId, level), DrawGuardianForces = BattleResources.ReadEnemyDrawGuardianForces(enemy.ModelId, level), AbilityPoints = BattleResources.ReadEnemyAbilityPoints(enemy.ModelId), RewardSlot = enemy.Slot, Rewards = BattleResources.ReadEnemyRewards(enemy.ModelId) });
        }
        BattleDefinition definition = new() { Name = $"Encounter {encounterId}", EncounterId = encounterId, StageId = encounter.StageId, Combatants = combatants.ToArray(), ActionDefinitions = CreateBattleActions(), NativeSeed = nativeSeed, NativeFlags = activeFlags, InitialRandom = random, PersistentAiVariables = session.State.BattleVariables?.Values.ToArray(), RareItem = (session.State.CharacterVitals!.GetEncounterAbilityFlags() & 2) != 0, Items = session.State.Economy?.Items.Where(stock => stock.ItemId > 0 && stock.Count > 0).Select(stock => new BattleStock(stock.ItemId, stock.Count)).ToArray() ?? throw new NotSupportedException("Interactive battle requires the canonical item owner.") };
        _ = new BattleScenePresentation(BattleResources, definition, true);
        return definition;
    }

    private BattleActionDefinition[] CreateBattleActions()
    {
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Battle actions require the canonical GF owner.");
        return BattlePartyProjection.CreateActions(vitals, BattleResources.ActionDefinitions);
    }
    public BattleCombatantDefinition[] CreateBattleParty()
    {
        battleCatalog ??= LiveMenuCatalog.ReadInstalled(assets);
        return BattlePartyProjection.Create(session.State, Events.Scene.Names, battleCatalog, BattleResources);
    }

    private void ValidateBattleHostState()
    {
        if (battle is null)
        {
            return;
        }
        if (session.State.RandomEncounters?.Pending is not null)
        {
            ValidateRandomBattleHostState();
            return;
        }
        NativeBattleCompletion request = Events.PendingInteractiveBattle ?? throw new InvalidDataException("Saved battle has no suspended interactive BATTLE owner.");
        if (SceneKind.Field != session.State.Scene || Events.Scene.NativeBattleWaits?.Values.Count(index => Events.Scene.NativeBattleCompletions![index] == request) != 1)
        {
            throw new InvalidDataException("Saved battle requires exactly one live field BATTLE owner.");
        }
        BattleDefinition current = CreateEncounterBattle(request.Encounter, battle.Capture().Definition.NativeSeed, request.Flags);
        long allocationClock = Events.Scene.NativeFieldInitialization!.TimerClockCallsAtAllocation!.Value;
        long completedCallbacks = Events.Scene.NativeBattleCompletions!.Where(completion => completion.Backend == NativeBattleBackend.Interactive && completion.ReturnFrame is not null && completion.BattleClockCallsAtEntry >= allocationClock).Sum(completion => (long)completion.EmulatedCombatCallbacks);
        if (Events.Scene.FieldCallbackClock!.BattleCallbacks != completedCallbacks + battle.Frame)
        {
            throw new InvalidDataException("Saved battle simulation frame disagrees with its executed native callback clock.");
        }
        if (!System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(current), System.Text.Json.JsonSerializer.SerializeToElement(battle.Capture().Definition)))
        {
            throw new InvalidDataException("Saved battle definition diverges from the current installed encounter and canonical party.");
        }
    }

    public int ApplyBattleInput(BattleInput input)
    {
        input.Validate();
        if (input.Kind == BattleInputKind.ConfigurePlaythrough)
        {
            session.SetFullBattlePlaythrough(input.FullCombat);
            if (input.FullCombat && session.State.Scene == SceneKind.WorldMap && !HasPendingBattle)
            {
                InitializeWorldRandomEncounters();
            }
            if (!input.FullCombat)
            {
                if (battle is null && (Events.PendingInteractiveBattle is not null || session.State.RandomEncounters?.Pending is not null))
                {
                    ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: input.Mode));
                }
                if (battle?.Outcome == BattleOutcome.Active)
                {
                    ApplyBattleInput(new(BattleInputKind.InstantVictory));
                }
            }
            return 0;
        }
        if (input.Kind == BattleInputKind.ConfigureEncounters)
        {
            ApplyEncounterConfiguration(input);
            return 0;
        }
        ObservePendingBattle();
        if (input.Kind == BattleInputKind.StartEncounter && input.NativeSeed is null)
        {
            // TODO(TD-009): Original CRT entropy
            input = input with { NativeSeed = (byte)System.Security.Cryptography.RandomNumberGenerator.GetInt32(256) };
        }
        if (TryApplyRandomBattleInput(input, out int randomAdvanced))
        {
            return randomAdvanced;
        }
        NativeBattleCompletion? request = Events.PendingInteractiveBattle;
        if (input.Kind == BattleInputKind.StartEncounter)
        {
            if (battle is not null || request is null)
            {
                throw new InvalidOperationException("Start Encounter requires one suspended interactive BATTLE request.");
            }
            battle = CreateLiveBattleSession(CreateEncounterBattle(request.Encounter, input.NativeSeed!.Value, request.Flags), input.Mode);
            session.SetBattle(battle.Capture());
            return 0;
        }
        BattleSession active = battle ?? throw new InvalidOperationException("No interactive battle is active.");
        long previous = active.Frame;
        if (input.Kind == BattleInputKind.Return)
        {
            if (active.Outcome != BattleOutcome.Victory || request is null)
            {
                throw new InvalidOperationException("Only a completed victory can return to its suspended field request.");
            }
            CompleteBattle(active);
            battle = null;
            session.SetBattle(null);
            return 0;
        }
        ExecuteBattleCommand(active, input);
        int advanced = checked((int)(active.Frame - previous));
        if (advanced > 0)
        {
            Events.AdvanceInteractiveBattleCallbacks(advanced);
        }
        session.SetBattle(active.Capture());
        return advanced;
    }
    internal static int ExecuteBattleCommand(BattleSession active, BattleInput input)
    {
        long before = active.Frame;
        switch (input.Kind)
        {
            case BattleInputKind.Tick:
                for (int frame = 0; frame < input.Frames; frame++)
                {
                    active.Tick();
                }
                break;
            case BattleInputKind.Attack:
                active.QueueAttack(input.ActorId!, input.TargetId!);
                break;
            case BattleInputKind.Action:
                active.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId, input.DrawCast);
                break;
            case BattleInputKind.AdvanceTurn:
                active.AdvanceTurn();
                break;
            case BattleInputKind.SetMode:
                active.SetMode(input.Mode);
                break;
            case BattleInputKind.InstantDefeat:
                active.InstantDefeat();
                break;
            case BattleInputKind.InstantVictory:
                active.InstantVictory();
                break;
            default:
                throw new NotSupportedException($"Battle input {input.Kind} is unsupported.");
        }
        return checked((int)(active.Frame - before));
    }

    internal BattleResult CaptureBattleOutcome(BattleSession active, BattleEncounterOccurrence encounter)
    {
        encounter.Validate();
        if (active.Outcome == BattleOutcome.Active || active.EncounterId != encounter.Formation)
        {
            throw new InvalidOperationException("A factual battle outcome can only be captured from its completed live encounter.");
        }
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidDataException("Battle result lost its canonical party owner.");
        NativeEconomy economy = session.State.Economy ?? throw new InvalidDataException("Battle result lost its canonical item owner.");
        List<BattlePartyResult> party = [];
        foreach (BattleCombatant player in active.Combatants.Where(actor => actor.IsPlayer))
        {
            int row = int.Parse(player.Id.AsSpan("party-".Length), System.Globalization.CultureInfo.InvariantCulture);
            NativeCharacterVital original = vitals.Records[row];
            if (original.HitPoints.Minimum != original.HitPoints.Maximum)
            {
                throw new NotSupportedException("Battle facts require the actual exact pre-battle HP operand.");
            }
            ushort status = (ushort)(player.NativeRetainedStatusBits | (player.CurrentHp == 0 ? 1 : 0));
            foreach ((int mask, BattleStatusKind kind) in new[] { (2, BattleStatusKind.Poison), (8, BattleStatusKind.Blind), (16, BattleStatusKind.Silence) })
            {
                if (player.StatusEffects.Any(effect => effect.Kind == kind))
                {
                    status |= (ushort)mask;
                }
            }
            List<BattleMagicChange> magic = [];
            foreach (int spell in original.MagicInventory.Where(stock => stock.SpellId != 0).Select(stock => (int)stock.SpellId).Concat(player.MagicStocks.Select(stock => stock.Id)).Distinct())
            {
                int before = original.MagicInventory.FirstOrDefault(stock => stock.SpellId == spell)?.Count ?? 0;
                int after = player.MagicStocks.FirstOrDefault(stock => stock.Id == spell)?.Count ?? 0;
                if (before != after)
                {
                    magic.Add(new BattleMagicChange(checked((byte)spell), checked((byte)before), after - before));
                }
            }
            party.Add(new BattlePartyResult(checked((byte)row), original.CharacterId, original.HitPoints.Minimum, checked((ushort)player.CurrentHp), original.Status, status, magic.ToArray()));
        }
        List<BattleItemConsumption> items = [];
        foreach (NativeItemStock stock in economy.Items.Where(stock => stock.ItemId != 0 && stock.Count != 0))
        {
            int after = active.Items.FirstOrDefault(item => item.Id == stock.ItemId)?.Count ?? 0;
            if (after > stock.Count)
            {
                throw new NotSupportedException("Battle item gains require a supported reward fact contract.");
            }
            if (after < stock.Count)
            {
                items.Add(new BattleItemConsumption(stock.ItemId, stock.Count, checked((byte)(stock.Count - after))));
            }
        }
        BattleSnapshot completed = active.Capture();
        bool debugVictory = active.Events.Any(entry => entry.Kind == "debug-victory");
        int[] defeated = debugVictory ? [] : active.EnemyDefeats.Select(defeat => defeat.Slot).ToArray();
        int[] eligible = active.Outcome == BattleOutcome.Victory && !debugVictory ? BattleResultProjection.EligibleGuardianForces(vitals) : [];
        BattleResult result = new(encounter, active.Outcome, party.ToArray(), items.ToArray(), defeated, eligible, debugVictory, debugVictory ? BattleRewardPolicy.DebugNoRewards : BattleRewardPolicy.NativeCombat, debugVictory ? [] : active.EnemyDefeats.ToArray()) { ScriptedItems = debugVictory ? [] : active.ScriptedRewards.ToArray(), PersistentAiVariablesBefore = debugVictory ? null : completed.Definition.PersistentAiVariables?.ToArray(), PersistentAiVariablesAfter = debugVictory ? null : completed.PersistentAiVariables?.ToArray() };
        result.Validate();
        return result;
    }

    internal BattleResult CaptureAutomaticBattleOutcome(BattleEncounterOccurrence encounter)
    {
        encounter.Validate();
        NativeBattleCompletion current = Events.PendingHostedBattle ?? throw new InvalidOperationException("Automatic outcome requires its actual held native BATTLE operation.");
        if (current.Backend != NativeBattleBackend.AutomaticVictory || current.Encounter != encounter.Formation || encounter.ScriptedOwner is null)
        {
            throw new InvalidOperationException("Automatic outcome cannot be inferred from completed battle history or another backend.");
        }
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidDataException("Automatic outcome requires its current canonical party owner.");
        List<BattlePartyResult> party = [];
        foreach (byte row in vitals.CombatParty.Where(row => row != 255))
        {
            NativeCharacterVital member = vitals.Records[row];
            if (member.HitPoints.Minimum != member.HitPoints.Maximum)
            {
                throw new NotSupportedException("Automatic victory cannot invent an exact HP fact from an uncertain native HP interval.");
            }
            party.Add(new BattlePartyResult(row, member.CharacterId, member.HitPoints.Minimum, member.HitPoints.Minimum, member.Status, member.Status, []));
        }
        BattleResult result = new(encounter, BattleOutcome.Victory, party.ToArray(), [], BattleResources.LoadEncounter(encounter.Formation).Enemies.Select(enemy => enemy.Slot).ToArray(), BattleResultProjection.EligibleGuardianForces(vitals), false, BattleRewardPolicy.ResearchAutomaticApOnly, []);
        result.Validate();
        return result;
    }

    internal void ApplyBattleResult(BattleEncounterOccurrence expected, BattleResult result)
    {
        expected.Validate();
        result.Validate();
        if (result.Encounter != expected || session.State.PendingBattleEncounter != expected || session.State.AppliedBattleResultIds.Contains(expected.OccurrenceId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Battle result requires its unapplied matching encounter occurrence.");
        }
        if (session.State.Scene != expected.SourceScene || session.State.FieldId != expected.SourceFieldId || !BattleResources.FormationSourceHash(expected.Formation).Equals(expected.FormationHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Battle result source scene or current original formation resource differs from its observed encounter.");
        }
        if (expected.ScriptedOwner is null && Events.PendingHostedBattle is not null || expected.ScriptedOwner is not null && session.State.RandomEncounters?.Pending is not null)
        {
            throw new InvalidDataException("Battle result conflicts with the current native battle owner.");
        }
        if (expected.ScriptedOwner is { } owner)
        {
            NativeBattleCompletion request = Events.PendingHostedBattle ?? throw new InvalidDataException("Scripted battle fact lost its original pending BATTLE owner.");
            if (request.FieldId != owner.FieldId || request.ObjectId != owner.ObjectId || request.ScriptId != owner.ScriptId || request.Operation != owner.Operation || request.Flags != owner.Flags || request.Encounter != expected.Formation || request.ProgramHash != owner.ProgramHash)
            {
                throw new InvalidDataException("Scripted battle result owner differs from the actual suspended BATTLE operation.");
            }
            if ((request.Backend == NativeBattleBackend.AutomaticVictory) != (result.RewardPolicy == BattleRewardPolicy.ResearchAutomaticApOnly))
            {
                throw new InvalidDataException("Battle reward policy differs from its actual hosted battle backend.");
            }
        }
        BattleResultOwners owners = ProjectBattleResult(result);
        NativeBattleVariableState? variables = null;
        if (result.PersistentAiVariablesBefore is { } before)
        {
            NativeBattleVariableState current = session.State.BattleVariables ?? throw new InvalidDataException("Battle result lost its saved native variable bank.");
            if (!current.Values.SequenceEqual(before))
            {
                throw new InvalidDataException("Battle variable effects disagree with their current saved owner.");
            }
            variables = new NativeBattleVariableState(System.Collections.Immutable.ImmutableArray.CreateRange(result.PersistentAiVariablesAfter!));
        }
        session.CommitBattleResult(expected, owners.CharacterVitals, owners.Economy, owners.Cards, variables);
        LastBattleRewards = owners.Rewards;
    }

    private BattleResultOwners ProjectBattleResult(BattleResult result)
    {
        BattleEncounter formation = BattleResources.LoadEncounter(result.Encounter.Formation);
        if (result.DefeatedEnemySlots.Any(slot => !formation.Slots.Any(enemy => enemy.Slot == slot && enemy.NativeModelId >= 16 && enemy.NativeModelId != 255)) || result.RewardPolicy == BattleRewardPolicy.ResearchAutomaticApOnly && !formation.Enemies.Select(enemy => enemy.Slot).Order().SequenceEqual(result.DefeatedEnemySlots.Order()))
        {
            throw new InvalidDataException("Battle result defeated-enemy facts contradict the current original formation.");
        }
        BattleEnemyPlacement[] rewarded = formation.Slots.Where(enemy => result.DefeatedEnemySlots.Contains(enemy.Slot)).ToArray();
        Dictionary<int, int> currentAp = rewarded.ToDictionary(enemy => enemy.Slot, enemy => BattleResources.ReadEnemyAbilityPoints(enemy.ModelId));
        Dictionary<int, BattleEnemyRewardDefinition> currentRewards = rewarded.ToDictionary(enemy => enemy.Slot, enemy => BattleResources.ReadEnemyRewards(enemy.ModelId));
        bool noExperience = NativeBattleRewardDistribution.NoExperience(checked((ushort)(result.Encounter.ScriptedOwner?.Flags ?? 0)), formation.Flags);
        return BattleResultProjection.Apply(result, session.State.CharacterVitals ?? throw new InvalidDataException("Battle result lost its canonical party owner."), session.State.Economy ?? throw new InvalidDataException("Battle result lost its canonical economy owner."), currentAp, currentRewards, assets.ReadNativeBattleExperienceDefinition(), session.State.CardInventory, noExperience);
    }
}
