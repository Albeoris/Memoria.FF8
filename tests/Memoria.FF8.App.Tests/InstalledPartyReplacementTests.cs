using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledPartyReplacementTests
{
    [Fact]
    public void Authored_Eject_in_formation_511_binds_and_reveals_the_replacement()
    {
        RunReplacement(authoredEject: true);
    }

    [Fact]
    public void Pending_replacement_can_fork_before_the_bound_model_is_safe_to_release()
    {
        RunReplacement(authoredEject: false);
    }

    private static void RunReplacement(bool authoredEject)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        ExpandedBattleInstalledEntry.Prepare(world, session, PrepareParty, formation: 511, allies: 4, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: ExpandedBattleInstalledEntry.Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        if (authoredEject)
        {
            battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
        }
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer && actor.IsActive));
        BattleCombatant outgoing = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.MagicRecordId == 0);
        NativeBattleModelSlot originalSlot = NativeBattleModelSlot.Decode(0);
        BattleModelOwner oldOwner = new BattlePartyActorModel(new(outgoing.Id));
        Assert.Equal(oldOwner, process.FindOriginalActor(originalSlot));
        Assert.False(outgoing.IsAlive);

        if (authoredEject)
        {
            ReachInstalledEject(world, battle, outgoing);
        }
        else
        {
            // Enter at the installed status consumer's boundary, with the actual registered scene models.
            MethodInfo eject = typeof(BattleSession).GetMethod("EjectPartyOccupant", BindingFlags.Instance | BindingFlags.NonPublic)!;
            eject.Invoke(battle, [outgoing]);
        }
        Assert.Equal(new(BattleRosterSlot.Ally(0), 0, 4, 1), Assert.Single(battle.PartyOccupancyChanges));
        Assert.False(outgoing.IsActive);
        BattleSession fork = battle.ForkForContinuation();
        BattlePresentationProcess forkedProcess = Assert.IsType<BattlePresentationProcess>(fork.Presentation);
        List<uint> callbacks = [];
        List<uint> restoredCallbacks = [];
        uint targetCallback = authoredEject ? 0x497270u : 0x4971F0u;
        for (int callback = 0; callback < 1200 && !callbacks.Contains(targetCallback); callback++)
        {
            battle.Tick();
            fork.Tick();
            Observe(process, callbacks);
            Observe(forkedProcess, restoredCallbacks);
        }
        if (!authoredEject)
        {
            Assert.Equal(new uint[] { 0x4971F0 }, callbacks);
            Assert.Equal(callbacks, restoredCallbacks);
            Assert.Equal(oldOwner, process.FindOriginalActor(originalSlot));
            Assert.Contains(process.Graphics.Descriptors.Queue, slot => process.Graphics.Descriptors.Packets[slot] is { Command: 10, Argument: 0x497270 });
            return;
        }
        Assert.True(callbacks.SequenceEqual([0x4971F0u, 0x497270u]), $"Replacement callbacks [{string.Join(",", callbacks.Select(callback => callback.ToString("X")))}] at graphics callback {process.Graphics.Callback}; descriptors [{Describe(process)}]; files {process.NativeFileIo?.ActiveCount}; model load pending {process.HasPendingModelLoad}.");
        Assert.Equal(callbacks, restoredCallbacks);
        BattleCombatant incoming = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.MagicRecordId == 4);
        BattleModelOwner newOwner = new BattlePartyActorModel(new(incoming.Id));
        Assert.Equal(BattleRosterSlot.Ally(0), incoming.Slot);
        Assert.Equal(newOwner, process.FindOriginalActor(originalSlot));
        Assert.Equal(newOwner, forkedProcess.FindOriginalActor(originalSlot));
        Assert.Equal(incoming.Atb, Assert.Single(fork.Combatants, actor => actor.Id == incoming.Id).Atb);
        Assert.Equal((byte)4, battle.MagicVitals!.CombatParty[0]);
        Assert.DoesNotContain(process.Graphics.ActorModels.Owners, owner => owner == oldOwner);

        static void Observe(BattlePresentationProcess process, List<uint> reached)
        {
            if (process.LastFrameServices is { } frame)
            {
                reached.AddRange(frame.ReadOrderedReceipts().OfType<NativeBattlePartyReplacementReached>().Select(receipt => receipt.Descriptor.Callback));
            }
        }
    }

    private static void ReachInstalledEject(GameWorld world, BattleSession battle, BattleCombatant target)
    {
        BattleAssets assets = world.BattleResources;
        BattleCombatant emitter = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive && HasInstalledEjectTurn(actor));
        NativeBattleAiDefinition ai = Assert.IsType<NativeBattleAiDefinition>(emitter.NativeAi);
        BattleDiagnosticEnemyAbility turn = new(emitter.ModelId, emitter.SceneSlot!.Value, ai.LevelBand, -1, 8, 336, 0, null) { UseTurnScript = true };
        List<BattleDiagnosticEffectDispatch> ejections = [];
        battle.DiagnosticEffectDispatched += record =>
        {
            if (record.NativeCommand == 8 && record.ActionId == 336)
            {
                ejections.Add(record);
            }
        };
        BattlePresentationProcess process = battle.Presentation!;
        for (int attempt = 0; attempt < 9 && battle.PartyOccupancyChanges.IsEmpty; attempt++)
        {
            for (int poll = 0; poll < 200 && (battle.QueuedActions.Count != 0 || process.HasActiveAction || process.PendingCompletion is not null); poll++)
            {
                battle.Tick();
            }
            emitter.Atb = BattleSession.AtbMaximum;
            Assert.True(battle.CanSubmitDiagnosticEnemyAbility(emitter.Id, turn), $"Installed Eject actor {emitter.Id}/{emitter.ModelId} has gauge {emitter.Atb}, flags {emitter.NativeCombatantFlags:X}, saved {emitter.NativeStatus0:X}, battle {emitter.NativeStatus1:X}, busy {emitter.IsBusy}, native actor {emitter.NativeActorIndex}; queued {battle.QueuedActions.Count}.");
            battle.SubmitDiagnosticEnemyAbility(emitter.Id, turn, 83);
            for (int poll = 0; poll < 600 && battle.PartyOccupancyChanges.IsEmpty; poll++)
            {
                battle.Tick();
                if (battle.QueuedActions.Count == 0 && !process.HasActiveAction && process.PendingCompletion is null)
                {
                    break;
                }
            }
        }
        Assert.Single(ejections, record => record.RequestedEffect == 83 && record.ExecutedEffect == 83);
        Assert.Contains(battle.Events, record => record.Kind == "Ejected" && record.TargetId == target.Id);

        bool HasInstalledEjectTurn(BattleCombatant actor)
        {
            if (actor.NativeAi is not { } program)
            {
                return false;
            }
            var effects = new HashSet<ushort>();
            NativeBattleExpandedAi.Evaluate(program with { Scripts = [[0], program.Scripts[1], [0], [0], [0]] }, assets.NativeActions, 511, reachedEffects: effects);
            return effects.Contains(83);
        }
    }

    private static string Describe(BattlePresentationProcess process)
    {
        NativeBattleDescriptorState queue = process.Graphics.Descriptors;
        return string.Join(",", queue.Queue.Select(slot =>
        {
            NativeBattleDescriptorPacket packet = queue.Packets[slot];
            return $"{slot}:{packet.Command}/{packet.Argument:X}/{packet.PriorityState:X2}/{packet.Serial}";
        }));
    }

    private static void PrepareParty(GameSession session)
    {
        NativeCharacterVitals vitals = session.State.CharacterVitals!.WithGuestBattleRecords().Rest();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        for (int row = 0; row < NativeCharacterVitals.NativeRecordCount; row++)
        {
            records[row] = records[row] with { MemberFlags = (ushort)((records[row].MemberFlags & ~1) | (row <= 4 ? 1 : 0)) };
        }
        records[0] = records[0] with { Status = 1, HitPoints = new(0, 0) };
        session.SetCharacterVitals((vitals with { Records = records }).WithBattleParty([0, 1, 2, 3], 4));
    }
}
