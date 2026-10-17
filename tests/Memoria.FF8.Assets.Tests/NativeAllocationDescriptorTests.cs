using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAllocationDescriptorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitDebugOutcomeAtFrameZeroSurvivesRestoreWithoutCompletingNativeEntry(bool victory)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, [0]);
        NativeBattleGraphicsState before = battle.NativeGraphics!;
        Assert.False(battle.NativeEntryReady);
        if (victory)
        {
            battle.InstantVictory();
        }
        else
        {
            battle.InstantDefeat();
        }
        BattleOutcome expected = victory ? BattleOutcome.Victory : BattleOutcome.Defeat;
        Assert.Equal(expected, battle.Outcome);
        Assert.Equal(expected, battle.DebugOutcome);
        Assert.Equal(0, battle.Frame);
        Assert.False(battle.NativeEntryReady);
        Assert.Equal(before.EntryLifecycle, battle.NativeGraphics!.EntryLifecycle);
        Assert.Equal(before.Callback, battle.NativeGraphics.Callback);
        string json = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture()));
        Assert.False(restored.NativeEntryReady);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(battle.Capture() with { DebugOutcome = BattleOutcome.Active }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(battle.Capture() with { DebugOutcome = null }));
    }

    [Fact]
    public void NativeZeroCountdownRemainsAnActiveStandaloneBoundaryUntilTheNextOuterFrameReturns()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, [0]);
        for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot before = battle.Capture();
        NativeBattleGraphicsState graphics = before.NativeGraphics! with { NativeOutcome = new() { ResultByte6E7 = 4, VictoryQueuedDFE = 1, FadeDurationB0C = 0 } };
        battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before with { NativeGraphics = graphics }))!);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        long callbackBefore = battle.NativeGraphics!.Callback;
        battle.Tick();
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(callbackBefore, battle.NativeGraphics!.Callback);
        Assert.Equal(before.Frame, battle.Frame);
        Assert.Single(battle.Events, item => item.Kind == "Outcome");
        string completed = JsonSerializer.Serialize(battle.Capture());
        battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(completed)!);
        Assert.Equal(completed, JsonSerializer.Serialize(battle.Capture()));
        battle.Tick();
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(callbackBefore, battle.NativeGraphics!.Callback);
        Assert.Single(battle.Events, item => item.Kind == "Outcome");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeVictoryAdmissionReadsSavedKoOrPetrifyInsteadOfEjectionOrUntargetability(bool untargetable)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, [0]);
        for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot snapshot = battle.Capture();
        BattleCombatantSnapshot enemy = snapshot.Combatants.Single(actor => actor.Id == "caller");
        NativeBattleParticipantSnapshot participant = enemy.NativeParticipant!;
        enemy = untargetable
            ? enemy with { NativeParticipant = participant with { Untargetable = true, NativeCombatantFlags = participant.NativeCombatantFlags!.Value | 0x40 } }
            : enemy with { NativeStatus = enemy.NativeStatus! with { Battle = enemy.NativeStatus.Battle | 0x10000 } };
        BattleCombatantSnapshot[] actors = snapshot.Combatants.Select(actor => actor.Id == enemy.Id ? enemy : actor).ToArray();
        battle = BattleSession.Restore(snapshot with { Combatants = actors });
        battle.Tick();
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Null(battle.NativeGraphics!.NativeOutcome);
        Assert.DoesNotContain(battle.NativeGraphics.Descriptors.Packets, packet => packet.Command == 115);
    }

    [Fact]
    public void ActualOutcomeCallbackClearsC2AndSubsequentUiServicesCannotGrowParticipantGauges()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, [0]);
        for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot snapshot = battle.Capture();
        Assert.Equal((byte)1, snapshot.NativeClock!.Active);
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        graphics = graphics with { NativeOutcome = new() { ResultByte6E7 = 4 }, Descriptors = graphics.Descriptors.Enqueue(10, 128, 0x47DF60).State };
        battle = BattleSession.Restore(snapshot with { NativeGraphics = graphics });
        for (int callback = 0; callback < 10 && battle.NativeGraphics!.NativeOutcome!.AdmissionDFD != 1; callback++)
        {
            battle.Tick();
        }
        Assert.Equal((byte?)1, battle.NativeGraphics!.NativeOutcome!.AdmissionDFD);
        Assert.Equal((byte)0, battle.Capture().NativeClock!.Active);
        int?[] gauges = battle.Capture().Combatants.Select(actor => actor.NativeAtb?.Current).ToArray();
        battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        battle.Tick();
        Assert.Equal(gauges, battle.Capture().Combatants.Select(actor => actor.NativeAtb?.Current).ToArray());
        Assert.Equal((byte)0, battle.Capture().NativeClock!.Active);
    }

    [Fact]
    public void DeclaredPartyKoAdmitsDefeatAndWaitsForTheActualSharedExitCountdownBeforeTerminalCleanup()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, [0]);
        for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleCombatant player = battle.Combatants.Single(actor => actor.IsPlayer);
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.CurrentHp))!.SetValue(player, 0);
        battle.Tick();
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal((byte?)1, battle.NativeResult);
        Assert.Equal((byte?)3, battle.NativeGraphics!.NativeOutcome!.FadeStyleE01);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Outcome");
        long combatFrame = battle.Frame;
        battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int callback = 0; callback < 150 && battle.NativeGraphics!.NativeOutcome!.FadeDurationB0C != 0; callback++)
        {
            battle.Tick();
        }
        Assert.Equal((byte?)0, battle.NativeGraphics!.NativeOutcome!.FadeDurationB0C);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(combatFrame, battle.Frame);
        Assert.Contains(battle.Events, item => item.Kind == "NativeText" && item.Message == assets.ReadNativeGlobalBattleMessages()[0]);
        battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        long graphicsFrame = battle.NativeGraphics!.Callback;
        battle.Tick();
        Assert.Equal(BattleOutcome.Defeat, battle.Outcome);
        Assert.Equal((byte?)1, battle.NativeResult);
        Assert.Equal(graphicsFrame, battle.NativeGraphics!.Callback);
        Assert.Single(battle.Events, item => item.Kind == "Outcome");
        Assert.Empty(battle.QueuedActions);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Assert.Equal(BattleOutcome.Defeat, restored.Outcome);
    }

    [Fact]
    public void GuardianCancellation114ClearsTheGraphicsCastingFlagAndRefreshesThePendingStandingSequence()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, [0]);
        for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot snapshot = battle.Capture();
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        NativeBattleGraphicsModel model = graphics.Models[0];
        model = model with { Flags8 = 0x800000, Flags2 = (ushort)(model.Flags2!.Value | 4), Sequence = 19, SequenceState = model.SequenceState! with { Pending3 = 2 } };
        graphics = graphics with { Models = graphics.Models.SetItem(0, model), Descriptors = graphics.Descriptors.Enqueue(114, 128, 240, 0).State };
        battle = BattleSession.Restore(snapshot with { NativeGraphics = graphics });
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(333_334));
        Assert.Equal(0u, battle.NativeGraphics!.Models[0].Flags8!.Value & 0x800000u);
        Assert.Equal((byte)1, battle.NativeGraphics.Models[0].Sequence);
        Assert.Equal((byte)0, battle.NativeGraphics.Models[0].SequenceState!.Pending3);
    }

    [Theory]
    [InlineData(31, true)]
    [InlineData(52, false)]
    [InlineData(53, true)]
    [InlineData(59, true)]
    public void AllocationThenScriptedExitWaitsForTheActual113StatisticsAnd4876B0Gate(byte opcode, bool activated)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        byte[] program = opcode switch { 53 => [52, 6, 53, 209, 57, 0], 59 => [59, 6, 4, 57, 0], _ => [opcode, 6, 57, 0] };
        BattleSession battle = Create(game, assets, program);
        for (int callback = 0; callback < 300 && battle.Capture().NativeAllocations.Length == 0; callback++)
        {
            battle.Tick();
        }
        BattleSnapshot pending = battle.Capture();
        Assert.True(pending.NativeScriptState.ScriptedExitRequested);
        Assert.True(pending.NativeScriptState.AllocationControl!.ActionGate);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        BattleCombatantSnapshot allocation = pending.Combatants.Single(actor => actor.Id == "native-allocation-1");
        Assert.True(allocation.NativeParticipant!.AllocationPendingStatistics);
        Assert.Equal((uint?)0, allocation.NativeParticipant.NativeCombatantFlags);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Activated");
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(pending))!);
        NativeBattleRandomState before = pending.Random!;
        NativeBattleRandomState expected = before.Draw(out _);
        bool sawStatistics = false;
        for (int callback = 0; callback < 100 && battle.Outcome == BattleOutcome.Active; callback++)
        {
            battle.Tick();
            restored.Tick();
            BattleSnapshot current = battle.Capture();
            Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(restored.Capture()));
            sawStatistics |= !current.Combatants.Single(actor => actor.Id == allocation.Id).NativeParticipant!.AllocationPendingStatistics;
            if (current.NativeScriptState.AllocationControl!.ActionGate)
            {
                Assert.Equal(BattleOutcome.Active, battle.Outcome);
            }
        }
        Assert.True(sawStatistics);
        Assert.Equal(BattleOutcome.ScriptedExit, battle.Outcome);
        Assert.False(battle.NativeScriptState.AllocationControl!.ActionGate);
        Assert.False(battle.NativeScriptState.AllocationControl.PauseGate);
        Assert.Equal(activated, battle.Events.Any(entry => entry.Kind == "Activated"));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal(5, battle.Combatants.Single(actor => actor.Id == allocation.Id).Level);
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, byte[] init)
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        NativeBattleAiDefinition caller = new([init, [0], [0], [0], [0]], abilities, 0, []);
        NativeBattleAiDefinition spawned = new([[0], [0], [0], [0], [0]], abilities, 0, []);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, Speed = 1 },
            new() { Id = "caller", Name = "Caller", ModelId = 61, SceneSlot = 0, Speed = 1, NativeCombatantFlags = 0x10, NativeAi = caller },
            new() { Id = "prototype", Name = "Bite Bug", ModelId = 21, SceneSlot = 6, InitialActive = false, Speed = 1, NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(21), NativeAi = spawned, NativeTemplate = assets.ReadEnemyTemplate(21, 5) }
        ];
        NativeBattleGraphicsDefinition graphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(94));
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicFixture.Prepare(game, 94);
        BattleDefinition definition = new() { EncounterId = 94, StageId = 0, NativeMusicProcess = musicEntry.MusicProcess, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5), NativeGlobalSoundProcess = musicEntry.SoundProcess, NativeClockRules = assets.ReadNativeClockRules(), NativeBattleSpeed = 0, NativeFlags = 0x80, NativeMessageSpeed = game.ReadNewGameInputConfiguration().BattleMessageSpeed, NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(), NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeCommandSequences = game.ReadNativeBattleCommandSequences(), NativeStageSource = assets.LoadStage(0).NativeSource, NativeStageCameraBank = assets.LoadStage(0).NativeCameraBank, NativeStageCameraSelectors = [0, 0], NativeStageCameraControl = assets.LoadStage(0).NativeCameraControl, NativeGraphics = graphics, Combatants = actors };
        return new(definition, BattleAtbMode.TurnBased);
    }
}
