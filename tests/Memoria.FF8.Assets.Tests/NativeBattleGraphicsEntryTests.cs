using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGraphicsEntryTests
{
    [Theory]
    [InlineData(94)]
    [InlineData(512)]
    public void Installed_stage_camera_and_model_entry_resume_together_before_the_real_barrier(int encounterId)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(encounterId);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleSession seed = Create(game, assets, encounter.Enemies.First().ModelId, formation: encounter);
        BattleDefinition definition = seed.Capture().Definition with { EncounterId = encounterId, NativeStageSource = stage.NativeSource, NativeStageCameraBank = stage.NativeCameraBank, NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera], NativeStageCameraControl = stage.NativeCameraControl };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.Tick();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int callback = 0; callback < 1200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Assert.True(battle.NativeEntryReady);
        Assert.Equal(0, battle.NativeCameraTaskMask);
        Assert.Equal(0, battle.Frame);
    }

    [Fact]
    public void OriginalBalambAndIfritEntryProgramsResumeTheirModelsWhileTurnBasedGameplayWaits()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        foreach (int model in assets.LoadEncounter(512).Enemies.Select(enemy => enemy.ModelId).Append(61).Distinct())
        {
            BattleSession battle = Create(game, assets, model, formation: assets.LoadEncounter(model == 61 ? 94 : 512));
            Assert.Throws<InvalidOperationException>(() => battle.QueueAttack("party-0", "enemy"));
            Assert.Equal(0ul, battle.Capture().NativeGlobalRandomCalls);
            battle.Tick();
            Assert.False(battle.NativeGraphics!.EntryReady);
            Assert.Equal(0, battle.Frame);
            string saved = JsonSerializer.Serialize(battle.Capture());
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(saved)!);
            Assert.Equal(saved, JsonSerializer.Serialize(restored.Capture()));
            for (int callback = 0; callback < 200 && !battle.NativeGraphics.EntryReady; callback++)
            {
                battle.Tick();
                restored.Tick();
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            }
            Assert.True(battle.NativeGraphics.EntryReady);
            Assert.True(battle.Capture().NativeGlobalRandomCalls >= 13);
            Assert.Equal((byte)255, battle.NativeGraphics.EntryBarrierCompletion);
            Assert.All(battle.NativeGraphics.Models.Where(row => (row.Flags0 & 2) != 0), row => Assert.Equal((byte)0, row.Kind5));
            Assert.Equal(0, battle.Frame);
        }
    }

    [Fact]
    public void HeadlessEntryDoesNotAdvanceGameplayOrAtbBeforeItsActualBarrierCompletes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 61, ready: false);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
            if (!battle.NativeEntryReady)
            {
                Assert.Equal(0, battle.Frame);
                Assert.All(battle.Combatants, actor => Assert.Equal(0, actor.Atb));
            }
        }
        Assert.True(battle.NativeEntryReady);
        Assert.Equal(0, battle.Frame);
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_GRAPHICS_ENTRY_OUTPUT") is string output)
        {
            string path = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(stream, battle.Capture());
        }
    }

    [Fact]
    public void OriginalHostPumpAdvancesRawAtbFourTimesAndKeepsGraphicsAliveAtReadyDecision()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession seed = Create(game, assets, 61, ready: false);
        BattleDefinition definition = seed.Capture().Definition with { NativeClockRules = new(10), NativeBattleSpeed = 2, Combatants = seed.Capture().Definition.Combatants.Select(actor => actor with { Speed = 1 }).ToArray() };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.Equal(0, battle.Frame);
        //Case3 admits the first post-graphics service; the previous three pre-services still see C2=0.
        Assert.All(battle.Combatants, actor => Assert.Equal(31, actor.NativeAtb!.Current));
        battle.Tick();
        Assert.All(battle.Combatants, actor => Assert.Equal(155, actor.NativeAtb!.Current));
        BattleSnapshot snapshot = battle.Capture();
        BattleCombatantSnapshot[] actors = snapshot.Combatants.ToArray();
        actors[0] = actors[0] with { NativeAtb = new(12000, 11990), Atb = 9991 };
        battle = BattleSession.Restore(snapshot with { Combatants = actors });
        battle.SetMode(BattleAtbMode.TurnBased);
        long beforeFrame = battle.Frame;
        long beforeGraphics = battle.NativeGraphics!.Callback;
        battle.Tick();
        Assert.Equal(12000, battle.Combatants[0].NativeAtb!.Current);
        Assert.All(battle.Combatants.Skip(1), actor => Assert.Equal(186, actor.NativeAtb!.Current));
        Assert.Equal(beforeFrame, battle.Frame);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 4; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(beforeFrame, battle.Frame);
        Assert.Equal(beforeGraphics + 5, battle.NativeGraphics.Callback);
        Assert.All(battle.Combatants.Skip(1), actor => Assert.Equal(186, actor.NativeAtb!.Current));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(BattleAtbMode.Normal)]
    [InlineData(BattleAtbMode.TurnBased)]
    public void Initially_full_native_gauge_queues_ui_ready_once_then_wait_menu_stops_atb(BattleAtbMode mode)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot seed = Create(game, assets, 61).Capture();
        BattleSession battle = new(seed.Definition with { NativeClockRules = new(10), NativeBattleSpeed = 2, NativeSavedOptions = 1, Combatants = seed.Definition.Combatants.Select(actor => actor with { Speed = 1 }).ToArray() }, mode);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.Equal(0u, battle.NativeGraphics!.UiServices!.ReadyMask);
        Assert.Equal(3, battle.NativeGraphics.UiServices.Commands.Queue.Length);
        battle.Tick();
        Assert.Equal(7u, battle.NativeGraphics.UiServices.ReadyMask);
        Assert.Empty(battle.NativeGraphics.UiServices.Commands.Queue);
        Assert.Equal((sbyte)4, battle.NativeGraphics.UiServices.Menu957);
        battle.OpenNativeActionMenu();
        Assert.False(battle.NativeGraphics.UiServices.AllowsAtb(1));
        int gauge = battle.Combatants[3].NativeAtb!.Current;
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(gauge, battle.Combatants[3].NativeAtb!.Current);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        battle.CloseNativeActionMenu();
        battle.Tick();
        Assert.True(battle.NativeGraphics.UiServices.AllowsAtb(1));
        if (mode == BattleAtbMode.Normal)
        {
            Assert.True(battle.Combatants[3].NativeAtb!.Current > gauge);
        }
        else
        {
            Assert.Equal(gauge, battle.Combatants[3].NativeAtb!.Current);
            Assert.Equal((byte)0, battle.Capture().NativeClock!.AtbAdmitted);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TurnBased_raw_host_automatically_reaches_readiness_and_explicit_advance_preserves_waiting_actor(bool presentationDriven)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition seed = Create(game, assets, 61, ready: false).Capture().Definition;
        BattleDefinition definition = seed with { NativeClockRules = new(10), NativeBattleSpeed = 2, Combatants = seed.Combatants.Select((actor, index) => actor with { Speed = index == 0 ? 100 : actor.IsPlayer ? 10 : 1 }).ToArray() };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        TimeSpan frame = NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(1) + TimeSpan.FromTicks(1);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        long before = battle.NativeGraphics!.Callback;

        if (presentationDriven)
        {
            battle.AdvanceNativeGraphicsPresentation(frame);
        }
        else
        {
            battle.Tick();
        }

        Assert.True(battle.Combatants[0].IsReady);
        Assert.False(battle.Combatants[1].IsReady);
        Assert.Equal(before + 1, battle.NativeGraphics!.Callback);
        long waitingFrame = battle.Frame;
        int waitingGauge = battle.Combatants[1].NativeAtb!.Current;
        if (presentationDriven)
        {
            battle.AdvanceNativeGraphicsPresentation(frame);
        }
        else
        {
            battle.Tick();
        }
        Assert.Equal(waitingFrame, battle.Frame);
        Assert.Equal(waitingGauge, battle.Combatants[1].NativeAtb!.Current);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);

        int advanced = battle.AdvanceTurn();
        Assert.Equal(advanced, restored.AdvanceTurn());
        Assert.True(advanced > 1);
        Assert.True(battle.Combatants[0].IsReady);
        Assert.True(battle.Combatants[1].IsReady);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void NativePartyHudCacheKeepsItsContactBoundaryWhenLogicalHpAlreadyChanged()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 61, ready: false);
        BattleSnapshot snapshot = battle.Capture();
        int cached = battle.ReadPresentedVital("party-0").HitPoints;
        Assert.True(cached > 1);
        BattleCombatantSnapshot[] actors = snapshot.Combatants.ToArray();
        actors[0] = actors[0] with { CurrentHp = 1 };
        BattleSession changed = BattleSession.Restore(snapshot with { Combatants = actors });
        Assert.Equal(1, changed.Combatants[0].CurrentHp);
        Assert.Equal(cached, changed.ReadPresentedVital("party-0").HitPoints);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(changed.Capture()))!);
        Assert.Equal(changed.ReadPresentedVital("party-0"), restored.ReadPresentedVital("party-0"));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativePartyPresentation = null }));
    }

    [Fact]
    public void PresentationClockSurvivesRestoreWithoutPersistingTheHostTransportPolicy()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 61);
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(500_000));
        Assert.Equal(0, battle.NativeGraphics!.Callback);
        Assert.Equal(7_750_000, battle.NativeGraphics.PresentationSubticks);
        battle.Tick();
        Assert.Equal(0, battle.NativeGraphics.Callback);
        BattleSnapshot saved = battle.Capture();
        BattleSession headless = BattleSession.Restore(saved);
        headless.Tick();
        Assert.Equal(1, headless.NativeGraphics!.Callback);
        BattleSession hosted = BattleSession.Restore(saved);
        hosted.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(500_000));
        Assert.Equal(1, hosted.NativeGraphics!.Callback);
        hosted.Tick();
        Assert.Equal(1, hosted.NativeGraphics.Callback);
        Assert.Equal(0, hosted.Frame);
    }

    [Fact]
    public void OriginalIfritMessageSequenceOwnsTextAndAaReceiptAcrossGameplayWaitAndRestore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeBattleAiDefinition original = assets.ReadEnemyAi(61, 1);
        NativeBattleAiDefinition ai = original with { Scripts = [[4, 200, 5, 14, 7, 89, 6, 0], [0], [0], [0], [0]] };
        BattleSession battle = Create(game, assets, 61, ai: ai);
        for (int callback = 0; callback < 200 && battle.ActiveAction is null; callback++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)14, battle.ActiveAction!.NativeSequence);
        BattleSnapshot beforeText = battle.Capture();
        battle = BattleSession.Restore(beforeText with { NativeScriptState = beforeText.NativeScriptState with { Tasks = beforeText.NativeScriptState.Tasks.AppendWait(40) } });
        battle.Tick();
        Assert.Equal(original.Texts[3], Assert.Single(battle.Events, item => item.Kind == "NativeText").Message);
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_GRAPHICS_COMPONENT_OUTPUT") is string output)
        {
            string path = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(battle.Capture()));
        }
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int callback = 0; callback < 30; callback++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.True(battle.NativeGraphics!.ActionCallbackReady);
        Assert.Equal((ushort)9, Assert.Single(battle.Capture().NativeScriptState.WaitCounters));
        //AA admits contact; the factual header remains in windup until its final descriptor10 completes.
        Assert.Equal(BattleActionPhase.Windup, battle.ActiveAction!.Phase);
        Assert.Equal(beforeText.ActiveAction!.NativeHeaderIndex, battle.ActiveAction.NativeHeaderIndex);
        Assert.Equal(beforeText.Events.Count(item => item.Kind == "ActionComplete"), battle.Events.Count(item => item.Kind == "ActionComplete"));
        Assert.Single(battle.Events, item => item.Kind == "NativeText");
        Assert.Equal(beforeText.Events.Count(item => item.Kind == "Damage"), battle.Events.Count(item => item.Kind == "Damage"));
        Assert.Equal(beforeText.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        for (int callback = 0; callback < 100 && (battle.ActiveAction is not null || battle.NativeGraphics.Descriptors.Queue.Any(slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104)); callback++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Assert.DoesNotContain(battle.NativeGraphics.Descriptors.Queue, slot => battle.NativeGraphics.Descriptors.Packets[slot].Command == 104);
        Assert.Null(battle.ActiveAction);
        Assert.Equal(beforeText.Events.Count(item => item.Kind == "ActionComplete") + 1, battle.Events.Count(item => item.Kind == "ActionComplete"));
        Assert.Equal(beforeText.Events.Count(item => item.Kind == "Damage"), battle.Events.Count(item => item.Kind == "Damage"));
        Assert.Equal(beforeText.Combatants.Select(actor => actor.CurrentHp), battle.Capture().Combatants.Select(actor => actor.CurrentHp));
    }

    [Fact]
    public void ResumeExplicitStandaloneGraphicsComponentWithoutRecreatingTheEncounter()
    {
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_GRAPHICS_COMPONENT_SNAPSHOT") is not string path)
        {
            return;
        }
        BattleSnapshot saved = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(path)) ?? throw new InvalidDataException("Standalone graphics component is empty.");
        BattleSession battle = BattleSession.Restore(saved);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(battle.Capture()));
        long callback = battle.NativeGraphics!.Callback;
        int texts = battle.Events.Count(item => item.Kind == "NativeText");
        battle.Tick();
        Assert.Equal(callback + 1, battle.NativeGraphics.Callback);
        Assert.Equal(texts, battle.Events.Count(item => item.Kind == "NativeText"));
    }

    internal static BattleSession Create(GameAssets game, BattleAssets assets, int model, bool ready = true, NativeBattleAiDefinition? ai = null, BattleEncounter? formation = null)
    {
        formation ??= assets.LoadEncounter(94);
        List<BattleCombatantDefinition> actors = [];
        foreach (int character in new[] { 0, 3, 4 })
        {
            actors.Add(new() { Id = $"party-{character}", Name = $"Character{character}", IsPlayer = true, ModelId = character, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == character).Id, InitialAtb = ready ? BattleSession.AtbMaximum : 0, NativeDeathCount = 0 });
        }
        actors.Add(new() { Id = "enemy", Name = "Enemy", Information246 = assets.ReadEnemyNativeInformation246(model), ModelId = model, Speed = 1, NativeAi = ai, NativeCombatantFlags = ai is null ? 0u : 0x10u, SceneSlot = formation.Enemies.First(enemy => enemy.ModelId == model).Slot });
        NativeBattleGraphicsDefinition graphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation);
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicFixture.Prepare(game, formation.Id);
        return new(new() { Name = "Installed raw entry component", EncounterId = formation.Id, StageId = formation.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeMusicProcess = musicEntry.MusicProcess, NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5), NativeGlobalSoundProcess = musicEntry.SoundProcess, NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeGraphics = graphics, NativeActions = assets.NativeActions.Where(action => action.Command == 8 && action.ActionId == 89).ToArray(), NativeCommandSequences = game.ReadNativeBattleCommandSequences(), NativeSavedOptions = game.ReadNewGameInputConfiguration().Options, NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap, NativeStageSource = assets.LoadStage(formation.StageId).NativeSource, NativeStageCameraBank = assets.LoadStage(formation.StageId).NativeCameraBank, NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera], NativeStageCameraControl = assets.LoadStage(formation.StageId).NativeCameraControl, Combatants = actors.ToArray() }, BattleAtbMode.TurnBased);
    }
}
