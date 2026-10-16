using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGraphicsEntryTests
{
    [Fact]
    public void OriginalBalambAndIfritEntryProgramsResumeTheirModelsWhileTurnBasedGameplayWaits()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        foreach (int model in assets.LoadEncounter(512).Enemies.Select(enemy => enemy.ModelId).Append(61).Distinct())
        {
            BattleSession battle = Create(game, assets, model);
            Assert.Throws<InvalidOperationException>(() => battle.QueueAttack("party-0", "enemy"));
            Assert.Equal(13ul, battle.Capture().NativeGlobalRandomCalls);
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
            Assert.Equal((byte)255, battle.NativeGraphics.EntryBarrierCompletion);
            Assert.All(battle.NativeGraphics.Models.Where(row => (row.Flags0 & 2) != 0), row => Assert.Equal((byte)0, row.Kind5));
            Assert.Equal(0, battle.Frame);
        }
    }

    [Fact]
    public void HeadlessEntryDoesNotAdvanceGameplayOrAtbBeforeItsActualBarrierCompletes()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
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
        Assert.Equal(1, battle.Frame);
    }

    [Fact]
    public void PresentationClockSurvivesRestoreWithoutPersistingTheHostTransportPolicy()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 61);
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(500_000));
        Assert.Equal(1, battle.NativeGraphics!.Callback);
        Assert.Equal(5_000_000, battle.NativeGraphics.PresentationSubticks);
        battle.Tick();
        Assert.Equal(1, battle.NativeGraphics.Callback);
        BattleSnapshot saved = battle.Capture();
        BattleSession headless = BattleSession.Restore(saved);
        headless.Tick();
        Assert.Equal(2, headless.NativeGraphics!.Callback);
        BattleSession hosted = BattleSession.Restore(saved);
        hosted.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(500_000));
        Assert.Equal(3, hosted.NativeGraphics!.Callback);
        hosted.Tick();
        Assert.Equal(3, hosted.NativeGraphics.Callback);
        Assert.Equal(0, hosted.Frame);
    }

    [Fact]
    public void OriginalIfritMessageSequenceOwnsTextAndAaReceiptDuringGameplayWaitAcrossRestore()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
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
        battle = BattleSession.Restore(beforeText with { NativeScriptState = beforeText.NativeScriptState with { WaitCounters = [40] } });
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
        Assert.Equal(BattleActionPhase.Windup, battle.ActiveAction!.Phase);
        Assert.Single(battle.Events, item => item.Kind == "NativeText");
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
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

    private static BattleSession Create(GameAssets game, BattleAssets assets, int model, bool ready = true, NativeBattleAiDefinition? ai = null)
    {
        List<BattleCombatantDefinition> actors = [];
        foreach (int character in new[] { 0, 3, 4 })
        {
            actors.Add(new() { Id = $"party-{character}", Name = $"Character{character}", IsPlayer = true, ModelId = character, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == character).Id, InitialAtb = ready ? BattleSession.AtbMaximum : 0, NativeDeathCount = 0 });
        }
        actors.Add(new() { Id = "enemy", Name = "Enemy", ModelId = model, Speed = 1, NativeAi = ai });
        ImmutableArray<NativeBattleGraphicsBinding> bindings = actors.Select(actor => new NativeBattleGraphicsBinding(actor.Id, checked((byte)(actor.IsPlayer ? actor.ModelId : actor.ModelId + 16)), assets.ReadNativeGraphicsResource(actor))).ToImmutableArray();
        NativeBattleGraphicsDefinition graphics = new(game.ReadNativeBattleGraphicsRules(), bindings, new[] { "enemy" }.Concat(actors.Where(actor => actor.IsPlayer).Select(actor => actor.Id)).ToImmutableArray());
        return new(new() { Name = "Installed raw entry component", NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeGraphics = graphics, NativeActions = assets.NativeActions.Where(action => action.Command == 8 && action.ActionId == 89).ToArray(), NativeCommandSequences = game.ReadNativeBattleCommandSequences(), NativeStageCameraBank = new([0, 0]), NativeStageCameraSelectors = [0, 0], NativeStageCameraControl = [2], Combatants = actors.ToArray() }, BattleAtbMode.TurnBased);
    }
}
