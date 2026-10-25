using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticTargetPreparationTests
{
    [Fact]
    public void Target_prerequisite_cannot_omit_native_identity_or_targetability()
    {
        DiagnosticTargetExpectation target = new("enemy-6", 142, 126, 6, 3, 0xC3, 1, 355, 76);
        target.Validate();
        Assert.Throws<InvalidDataException>(() => (target with { FlagsMask = 1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (target with { FlagsValue = 0x41 }).Validate());
        Assert.Throws<InvalidDataException>(() => (target with { PhysicalIndex = 7 }).Validate());
        Assert.Throws<InvalidDataException>(() => (target with { OpeningAction = 0 }).Validate());
    }

    [Fact]
    public void Installed_final_form_preparation_runs_original_startup_and_preserves_secondary_participant()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original target initialization validation.");
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        DiagnosticBattleRequest request = new(982, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([], [], true),
            Target = new("enemy-6", 142, 126, 6, 3, 0xC3, 1, 355, 76)
        };
        BattleSession active = world.StartDiagnosticBattle(request);
        Assert.Null(world.VerifyDiagnosticTarget());
        for (int callback = 0; callback < 3000; callback++)
        {
            active.Tick();
            if (active.NativeEntryReady && active.ObserveDiagnosticDrainBlockers().Count == 0 && world.VerifyDiagnosticTarget() is not null)
            {
                break;
            }
        }
        DiagnosticTargetReceipt? verified = world.VerifyDiagnosticTarget();
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(active.NativeGraphics);
        object observation = new
        {
            Scope = "Bounded ordinary native startup; no focal effect or graphical-host acceptance.",
            active.Frame,
            active.NativeHostCallbacks,
            active.NativeEntryReady,
            active.Outcome,
            Preparation = world.ObserveDiagnosticTargetPreparation(),
            Target = active.ObserveDiagnosticTarget("enemy-6"),
            DrainBlockers = active.ObserveDiagnosticDrainBlockers(),
            ActiveAction = active.ActiveAction is { } action ? new { action.ActorId, action.TargetId, action.Ability, action.ActionId, action.Phase, action.NativeSequence } : null,
            QueuedActions = active.QueuedActions.Select(action => new { action.ActorId, action.TargetId, action.Ability, action.ActionId, action.NativeSequence, action.NativeCallback }).ToArray(),
            EventTail = active.Events.TakeLast(12).ToArray(),
            Camera = active.NativeCamera is { } camera ? new { camera.Mask, camera.Mode, camera.ForceCamera, camera.CurrentPose } : null,
            Graphics = new
            {
                graphics.Callback,
                graphics.EntryGatePhase,
                graphics.EntryLifecycle,
                graphics.EntryBarrierCompletion,
                graphics.ModelLoadCount,
                graphics.ActionActorId,
                graphics.ActionCallbackReady,
                graphics.Requests,
                EffectHead = graphics.Effects.Head,
                EffectTasks = graphics.Effects.PublishedCount,
                AuxiliaryHead = graphics.Auxiliary.Head,
                ModuleHead = graphics.ModuleEffects.Head,
                OriginalEffect = graphics.OriginalEffect is { } effect ? new { effect.RequestedEffect, effect.SelectedIndex, effect.Binding.Effect } : null,
                Models = graphics.Models.Select(model => new { model.Index, Actor = graphics.ActorIds[model.Index], model.ModelId4, model.Flags0, model.Phase6, model.Sequence, model.SequenceFrame, SequenceState = model.SequenceState is { } sequence ? new { sequence.Offset46, sequence.Wait1, sequence.Pending3, sequence.Callback42, sequence.Pose, sequence.PoseFrame114, sequence.PoseLimit115 } : null }).ToArray()
            }
        };
        string summary = JsonSerializer.Serialize(observation, new JsonSerializerOptions { WriteIndented = true });
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIAGNOSTIC_TARGET_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Assert.True(Path.IsPathFullyQualified(output), "Diagnostic target witness output requires an explicit absolute path.");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using (FileStream summaryFile = new(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                JsonSerializer.Serialize(summaryFile, observation, new JsonSerializerOptions { WriteIndented = true });
            }
            using FileStream snapshotFile = new(Path.ChangeExtension(output, "battle.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(snapshotFile, active.Capture());
        }
        Assert.True(verified is not null, summary);
        DiagnosticTargetReceipt receipt = verified!;
        Assert.Equal(355, receipt.OpeningDispatch.ActionId);
        Assert.Equal(76, receipt.OpeningDispatch.ExecutedEffect);
        Assert.True(receipt.Target.Alive);
        Assert.True(receipt.Target.StatisticsReady);
        Assert.Equal(3, receipt.Target.PhysicalIndex);
        Assert.True(receipt.OpeningCompletedCallback >= receipt.OpeningDispatch.Callback);
        Assert.Contains(active.Combatants, actor => actor.ModelId == 127 && actor.IsActive);
        Assert.DoesNotContain(active.Events, value => value.Kind == "NativeSpawn" && value.Amount != 7);
    }
}
