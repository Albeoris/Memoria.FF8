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
    public BattleAssets BattleResources => battleAssets ??= new BattleAssets(assets.GameDirectory);

    private void ConfigureBattleHost()
    {
        battle = session.State.Battle is null ? null : BattleSession.Restore(session.State.Battle);
        Events.HostAutomaticBattles = true;
        Events.UseInteractiveBattle = (_, _) => session.State.FullBattlePlaythrough == true;
        Events.ValidateInteractiveBattle = (encounter, flags) =>
        {
            try
            {
                if (IsCausalReconstruction)
                {
                    _ = BattleResources.LoadEncounter(encounter);
                    _ = BattleResources.FormationSourceHash(encounter);
                    return null;
                }
                BattleDefinition definition = CreateEncounterBattle(encounter);
                _ = new BattleSession(definition, BattleAtbMode.Normal);
                return null;
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or FileNotFoundException or ArgumentException)
            {
                return "Interactive battle cannot enter: " + exception.Message;
            }
        };
    }

    public BattleDefinition CreateEncounterBattle(int encounterId, byte nativeSeed = 0)
    {
        BattleEncounter encounter = BattleResources.LoadEncounter(encounterId);
        _ = BattleResources.LoadStage(encounter.StageId);
        List<BattleCombatantDefinition> combatants = [.. CreateBattleParty()];
        NativeBattleRandomState random = NativeBattleRandomState.Initialize(nativeSeed);
        byte[] partyLevels = combatants.Select(actor => checked((byte)actor.Level)).ToArray();
        foreach (BattleEnemyPlacement enemy in encounter.Enemies)
        {
            if (enemy.Hidden || enemy.Untargetable)
            {
                throw new NotSupportedException("Hidden/untargetable encounter participants require their battle-script activation contract.");
            }
            _ = BattleResources.LoadEnemy(enemy.ModelId);
            byte roll = 0;
            if (enemy.Level == 255)
            {
                random = random.Draw(out roll);
            }
            int level = BattleEnemyNativeStats.ResolveLevel(checked((byte)enemy.Level), partyLevels, roll);
            BattleEnemyNativeProjection stats = BattleResources.ReadEnemyNativeStats(enemy.ModelId).Project(level);
            (int power, int drainPercent) = BattleResources.ReadEnemyPrimitiveAttack(enemy.ModelId, level);
            combatants.Add(new BattleCombatantDefinition { Id = $"enemy-{enemy.Slot}", Name = BattleResources.ReadEnemyName(enemy.ModelId), ModelId = enemy.ModelId, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Speed = Math.Max(1, stats.Speed), Attack = stats.Strength, Defense = stats.Vitality, Level = level, Magic = stats.Magic, Spirit = stats.Spirit, PhysicalPower = power, DrainPercent = drainPercent, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), DrawSpells = BattleResources.ReadEnemyDrawSpells(enemy.ModelId, level), AbilityPoints = BattleResources.ReadEnemyAbilityPoints(enemy.ModelId) });
        }
        BattleDefinition definition = new() { Name = $"Encounter {encounterId}", EncounterId = encounterId, StageId = encounter.StageId, Combatants = combatants.ToArray(), ActionDefinitions = CreateBattleActions(), NativeSeed = nativeSeed, InitialRandom = random, Items = session.State.Economy?.Items.Where(stock => stock.ItemId > 0 && stock.Count > 0).Select(stock => new BattleStock(stock.ItemId, stock.Count)).ToArray() ?? throw new NotSupportedException("Interactive battle requires the canonical item owner.") };
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
        if (SceneKind.Field != session.State.Scene || session.State.FullBattlePlaythrough != true || Events.Scene.NativeBattleWaits?.Values.Count(index => Events.Scene.NativeBattleCompletions![index] == request) != 1)
        {
            throw new InvalidDataException("Saved battle requires exactly one live field BATTLE owner and its full-combat playthrough.");
        }
        BattleDefinition current = CreateEncounterBattle(request.Encounter, battle.Capture().Definition.NativeSeed);
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
        if (input.Kind == BattleInputKind.StartEncounter && input.NativeSeed is null)
        {
            // TODO(TD-009): Live combat samples external entropy; its
            input = input with { NativeSeed = (byte)System.Security.Cryptography.RandomNumberGenerator.GetInt32(256) };
        }
        if (TryApplyRandomBattleInput(input, out int randomAdvanced))
        {
            return randomAdvanced;
        }
        if (input.Kind == BattleInputKind.ConfigurePlaythrough)
        {
            // Allocate the genuine New Game field before recording a host policy input: its music/input/stat bootstraps require the untouched root journal.
            if (Field is null)
            {
                EnsureField();
            }
            if (session.State.Scene != SceneKind.Field || Events.FieldId != 74 || Events.Frame != 0 || session.State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
            {
                throw new InvalidOperationException("Battle policy belongs to a fresh New Game before its first field callback.");
            }
        }
        long fieldFrame = Events.Frame;
        PlayerTrigger before = Events.NextTrigger;
        SceneKind sourceScene = session.State.Scene;
        int sourceField = session.State.FieldId;
        NativeBattleCompletion? request = Events.PendingInteractiveBattle;
        int advanced = 0;
        using (input.Kind == BattleInputKind.Return ? null : session.BeginReplayCapture())
        {
            if (input.Kind == BattleInputKind.ConfigurePlaythrough)
            {
                session.SetFullBattlePlaythrough(input.FullCombat);
            }
            else if (input.Kind == BattleInputKind.ConfigureEncounters)
            {
                ApplyEncounterConfiguration(input);
            }
            else if (input.Kind == BattleInputKind.StartEncounter)
            {
                if (battle is not null || request is null)
                {
                    throw new InvalidOperationException("Start Encounter requires one suspended interactive BATTLE request.");
                }
                battle = CreateLiveBattleSession(CreateEncounterBattle(request.Encounter, input.NativeSeed!.Value), input.Mode);
                session.SetBattle(battle.Capture());
            }
            else
            {
                BattleSession active = battle ?? throw new InvalidOperationException("No interactive battle is active.");
                long previous = active.Frame;
                switch (input.Kind)
                {
                    case BattleInputKind.Return:
                        if (active.Outcome != BattleOutcome.Victory || request is null)
                        {
                            throw new InvalidOperationException("Only a completed victory can return to its suspended field request.");
                        }
                        CompleteRecordedBattle(active);
                        battle = null;
                        session.SetBattle(null);
                        break;
                    default:
                        ExecuteBattleCommand(active, input);
                        break;
                }
                advanced = checked((int)(active.Frame - previous));
                if (advanced > 0)
                {
                    Events.AdvanceInteractiveBattleCallbacks(advanced);
                }
                if (active.Outcome == BattleOutcome.Defeat && session.State.PendingBattleEncounter is not null)
                {
                    RecordCompletedDefeat(active);
                }
                if (battle is not null)
                {
                    session.SetBattle(active.Capture());
                }
            }
        }
        PlayerTrigger after = Events.NextTrigger;
        if (input.Kind is BattleInputKind.ConfigurePlaythrough or BattleInputKind.ConfigureEncounters)
        {
            session.RecordReplayEntry(new ReplayJournalEntry(ReplayEntryKind.BattleInput, sourceScene, sourceField, fieldFrame) { BattleInput = input, Code = request?.Flags ?? 0, Value = request?.Encounter ?? 0, NativeSourceHash = request?.ProgramHash, Advanced = advanced, Trigger = (int)before.Kind, ResultTrigger = (int)after.Kind, ResultFrame = Events.Frame, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ObjectId = request?.ObjectId, ScriptId = request?.ScriptId, Operation = request?.Operation });
        }
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
            case BattleInputKind.InstantVictory:
                active.InstantVictory();
                break;
            default:
                throw new NotSupportedException($"Battle input {input.Kind} is unsupported.");
        }
        return checked((int)(active.Frame - before));
    }

    internal ReplayBattleOutcome CaptureBattleOutcome(BattleSession active, ReplayBattleEncounter encounter)
    {
        encounter.Validate();
        if (active.Outcome == BattleOutcome.Active || active.EncounterId != encounter.Formation)
        {
            throw new InvalidOperationException("A factual battle outcome can only be captured from its completed live encounter.");
        }
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidDataException("Battle result lost its canonical party owner.");
        NativeEconomy economy = session.State.Economy ?? throw new InvalidDataException("Battle result lost its canonical item owner.");
        List<ReplayBattlePartyResult> party = [];
        foreach (BattleCombatant player in active.Combatants.Where(actor => actor.IsPlayer))
        {
            int row = int.Parse(player.Id.AsSpan("party-".Length), System.Globalization.CultureInfo.InvariantCulture);
            NativeCharacterVital original = vitals.Records[row];
            if (original.HitPoints.Minimum != original.HitPoints.Maximum)
            {
                throw new NotSupportedException("Battle facts require the actual exact pre-battle HP operand.");
            }
            ushort status = (ushort)((original.Status & 0x80) | (player.CurrentHp == 0 ? 1 : 0));
            foreach ((int mask, BattleStatusKind kind) in new[] { (2, BattleStatusKind.Poison), (8, BattleStatusKind.Blind), (16, BattleStatusKind.Silence) })
            {
                if (player.StatusEffects.Any(effect => effect.Kind == kind))
                {
                    status |= (ushort)mask;
                }
            }
            List<ReplayBattleMagicChange> magic = [];
            foreach (int spell in original.MagicInventory.Where(stock => stock.SpellId != 0).Select(stock => (int)stock.SpellId).Concat(player.MagicStocks.Select(stock => stock.Id)).Distinct())
            {
                int before = original.MagicInventory.FirstOrDefault(stock => stock.SpellId == spell)?.Count ?? 0;
                int after = player.MagicStocks.FirstOrDefault(stock => stock.Id == spell)?.Count ?? 0;
                if (before != after)
                {
                    magic.Add(new ReplayBattleMagicChange(checked((byte)spell), checked((byte)before), after - before));
                }
            }
            party.Add(new ReplayBattlePartyResult(checked((byte)row), original.CharacterId, original.HitPoints.Minimum, checked((ushort)player.CurrentHp), original.Status, status, magic.ToArray()));
        }
        List<ReplayBattleItemConsumption> items = [];
        foreach (NativeItemStock stock in economy.Items.Where(stock => stock.ItemId != 0 && stock.Count != 0))
        {
            int after = active.Items.FirstOrDefault(item => item.Id == stock.ItemId)?.Count ?? 0;
            if (after > stock.Count)
            {
                throw new NotSupportedException("Battle item gains require a supported reward fact contract.");
            }
            if (after < stock.Count)
            {
                items.Add(new ReplayBattleItemConsumption(stock.ItemId, stock.Count, checked((byte)(stock.Count - after))));
            }
        }
        bool debugVictory = active.Events.Any(entry => entry.Kind == "debug-victory");
        int[] defeated = active.Combatants.Where(actor => !actor.IsPlayer && !actor.IsAlive).Select(actor => int.Parse(actor.Id.AsSpan("enemy-".Length), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        int[] eligible = active.Outcome == BattleOutcome.Victory && !debugVictory ? BattleResultProjection.EligibleGuardianForces(vitals) : [];
        ReplayBattleOutcome result = new(encounter, active.Outcome, party.ToArray(), items.ToArray(), defeated, eligible, debugVictory);
        result.Validate();
        return result;
    }

    internal ReplayBattleOutcome CaptureAutomaticBattleOutcome(ReplayBattleEncounter encounter)
    {
        encounter.Validate();
        NativeBattleCompletion current = Events.PendingHostedBattle ?? throw new InvalidOperationException("Automatic outcome requires its actual held native BATTLE operation.");
        if (current.Backend != NativeBattleBackend.AutomaticVictory || current.Encounter != encounter.Formation || encounter.ScriptedOwner is null)
        {
            throw new InvalidOperationException("Automatic outcome cannot be inferred from completed battle history or another backend.");
        }
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidDataException("Automatic outcome requires its current canonical party owner.");
        List<ReplayBattlePartyResult> party = [];
        foreach (byte row in vitals.CombatParty.Where(row => row != 255))
        {
            NativeCharacterVital member = vitals.Records[row];
            if (member.HitPoints.Minimum != member.HitPoints.Maximum)
            {
                throw new NotSupportedException("Automatic victory cannot invent an exact HP fact from an uncertain native HP interval.");
            }
            party.Add(new ReplayBattlePartyResult(row, member.CharacterId, member.HitPoints.Minimum, member.HitPoints.Minimum, member.Status, member.Status, []));
        }
        ReplayBattleOutcome result = new(encounter, BattleOutcome.Victory, party.ToArray(), [], BattleResources.LoadEncounter(encounter.Formation).Enemies.Select(enemy => enemy.Slot).ToArray(), BattleResultProjection.EligibleGuardianForces(vitals), false);
        result.Validate();
        return result;
    }

    internal void ApplyBattleResult(ReplayBattleEncounter expected, ReplayBattleOutcome result)
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
        if (expected.ScriptedOwner is { } owner)
        {
            NativeBattleCompletion request = Events.PendingHostedBattle ?? throw new InvalidDataException("Scripted battle fact lost its original pending BATTLE owner.");
            if (request.FieldId != owner.FieldId || request.ObjectId != owner.ObjectId || request.ScriptId != owner.ScriptId || request.Operation != owner.Operation || request.Flags != owner.Flags || request.Encounter != expected.Formation || request.ProgramHash != owner.ProgramHash)
            {
                throw new InvalidDataException("Scripted battle result owner differs from the actual suspended BATTLE operation.");
            }
        }
        BattleEncounter formation = BattleResources.LoadEncounter(expected.Formation);
        if (result.DefeatedEnemySlots.Any(slot => !formation.Enemies.Any(enemy => enemy.Slot == slot)) || result.Outcome == BattleOutcome.Victory && !formation.Enemies.Select(enemy => enemy.Slot).Order().SequenceEqual(result.DefeatedEnemySlots.Order()))
        {
            throw new InvalidDataException("Battle result defeated-enemy facts contradict the current original formation.");
        }
        Dictionary<int, int> currentAp = formation.Enemies.ToDictionary(enemy => enemy.Slot, enemy => BattleResources.ReadEnemyAbilityPoints(enemy.ModelId));
        BattleResultOwners owners = BattleResultProjection.Apply(result, session.State.CharacterVitals ?? throw new InvalidDataException("Battle result lost its canonical party owner."), session.State.Economy ?? throw new InvalidDataException("Battle result lost its canonical economy owner."), currentAp);
        session.CommitBattleResult(expected, owners.CharacterVitals, owners.Economy);
    }
}
