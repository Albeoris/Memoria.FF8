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
            _ = BattleResources.LoadPlayer(character);
            string name = Events.Scene.Names.GetValueOrDefault(character, battleCatalog.CharacterName(character) ?? $"Character {character}");
            // TODO(TD-009): non-HP stats use explicit scaffold tuning until the native combat stat/damage contract is implemented
            combatants.Add(new BattleCombatantDefinition { Id = $"party-{row}", Name = name, IsPlayer = true, ModelId = character, MaximumHp = vitals.Definitions.CharacterMaximum(saved), CurrentHp = saved.HitPoints.Minimum, Speed = 90 + level, Attack = 25 + level * 4 + battleCatalog.WeaponAttackPower(weapon), Defense = 10 + level, WeaponId = weapon, WeaponName = battleCatalog.WeaponName(weapon), EquipmentIds = saved.Junction.GuardianForceAssignments.ToArray() });
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
            combatants.Add(new BattleCombatantDefinition { Id = $"enemy-{enemy.Slot}", Name = $"Enemy {enemy.ModelId:000}", ModelId = enemy.ModelId, MaximumHp = hp, CurrentHp = hp, Speed = 70 + level, Attack = 15 + level * 2, Defense = 5 + level });
        }
        return new BattleDefinition { Name = $"Encounter {encounterId}", EncounterId = encounterId, StageId = encounter.StageId, Combatants = combatants.ToArray() };
    }

    public int ApplyBattleInput(BattleInput input)
    {
        input.Validate();
        long fieldFrame = Events.Frame;
        PlayerTrigger before = Events.NextTrigger;
        NativeBattleCompletion? request = Events.PendingInteractiveBattle;
        int advanced = 0;
        using (session.BeginReplayCapture())
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
                        session.SetCharacterVitals(vitals);
                        Events.ReturnFromInteractiveBattle(checked((int)active.Frame));
                        battle = null;
                        session.SetBattle(null);
                        break;
                    default:
                        throw new NotSupportedException($"Battle input {input.Kind} is unsupported.");
                }
                advanced = checked((int)(active.Frame - previous));
                if (battle is not null)
                {
                    session.SetBattle(active.Capture());
                }
            }
        }
        PlayerTrigger after = Events.NextTrigger;
        session.RecordReplayEntry(new ReplayJournalEntry(ReplayEntryKind.BattleInput, session.State.Scene, session.State.FieldId, fieldFrame) { BattleInput = input, Advanced = advanced, Trigger = (int)before.Kind, ResultTrigger = (int)after.Kind, ResultFrame = Events.Frame, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ObjectId = request?.ObjectId, ScriptId = request?.ScriptId, Operation = request?.Operation });
        return advanced;
    }
}
