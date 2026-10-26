using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleOriginalAllocation269SourceTests
{
    [Fact]
    public void Original_three_death_callbacks_allocate_the_original_dormant_caster()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? baselinePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_ALLOCATION_SOURCE_BASELINE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ALLOCATION_SOURCE_OUTPUT");
        Assert.SkipUnless(directory is not null && baselinePath is not null && output is not null, "Select original resources, a fresh original formation baseline and a distinct source output directory.");
        Assert.False(Directory.Exists(output), "Preserve prior standalone sources by selecting a distinct output directory.");
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot baseline = NativeBattleEffectFamilyRunner.LoadSnapshot(baselinePath!);
        Assert.Equal(436, baseline.Definition.EncounterId);
        Assert.Equal(125, baseline.Definition.StageId);
        BattleEncounter formation = assets.LoadEncounter(436);
        File.WriteAllText(Path.Combine(output!, "original-formation.json"), JsonSerializer.Serialize(formation, new JsonSerializerOptions { WriteIndented = true }));
        BattleEnemyPlacement original = Assert.Single(formation.Slots, row => row.Slot == 3);
        Assert.Equal(94, original.ModelId);
        Assert.False(original.Enabled);
        Assert.True(original.Hidden && original.InitiallySuspended && original.Untargetable);
        Assert.Equal(new[] { 54, 39, 40 }, baseline.Definition.Combatants.Where(actor => !actor.IsPlayer).OrderBy(actor => actor.SceneSlot).Select(actor => actor.ModelId).ToArray());
        BattleCombatantDefinition prototype = new()
        {
            Id = "original-reserve-3",
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
        BattleCombatantDefinition[] initialized = baseline.Definition.Combatants.Select(actor =>
        {
            if (actor.IsPlayer)
            {
                return actor;
            }
            BattleEnemyPlacement placement = Assert.Single(formation.Slots, row => row.Slot == actor.SceneSlot);
            Assert.Equal(actor.ModelId, placement.ModelId);
            return actor with { NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(actor.ModelId), NativeAi = assets.ReadEnemyAi(actor.ModelId, actor.Level), NativeTemplate = assets.ReadEnemyTemplate(actor.ModelId, checked((byte)placement.Level)) };
        }).ToArray();
        BattleCombatantDefinition[] actors = [.. initialized, prototype];
        BattleDefinition definition = baseline.Definition with { Combatants = actors, NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation), NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeClockRules = assets.ReadNativeClockRules(), NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte, NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([269]) };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        Save("before-entry", session.Capture());
        Pump(() => session.NativeEntryReady, "original-entry");
        Assert.Empty(session.Capture().NativeAllocations);
        BattleCombatant party = Assert.Single(session.Combatants, actor => actor.IsPlayer);
        foreach (int modelId in new[] { 54, 39, 40 })
        {
            BattleCombatant target = Assert.Single(session.Combatants, actor => actor.ModelId == modelId && actor.IsActive);
            Assert.True((target.NativeCombatantFlags & 0x20) != 0, "The original nonempty death callback must own native flag20.");
            Save($"before-original-death-{modelId}", session.Capture());
            Invoke(session, "ApplyDamage", [party, target, target.CurrentHp, "Controlled post-formula lethal contact", false, true]);
            Pump(() => session.Capture().Combatants.Single(actor => actor.Id == target.Id).NativeParticipant!.DeathHandled && (modelId == 40 || !target.IsActive), $"original-death-{modelId}");
            Save($"after-original-death-{modelId}", session.Capture());
        }
        Pump(AllocationReady, "original-reserve-activation");
        BattleCombatant allocated = Assert.Single(session.Combatants, actor => actor.ModelId == 94 && actor.IsActive);
        BattleSnapshot ready = session.Capture();
        NativeBattleEnemyAllocation allocation = Assert.Single(ready.NativeAllocations);
        Assert.Equal(3, allocation.SceneSlot);
        Assert.Equal(allocated.Id, allocation.Id);
        int caster = ready.NativeLastAllocatedIndex;
        Assert.InRange(caster, 3, 6);
        NativeBattleGraphicsModel loaded = ready.NativeGraphics!.Models[caster];
        Assert.Equal((byte)110, loaded.ModelId4);
        Assert.NotNull(loaded.GeometryWorldMatrix);
        BattleCombatantDefinition current = session.GetCombatantDefinition(allocated.Id);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(current).PoseSource ?? throw new InvalidDataException("The original allocated caster requires its actual skeleton.");
        var bones = NativeBattleBodyBoneMatrices.Require(loaded, pose);
        Assert.Equal(pose.BoneCount, bones.Length);
        Assert.Null(ready.NativeGraphics.OriginalEffect);
        Save("after-entry", ready);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 266 && row.MagicId == 269);
        byte sequence = NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(94, allocated.Level), action);
        NativeBattleActionPacketsState packets = ready.NativeActionPackets!;
        (packets, int header) = packets.Prepare(new(checked((byte)caster), action.Command, sequence, -1, action.ActionId, action.MagicId, null));
        BattleCombatantSnapshot targetStatusOwner = Assert.Single(ready.Combatants, actor => actor.Id == party.Id);
        NativeBattleStatusSnapshot status = targetStatusOwner.NativeStatus ?? throw new InvalidDataException("The original party target requires its actual status owner.");
        (packets, _) = packets.AppendContact(new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = ready.NativeGraphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        Save("queued-action104", ready with { NativeActionPackets = packets, NativeGraphics = ready.NativeGraphics with { Descriptors = descriptors } });
        File.WriteAllText(Path.Combine(output!, "allocation-source-result.json"), JsonSerializer.Serialize(new { Effect = 269, Status = "passed", Scenario = "Controlled post-formula deaths of three original enemies; actual authored death callbacks/barrier allocate and activate original reserve row3; no natural formula or inventory admission", Encounter = 436, Stage = 125, ActualCaster = caster, NativeModel = 110, OriginalModel = 94, allocation.SceneSlot, BoneCount = bones.Length, Command = action.Command, Action = action.ActionId, Sequence = sequence, Frame = ready.Frame, EffectCallbacks = 0 }, new JsonSerializerOptions { WriteIndented = true }));

        bool AllocationReady()
        {
            BattleSnapshot snapshot = session.Capture();
            if (snapshot.NativeAllocations.Length != 1)
            {
                return false;
            }
            NativeBattleParticipantSnapshot participant = snapshot.Combatants.Single(actor => actor.Id == snapshot.NativeAllocations[0].Id).NativeParticipant!;
            if (!participant.Active || participant.Hidden || participant.Suspended || participant.Untargetable || participant.AllocationPendingStatistics)
            {
                return false;
            }
            NativeBattleGraphicsModel model = snapshot.NativeGraphics!.Models[participant.ActorIndex];
            return snapshot.NativeGraphics.Descriptors.Queue.IsEmpty && model.CurrentBodyBoneMatrices is not null && model.GeometryWorldMatrix is not null;
        }

        void Save(string name, BattleSnapshot snapshot)
        {
            File.WriteAllText(Path.Combine(output!, name + ".battle.json"), JsonSerializer.Serialize(snapshot));
        }

        void Pump(Func<bool> complete, string boundary)
        {
            for (int callback = 0; callback < 2400; callback++)
            {
                if (complete())
                {
                    return;
                }
                try
                {
                    session.Tick();
                }
                catch
                {
                    Save(boundary + "-failure", session.Capture());
                    throw;
                }
            }
            Save(boundary + "-budget", session.Capture());
            throw new InvalidDataException("The original allocation continuation exceeded its bounded callback budget at " + boundary + ".");
        }
    }

    private static void Invoke(BattleSession session, string name, object?[] arguments)
    {
        MethodInfo method = typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException(name);
        try
        {
            method.Invoke(session, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }
}
