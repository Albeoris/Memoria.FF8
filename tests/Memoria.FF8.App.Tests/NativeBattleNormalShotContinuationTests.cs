using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleNormalShotContinuationTests
{
    [Fact]
    public void Selected_Normal_Shot_standalone_fires_and_closes_with_independent_restores()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SHOT_RESUME");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(source is not null && File.Exists(source) && directory is not null && Directory.Exists(directory), "Select a standalone Shot process and its installed original resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot input = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        BattleSession battle = Restore(input);
        BattleScenePresentation scene = new(assets, input.Definition, true);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "normal-shot-continuation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Normal Shot continuation evidence: " + output);
        File.WriteAllBytes(Path.Combine(output, "selected-source.battle.json"), File.ReadAllBytes(source!));
        BattleSnapshot before = battle.Capture();
        try
        {
            if (!Released())
            {
                Assert.Equal((byte)4, battle.GetNativeLimitLevel("party"));
                CompareNext("selected");
                if (battle.GetNativeShot("party")?.Phase is BattleNativeShotPhase.Opening)
                {
                    Until(() => battle.GetNativeShot("party")?.Phase == BattleNativeShotPhase.Ready);
                }
                if (battle.GetNativeShot("party")?.Phase is BattleNativeShotPhase.Ready && !HasDamage())
                {
                    Assert.True(battle.TriggerNativeShot("party"));
                    CompareNext("triggered");
                }
                Until(HasDamage);
                CompareNext("contact");
                Until(() => battle.GetNativeShot("party")?.Phase == BattleNativeShotPhase.Ready);
                CompareNext("ready-after-contact");
                battle.EndNativeShot("party");
                CompareNext("ending");
                Until(Released);
            }
            NativeBattleRestorationPresentationTests.Publish(output, "completed-before-host-mode", battle.Capture(), scene.Render(battle, 320, 224).ToArray());
            // This visual component has no enemy AI; select one real host poll instead of accelerating to another combatant's turn.
            battle.SetMode(BattleAtbMode.Normal);
            CompareNext("completed");
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", Source = source, InputMode = input.Mode, CompletionHostMode = battle.Mode, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", Source = source, Error = error.ToString() }));
            throw;
        }

        bool Released()
        {
            return battle.GetNativeShot("party") is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: 0 };
        }

        bool HasDamage()
        {
            return battle.Events.Any(entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy");
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }

        void Until(Func<bool> complete)
        {
            for (int callback = 0; callback < 4096 && !complete(); callback++)
            {
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(complete(), "The selected Shot process did not reach its next original callback boundary.");
        }

        void CompareNext(string name)
        {
            before = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output, name, before, scene.Render(battle, 320, 224).ToArray());
            BattleSession clone = Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(clone.Capture()));
            battle.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, input.Definition, true).Render(clone, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(output, name + "-next", battle.Capture(), null);
        }
    }
}
