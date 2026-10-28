using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticTargetPreparationTests
{
    [Fact]
    public void OriginalEncounter509RejectsDirectM122ReadinessWithoutItsLaterPhaseModel140Owner()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original final-encounter initialization.");
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        DiagnosticBattleDriverRecipe driver = new(new(BattleInputKind.Action, ActorId: "enemy-1", TargetId: "enemy-1", Ability: BattleAbility.EnemyAbility, ActionId: 31), 106)
        {
            EnemyAbility = new(122, 1, 0, 10, 2, 31, 11, 200),
            ExpectedNativeAction = 31,
            ExpectedNativeCommand = 2
        };
        DiagnosticBattleRequest request = new(509, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([new(0, true)], [new(0, 0, [], [0]) { MagicCommand = new(0, 20), HitPoints = 486, Level = 7 }], true),
            Target = new("enemy-1", 142, 122, 1, 4, 0xC3, 1, null, null),
            Driver = driver,
            PrepareEntryBeforePresentation = true
        };
        BattleSession active = world.StartDiagnosticBattle(request);
        try
        {
            NotSupportedException rejected = Assert.Throws<NotSupportedException>(() => world.PrepareDiagnosticEntryBeforePresentation(request));
            Assert.Contains("within the declared callback budget", rejected.Message);
            Assert.True(active.NativeEntryReady);
            Assert.Contains("action-or-command", active.ObserveDiagnosticDrainBlockers());
            Assert.Contains("contact-or-descriptor", active.ObserveDiagnosticDrainBlockers());
            BattleDiagnosticTarget target = active.ObserveDiagnosticTarget("enemy-1");
            Assert.Equal(122, target.Model);
            Assert.Equal(4, target.PhysicalIndex);
            Assert.True(target.Alive);
            Assert.True(target.StatisticsReady);
            NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(active.NativeGraphics);
            Assert.DoesNotContain(graphics.Models, model => model.ModelId4 == 140);
            Assert.Equal(139, graphics.Models[3].ModelId4);
            Assert.Equal(138, graphics.Models[4].ModelId4);
            Assert.False(RecreateGame.DiagnosticCommandReady(active, driver));
        }
        finally
        {
            world.AbandonFaultedDiagnosticBattle();
            world.EndDiagnosticBattle();
        }
    }

    [Theory]
    [InlineData(508)]
    [InlineData(509)]
    public void OriginalFinalEncounterEarlyEffect80ContactRetainsTheColdPresentationMirror(int encounter)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original final-encounter initialization.");
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession active = world.StartDiagnosticBattle(new(encounter, 0, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1, Fixture = new([], [], true) });
        NativeBattlePartyStatusMirrors initial = active.NativePartyStatusMirrors;
        bool observedEffect80 = false;
        bool publishedContact = false;
        try
        {
            for (int callback = 0; callback < 3000; callback++)
            {
                observedEffect80 |= active.NativeGraphics?.OriginalEffect?.Binding.Effect == 80;
                active.Tick();
                if (!ReferenceEquals(initial, active.NativePartyStatusMirrors))
                {
                    publishedContact = true;
                    break;
                }
            }
            Assert.True(observedEffect80, "The original initialization must reach its existing effect80 presentation.");
            Assert.True(publishedContact, "The actual early contact must publish493840 before accepting this regression boundary.");
            Assert.NotNull(active.NativePresentationStatusMemory.Rows[0].Saved);
            Assert.NotNull(active.NativePresentationStatusMemory.Rows[0].Battle);
            Assert.Equal((ushort)0, active.NativePartyStatusMirrors.Saved[0]);
            Assert.Equal(0u, active.NativePartyStatusMirrors.Battle[0]);
        }
        finally
        {
            world.EndDiagnosticBattle();
        }
    }

    [Fact]
    public void Target_prerequisite_cannot_omit_native_identity_or_targetability()
    {
        DiagnosticTargetExpectation target = new("enemy-6", 142, 126, 6, 3, 0xC3, 1, 355, 76);
        target.Validate();
        Assert.Throws<InvalidDataException>(() => (target with { FlagsMask = 1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (target with { FlagsValue = 0x41 }).Validate());
        Assert.Throws<InvalidDataException>(() => (target with { PhysicalIndex = 7 }).Validate());
        Assert.Throws<InvalidDataException>(() => (target with { OpeningAction = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (target with { OpeningAction = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (target with { OpeningEffect = null }).Validate());
        (target with { OpeningAction = null, OpeningEffect = null }).Validate();
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DiagnosticTargetExpectation>("{\"ActorId\":\"enemy-0\",\"Stage\":31,\"Model\":61,\"SceneSlot\":0,\"PhysicalIndex\":3,\"FlagsMask\":195,\"FlagsValue\":1}"));
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
        for (int callback = 0; callback < 1000; callback++)
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
        BattleDiagnosticEffectDispatch opening = Assert.IsType<BattleDiagnosticEffectDispatch>(receipt.OpeningDispatch);
        Assert.Equal(355, opening.ActionId);
        Assert.Equal(76, opening.ExecutedEffect);
        Assert.True(receipt.Target.Alive);
        Assert.True(receipt.Target.StatisticsReady);
        Assert.Equal(3, receipt.Target.PhysicalIndex);
        Assert.True(receipt.OpeningCompletedCallback >= opening.Callback);
        Assert.Contains(active.Combatants, actor => actor.ModelId == 127 && actor.IsActive);
        Assert.DoesNotContain(active.Events, value => value.Kind == "NativeSpawn" && value.Amount != 7);
    }

    [Fact]
    public void Installed_target_without_declared_opening_still_requires_live_identity_and_initialized_statistics()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original target initialization validation.");
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        BattleSession active = world.StartDiagnosticBattle(new(94, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([], [], true),
            Target = new("enemy-0", 31, 61, 0, 3, 0xC3, 1, null, null)
        });
        Assert.Null(world.VerifyDiagnosticTarget());
        DiagnosticTargetReceipt? receipt = null;
        for (int callback = 0; callback < 3000; callback++)
        {
            active.Tick();
            receipt = world.VerifyDiagnosticTarget();
            if (receipt is not null)
            {
                break;
            }
        }
        Assert.NotNull(receipt);
        Assert.Equal(61, receipt.Target.Model);
        Assert.True(receipt.Target.Alive);
        Assert.True(receipt.Target.StatisticsReady);
        Assert.Null(receipt.OpeningDispatch);
        Assert.Null(receipt.OpeningCompletedCallback);
    }
}
