using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEscapePresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Escape_clock_obeys_native_action_and_allocation_pauses(bool allocationPaused)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot ready = CreateReady(game, assets).Capture();
        NativeBattleClockState clock = Assert.IsType<NativeBattleClockState>(ready.NativeClock);
        NativeBattleAllocationControl control = Assert.IsType<NativeBattleAllocationControl>(ready.NativeScriptState.AllocationControl);
        BattleSession battle = BattleSession.Restore(ready with
        {
            NativeClock = clock with { ActionPause = allocationPaused ? 0u : 1u },
            NativeScriptState = ready.NativeScriptState with { EscapeDisabled = true, AllocationControl = control with { PauseGate = allocationPaused } }
        });
        int[] gauges = battle.Combatants.Select(actor => actor.NativeAtb!.Current).ToArray();
        battle.PublishNativeEscapeButtons(3);
        for (int callback = 0; callback < 8; callback++)
        {
            battle.Tick();
        }
        Assert.Equal(gauges, battle.Combatants.Select(actor => actor.NativeAtb!.Current).ToArray());
        Assert.Equal(BattleAtbMode.TurnBased, battle.Mode);
        battle.PublishNativeEscapeButtons(0);
        BattleSnapshot released = battle.Capture();
        battle = BattleSession.Restore(released with
        {
            NativeClock = released.NativeClock! with { ActionPause = 0 },
            NativeScriptState = released.NativeScriptState with { AllocationControl = control with { PauseGate = false } }
        });
        battle.Tick();
        Assert.Equal(gauges, battle.Combatants.Select(actor => actor.NativeAtb!.Current).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_escape_hold_and_release_drive_standing_sequences_and_resume_from_json(bool escapeDisabled)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateReady(game, assets);
        for (int callback = 0; callback < 120 && (battle.NativeGraphics!.Models.Take(3).Any(actor => actor.Sequence != 1) || !battle.NativeGraphics.Descriptors.Queue.IsEmpty); callback++)
        {
            battle.Tick();
        }
        Assert.All(battle.NativeGraphics!.Models.Take(3), actor => Assert.Equal((byte?)1, actor.Sequence));
        Assert.Empty(battle.NativeGraphics.Descriptors.Queue);
        BattleSnapshot ready = battle.Capture();
        battle = BattleSession.Restore(ready with { NativeScriptState = ready.NativeScriptState with { EscapeDisabled = escapeDisabled } });
        Assert.Equal(escapeDisabled, battle.EscapeDisabled);
        byte?[] standing = battle.NativeGraphics!.Models.Take(3).Select(actor => actor.Sequence).ToArray();

        battle.PublishNativeEscapeButtons(3);
        Assert.False(battle.Escape.PresentationHeld);
        battle.Tick();
        Assert.True(battle.Escape.PresentationHeld);
        Assert.All(battle.NativeGraphics.Models.Take(3), actor => Assert.Equal(0x1000000u, actor.Flags8!.Value & 0x1000000u));
        for (int callback = 0; callback < 30 && battle.NativeGraphics.Models.Take(3).Any(actor => actor.Sequence != 16); callback++)
        {
            battle.Tick();
        }
        Assert.All(battle.NativeGraphics.Models.Take(3), actor => Assert.Equal((byte?)16, actor.Sequence));

        BattleSession restored = RoundTrip(battle);
        Assert.True(restored.Escape.PresentationHeld);
        int frame = battle.NativeGraphics.Models[0].SequenceFrame;
        bool advanced = false;
        for (int callback = 0; callback < 8; callback++)
        {
            battle.PublishNativeEscapeButtons(3);
            battle.Tick();
            restored.Tick();
            advanced |= battle.NativeGraphics.Models[0].SequenceFrame != frame;
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Assert.True(advanced);

        battle.PublishNativeEscapeButtons(0);
        Assert.False(battle.Escape.Held);
        Assert.True(battle.Escape.PresentationHeld);
        restored = RoundTrip(battle);
        Assert.True(restored.Escape.PresentationHeld);
        battle.Tick();
        restored.Tick();
        Assert.False(battle.Escape.PresentationHeld);
        Assert.All(battle.NativeGraphics.Models.Take(3), actor => Assert.Equal(0u, actor.Flags8!.Value & 0x1000000u));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        for (int callback = 0; callback < 30 && battle.NativeGraphics.Models.Take(3).Where((actor, index) => actor.Sequence != standing[index]).Any(); callback++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(standing, battle.NativeGraphics.Models.Take(3).Select(actor => actor.Sequence).ToArray());
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void Original_non_boss_entry_escape_message_and_fade_resume_from_independent_json()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateReady(game, assets);
        BattleDefinition definition = battle.Capture().Definition;
        Assert.False(battle.EscapeDisabled);
        BattleSnapshot ready = battle.Capture();
        battle = BattleSession.Restore(ready with { Random = NativeBattleRandomState.ForecastPhase(1) });
        battle.PublishNativeEscapeButtons(3);
        for (int frame = 0; frame < 30; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(30, battle.Escape.HeldFrames);
        BattleSession restored = RoundTrip(battle);
        for (int frame = 0; frame < 30; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal((byte?)2, battle.NativeResult);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        restored = RoundTrip(battle);
        for (int callback = 0; callback < 1200 && battle.Outcome == BattleOutcome.Active; callback++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(BattleOutcome.Escaped, battle.Outcome);
        Assert.True(battle.CanReturnToField);
        Assert.Equal((byte?)0, battle.NativeGraphics!.NativeOutcome!.FadeDurationB0C);
        Assert.Equal((byte?)2, battle.NativeResult);
        Assert.Contains(battle.Events, item => item.Kind == "NativeText" && item.Message == definition.NativeGlobalMessages[1]);
        Assert.Single(battle.Events, item => item.Kind == "Outcome");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static BattleSession CreateReady(GameAssets game, BattleAssets assets)
    {
        BattleEncounter formation = assets.LoadEncounter(512);
        int model = formation.Enemies.First().ModelId;
        BattleSession seed = NativeBattleGraphicsEntryTests.Create(game, assets, model, formation: formation);
        BattleDefinition definition = seed.Capture().Definition with
        {
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeMessageSpeed = game.ReadNewGameInputConfiguration().BattleMessageSpeed,
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte,
            NativeStatusRules = assets.ReadNativeStatusRules(game.ReadNewGameInputConfiguration().NativeBattleSpeedByte!.Value),
            PersistentAiVariables = new uint[8],
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            Combatants = seed.Capture().Definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { NativeTemplate = assets.ReadEnemyTemplate(model, 1), NativeAi = assets.ReadEnemyAi(model, actor.Level) }).ToArray()
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 1200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        return battle;
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }
}
