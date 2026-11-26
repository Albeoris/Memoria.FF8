using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedHostedGuardianFormTests
{
    private const string Caster = "party-3";
    private const int Ifrit = 2;

    [Fact]
    public void Fourth_caster_enters_and_uses_attack_and_innate_fire_on_the_attached_process()
    {
        Run((world, battle, process, actor, owner) =>
        {
            int begin = battle.Events.Count;
            int modelCount = process.Graphics.ActorModels.Roster.Count;
            Enter(world, battle, process, actor, owner, queueAnotherAlly: true);
            Assert.DoesNotContain(battle.Events.Skip(begin), entry => entry.Kind == "EffectContact" && entry.ActorId == Caster);
            Assert.Equal((byte)77, process.Graphics.ActorModels.Get(owner).ModelId4);
            Assert.True(battle.Combatants.Count(IsLivingEnemy) >= 7);
            Assert.Equal(modelCount, process.Graphics.ActorModels.Roster.Count);
            Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(Caster)!.Phase);

            int fireStock = actor.MagicStocks.FirstOrDefault(stock => stock.Id == 1)?.Count ?? 0;
            RunFormAction(world, battle, process, actor, owner, BattleGuardianFormAction.Attack);
            RunFormAction(world, battle, process, actor, owner, BattleGuardianFormAction.Fire);
            Assert.Equal(fireStock, actor.MagicStocks.FirstOrDefault(stock => stock.Id == 1)?.Count ?? 0);
            Assert.Equal((byte)77, process.Graphics.ActorModels.Get(owner).ModelId4);
            Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(Caster)!.Phase);
        }, additionalEnemies: 6);
    }

    [Fact]
    public void Debug_victory_preserves_admitted_innate_fire_on_the_attached_process()
    {
        Run((world, battle, process, actor, owner) =>
        {
            Enter(world, battle, process, actor, owner);
            Ready(world, battle, actor);
            BattleCombatant target = LivingEnemy(battle);
            int begin = battle.Events.Count;
            Assert.True(battle.CanAdmitDebugOutcome);
            world.ApplyBattleInput(new(BattleInputKind.GuardianFormAction, ActorId: Caster, TargetId: target.Id, ActionId: (int)BattleGuardianFormAction.Fire));
            for (int frame = 0; frame < 600 && process.ActiveActionToken is null; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }

            long token = Assert.IsType<long>(process.ActiveActionToken);
            BattleActionPresentation action = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
            Assert.Equal(BattleGuardianFormAction.Fire, action.GuardianFormAction);
            Assert.Throws<InvalidOperationException>(battle.InstantVictory);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
            Assert.Equal(token, process.ActiveActionToken);
            Assert.Same(action, battle.ActiveAction);
            Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(Caster)!.Phase);

            for (int frame = 0; frame < 600 && process.ActiveActionToken is not null; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.Contains(battle.Events.Skip(begin), entry => entry is { Kind: "EffectContact", ActorId: Caster } && entry.TargetId == target.Id);
        });
    }

    [Fact]
    public void Fourth_caster_finishes_and_restores_the_human_model_on_the_same_process()
    {
        Run((world, battle, process, actor, owner) =>
        {
            byte? humanModel = process.Graphics.ActorModels.Get(owner).ModelId4;
            Enter(world, battle, process, actor, owner);
            Assert.True(battle.Combatants.Count(IsLivingEnemy) >= 7);
            Ready(world, battle, actor);
            int humanHp = actor.HumanHitPoints;
            BattleCombatant target = LivingEnemy(battle);
            int begin = battle.Events.Count;
            world.ApplyBattleInput(new(BattleInputKind.GuardianFormAction, ActorId: Caster, TargetId: target.Id, ActionId: (int)BattleGuardianFormAction.HellFire));
            bool skipped = false;
            bool sawCinematic = false;
            for (int frame = 0; frame < 1600 && (battle.GetGuardianForm(Caster) is not null || battle.ActiveAction is not null || process.ActiveActionToken is not null); frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                Assert.Same(process, battle.Presentation);
                sawCinematic |= process.Graphics.OriginalEffect is { Binding.Effect: 201, ReturnedDescriptor: > 0 };
                if (!skipped && battle.CanSkipGuardianFormFinisher)
                {
                    _ = battle.PressNativeGuardianBoost();
                    skipped = battle.SkipGuardianFormFinisher();
                }
            }
            Assert.True(sawCinematic);
            Assert.True(skipped);
            Assert.Null(battle.GetGuardianForm(Caster));
            Assert.Null(process.Graphics.GuardianFormPresentation);
            Assert.Equal(humanModel, process.Graphics.ActorModels.Get(owner).ModelId4);
            Assert.Equal(humanHp, actor.CurrentHp);
            Assert.Single(battle.Events.Skip(begin), entry => entry is { Kind: "GuardianFormReturned", ActorId: Caster });
            Assert.Single(battle.Events.Skip(begin), entry => entry is { Kind: "ActionComplete", ActorId: Caster });
            BattleEvent[] contacts = battle.Events.Skip(begin).Where(entry => entry is { Kind: "EffectContact", ActorId: Caster }).ToArray();
            Assert.Contains(contacts, entry => entry.TargetId == target.Id);
            Assert.All(contacts.GroupBy(entry => entry.TargetId), group => Assert.Single(group));
            if (battle.Outcome == BattleOutcome.Active && battle.Combatants.Any(IsLivingEnemy))
            {
                RunHumanAttack(world, battle, process, actor, owner, humanModel);
            }
        }, additionalEnemies: 6);
    }

    [Fact]
    public void Prepared_form_entry_leaves_both_owners_unchanged_until_canonical_publication()
    {
        Run((world, battle, process, actor, owner) =>
        {
            var source = new BattlePresentationProcess.HostedGuardianFormVisualOwner(Assert.IsType<BattlePartyActorModel>(owner), Caster, 1, Ifrit);
            var change = new BattlePresentationProcess.HostedGuardianFormVisualChange(source, Entering: true, new(source, BattleGuardianFormPhase.Entering, HasAction: false, Finishing: false));
            NativeBattleGraphicsState graphics = process.Graphics;
            NativeBattleEffectMemory memory = process.Memory;
            NativeBattleCameraState camera = process.Camera;
            NativeBattleStageState stage = process.Stage;
            int atb = actor.Atb;
            int events = battle.Events.Count;
            ulong randomCalls = process.GlobalRandomCalls;
            Assert.Throws<InvalidOperationException>(() => process.PrepareGuardianFormChange(change with { Owner = source with { ActorId = "party-0" } }));
            BattlePresentationProcess.PreparedHostedGuardianFormChange prepared = process.PrepareGuardianFormChange(change);
            process.ValidatePreparedGuardianFormChange(prepared);
            Assert.Equal(owner, prepared.Replacement.NativeGraphics!.GuardianFormPresentation!.Actor);
            Assert.Same(graphics, process.Graphics);
            Assert.Same(memory, process.Memory);
            Assert.Same(camera, process.Camera);
            Assert.Same(stage, process.Stage);
            Assert.Null(battle.GetGuardianForm(Caster));
            Assert.Equal(atb, actor.Atb);
            Assert.Equal(events, battle.Events.Count);
            Assert.Equal(randomCalls, process.GlobalRandomCalls);

            world.ApplyBattleInput(new(BattleInputKind.Tick));
            NativeBattleGraphicsState advanced = process.Graphics;
            Assert.Throws<InvalidOperationException>(() => process.ValidatePreparedGuardianFormChange(prepared));
            Assert.Same(advanced, process.Graphics);
            Assert.Null(process.Graphics.GuardianFormPresentation);
            Assert.Null(battle.GetGuardianForm(Caster));
        });
    }

    [Fact]
    public void Attached_enemy_ai_hit_depletes_the_form_and_restores_its_human_model()
    {
        Run((world, battle, process, actor, owner) =>
        {
            byte? humanModel = process.Graphics.ActorModels.Get(owner).ModelId4;
            Enter(world, battle, process, actor, owner);
            int humanHp = actor.HumanHitPoints;
            actor.CurrentHp = 1;
            var (enemy, attackSlot) = battle.Combatants.Where(candidate => !candidate.IsPlayer && candidate.IsActive && candidate.NativeAi is not null)
                .SelectMany(candidate => Enumerable.Range(0, 16)
                    .Where(slot => candidate.NativeAi!.Abilities[16 * candidate.NativeAi.LevelBand + slot] is { Command: 8, AttackType: 1, Power: > 0, Element: 0, Statuses0: 0, Statuses1: 0 })
                    .Select(slot => (candidate, slot))).First();
            // Isolate target selection while still executing the enemy's original installed ability and sequence.
            byte[][] scripts = enemy.NativeAi!.Scripts;
            byte[] originalTurn = scripts[1];
            scripts[1] = [4, checked((byte)actor.ModelId), 12, checked((byte)attackSlot), 0];
            NativeBattleAiAbility ability = enemy.NativeAi.Abilities[16 * enemy.NativeAi.LevelBand + attackSlot];
            BattleNativeAction native = battle.ReadDefinition().NativeActions.Single(action => action.Command == ability.Command && action.ActionId == ability.Id);
            BattleDiagnosticEnemyAbility selection = new(enemy.ModelId, Assert.IsType<int>(enemy.SceneSlot), enemy.NativeAi.LevelBand, attackSlot, ability.Command, ability.Id, ability.Sequence, TargetSelector: null) { UseTurnScript = true };
            enemy.Atb = BattleSession.AtbMaximum;
            battle.SubmitDiagnosticEnemyAbility(enemy.Id, selection, native.MagicId);
            BattleSnapshot boundary = battle.Capture();
            BattleSession continuation = BattleSession.Restore(boundary);
            Assert.Equal(BattleGuardianFormPhase.Active, continuation.GetGuardianForm(Caster)!.Phase);
            Assert.Contains(continuation.QueuedActions, command => command.ExpandedAiCallback is { Stage: 1 } && command.ActorId == enemy.Id);

            int begin = battle.Events.Count;
            for (int frame = 0; frame < 600 && battle.GetGuardianForm(Caster) is not null; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.Null(battle.GetGuardianForm(Caster));
            Assert.Null(process.Graphics.GuardianFormPresentation);
            Assert.Equal(humanModel, process.Graphics.ActorModels.Get(owner).ModelId4);
            Assert.Equal(humanHp, actor.CurrentHp);
            Assert.Single(battle.Events.Skip(begin), entry => entry is { Kind: "GuardianFormReturned", ActorId: Caster });
            Assert.NotNull(battle.ActiveAction?.ExpandedAiCallback);

            for (int frame = 0; frame < 600 && battle.ActiveAction is not null; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.Null(battle.ActiveAction);
            scripts[1] = originalTurn;
            FieldInfo forcesField = typeof(BattleSession).GetField("guardianForces", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var forces = Assert.IsType<Dictionary<int, BattleGuardianForceVital>>(forcesField.GetValue(battle));
            forces[Ifrit] = forces[Ifrit] with { HitPoints = forces[Ifrit].MaximumHitPoints };
            Enter(world, battle, process, actor, owner);
        });
    }

    [Fact]
    public void TurnBasedExpirySchedulesFinisherWhileTheFormActorIsReady()
    {
        Run((world, battle, process, actor, owner) =>
        {
            Enter(world, battle, process, actor, owner);
            Ready(world, battle, actor);
            Assert.Equal(BattleAtbMode.TurnBased, battle.Mode);
            FieldInfo field = typeof(BattleSession).GetField("guardianForms", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var forms = Assert.IsType<Dictionary<string, BattleGuardianFormState>>(field.GetValue(battle));
            forms[Caster] = forms[Caster] with { RemainingTicks = 0 };
            Assert.False(battle.CanChoosePlayerCommand(Caster));
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Equal(BattleGuardianFormPhase.Finishing, battle.GetGuardianForm(Caster)?.Phase);
            Assert.True(battle.GetGuardianForm(Caster)!.FinisherAdmitted);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Attached_form_returns_after_actor_ko_or_silenced_expiry(bool knockout)
    {
        Run((world, battle, process, actor, owner) =>
        {
            byte? humanModel = process.Graphics.ActorModels.Get(owner).ModelId4;
            Enter(world, battle, process, actor, owner);
            int humanHp = actor.HumanHitPoints;
            int begin = battle.Events.Count;
            if (knockout)
            {
                actor.NativeStatus0 |= 1;
            }
            else
            {
                // Seed only the remaining lifetime, then let the real battle clock expire it.
                FieldInfo formsField = typeof(BattleSession).GetField("guardianForms", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var forms = Assert.IsType<Dictionary<string, BattleGuardianFormState>>(formsField.GetValue(battle));
                forms[Caster] = forms[Caster] with { RemainingTicks = 1 };
                actor.NativeStatus0 |= 0x10;
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                Assert.Equal(0, battle.GetGuardianForm(Caster)?.RemainingTicks);
            }
            world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
            for (int frame = 0; frame < 32 && battle.GetGuardianForm(Caster) is not null; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.Same(process, battle.Presentation);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
            Assert.Null(battle.GetGuardianForm(Caster));
            Assert.Null(process.Graphics.GuardianFormPresentation);
            Assert.Equal(humanModel, process.Graphics.ActorModels.Get(owner).ModelId4);
            Assert.Equal(knockout ? 0 : humanHp, actor.CurrentHp);
            Assert.Equal(knockout, !actor.IsAlive);
            Assert.Single(battle.Events.Skip(begin), entry => entry is { Kind: "GuardianFormReturned", ActorId: Caster });
            Assert.DoesNotContain(battle.Events.Skip(begin), entry => entry is { Kind: "EffectContact", ActorId: Caster });
        });
    }

    private static void Run(Action<GameWorld, BattleSession, BattlePresentationProcess, BattleCombatant, BattleModelOwner> verify, int additionalEnemies = 4)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        GameWorld world = new(session, directory, sharedAssets: game);
        try
        {
            ExpandedBattleInstalledEntry.Prepare(world, session, current => ExpandedBattleGuardianTests.PrepareParty(current, learnedBoost: true, guardianId: Ifrit), additionalEnemies: additionalEnemies);
            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { GuardianFormsEnabled = true });
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: ExpandedBattleInstalledEntry.Seed));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
            ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
            BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
            BattleCombatant actor = Assert.Single(battle.Combatants, actor => actor.Id == Caster);
            BattleModelOwner owner = battle.ReadPresentationModelOwner(Caster);
            Assert.Equal(4, battle.Combatants.Count(value => value.IsPlayer));
            Assert.False(process.Graphics.NativeBindings.TryEncodeExactActor(owner, process.Graphics.ActorModels, out _));
            verify(world, battle, process, actor, owner);
        }
        catch (Exception failure)
        {
            try
            {
                world.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(failure, cleanup);
            }
            throw;
        }
        world.Dispose();
    }

    private static void Enter(GameWorld world, BattleSession battle, BattlePresentationProcess process, BattleCombatant actor, BattleModelOwner owner, bool queueAnotherAlly = false)
    {
        Ready(world, battle, actor);
        int begin = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.GuardianFormActivate, ActorId: Caster, TargetId: Caster, ActionId: Ifrit));
        Assert.Single(battle.GuardianForceCasts);
        bool sawEntering = false;
        bool queued = false;
        for (int frame = 0; frame < 1600 && !(sawEntering && EntryCompleted()); frame++)
        {
            if (battle.GetGuardianForm(Caster)?.Phase == BattleGuardianFormPhase.Charging)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
            if (battle.GetGuardianForm(Caster)?.Phase == BattleGuardianFormPhase.Entering)
            {
                sawEntering = true;
                Assert.True(actor.IsBusy);
                Assert.Null(process.ActiveActionToken);
                Assert.Null(battle.ActiveAction);
                Assert.Equal(owner, process.Graphics.GuardianFormPresentation!.Actor);
                if (queueAnotherAlly && !queued)
                {
                    BattleCombatant other = battle.Combatants.First(value => value.IsPlayer && value.Id != Caster && !value.IsBusy && value.IsAlive);
                    other.Atb = BattleSession.AtbMaximum;
                    world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: other.Id, TargetId: battle.Combatants.First(IsLivingEnemy).Id));
                    Assert.Contains(battle.QueuedActions, command => command.ActorId == other.Id);
                    Assert.Equal(0, battle.AdvanceTurn());
                    queued = true;
                }
            }
        }
        Assert.True(sawEntering, $"Ifrit did not enter by frame {battle.Frame}. Form={battle.GetGuardianForm(Caster)?.Phase}, charge={battle.GuardianForceCasts.FirstOrDefault()?.RemainingTicks}, events={string.Join(", ", battle.Events.Skip(begin).TakeLast(8).Select(entry => $"{entry.Kind}/{entry.ActorId}/{entry.Message}"))}.");
        Assert.Equal(queueAnotherAlly, queued);
        Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(Caster)!.Phase);
        Assert.Equal(3, process.Graphics.GuardianFormPresentation!.Phase);
        Assert.Single(battle.Events.Skip(begin), entry => entry is { Kind: "GuardianFormActive", ActorId: Caster });
        Assert.True(EntryCompleted());

        bool EntryCompleted()
        {
            return battle.Events.Skip(begin).SkipWhile(entry => entry is not { Kind: "GuardianFormActive", ActorId: Caster })
                .Any(entry => entry is { Kind: "ActionComplete", ActorId: Caster });
        }
    }

    private static void RunFormAction(GameWorld world, BattleSession battle, BattlePresentationProcess process, BattleCombatant actor, BattleModelOwner owner, BattleGuardianFormAction action)
    {
        Ready(world, battle, actor);
        BattleCombatant target = LivingEnemy(battle);
        int begin = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.GuardianFormAction, ActorId: Caster, TargetId: target.Id, ActionId: (int)action));
        for (int frame = 0; frame < 600 && !battle.Events.Skip(begin).Any(entry => entry is { Kind: "ActionComplete", ActorId: Caster }); frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
            Assert.Equal((byte)77, process.Graphics.ActorModels.Get(owner).ModelId4);
        }
        Assert.Single(battle.Events.Skip(begin), entry => entry is { Kind: "ActionComplete", ActorId: Caster });
        Assert.Contains(battle.Events.Skip(begin), entry => entry is { Kind: "EffectContact", ActorId: Caster } && entry.TargetId == target.Id);
    }

    private static BattleCombatant LivingEnemy(BattleSession battle)
    {
        return battle.Combatants.Last(IsLivingEnemy);
    }

    private static bool IsLivingEnemy(BattleCombatant actor)
    {
        return !actor.IsPlayer && actor.IsAlive && actor.IsActive && !actor.Untargetable;
    }

    private static void RunHumanAttack(GameWorld world, BattleSession battle, BattlePresentationProcess process, BattleCombatant actor, BattleModelOwner owner, byte? humanModel)
    {
        Ready(world, battle, actor);
        BattleCombatant target = LivingEnemy(battle);
        int begin = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: Caster, TargetId: target.Id));
        for (int frame = 0; frame < 600 && !battle.Events.Skip(begin).Any(entry => entry is { Kind: "ActionComplete", ActorId: Caster }); frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.Same(process, battle.Presentation);
        Assert.Equal(humanModel, process.Graphics.ActorModels.Get(owner).ModelId4);
        Assert.Contains(battle.Events.Skip(begin), entry => entry is { Kind: "EffectContact", ActorId: Caster } && entry.TargetId == target.Id);
        Assert.Single(battle.Events.Skip(begin), entry => entry is { Kind: "ActionComplete", ActorId: Caster });
    }

    private static void Ready(GameWorld world, BattleSession battle, BattleCombatant actor)
    {
        for (int turn = 0; turn < 32 && !battle.CanChoosePlayerCommand(actor.Id); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 24));
        }
        Assert.True(battle.CanChoosePlayerCommand(actor.Id), $"Could not ready {actor.Id} at frame {battle.Frame}.");
    }
}
