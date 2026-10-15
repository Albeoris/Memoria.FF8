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

    public BattleDefinition CreateEncounterBattle(int encounterId)
    {
        BattleEncounter encounter = BattleResources.LoadEncounter(encounterId);
        _ = BattleResources.LoadStage(encounter.StageId);
        List<BattleCombatantDefinition> combatants = [.. CreateBattleParty()];
        foreach (BattleEnemyPlacement enemy in encounter.Enemies)
        {
            if (enemy.Hidden || enemy.Untargetable)
            {
                throw new NotSupportedException("Hidden/untargetable encounter participants require their battle-script activation contract.");
            }
            _ = BattleResources.LoadEnemy(enemy.ModelId);
            // TODO(TD-009): native enemy level/stat/AI scripts remain deferred; this primitive-attack scaffold uses declared tuning
            int level = enemy.Level is > 0 and < 100 ? enemy.Level : 10;
            int hp = 150 + level * 25;
            combatants.Add(new BattleCombatantDefinition { Id = $"enemy-{enemy.Slot}", Name = BattleResources.ReadEnemyName(enemy.ModelId), ModelId = enemy.ModelId, MaximumHp = hp, CurrentHp = hp, Speed = 70 + level, Attack = 15 + level * 2, Defense = 5 + level });
        }
        BattleDefinition definition = new() { Name = $"Encounter {encounterId}", EncounterId = encounterId, StageId = encounter.StageId, Combatants = combatants.ToArray() };
        _ = new BattleScenePresentation(BattleResources, definition, true);
        return definition;
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
        NativeBattleCompletion request = Events.PendingInteractiveBattle ?? throw new InvalidDataException("Saved battle has no suspended interactive BATTLE owner.");
        if (SceneKind.Field != session.State.Scene || session.State.FullBattlePlaythrough != true || Events.Scene.NativeBattleWaits?.Values.Count(index => Events.Scene.NativeBattleCompletions![index] == request) != 1)
        {
            throw new InvalidDataException("Saved battle requires exactly one live field BATTLE owner and its full-combat playthrough.");
        }
        BattleDefinition current = CreateEncounterBattle(request.Encounter);
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
            // Allocate the genuine New Game field before applying a host policy input and its native music/input/stat bootstraps.
            if (Field is null)
            {
                EnsureField();
            }
            if (session.State.Scene != SceneKind.Field || Events.FieldId != 74 || Events.Frame != 0 || session.State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
            {
                throw new InvalidOperationException("Battle policy belongs to a fresh New Game before its first field callback.");
            }
        }
        NativeBattleCompletion? request = Events.PendingInteractiveBattle;
        int advanced = 0;
        if (input.Kind == BattleInputKind.ConfigurePlaythrough)
        {
            session.SetFullBattlePlaythrough(input.FullCombat);
        }
        else if (input.Kind == BattleInputKind.StartEncounter)
        {
            if (battle is not null || request is null)
            {
                throw new InvalidOperationException("Start Encounter requires one suspended interactive BATTLE request.");
            }
            battle = new BattleSession(CreateEncounterBattle(request.Encounter), input.Mode);
            session.SetBattle(battle.Capture());
        }
        else
        {
            BattleSession active = battle ?? throw new InvalidOperationException("No interactive battle is active.");
            long previous = active.Frame;
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
                case BattleInputKind.AdvanceTurn:
                    active.AdvanceTurn();
                    break;
                case BattleInputKind.SetMode:
                    active.SetMode(input.Mode);
                    break;
                case BattleInputKind.InstantVictory:
                    active.InstantVictory();
                    break;
                case BattleInputKind.Return:
                    if (active.Outcome != BattleOutcome.Victory || request is null)
                    {
                        throw new InvalidOperationException("Only a completed victory can return to its suspended field request.");
                    }
                    NativeCharacterVitals vitals = session.State.CharacterVitals!;
                    foreach (BattleCombatant player in active.Combatants.Where(actor => actor.IsPlayer))
                    {
                        int row = int.Parse(player.Id.AsSpan("party-".Length), System.Globalization.CultureInfo.InvariantCulture);
                        vitals = vitals.WithSavedHitPoints(row, player.CurrentHp);
                    }
                    Events.ReturnFromInteractiveBattle(checked((int)active.Frame), () => session.SetCharacterVitals(vitals));
                    battle = null;
                    session.SetBattle(null);
                    break;
                default:
                    throw new NotSupportedException($"Battle input {input.Kind} is unsupported.");
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

        PlayerTrigger after = Events.NextTrigger;
        return advanced;
    }
}
