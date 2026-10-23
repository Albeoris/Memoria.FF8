using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleMiniPulseCannon286FixturePreparation
{
    internal static (BattleSnapshot Snapshot, int CasterActorIndex) Prepare(GameAssets game, BattleAssets assets, BattleSnapshot baseline, string output)
    {
        Assert.Equal(431, baseline.Definition.EncounterId);
        BattleEncounter formation = assets.LoadEncounter(431);
        Assert.Equal(126, formation.StageId);
        BattleCombatantDefinition companion = Assert.Single(baseline.Definition.Combatants, actor => !actor.IsPlayer);
        Assert.Equal(109, companion.ModelId);
        Assert.Equal(1, companion.SceneSlot);
        BattleEnemyPlacement placement = Assert.Single(formation.Slots, slot => slot.Slot == 0);
        Assert.Equal(110, placement.ModelId);
        Assert.Equal(new NativeBattleVector(-2000, 0, -4000), placement.NativePosition);
        BattleCombatantDefinition[] prototypes = new[] { 0, 2 }.Select(slot =>
        {
            BattleEnemyPlacement original = Assert.Single(formation.Slots, row => row.Slot == slot);
            Assert.Equal(110, original.ModelId);
            Assert.Equal(156, original.Level);
            Assert.False(original.Enabled);
            Assert.True(original.Hidden);
            Assert.True(original.Untargetable);
            Assert.True(original.InitiallySuspended);
            Assert.DoesNotContain(baseline.Definition.Combatants, actor => !actor.IsPlayer && actor.SceneSlot == slot);
            return new BattleCombatantDefinition
            {
                Id = $"mini-pulse-prototype-{slot}",
                Name = NativeBattleEffectFixtureExporter.ComponentEnemyName(assets.ReadEnemyName(original.ModelId), original.ModelId),
                ModelId = original.ModelId,
                SceneSlot = original.Slot,
                InitialActive = original.Enabled,
                InitialHidden = original.Hidden,
                InitialUntargetable = original.Untargetable,
                InitialSuspended = original.InitiallySuspended,
                NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(original.ModelId),
                NativeAi = assets.ReadEnemyAi(original.ModelId, 1),
                NativeTemplate = assets.ReadEnemyTemplate(original.ModelId, checked((byte)original.Level))
            };
        }).ToArray();
        BattleCombatantDefinition[] actors = [.. baseline.Definition.Combatants, .. prototypes];
        BattleDefinition definition = baseline.Definition with { Combatants = actors, NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation), NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeClockRules = assets.ReadNativeClockRules(), NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleSnapshot fresh = session.Capture();
        NativeBattleGraphicsState freshGraphics = fresh.NativeGraphics ?? throw new InvalidDataException("The original allocation requires its native graphics owner.");
        session = BattleSession.Restore(fresh with { NativeGraphics = freshGraphics with { NativeOutcome = new NativeBattleOutcomeState() } });
        Directory.CreateDirectory(output);
        Save("allocation-before-entry", session.Capture());
        int entryCallbacks = 0;
        while (!session.NativeEntryReady && entryCallbacks < 1200)
        {
            session.Tick();
            entryCallbacks++;
        }
        Assert.True(session.NativeEntryReady, "The original component did not finish native entry within its diagnostic budget.");
        BattleCombatant caller = Assert.Single(session.Combatants, actor => actor.ModelId == 109 && actor.IsActive);
        BattleSnapshot before = session.Capture();
        foreach (BattleCombatantDefinition prototype in prototypes)
        {
            NativeBattleParticipantSnapshot dormant = Assert.Single(before.Combatants, actor => actor.Id == prototype.Id).NativeParticipant ?? throw new InvalidDataException("An original dormant prototype lacks its native owner.");
            Assert.False(dormant.Active);
            Assert.True(dormant.Hidden && dormant.Untargetable && dormant.Suspended);
        }
        Save("allocation-before-native-call", before);
        BattleCombatant allocated = Invoke<BattleCombatant>(session, "AllocateNativeEnemy", [caller, placement.Slot, 0, false]) ?? throw new InvalidDataException("The native allocation producer did not return its actor.");
        BattleSnapshot pending = session.Capture();
        NativeBattleEnemyAllocation allocation = Assert.Single(pending.NativeAllocations);
        Assert.Equal(0, allocation.SceneSlot);
        Assert.Equal(allocated.Id, allocation.Id);
        NativeBattleParticipantSnapshot pendingActor = Assert.Single(pending.Combatants, actor => actor.Id == allocated.Id).NativeParticipant!;
        int actualIndex = pendingActor.ActorIndex;
        Assert.InRange(actualIndex, 3, 6);
        Assert.Equal(actualIndex, pending.NativeLastAllocatedIndex);
        Assert.True(pendingActor.AllocationPendingStatistics);
        NativeBattleDescriptorState descriptors = pending.NativeGraphics!.Descriptors;
        Assert.Contains(descriptors.Queue, slot => descriptors.Packets[slot].Command == 102);
        Assert.Contains(descriptors.Queue, slot => descriptors.Packets[slot].Command == 113 && descriptors.Packets[slot].Argument == 0x48AD10);
        Assert.DoesNotContain(descriptors.Queue, slot => descriptors.Packets[slot].Command == 113 && descriptors.Packets[slot].Argument == 0x487670);
        Save("allocation-pending-52", pending);
        int allocationCallbacks = Pump(session, snapshot =>
        {
            NativeBattleParticipantSnapshot actor = Assert.Single(snapshot.Combatants, row => row.Id == allocated.Id).NativeParticipant!;
            return !actor.AllocationPendingStatistics && snapshot.NativeScriptState.AllocationControl is { ActionGate: false, PauseGate: false } && snapshot.NativeGraphics!.Descriptors.Queue.IsEmpty && snapshot.NativeGraphics.ActorIds[actualIndex] == allocated.Id && snapshot.NativeGraphics.Models[actualIndex].ActiveResourceRow100 is not null;
        });
        BattleSnapshot loaded = session.Capture();
        NativeBattleParticipantSnapshot unactivated = Assert.Single(loaded.Combatants, actor => actor.Id == allocated.Id).NativeParticipant!;
        Assert.True(unactivated.Active);
        Assert.True(unactivated.Hidden && unactivated.Untargetable && unactivated.Suspended);
        Assert.False(unactivated.AllocationPendingStatistics);
        Assert.DoesNotContain(session.Events, entry => entry.Kind == "Activated" && entry.ActorId == allocated.Id);
        Assert.Equal(actualIndex, loaded.NativeLastAllocatedIndex);
        Assert.Equal(allocated.Id, loaded.NativeGraphics!.ActorIds[actualIndex]);
        Assert.NotNull(loaded.NativeGraphics.Models[actualIndex].ActiveResourceRow100);
        Save("allocation-ready-52-false", loaded);
        BattleSnapshot coldLoaded = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "allocation-ready-52-false.battle.json"))).Capture();
        Assert.Equal(JsonSerializer.Serialize(loaded), JsonSerializer.Serialize(coldLoaded));
        BattleSnapshot ready = Activate(session, "activation", out int activationCallbacks);
        BattleSnapshot replay = Activate(BattleSession.Restore(coldLoaded), "cold-activation", out int coldActivationCallbacks);
        Assert.Equal(activationCallbacks, coldActivationCallbacks);
        Assert.Equal(JsonSerializer.Serialize(ready), JsonSerializer.Serialize(replay));
        NativeBattleParticipantSnapshot active = Assert.Single(ready.Combatants, actor => actor.Id == allocated.Id).NativeParticipant!;
        Assert.True(active.Active);
        Assert.False(active.Hidden || active.Untargetable || active.Suspended || active.AllocationPendingStatistics);
        Assert.Equal(actualIndex, active.ActorIndex);
        Assert.Contains(session.Events, entry => entry.Kind == "NativeSpawn" && entry.TargetId == allocated.Id);
        Assert.Contains(session.Events, entry => entry.Kind == "Activated" && entry.ActorId == allocated.Id);
        BattleCombatantDefinition currentDefinition = session.GetCombatantDefinition(allocated.Id);
        Assert.Equal(110, currentDefinition.ModelId);
        Assert.Equal(0, currentDefinition.SceneSlot);
        Assert.True(currentDefinition.InitialHidden && currentDefinition.InitialUntargetable && currentDefinition.InitialSuspended);
        NativeBattleGraphicsState graphics = ready.NativeGraphics!;
        NativeBattleGraphicsModel model = graphics.Models[actualIndex];
        Assert.Equal((byte)126, model.ModelId4);
        Assert.Equal(placement.NativePosition, model.Transform!.Position28);
        Assert.NotNull(model.GeometryWorldMatrix);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(currentDefinition).PoseSource ?? throw new InvalidDataException("The actual allocation requires its original skeleton.");
        ImmutableArray<NativeBattleActionMatrix> current = NativeBattleBodyBoneMatrices.Require(model, pose);
        Assert.True(current.Length > 2);
        NativeBattlePackedMatrix32 selected = NativeBattlePackedMatrix32.FromMatrix(current[2], null);
        Assert.False(selected.Known[18]);
        Assert.False(selected.Known[19]);
        Assert.All(selected.Known.Where((_, index) => index is not (18 or 19)), known => Assert.True(known));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(pose.Skeleton.AsSpan())), model.CurrentBodyBoneMatrices!.SkeletonSha256);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(110, currentDefinition.Level), Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 293 && action.MagicId == 286)));
        NativeBattleParticipantSnapshot retainedCaller = Assert.Single(ready.Combatants, actor => actor.Id == caller.Id).NativeParticipant!;
        Assert.True(retainedCaller.Active);
        Assert.Equal(new NativeBattleVector(0, 0, -4600), graphics.Models[retainedCaller.ActorIndex].Transform!.Position28);
        BattleCombatantDefinition otherPrototype = prototypes.Single(actor => actor.SceneSlot == 2);
        NativeBattleParticipantSnapshot dormantOther = Assert.Single(ready.Combatants, actor => actor.Id == otherPrototype.Id).NativeParticipant!;
        Assert.False(dormantOther.Active);
        Assert.True(dormantOther.Hidden && dormantOther.Untargetable && dormantOther.Suspended);
        Save("allocation-ready", ready);
        Assert.Equal(JsonSerializer.Serialize(ready), JsonSerializer.Serialize(BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "allocation-ready.battle.json"))).Capture()));
        File.WriteAllText(Path.Combine(output, "allocation-events.json"), JsonSerializer.Serialize(session.Events));
        File.WriteAllText(Path.Combine(output, "allocation-receipt.json"), JsonSerializer.Serialize(new { Scenario = "Controlled original scene0 allocation52(false), actual load/statistics readiness, then actual53 native activation; no ordinary AI turn, action290 visual or Mini Pulse Cannon effect execution", FormationId = formation.Id, formation.StageId, OriginalPrototypes = formation.Slots.Where(slot => slot.Slot is 0 or 2).ToArray(), AllocationId = allocation.Id, allocation.SceneSlot, CasterActorIndex = actualIndex, EntryCallbacks = entryCallbacks, AllocationCallbacks = allocationCallbacks, ActivationCallbacks = activationCallbacks, ColdActivationCallbacks = coldActivationCallbacks, BoneCount = pose.BoneCount, SelectedBone = 2, CurrentBone2 = selected, CurrentWorld = model.GeometryWorldMatrix, SkeletonSha256 = model.CurrentBodyBoneMatrices!.SkeletonSha256 }));
        return (ready, actualIndex);

        BattleSnapshot Activate(BattleSession owner, string name, out int callbacks)
        {
            BattleSnapshot source = owner.Capture();
            Assert.Equal(actualIndex, source.NativeLastAllocatedIndex);
            Invoke<object?>(owner, "QueueNativeActivation", [source.NativeLastAllocatedIndex]);
            BattleSnapshot queued = owner.Capture();
            NativeBattleDescriptorState queue = queued.NativeGraphics!.Descriptors;
            Assert.Contains(queue.Queue, slot => queue.Packets[slot].Command == 113 && queue.Packets[slot].Argument == 0x487670);
            Save(name + "-pending-53", queued);
            callbacks = Pump(owner, snapshot =>
            {
                NativeBattleParticipantSnapshot actor = Assert.Single(snapshot.Combatants, row => row.Id == allocated.Id).NativeParticipant!;
                return actor.Active && !actor.Hidden && !actor.Untargetable && !actor.Suspended && !actor.AllocationPendingStatistics && snapshot.NativeGraphics!.Descriptors.Queue.IsEmpty && snapshot.NativeGraphics.Models[actualIndex].GeometryWorldMatrix is not null && snapshot.NativeGraphics.Models[actualIndex].CurrentBodyBoneMatrices is not null;
            });
            BattleSnapshot result = owner.Capture();
            Save(name + "-ready-53", result);
            return result;
        }

        void Save(string name, BattleSnapshot snapshot) => File.WriteAllText(Path.Combine(output, name + ".battle.json"), JsonSerializer.Serialize(snapshot));
    }

    private static int Pump(BattleSession session, Func<BattleSnapshot, bool> ready)
    {
        for (int count = 1; count <= 1200; count++)
        {
            session.Tick();
            if (ready(session.Capture()))
            {
                return count;
            }
        }
        throw new InvalidDataException("The actual native descriptor phase exceeded its diagnostic callback budget.");
    }

    private static T Invoke<T>(BattleSession session, string name, object?[] arguments)
    {
        MethodInfo method = typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException($"The current native producer {name} is unavailable.");
        try
        {
            object? result = method.Invoke(session, arguments);
            return result is null ? default! : (T)result;
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }
}
