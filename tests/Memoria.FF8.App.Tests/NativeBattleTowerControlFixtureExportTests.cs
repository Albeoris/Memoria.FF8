using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleTowerControlFixtureExportTests
{
    [Fact]
    public void Export_sparse_original_tower_control17_and18_components()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TOWER_CONTROL_EXPORT");
        Assert.SkipUnless(directory is not null && output is not null, "Select original resources and a distinct standalone export directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(29);
        Assert.Equal(3, formation.StageId);
        BattleEnemyPlacement prototypePlacement = Assert.Single(formation.Slots, placement => placement.ModelId == 91 && placement.Slot == 2);
        Assert.False(prototypePlacement.Enabled);
        Assert.True(prototypePlacement.Hidden);
        Assert.True(prototypePlacement.Untargetable);
        Assert.True(prototypePlacement.InitiallySuspended);
        BattleNativeAction shell = Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 2);
        BattleNativeAction control = shell with { Command = 252, ActionId = 1, MagicId = 17, AttackType = 0 };
        BattleDefinition original = NativeBattleRegionalMonsterPresentationTests.CreateDefinition(game, assets, 91, 20, 0, assets.ReadEnemyAi(91, 20), control);
        BattleCombatantDefinition[] actors = original.Combatants.Select(actor =>
        {
            if (actor.IsPlayer)
            {
                return actor;
            }
            BattleEnemyPlacement placement = Assert.Single(formation.Slots, candidate => candidate.Slot == actor.SceneSlot);
            return actor with
            {
                InitialActive = placement.Enabled,
                InitialHidden = placement.Hidden,
                InitialUntargetable = placement.Untargetable,
                InitialSuspended = placement.InitiallySuspended,
                NativeAi = actor.ModelId == 91 ? assets.ReadEnemyAi(91, 20) with { Scripts = [[0], [0], [0], [0], [0]] } : null,
                NativeTemplate = actor.ModelId == 91 ? assets.ReadEnemyTemplate(91, checked((byte)placement.Level)) : null
            };
        }).ToArray();
        BattleDefinition definition = original with
        {
            Combatants = actors,
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, original.NativeGraphics!.Rules, formation),
            NativeRetainedMemory = original.NativeRetainedMemory,
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte,
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([17, 18])
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleSnapshot cold = session.Capture();
        session = BattleSession.Restore(cold with { NativeGraphics = cold.NativeGraphics! with { NativeOutcome = new NativeBattleOutcomeState() } });
        SaveCold("before-entry", session.Capture());
        int entryCallbacks = 0;
        while (!session.NativeEntryReady && entryCallbacks < 1200)
        {
            session.Tick();
            entryCallbacks++;
        }
        Assert.True(session.NativeEntryReady);
        BattleSnapshot entered = session.Capture();
        Assert.Null(entered.ActiveAction);
        Assert.Empty(entered.QueuedActions);
        Assert.Null(entered.NativeGraphics!.OriginalEffect);
        BattleCombatant removed = Assert.Single(session.Combatants, actor => actor.ModelId == 74 && actor.IsActive);
        BattleCombatant retained = Assert.Single(session.Combatants, actor => actor.ModelId == 73 && actor.IsActive);
        Assert.True(retained.Hidden);
        Assert.True(retained.Suspended);
        Assert.Equal(3, Assert.Single(entered.Combatants, actor => actor.Id == removed.Id).NativeParticipant!.ActorIndex);
        Assert.Equal(4, Assert.Single(entered.Combatants, actor => actor.Id == retained.Id).NativeParticipant!.ActorIndex);
        int removedRow = entered.NativeGraphics.Models[3].ActiveResourceRow100!.Value;
        SaveColdNext("entry-ready", session.Capture());
        Invoke(session, "QueueNativeGraphicsRemoval", removed);
        Invoke(session, "QueueNativeGraphicsBarrier");
        BattleCombatant caller = Assert.Single(session.Combatants, actor => actor.IsPlayer && actor.ModelId == 0);
        BattleCombatant allocated = (BattleCombatant)Invoke(session, "AllocateNativeEnemy", caller, 2, 5, true)!;
        BattleSnapshot pending = session.Capture();
        NativeBattleEnemyAllocation allocation = Assert.Single(pending.NativeAllocations);
        Assert.Equal(allocated.Id, allocation.Id);
        Assert.Equal(2, allocation.SceneSlot);
        Assert.Equal(5, Assert.Single(pending.Combatants, actor => actor.Id == allocated.Id).NativeParticipant!.ActorIndex);
        AssertDescriptor(pending, 105, null);
        AssertDescriptor(pending, 112, null);
        AssertDescriptor(pending, 102, null);
        AssertDescriptor(pending, 113, 0x48AD10);
        AssertDescriptor(pending, 113, 0x487670);
        SaveColdNext("removal-allocation-pending", pending);
        bool releaseObserved = false;
        int allocationCallbacks = 0;
        for (; allocationCallbacks < 1200; allocationCallbacks++)
        {
            BattleSnapshot current = session.Capture();
            if (current.NativeModelTextureCache!.Rows[removedRow].Kind == 0)
            {
                releaseObserved = true;
            }
            if (current.NativeGraphics!.Descriptors.Queue.IsEmpty && current.ActiveAction is null && current.QueuedActions.Length == 0 && current.NativeScriptState.AllocationControl is { ActionGate: false, PauseGate: false })
            {
                break;
            }
            session.Tick();
        }
        Assert.True(allocationCallbacks < 1200);
        Assert.True(releaseObserved, "Original105 removal must release the Biggs resource before the new91 loader uses its row.");
        BattleSnapshot ready = session.Capture();
        AssertReady(ready);
        SaveColdNext("allocation-ready", ready);
        ready = session.Capture();
        AssertReady(ready);
        SaveCold("ready", ready);
        foreach (ushort effect in new ushort[] { 17, 18 })
        {
            NativeBattleEffectBinding binding = ready.Definition.NativeEffectCatalog[effect - 1];
            Assert.Equal(effect, binding.Effect);
            NativeBattleActionPacketsState packets = ready.NativeActionPackets!.ResetCounts();
            (packets, int header) = packets.Prepare(new(5, 252, 1, -1, 1, effect, null));
            BattleCombatantSnapshot target = Assert.Single(ready.Combatants, actor => actor.NativeParticipant!.ActorIndex == 0);
            NativeBattleStatusSnapshot status = target.NativeStatus!;
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            packets.Validate();
            (NativeBattleDescriptorState descriptors, _) = ready.NativeGraphics!.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
            NativeBattleRetainedMemory mirror = ready.Definition.NativeRetainedMemory!;
            BattleSnapshot queued = ready with { NativeActionPackets = packets, NativeGraphics = ready.NativeGraphics with { Descriptors = descriptors }, Definition = ready.Definition with { NativeRetainedMemory = mirror with { ActionPackets = packets } } };
            Assert.Equal(JsonSerializer.Serialize(ready.Combatants), JsonSerializer.Serialize(queued.Combatants));
            Assert.Equal(JsonSerializer.Serialize(ready.NativeEffectMemory), JsonSerializer.Serialize(queued.NativeEffectMemory));
            Assert.Equal(JsonSerializer.Serialize(ready.NativeCrtRandom), JsonSerializer.Serialize(queued.NativeCrtRandom));
            Assert.Equal(JsonSerializer.Serialize(ready.NativeGlobalRandom), JsonSerializer.Serialize(queued.NativeGlobalRandom));
            SaveCold("control" + effect + "-queued-action104", queued);
        }
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "prepared", Formation = 29, Stage = 3, RetiredModel = 74, RetainedDormantModel = 73, AllocatedModel = 91, CasterActorIndex = 5, CasterId = allocated.Id, allocation, EntryCallbacks = entryCallbacks, AllocationCallbacks = allocationCallbacks, ExpectedContactTransitions17 = 0, ExpectedContactTransitions18 = 0, Scenario = "Declared sparse original tower allocation and unused seven-HP party0 contact; native control252/action1; no ordinary AI, story trigger or kernel action mapping claim", QueuedNextCheck = "Deferred to the existing complete selected runner so export does not enter or advance either effect" }));

        void AssertReady(BattleSnapshot snapshot)
        {
            NativeBattleParticipantSnapshot participant = Assert.Single(snapshot.Combatants, actor => actor.Id == allocated.Id).NativeParticipant!;
            Assert.Equal(5, participant.ActorIndex);
            Assert.True(participant.Active);
            Assert.False(participant.Hidden);
            Assert.False(participant.Suspended);
            Assert.False(participant.Untargetable);
            Assert.False(participant.AllocationPendingStatistics);
            NativeBattleGraphicsModel model = snapshot.NativeGraphics!.Models[5];
            Assert.Equal((byte)107, model.ModelId4);
            Assert.Equal(allocated.Id, snapshot.NativeGraphics.ActorIds[5]);
            Assert.Equal(prototypePlacement.NativePosition, model.Transform!.Position28);
            NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(session.GetCombatantDefinition(allocated.Id)).PoseSource!;
            Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
            Assert.NotNull(model.GeometryWorldMatrix);
            Assert.NotNull(model.ActiveResourceRow100);
            NativeBattleModelCpuResources cpu = snapshot.NativeModelCpuResources!;
            Assert.True(NativeBattleModelCpuResources.EnemyAddress + cpu.EnemyCursor!.Value <= 0x196B018u - cpu.ScratchMaximum!.Value * 8u, "The sparse original enemy heap must precede the original projected workspace.");
            NativeBattleGraphicsResource originalResource = Assert.Single(snapshot.Definition.NativeGraphics!.Bindings, binding => binding.ActorId == "monster").Resource;
            _ = NativeBattleEnemyResourceView.Resolve(cpu, snapshot.NativeFileStaging!, model.ActiveResourceRow100.Value, originalResource);
            Assert.Null(snapshot.ActiveAction);
            Assert.Empty(snapshot.QueuedActions);
            Assert.Null(snapshot.NativeGraphics.OriginalEffect);
            Assert.Equal(0, snapshot.NativeEffectMemory!.Active);
        }

        void SaveCold(string name, BattleSnapshot snapshot)
        {
            string json = JsonSerializer.Serialize(snapshot);
            BattleSnapshot loaded = JsonSerializer.Deserialize<BattleSnapshot>(json)!;
            Assert.Equal(json, JsonSerializer.Serialize(BattleSession.Restore(loaded).Capture()));
            File.WriteAllText(Path.Combine(output!, name + ".battle.json"), json);
        }

        void SaveColdNext(string name, BattleSnapshot snapshot)
        {
            SaveCold(name, snapshot);
            BattleSession direct = BattleSession.Restore(snapshot);
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
            direct.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(restored.Capture()));
            SaveCold(name + "-next", direct.Capture());
            session = direct;
        }
    }

    private static void AssertDescriptor(BattleSnapshot snapshot, ushort command, uint? argument)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        Assert.Contains(graphics.Descriptors.Queue, slot => graphics.Descriptors.Packets[slot].Command == command && (argument is null || graphics.Descriptors.Packets[slot].Argument == argument));
    }

    private static object? Invoke(BattleSession session, string method, params object[] arguments)
    {
        MethodInfo function = typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException("The existing typed allocation service is unavailable.");
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
