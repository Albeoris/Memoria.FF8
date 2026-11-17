using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleReturnTests
{
    private const ushort Formation = 87;
    private const string Caster = "party-3";

    [Fact]
    public void Installed_expanded_commands_return_once_save_the_field_and_enter_the_next_battle()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState bootstrap = ExpandedBattleEffectMatrixTests.PrepareField(directory);
        GameSession session = new(bootstrap with { FieldId = 132, FieldName = "field_132", GlobalRandom = new(917) });
        session.RequestField(new FieldTransition(133, -105, -1751, 22, 128));
        session.SetDepartingFieldState(bootstrap.DepartingFieldState);
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        VerifyReturn(world, session, assets, directory);
    }

    [Fact]
    public void Silenced_hosted_fire_releases_the_reservation_and_completes_without_spending_stock()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory) with { GlobalRandom = new(917) });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 4)).Accepted);
        BattleActionDefinition fire = Assert.Single(world.BattleResources.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Name == "Fire");
        PrepareParty(session, fire.Id);
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 4, AdditionalEnemies = 4 });
        BattleSession battle = Enter(world, session);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == Caster);
        BattleCombatant target = battle.Combatants.Last(actor => !actor.IsPlayer);
        int initialHp = target.CurrentHp;
        caster.Atb = BattleSession.AtbMaximum;
        int eventStart = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: Caster, TargetId: target.Id, Ability: BattleAbility.Magic, ActionId: fire.Id));
        string reservation = Assert.Single(battle.Capture().MagicVitals!.MagicSystem.Reservations).ActionId;
        for (int callback = 0; callback < 1200 && battle.ActiveAction is not { ActorId: Caster, Phase: BattleActionPhase.Windup }; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(battle.ActiveAction is { ActorId: Caster, Phase: BattleActionPhase.Windup });
        caster.NativeStatus0 |= 0x10;

        bool observedRecovery = false;
        for (int callback = 0; callback < 1200 && battle.ActiveAction is not null; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (!observedRecovery && battle.ActiveAction is { Phase: BattleActionPhase.Recovery, MagicReservationId: null })
            {
                BattleSnapshot snapshot = battle.Capture();
                Assert.Empty(snapshot.MagicVitals!.MagicSystem.Reservations);
                Assert.DoesNotContain(reservation, snapshot.MagicVitals.MagicSystem.CommittedActionIds);
                Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot));
                observedRecovery = true;
            }
        }
        Assert.True(observedRecovery);
        Assert.Null(battle.ActiveAction);
        Assert.Same(process, battle.Presentation);
        Assert.Equal(5, battle.GetAvailableMagicCasts(Caster, fire.Id));
        Assert.Equal(initialHp, target.CurrentHp);
        Assert.DoesNotContain(battle.Events.Skip(eventStart), item => item.Kind == "EffectContact" && item.ActorId == Caster);
        Assert.Single(battle.Events.Skip(eventStart), item => item.Kind == "ActionComplete" && item.ActorId == Caster);
    }

    private static void VerifyReturn(GameWorld world, GameSession session, GameAssets assets, string directory)
    {
        world.EnsureField();
        RequireCavernPlayerControl(world);
        BattleActionDefinition fire = Assert.Single(world.BattleResources.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Name == "Fire");
        PrepareParty(session, fire.Id);
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 4, AdditionalEnemies = 4 });
        int initialResults = session.State.AppliedBattleResultIds.Length;
        uint initialVictories = session.State.BattleStatistics!.Victories;
        int[] fieldParty = world.Events.Scene.Party.ToArray();
        int[] fieldBindings = world.Events.Scene.FieldPartyNativeActorIds.ToArray();
        Assert.Equal(new[] { 0, 255, 255 }, fieldParty);

        BattleSession battle = Enter(world, session);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleEncounterOccurrence occurrence = Assert.IsType<BattleEncounterOccurrence>(session.State.PendingBattleEncounter);
        Assert.Same(process, battle.Presentation);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        BattleCombatant[] activeEnemies = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive).ToArray();
        Assert.Equal(5, activeEnemies.Length);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == Caster);
        Assert.False(battle.ReadBattlePresentationPacketBindings().Aliases.TryEncodeExact(battle.ReadPresentationModelOwner(Caster), out _));
        BattleCombatant target = activeEnemies[^1];
        Assert.False(battle.ReadBattlePresentationPacketBindings().Aliases.TryEncodeExact(battle.ReadPresentationModelOwner(target.Id), out _));
        string attackTarget = activeEnemies[0].Id;
        NativeItemStock[] initialItems = battle.Capture().NativeItemInventory!;
        Assert.Equal(5, battle.GetAvailableMagicCasts(Caster, fire.Id));

        caster.Atb = BattleSession.AtbMaximum;
        int attackStart = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: Caster, TargetId: attackTarget));
        var (attack, _) = CompleteCommand(world, battle, process, attackStart, attackTarget, BattleAbility.Attack);
        Assert.Equal(5, battle.GetAvailableMagicCasts(Caster, fire.Id));
        Assert.Equal(initialItems, battle.Capture().NativeItemInventory);

        // Leave only the Bomb standing so the final Attack decides victory.
        foreach (BattleCombatant other in activeEnemies.Where(actor => actor != target))
        {
            other.CurrentHp = 0;
            other.NativeStatus0 |= 1;
        }
        target.CurrentHp = 1;
        caster.Atb = BattleSession.AtbMaximum;
        int fireStart = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: Caster, TargetId: target.Id, Ability: BattleAbility.Magic, ActionId: fire.Id));
        var (cast, fireContact) = CompleteCommand(world, battle, process, fireStart, target.Id, BattleAbility.Magic);
        Assert.True(attack.Frame < cast.Frame);
        Assert.Equal(4, battle.GetAvailableMagicCasts(Caster, fire.Id));
        Assert.Equal(initialItems, battle.Capture().NativeItemInventory);
        Assert.Equal(43, target.ModelId);
        Assert.Equal(1, fireContact.HitFlags & 1);
        BattleEvent healing = Assert.Single(battle.Events.Skip(fireStart), item => item.Kind == "Healing" && item.TargetId == target.Id);
        Assert.True(healing.Amount > 0);
        Assert.Equal(healing.Amount, fireContact.PresentedAmount);
        Assert.Equal(1 + healing.Amount, target.CurrentHp);

        target.CurrentHp = 1;
        caster.Atb = BattleSession.AtbMaximum;
        int finishStart = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: Caster, TargetId: target.Id));
        var (finisher, finishContact) = CompleteCommand(world, battle, process, finishStart, target.Id, BattleAbility.Attack);
        Assert.True(cast.Frame < finisher.Frame);
        Assert.Equal(0, finishContact.HitFlags & 1);
        Assert.True(finishContact.PresentedAmount > 0);
        Assert.Equal(0, target.CurrentHp);
        Assert.Equal(4, battle.GetAvailableMagicCasts(Caster, fire.Id));
        Assert.Contains(battle.EnemyDefeats, defeat => defeat.Slot == target.SceneSlot);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.False(battle.CanReturnToField);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleInput(new(BattleInputKind.Return)));

        for (int callback = 0; callback < 500 && battle.Outcome == BattleOutcome.Active; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, world.Battle!.Presentation);
            Assert.Equal(initialResults, session.State.AppliedBattleResultIds.Length);
        }
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal((byte)0, process.Graphics.NativeOutcome?.FadeDurationB0C);
        Assert.True(process.TerminalFinished);
        Assert.NotNull(process.PreparedPresentation);
        Assert.Same(process.PreparedPresentation, battle.PreparedPresentation);
        Assert.Null(battle.DebugOutcome);
        Assert.Single(battle.Events, item => item.Kind == "Outcome");

                Assert.Equal(process.NativeGlobalRandomChange, battle.NativeGlobalRandomChange);
        Assert.Same(process.NativeMusicProcess, battle.NativeMusicProcess);
        Assert.Same(process.NativeGlobalSoundProcess, battle.NativeGlobalSoundProcess);
        Assert.Same(process.NativeVibrationProcess, battle.NativeVibrationProcess);
        NativeExamTimerState finalTimer = Assert.IsType<NativeExamTimerState>(battle.NativeExamTimer);
        Assert.True(finalTimer.HasSameClock(Assert.IsType<NativeExamTimerState>(process.Timer)));
        Assert.True(finalTimer.HasSameClock(session.ExamTimer!));
        GameState completed = session.PrepareSnapshot();
        NativeBattleRetainedMemory retained = Assert.IsType<NativeBattleRetainedMemory>(completed.NativeBattleMemory);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.NativeMusicProcess), JsonSerializer.SerializeToElement(retained.MusicProcess)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.NativeVibrationProcess), JsonSerializer.SerializeToElement(retained.VibrationProcess)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.NativeGlobalSoundProcess), JsonSerializer.SerializeToElement(completed.GlobalSoundProcess)));
        AssertRetainedModelCells(process.Graphics, retained);

        world.ApplyBattleInput(new(BattleInputKind.Return));
        Assert.Null(world.Battle);
        Assert.Null(world.Events.LastFault);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Null(session.State.RandomEncounters!.Pending);
        Assert.Equal(initialResults + 1, session.State.AppliedBattleResultIds.Length);
        Assert.Equal(occurrence.OccurrenceId, session.State.AppliedBattleResultIds[^1]);
        Assert.Equal(initialVictories + 1, session.State.BattleStatistics!.Victories);
        BattleRewardSummary rewards = Assert.IsType<BattleRewardSummary>(world.LastBattleRewards);
        Assert.Contains(rewards.CharacterExperience, reward => reward.Value > 0);
        NativeBattleFieldReturnReceipt receipt = Assert.IsType<NativeBattleFieldReturnReceipt>(world.Events.Scene.NativeBattleFieldReturn);
        Assert.Equal((byte)4, receipt.Reload.NativeResult);
        Assert.Equal(133, receipt.Reload.FieldId);
        Assert.Equal(battle.NativeHostCallbacks, world.Events.Scene.NativeRandomFieldBattle!.Callbacks);
        Assert.Equal(battle.NativeGlobalRandomChange!.After, session.State.GlobalRandom);
        Assert.True(session.ExamTimer!.ClockCalls > finalTimer.ClockCalls);
        AssertRetainedModelCells(process.Graphics, session.State.NativeBattleMemory!);
        AssertFieldParty(world, session, fieldParty, fieldBindings);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleInput(new(BattleInputKind.Return)));
        Assert.Same(rewards, world.LastBattleRewards);
        Assert.Same(receipt, world.Events.Scene.NativeBattleFieldReturn);
        Assert.Equal(1, session.State.AppliedBattleResultIds.Count(id => id == occurrence.OccurrenceId));

        RequireCavernPlayerControl(world);
        string path = Path.Combine(Path.GetTempPath(), $"Memoria.FF8-expanded-return-{Guid.NewGuid():N}.sav");
        try
        {
            world.WriteGameplaySave(path);
            GameState returned = GameplaySaveSessionLoader.Load(path, directory, sharedAssets: assets);
            Assert.Equal(SceneKind.Field, returned.Scene);
            Assert.Equal(133, returned.FieldId);
            Assert.Null(returned.Battle);
            Assert.Equal(session.State.AppliedBattleResultIds, returned.AppliedBattleResultIds);
            Assert.Equal(session.State.BattleStatistics, returned.BattleStatistics);
            Assert.Equal(session.State.CrtRandom, returned.CrtRandom);
            Assert.Equal(session.State.GlobalRandom, returned.GlobalRandom);
            Assert.True(session.ExamTimer!.HasSameClock(returned.ExamTimer!));
            Assert.Equal(4, returned.CharacterVitals!.Records[3].MagicInventory.Single(stock => stock.SpellId == fire.Id).Count);

            GameSession loaded = new(returned);
            using GameWorld resumed = new(loaded, directory, sharedAssets: assets);
            resumed.EnsureField();
            AssertFieldParty(resumed, loaded, fieldParty, fieldBindings);
            BattleSession next = Enter(resumed, loaded);
            Assert.NotSame(process, next.Presentation);
            Assert.Equal(4, next.Combatants.Count(actor => actor.IsPlayer));
            Assert.Equal(5, next.Combatants.Count(actor => !actor.IsPlayer && actor.IsActive));
            Assert.Equal(4, next.GetAvailableMagicCasts(Caster, fire.Id));
            Assert.NotEqual(occurrence.OccurrenceId, loaded.State.PendingBattleEncounter!.OccurrenceId);
            Assert.Equal(initialResults + 1, loaded.State.AppliedBattleResultIds.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void RequireCavernPlayerControl(GameWorld world)
    {
        for (int callback = 0; callback < 60 && !world.Events.CanAcceptMovementInput && world.Events.LastFault is null; callback++)
        {
            if (!world.Apply(new(PlayerActionKind.Wait)).Accepted)
            {
                break;
            }
        }
        Assert.Equal(133, world.Events.FieldId);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(NativeFieldInitializationStage.Completed, world.Events.Scene.NativeFieldInitialization?.Stage);
        Assert.True(world.Events.CanAcceptMovementInput, $"Cavern frame {world.Events.Frame} did not return player control. Next trigger {world.Events.NextTrigger}.");
    }

    private static void PrepareParty(GameSession session, int spell)
    {
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithGuestBattleRecords().Rest();
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        NativeCharacterVital caster = vitals.Records[3];
        int magic = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2);
        Assert.True(magic >= 0);
        NativeMagicStock[] stocks = caster.MagicInventory.ToArray();
        int slot = Array.FindIndex(stocks, stock => stock.SpellId == spell);
        if (slot < 0)
        {
            slot = Array.FindIndex(stocks, stock => stock.SpellId == 0);
        }
        Assert.True(slot >= 0);
        stocks[slot] = new(checked((byte)spell), 5);
        vitals.Records[3] = caster with { MagicInventory = stocks, Junction = caster.Junction with { Commands = (uint)(20 + magic) } };
        session.SetCharacterVitals(vitals.WithBattleParty([0, 1, 2, 3], 4));
    }

    private static BattleSession Enter(GameWorld world, GameSession session)
    {
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        NativeRandomEncounterState random = session.State.RandomEncounters?.Native ?? NativeRandomEncounterState.FromColdProcess();
        session.SetRandomEncounters(new(true, random, new(SceneKind.Field, 133, world.Events.Frame, Formation, source)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        for (int callback = 0; callback < 1200 && !process.EntryReady; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(process.EntryReady);
        Assert.Same(process, battle.Presentation);
        return battle;
    }

    private static (BattleEvent Event, NativeBattleContactHalf Contact) CompleteCommand(GameWorld world, BattleSession battle, BattlePresentationProcess process, int eventStart, string target, BattleAbility ability)
    {
        HashSet<long> tokens = [];
        List<NativeBattleContactHalf> contacts = [];
        BattleModelOwner recipient = battle.ReadPresentationModelOwner(target);
        for (int callback = 0; callback < 1500 && !battle.Events.Skip(eventStart).Any(item => item.Kind == "ActionComplete" && item.ActorId == Caster); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
            if (process.LastStepActionToken is long token)
            {
                tokens.Add(token);
                contacts.AddRange(process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Select(receipt => receipt.Packet.Primary).Where(half => half.Recipient == recipient));
            }
        }
        Assert.True(process.IsActionComplete(Assert.Single(tokens)));
        Assert.Single(battle.Events.Skip(eventStart), item => item.Kind == "ActionComplete" && item.ActorId == Caster);
        BattleEvent contact = Assert.Single(battle.Events.Skip(eventStart), item => item.Kind == "EffectContact" && item.ActorId == Caster);
        Assert.Equal(target, contact.TargetId);
        Assert.Equal(ability, contact.Ability);
        return (contact, Assert.Single(contacts));
    }

    private static void AssertRetainedModelCells(NativeBattleGraphicsState graphics, NativeBattleRetainedMemory retained)
    {
        var cells = graphics.NativeBindings.CaptureOriginalCells(graphics.ActorModels);
        Assert.Equal(7, cells.Length);
        Assert.Equal(cells.Select(NativeBattleModelRetainedFields.Capture), retained.ModelTransformRows!.Value);
        Assert.Equal(cells.Select(model => model.Color40), retained.ModelColors40!.Value);
        for (int index = 0; index < cells.Length; index++)
        {
            Assert.Equal(cells[index].StatusTimers.ToArray(), retained.ModelStatusTimers!.Value[index].ToArray());
        }
    }

    private static void AssertFieldParty(GameWorld world, GameSession session, int[] fieldParty, int[] fieldBindings)
    {
        Assert.Equal(fieldParty, world.Events.Scene.Party);
        Assert.Equal(fieldBindings, world.Events.Scene.FieldPartyNativeActorIds);
        Assert.Equal(new byte[] { 0, 1, 2 }, session.State.CharacterVitals!.CombatParty);
        Assert.Equal(new byte[] { 3 }, session.State.CharacterVitals.AdditionalCombatParty);
    }
}
