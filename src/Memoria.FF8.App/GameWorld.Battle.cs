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
        Events.UseInteractiveBattle = (_, _) => session.State.FullBattlePlaythrough == true;
        Events.ValidateInteractiveBattle = (encounter, flags) =>
        {
            try
            {
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
            // TODO(TD-009): Host samples and journals explicit external per-battle entropy; the original CRT wall-clock
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
                battle = new BattleSession(CreateEncounterBattle(request.Encounter, input.NativeSeed!.Value), input.Mode);
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
                        Events.ReturnFromInteractiveBattle(checked((int)active.Frame), () => PublishBattleOutcome(active));
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
                if (battle is not null)
                {
                    session.SetBattle(active.Capture());
                }
            }
        }
        PlayerTrigger after = Events.NextTrigger;
        if (input.Kind != BattleInputKind.Return)
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

    private NativeCharacterVitals ProjectBattleOutcome(BattleSession active)
    {
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidDataException("Battle return lost its canonical party owner.");
        foreach (BattleCombatant player in active.Combatants.Where(actor => actor.IsPlayer))
        {
            int row = int.Parse(player.Id.AsSpan("party-".Length), System.Globalization.CultureInfo.InvariantCulture);
            vitals = vitals.WithSavedHitPoints(row, player.CurrentHp);
            NativeMagicStock[] stocks = vitals.Records[row].MagicInventory.Select(stock => stock.SpellId == 0 ? stock : new NativeMagicStock(stock.SpellId, checked((byte)(player.MagicStocks.FirstOrDefault(current => current.Id == stock.SpellId)?.Count ?? 0)))).ToArray();
            foreach (BattleStock stock in player.MagicStocks.Where(stock => !stocks.Any(current => current.SpellId == stock.Id)))
            {
                int empty = Array.FindIndex(stocks, entry => entry.SpellId == 0 || entry.Count == 0);
                if (empty < 0)
                {
                    throw new InvalidDataException("Battle magic inventory exceeded its native thirty-two slots.");
                }
                stocks[empty] = new NativeMagicStock(checked((byte)stock.Id), checked((byte)stock.Count));
            }
            NativeCharacterVital[] records = vitals.Records.ToArray();
            ushort status = (ushort)((records[row].Status & 0x80) | (player.CurrentHp == 0 ? 1 : 0));
            foreach ((int mask, BattleStatusKind kind) in new[] { (2, BattleStatusKind.Poison), (8, BattleStatusKind.Blind), (16, BattleStatusKind.Silence) })
            {
                if (player.StatusEffects.Any(effect => effect.Kind == kind))
                {
                    status |= (ushort)mask;
                }
            }
            records[row] = records[row] with { MagicInventory = stocks, Status = status };
            vitals = vitals with { Records = records };
        }
        if (active.Outcome == BattleOutcome.Victory && !active.Events.Any(entry => entry.Kind == "debug-victory"))
        {
            ushort points = unchecked((ushort)active.Capture().Definition.Combatants.Where(actor => !actor.IsPlayer).Sum(actor => actor.AbilityPoints));
            // Native494D40 includes an active party actor when KO is clear OR Petrify is clear; current supported saved statuses have no Petrify.
            // Every junctioned GF with nonzero current HP receives the entire AP WORD;4A6680/497010 apply learning and saturation.
            foreach (int id in vitals.CombatParty.Where(row => row != 255).SelectMany(row => vitals.Records[row].Junction.GuardianForceAssignments).Distinct().Where(id => vitals.GuardianForces[id].HitPoints > 0))
            {
                vitals = vitals.AwardGuardianForceAbilityPoints(id, points);
            }
        }
        return vitals.RebuildParty();
    }

    private void PublishBattleOutcome(BattleSession active)
    {
        NativeCharacterVitals vitals = ProjectBattleOutcome(active);
        NativeEconomy economy = session.State.Economy ?? throw new InvalidDataException("Battle return lost its canonical item owner.");
        NativeItemStock[] stocks = economy.Items.Select(stock => stock.ItemId == 0 ? stock : new NativeItemStock(stock.ItemId, checked((byte)(active.Items.FirstOrDefault(current => current.Id == stock.ItemId)?.Count ?? 0)))).ToArray();
        session.SetCharacterVitals(vitals);
        session.SetEconomy(economy with { Items = stocks });
    }
}
