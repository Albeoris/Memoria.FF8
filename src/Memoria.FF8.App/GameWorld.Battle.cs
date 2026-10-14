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
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Interactive battle requires the actual current character records.");
        vitals.Validate();
        BattleEncounter encounter = BattleResources.LoadEncounter(encounterId);
        _ = BattleResources.LoadStage(encounter.StageId);
        battleCatalog ??= LiveMenuCatalog.ReadInstalled(assets);
        List<BattleCombatantDefinition> combatants = [];
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
            int costume = session.State.CharacterAppearance?.Records[row].Dress ?? throw new NotSupportedException("Interactive battle requires the current saved costume owner.");
            _ = BattleResources.LoadPlayer(character, costume);
            string name = Events.Scene.Names.GetValueOrDefault(character, battleCatalog.CharacterName(character) ?? $"Character {character}");
            // TODO(TD-009): non-HP stats use explicit scaffold tuning until the native combat stat/damage contract is implemented
            BattleWeaponDefinition equipped = BattleResources.WeaponDefinitions[weapon];
            combatants.Add(new BattleCombatantDefinition { Id = $"party-{row}", Name = name, IsPlayer = true, ModelId = character, Costume = costume, MaximumHp = vitals.Definitions.CharacterMaximum(saved), CurrentHp = saved.HitPoints.Minimum, Speed = 90 + level, Attack = 25 + level * 4 + equipped.AttackPower + equipped.StrengthBonus, Defense = 10 + level, WeaponId = weapon, WeaponName = equipped.Name, EquipmentIds = saved.Junction.GuardianForceAssignments.ToArray() });
        }
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
        }
        PlayerTrigger after = Events.NextTrigger;
        if (input.Kind != BattleInputKind.Return)
        {
            session.RecordReplayEntry(new ReplayJournalEntry(ReplayEntryKind.BattleInput, sourceScene, sourceField, fieldFrame) { BattleInput = input, Code = request?.Flags ?? 0, Value = request?.Encounter ?? 0, NativeSourceHash = request?.ProgramHash, Advanced = advanced, Trigger = (int)before.Kind, ResultTrigger = (int)after.Kind, ResultFrame = Events.Frame, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ObjectId = request?.ObjectId, ScriptId = request?.ScriptId, Operation = request?.Operation });
        }
        return advanced;
    }
}
