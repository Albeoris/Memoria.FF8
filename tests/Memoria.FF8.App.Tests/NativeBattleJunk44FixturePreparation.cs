using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleJunk44FixturePreparation
{
    internal static (BattleSnapshot Snapshot, int CasterActorIndex) Prepare(GameAssets game, BattleAssets assets, BattleSnapshot baseline, string output)
    {
        Assert.Equal(236, baseline.Definition.EncounterId);
        BattleEncounter formation = assets.LoadEncounter(236);
        Assert.Equal(76, formation.StageId);
        BattleEnemyPlacement placement = Assert.Single(formation.Slots, slot => slot.Slot == 0);
        Assert.Equal(83, placement.ModelId);
        Assert.False(placement.Enabled);
        Assert.True(placement.Hidden);
        Assert.True(placement.Untargetable);
        Assert.True(placement.InitiallySuspended);
        Assert.Equal(254, placement.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -2900), placement.NativePosition);
        Assert.DoesNotContain(baseline.Definition.Combatants, actor => !actor.IsPlayer && actor.SceneSlot == placement.Slot);
        BattleCombatantDefinition prototype = new()
        {
            Id = "junk-prototype-0",
            Name = NativeBattleEffectFixtureExporter.ComponentEnemyName(assets.ReadEnemyName(placement.ModelId), placement.ModelId),
            ModelId = placement.ModelId,
            SceneSlot = placement.Slot,
            InitialActive = placement.Enabled,
            InitialHidden = placement.Hidden,
            InitialUntargetable = placement.Untargetable,
            InitialSuspended = placement.InitiallySuspended,
            NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(placement.ModelId),
            NativeAi = assets.ReadEnemyAi(placement.ModelId, 1),
            NativeTemplate = assets.ReadEnemyTemplate(placement.ModelId, checked((byte)placement.Level))
        };
        BattleCombatantDefinition[] actors = [.. baseline.Definition.Combatants, prototype];
        BattleDefinition definition = baseline.Definition with { Combatants = actors, NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation), NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeClockRules = assets.ReadNativeClockRules(), NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleSnapshot fresh = session.Capture();
        NativeBattleGraphicsState freshGraphics = fresh.NativeGraphics ?? throw new InvalidDataException("The original allocation component requires its fresh graphics owner.");
        session = BattleSession.Restore(fresh with { NativeGraphics = freshGraphics with { NativeOutcome = new NativeBattleOutcomeState() } });
        Directory.CreateDirectory(output);
        Save("allocation-before-entry", session.Capture());
        int entryCallbacks = 0;
        while (!session.NativeEntryReady && entryCallbacks < 1200)
        {
            session.Tick();
            entryCallbacks++;
        }
        Assert.True(session.NativeEntryReady, "The original allocation component did not finish native entry within its diagnostic budget.");
        BattleCombatant caller = Assert.Single(session.Combatants, actor => actor.ModelId == 56 && actor.IsActive);
        BattleSnapshot before = session.Capture();
        BattleCombatantSnapshot dormant = Assert.Single(before.Combatants, actor => actor.Id == prototype.Id);
        NativeBattleParticipantSnapshot dormantParticipant = dormant.NativeParticipant ?? throw new InvalidDataException("The original inactive prototype lacks its participant owner.");
        Assert.False(dormantParticipant.Active);
        Assert.True(dormantParticipant.Hidden);
        Assert.True(dormantParticipant.Untargetable);
        Assert.True(dormantParticipant.Suspended);
        Save("allocation-before-native-call", before);

        MethodInfo allocate = typeof(BattleSession).GetMethod("AllocateNativeEnemy", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException("The current native enemy allocation producer is unavailable.");
        BattleCombatant allocated;
        try
        {
            allocated = (BattleCombatant)(allocate.Invoke(session, [caller, placement.Slot, 0, true]) ?? throw new InvalidDataException("The native allocation producer did not return its actor."));
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
        BattleSnapshot pending = session.Capture();
        NativeBattleEnemyAllocation allocation = Assert.Single(pending.NativeAllocations);
        Assert.Equal(placement.Slot, allocation.SceneSlot);
        Assert.Equal(allocated.Id, allocation.Id);
        BattleCombatantSnapshot pendingActor = Assert.Single(pending.Combatants, actor => actor.Id == allocated.Id);
        NativeBattleParticipantSnapshot pendingParticipant = pendingActor.NativeParticipant ?? throw new InvalidDataException("The pending original allocation lacks its participant owner.");
        int actualIndex = pendingParticipant.ActorIndex;
        Assert.InRange(actualIndex, 3, 6);
        Assert.True(pendingParticipant.AllocationPendingStatistics);
        Assert.True(pending.NativeScriptState.AllocationControl!.ActionGate);
        NativeBattleGraphicsState pendingGraphics = pending.NativeGraphics ?? throw new InvalidDataException("The pending original allocation lacks its graphics owner.");
        Assert.Contains(pendingGraphics.Descriptors.Queue, slot => pendingGraphics.Descriptors.Packets[slot].Command == 102);
        Save("allocation-pending", pending);

        BattleSnapshot ready = pending;
        int allocationCallbacks = 0;
        while (allocationCallbacks < 1200)
        {
            session.Tick();
            allocationCallbacks++;
            ready = session.Capture();
            BattleCombatantSnapshot actor = Assert.Single(ready.Combatants, candidate => candidate.Id == allocated.Id);
            NativeBattleParticipantSnapshot participant = actor.NativeParticipant ?? throw new InvalidDataException("The actual allocation lost its native participant.");
            if (participant.Active && !participant.AllocationPendingStatistics && ready.NativeScriptState.AllocationControl is { ActionGate: false, PauseGate: false } && ready.NativeGraphics!.Descriptors.Queue.IsEmpty)
            {
                break;
            }
        }
        BattleCombatantSnapshot active = Assert.Single(ready.Combatants, actor => actor.Id == allocated.Id);
        NativeBattleParticipantSnapshot activeParticipant = active.NativeParticipant ?? throw new InvalidDataException("The activated original actor lacks its native participant.");
        Assert.True(allocationCallbacks < 1200, "The actual102/113 allocation and activation descriptors did not finish within the diagnostic budget.");
        Assert.True(activeParticipant.Active);
        Assert.False(activeParticipant.Hidden);
        Assert.False(activeParticipant.Untargetable);
        Assert.False(activeParticipant.Suspended);
        Assert.False(activeParticipant.AllocationPendingStatistics);
        Assert.Equal(actualIndex, activeParticipant.ActorIndex);
        Assert.Contains(session.Events, entry => entry.Kind == "NativeSpawn" && entry.TargetId == allocated.Id);
        Assert.Contains(session.Events, entry => entry.Kind == "Activated" && entry.ActorId == allocated.Id);
        BattleCombatantDefinition currentDefinition = session.GetCombatantDefinition(allocated.Id);
        Assert.Equal(83, currentDefinition.ModelId);
        Assert.Equal(0, currentDefinition.SceneSlot);
        Assert.Equal(prototype.InitialHidden, currentDefinition.InitialHidden);
        Assert.Equal(prototype.InitialUntargetable, currentDefinition.InitialUntargetable);
        Assert.Equal(prototype.InitialSuspended, currentDefinition.InitialSuspended);
        NativeBattleGraphicsState graphics = ready.NativeGraphics ?? throw new InvalidDataException("The activated original actor lost its graphics owner.");
        Assert.Equal(allocated.Id, graphics.ActorIds[actualIndex]);
        NativeBattleGraphicsModel model = graphics.Models[actualIndex];
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(currentDefinition);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The activated original model lacks its skeleton.");
        Assert.True(pose.BoneCount > 23);
        Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
        Assert.NotNull(model.Transform);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.ActiveResourceRow100);
        Assert.Equal(placement.NativePosition, model.Transform!.Position28);
        Assert.Equal((byte)15, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(83, currentDefinition.Level), Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 226 && action.MagicId == 44)));
        BattleSnapshot restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(ready))!).Capture();
        Assert.Equal(JsonSerializer.Serialize(ready), JsonSerializer.Serialize(restored));
        Save("allocation-ready", ready);
        File.WriteAllText(Path.Combine(output, "allocation-receipt.json"), JsonSerializer.Serialize(new { Scenario = "Controlled original scene-prototype allocation and actual102/113 activation; no ordinary twenty-defeat admission claim", FormationId = formation.Id, formation.StageId, Prototype = placement, AllocationId = allocation.Id, allocation.SceneSlot, CasterActorIndex = actualIndex, EntryCallbacks = entryCallbacks, AllocationCallbacks = allocationCallbacks, ResolvedLevel = activeParticipant.ResolvedLevel, PoseBones = pose.BoneCount, Events = session.Events.Where(entry => entry.Kind is "NativeSpawn" or "Activated").ToArray() }));
        return (ready, actualIndex);

        void Save(string name, BattleSnapshot snapshot) => File.WriteAllText(Path.Combine(output, name + ".battle.json"), JsonSerializer.Serialize(snapshot));
    }
}
