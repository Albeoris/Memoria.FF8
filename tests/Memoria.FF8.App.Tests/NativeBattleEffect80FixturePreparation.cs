using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleEffect80FixturePreparation
{
    internal static (BattleSnapshot Snapshot, int CasterActorIndex, int TargetActorIndex) Prepare(GameAssets game, BattleAssets assets, BattleSnapshot baseline, string output)
    {
        Assert.Equal(511, baseline.Definition.EncounterId);
        NativeBattleGraphicsState baselineGraphics = baseline.NativeGraphics ?? throw new InvalidDataException("The original source requires its current graphics owner.");
        Assert.True(baselineGraphics.EntryReady);
        Assert.Null(baselineGraphics.OriginalEffect);
        Assert.Empty(baseline.QueuedActions);
        Assert.Null(baseline.ActiveAction);
        Assert.Empty(baseline.NativeAllocations);
        BattleEncounter formation = assets.LoadEncounter(511);
        Assert.Equal(142, formation.StageId);
        BattleCombatantDefinition[] originalEnemies = baseline.Definition.Combatants.Where(actor => !actor.IsPlayer).ToArray();
        Assert.Equal(new[] { 121, 123 }, originalEnemies.Select(actor => actor.ModelId).Order().ToArray());
        Assert.All(originalEnemies, actor => Assert.Null(actor.NativeAi));
        BattleCombatantDefinition casterDefinition = Assert.Single(originalEnemies, actor => actor.ModelId == 123);
        Assert.Equal(1, casterDefinition.SceneSlot);
        NativeBattleParticipantSnapshot originalCaster = Assert.Single(baseline.Combatants, actor => actor.Id == casterDefinition.Id).NativeParticipant!;
        Assert.Equal(4, originalCaster.ActorIndex);
        Assert.True(originalCaster.Active);
        Assert.True(originalCaster.Hidden);
        Assert.True(originalCaster.Suspended);
        Assert.True(originalCaster.Untargetable);
        BattleEnemyPlacement placement = Assert.Single(formation.Slots, slot => slot.Slot == 3);
        Assert.Equal(124, placement.ModelId);
        Assert.False(placement.Enabled);
        Assert.True(placement.Hidden);
        Assert.False(placement.InitiallySuspended);
        Assert.False(placement.Untargetable);
        Assert.Equal(251, placement.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -5300), placement.NativePosition);
        Assert.DoesNotContain(baseline.Definition.Combatants, actor => !actor.IsPlayer && actor.SceneSlot == 3);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(124, 1);
        Assert.Equal(new byte[] { 14, 224, 0, 0 }, ai.Scripts[0].Take(4).ToArray());
        BattleCombatantDefinition prototype = new()
        {
            Id = "effect80-prototype-3",
            Name = NativeBattleEffectFixtureExporter.ComponentEnemyName(assets.ReadEnemyName(124), 124),
            ModelId = placement.ModelId,
            SceneSlot = placement.Slot,
            InitialActive = placement.Enabled,
            InitialHidden = placement.Hidden,
            InitialUntargetable = placement.Untargetable,
            InitialSuspended = placement.InitiallySuspended,
            NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(124),
            NativeAi = ai,
            NativeTemplate = assets.ReadEnemyTemplate(124, checked((byte)placement.Level))
        };
        BattleCombatantDefinition[] actors = [.. baseline.Definition.Combatants, prototype];
        NativeBattleGraphicsDefinition originalGraphics = baseline.Definition.NativeGraphics!;
        NativeBattleGraphicsDefinition expandedGraphics = assets.ReadNativeGraphicsDefinition(actors, originalGraphics.Rules, formation);
        NativeBattleGraphicsBinding prototypeBinding = Assert.Single(expandedGraphics.Bindings, binding => binding.ActorId == prototype.Id);
        foreach (NativeBattleGraphicsBinding binding in originalGraphics.Bindings)
        {
            Assert.Equal(JsonSerializer.Serialize(binding), JsonSerializer.Serialize(Assert.Single(expandedGraphics.Bindings, candidate => candidate.ActorId == binding.ActorId)));
        }
        BattleDefinition definition = baseline.Definition with
        {
            Combatants = actors,
            NativeGraphics = originalGraphics with { Bindings = originalGraphics.Bindings.Add(prototypeBinding) },
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte
        };
        Directory.CreateDirectory(output);
        Save("source-reference-input", baseline);
        BattleSession session = new(definition, baseline.Mode);
        BattleSnapshot cold = session.Capture();
        Assert.Equal(0, cold.Frame);
        Assert.NotNull(cold.NativeClock);
        Assert.NotNull(cold.Definition.NativeClockRules);
        Assert.NotNull(cold.Definition.NativeBattleSpeed);
        Assert.NotNull(cold.NativeAiVariables);
        Assert.Equal(8, cold.NativeAiVariables!.Length);
        Assert.All(cold.NativeAiVariables, value => Assert.Equal(0u, value));
        NativeBattleGraphicsState coldGraphics = cold.NativeGraphics ?? throw new InvalidDataException("The fresh original fixture requires its actual graphics owner.");
        session = BattleSession.Restore(cold with { NativeGraphics = coldGraphics with { NativeOutcome = new NativeBattleOutcomeState() } });
        Save("source-before-entry", session.Capture());
        int entryCallbacks = 0;
        while (!session.NativeEntryReady && entryCallbacks < 1200)
        {
            session.Tick();
            entryCallbacks++;
        }
        Assert.True(session.NativeEntryReady, "The fresh original fixture did not complete its native entry within the diagnostic budget.");
        BattleSnapshot source = session.Capture();
        Assert.NotNull(source.NativeClock);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        Assert.Empty(source.NativeAllocations);
        Assert.Empty(source.QueuedActions);
        Assert.Null(source.ActiveAction);
        foreach (BattleCombatantDefinition originalActor in originalEnemies)
        {
            BattleCombatantSnapshot actor = Assert.Single(source.Combatants, candidate => candidate.Id == originalActor.Id);
            NativeBattleParticipantSnapshot participant = actor.NativeParticipant!;
            BattleEnemyPlacement originalPlacement = Assert.Single(formation.Slots, candidate => candidate.Slot == originalActor.SceneSlot);
            Assert.True(participant.Active);
            Assert.Equal(originalPlacement.Hidden, participant.Hidden);
            Assert.Equal(originalPlacement.InitiallySuspended, participant.Suspended);
            Assert.Equal(originalPlacement.Untargetable, participant.Untargetable);
            Assert.Equal(originalPlacement.NativePosition, source.NativeGraphics.Models[participant.ActorIndex].Transform!.Position28);
            Assert.Null(originalActor.NativeAi);
        }
        NativeBattleParticipantSnapshot freshCaster = Assert.Single(source.Combatants, actor => actor.Id == casterDefinition.Id).NativeParticipant!;
        Assert.Equal(4, freshCaster.ActorIndex);
        NativeBattleParticipantSnapshot inert = Assert.Single(source.Combatants, actor => actor.Id == prototype.Id).NativeParticipant!;
        Assert.False(inert.Active);
        Assert.True(inert.Hidden);
        Assert.Equal(placement.InitiallySuspended, inert.Suspended);
        Assert.Equal(placement.Untargetable, inert.Untargetable);
        Save("source-after-entry", source);
        File.WriteAllText(Path.Combine(output, "fresh-source-origin.json"), JsonSerializer.Serialize(new { Scenario = "Independent fresh original formation511 fixture with native clock rules and battle speed present before construction; no mutable state copied from the reference snapshot", FormationId = 511, StageId = 142, SourceFrame = source.Frame, NativeCallback = source.NativeGraphics.Callback, EntryCallbacks = entryCallbacks, ClockRules = definition.NativeClockRules, BattleSpeed = definition.NativeBattleSpeed, ColdAiBank = cold.NativeAiVariables, Prototype = prototype, Binding = prototypeBinding, ExistingResourcesVerified = originalGraphics.Bindings.Select(binding => binding.ActorId).ToArray() }));
        BattleCombatantDefinition removedDefinition = Assert.Single(originalEnemies, actor => actor.ModelId == 121);
        BattleCombatantSnapshot removedBefore = Assert.Single(source.Combatants, actor => actor.Id == removedDefinition.Id);
        int removedIndex = removedBefore.NativeParticipant!.ActorIndex;
        int removedResource = source.NativeGraphics!.Models[removedIndex].TextureResourceIndex ?? throw new InvalidDataException("The original participant requires its loaded resource row before removal.");
        NativeBattleModelTextureCacheRow removedTexture = source.NativeModelTextureCache!.Rows[removedResource];
        bool removalObserved = false;
        BattleCombatant removedActor = Assert.Single(session.Combatants, actor => actor.Id == removedDefinition.Id);
        Invoke(session, "QueueNativeGraphicsRemoval", removedActor);
        Invoke(session, "QueueNativeGraphicsBarrier");
        BattleCombatant caller = Assert.Single(session.Combatants, actor => actor.Id == casterDefinition.Id);
        BattleCombatant allocated = (BattleCombatant)Invoke(session, "AllocateNativeEnemy", caller, 3, 0, true)!;
        BattleSnapshot pending = session.Capture();
        NativeBattleEnemyAllocation allocation = Assert.Single(pending.NativeAllocations);
        Assert.Equal(3, allocation.SceneSlot);
        Assert.Equal(allocated.Id, allocation.Id);
        int actualTarget = Assert.Single(pending.Combatants, actor => actor.Id == allocated.Id).NativeParticipant!.ActorIndex;
        Assert.InRange(actualTarget, 3, 6);
        Assert.NotEqual(4, actualTarget);
        Assert.True(Assert.Single(pending.Combatants, actor => actor.Id == allocated.Id).NativeParticipant!.AllocationPendingStatistics);
        AssertDescriptor(pending, 105, null);
        AssertDescriptor(pending, 112, null);
        AssertDescriptor(pending, 102, null);
        AssertDescriptor(pending, 113, 0x48AD10);
        AssertDescriptor(pending, 113, 0x487670);
        SaveNext("allocation-pending");
        int allocationCallbacks = Drain();
        Assert.True(removalObserved, "The actual original participant texture release must precede the new resource load.");
        BattleSnapshot allocatedReady = session.Capture();
        AssertReadyActor(allocatedReady, allocated.Id, actualTarget);
        NativeBattleParticipantSnapshot retainedCaster = Assert.Single(allocatedReady.Combatants, actor => actor.Id == casterDefinition.Id).NativeParticipant!;
        Assert.True(retainedCaster.Hidden);
        Assert.True(retainedCaster.Suspended);
        SaveNext("target-allocation-ready");
        Invoke(session, "QueueNativeActivation", 4);
        AssertDescriptor(session.Capture(), 113, 0x487670);
        SaveNext("caster-activation-pending");
        int activationCallbacks = Drain();
        BattleSnapshot ready = session.Capture();
        AssertReadyActor(ready, casterDefinition.Id, 4);
        AssertReadyActor(ready, allocated.Id, actualTarget);
        BattleCombatantDefinition targetDefinition = session.GetCombatantDefinition(allocated.Id);
        Assert.Equal(124, targetDefinition.ModelId);
        Assert.Equal(3, targetDefinition.SceneSlot);
        Assert.Equal(prototype.InitialHidden, targetDefinition.InitialHidden);
        Assert.Equal(prototype.InitialSuspended, targetDefinition.InitialSuspended);
        Assert.Equal(prototype.InitialUntargetable, targetDefinition.InitialUntargetable);
        int casterBones = AssertCurrentPose(ready, casterDefinition, 4, 2);
        int targetBones = AssertCurrentPose(ready, targetDefinition, actualTarget, 71);
        Assert.Equal(placement.NativePosition, ready.NativeGraphics!.Models[actualTarget].Transform!.Position28);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(123, casterDefinition.Level), Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 352 && action.MagicId == 80)));
        NativeBattleGraphicsResource targetResource = assets.ReadNativeGraphicsResource(targetDefinition);
        Assert.True(targetResource.Contains(26));
        BattleCombatantDefinition retainedDefinition = Assert.Single(originalEnemies, actor => actor.ModelId == 121);
        BattleCombatantSnapshot retained = Assert.Single(ready.Combatants, actor => actor.Id == retainedDefinition.Id);
        BattleCombatantSnapshot original = Assert.Single(source.Combatants, actor => actor.Id == retainedDefinition.Id);
        Assert.Equal(original.CurrentHp, retained.CurrentHp);
        Assert.Equal(original.NativeParticipant!.ActorIndex, retained.NativeParticipant!.ActorIndex);
        Assert.False(retained.NativeParticipant.Active);
        Assert.Equal(original.NativeParticipant.Hidden, retained.NativeParticipant.Hidden);
        Assert.Equal(original.NativeParticipant.Suspended, retained.NativeParticipant.Suspended);
        Assert.True(retained.NativeParticipant.Untargetable);
        Assert.Equal((ushort)(original.NativeStatus!.Saved | 1), retained.NativeStatus!.Saved);
        Assert.Equal(source.NativeGraphics!.Models[original.NativeParticipant.ActorIndex].Transform!.Position28, ready.NativeGraphics!.Models[original.NativeParticipant!.ActorIndex].Transform!.Position28);
        Assert.Null(ready.NativeGraphics!.OriginalEffect);
        SaveNext("allocation-ready");
        File.WriteAllText(Path.Combine(output, "allocation-receipt.json"), JsonSerializer.Serialize(new { Status = "passed", Scenario = "Controlled original participant105 removal and112 barrier, scene-slot3 allocation and direct actor4 activation; no preceding death AI, effect227, action74, formula or effect80 execution", FormationId = 511, StageId = 142, Allocation = allocation, CasterActorIndex = 4, TargetActorIndex = actualTarget, CasterModel = 123, TargetModel = 124, CasterBones = casterBones, TargetBones = targetBones, CasterSequence = 13, TargetSequence = 26, AllocationCallbacks = allocationCallbacks, ActivationCallbacks = activationCallbacks, Events = session.Events.Where(entry => entry.Kind is "NativeSpawn" or "Activated").ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
        return (ready, 4, actualTarget);

        int AssertCurrentPose(BattleSnapshot snapshot, BattleCombatantDefinition actor, int index, int expected)
        {
            NativeBattleGraphicsModel model = snapshot.NativeGraphics!.Models[index];
            Assert.Equal(actor.Id, snapshot.NativeGraphics!.ActorIds[index]);
            Assert.Equal(checked((byte)(actor.ModelId + 16)), model.ModelId4);
            NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(actor).PoseSource ?? throw new InvalidDataException("The actual loaded model requires its original skeleton.");
            Assert.Equal(expected, pose.BoneCount);
            Assert.Equal(expected, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(pose.Skeleton.AsSpan())), model.CurrentBodyBoneMatrices!.SkeletonSha256);
            Assert.NotNull(model.GeometryWorldMatrix);
            Assert.NotNull(model.ActiveResourceRow100);
            return expected;
        }

        int Drain()
        {
            for (int count = 0; count < 1200; count++)
            {
                BattleSnapshot current = session.Capture();
                if (!removalObserved && !Assert.Single(current.Combatants, actor => actor.Id == removedDefinition.Id).NativeParticipant!.Active && (current.NativeGraphics!.Models[removedIndex].Flags0 & 2) == 0)
                {
                    BattleCombatantSnapshot released = Assert.Single(current.Combatants, actor => actor.Id == removedDefinition.Id);
                    Assert.Equal(removedBefore.CurrentHp, released.CurrentHp);
                    Assert.Equal((ushort)(removedBefore.NativeStatus!.Saved | 1), released.NativeStatus!.Saved);
                    Assert.True(released.NativeParticipant!.Untargetable);
                    NativeBattleModelTextureCache cache = current.NativeModelTextureCache!;
                    Assert.Equal((byte)0, cache.Rows[removedResource].Kind);
                    Assert.Equal(0, cache.BlockedSlots & removedTexture.TextureMask!.Value);
                    removalObserved = true;
                    File.WriteAllText(Path.Combine(output, "participant-release-receipt.json"), JsonSerializer.Serialize(new { Scenario = "Actual105 kind5 participant, heap and texture release before124 load; no manual texture or participant flag mutation", Actor = removedDefinition.Id, PhysicalIndex = removedIndex, ResourceRow = removedResource, BeforeTexture = removedTexture, AfterTexture = cache.Rows[removedResource], BeforeBlockedSlots = source.NativeModelTextureCache!.BlockedSlots, AfterBlockedSlots = cache.BlockedSlots, BeforeParticipant = removedBefore.NativeParticipant, AfterParticipant = released.NativeParticipant, BeforeStatus = removedBefore.NativeStatus, AfterStatus = released.NativeStatus, BeforeHp = removedBefore.CurrentHp, AfterHp = released.CurrentHp }));
                    SaveNext("original121-released");
                    continue;
                }
                if (current.NativeGraphics!.Descriptors.Queue.IsEmpty && current.QueuedActions.Length == 0 && current.ActiveAction is null && current.NativeScriptState.AllocationControl is { ActionGate: false, PauseGate: false })
                {
                    return count;
                }
                session.Tick();
            }
            throw new InvalidDataException("The actual allocation or activation pipeline did not drain within its diagnostic budget.");
        }

        void Save(string name, BattleSnapshot snapshot)
        {
            File.WriteAllText(Path.Combine(output, name + ".battle.json"), JsonSerializer.Serialize(snapshot));
        }

        void SaveNext(string name)
        {
            BattleSnapshot snapshot = session.Capture();
            Save(name, snapshot);
            string json = JsonSerializer.Serialize(snapshot);
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
            Assert.Equal(json, JsonSerializer.Serialize(restored.Capture()));
            session.Tick();
            restored.Tick();
            BattleSnapshot next = session.Capture();
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restored.Capture()));
            Save(name + "-next", next);
        }
    }

    private static void AssertReadyActor(BattleSnapshot snapshot, string id, int index)
    {
        NativeBattleParticipantSnapshot participant = Assert.Single(snapshot.Combatants, actor => actor.Id == id).NativeParticipant!;
        Assert.Equal(index, participant.ActorIndex);
        Assert.True(participant.Active);
        Assert.False(participant.Hidden);
        Assert.False(participant.Suspended);
        Assert.False(participant.Untargetable);
        Assert.False(participant.AllocationPendingStatistics);
    }

    private static void AssertDescriptor(BattleSnapshot snapshot, ushort command, uint? argument)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        Assert.Contains(graphics.Descriptors.Queue, slot => graphics.Descriptors.Packets[slot].Command == command && (argument is null || graphics.Descriptors.Packets[slot].Argument == argument));
    }

    private static object? Invoke(BattleSession session, string method, params object[] arguments)
    {
        MethodInfo function = typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException("The current native allocation producer is unavailable.");
        try
        {
            return function.Invoke(session, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }
}
