using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleArmorShotCastTests
{
    [Fact]
    public void Installed_Armor_Shot_opens_fires_and_closes_with_independent_standalones()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 2, 197) with { Items = [new(107, 2)], NativeShotAvailability = game.ReadNewGameShotAvailability() };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "armor-shot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Armor Shot evidence: " + output);
        BattleSnapshot before = battle.Capture();
        try
        {
            Until(() => battle.CanChoosePlayerCommand("party"));
            battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("party", 4)] });
            Publish("before-command");
            battle.QueueNativePlayerCommand("party", "enemy", 14, 6);
            CompareNext("queued");
            Until(() => battle.GetNativeShot("party")?.Phase == BattleNativeShotPhase.Ready);
            Assert.Equal(9999, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
            Assert.Equal((ushort)197, battle.NativeGraphics!.OriginalEffect!.Binding.Effect);
            CompareNext("opening-ready");
            Assert.Equal((byte)4, battle.GetNativeLimitLevel("party"));
            Assert.True(battle.TriggerNativeShot("party"));
            Assert.Equal(1, battle.Items.Single(item => item.Id == 107).Count);
            CompareNext("shot-queued");
            Until(() => battle.Events.Any(entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy"));
            CompareNext("shot-contact");
            Until(() => battle.GetNativeShot("party")?.Phase == BattleNativeShotPhase.Ready);
            battle.EndNativeShot("party");
            CompareNext("ending-queued");
            Until(() => battle.GetNativeShot("party") is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: 0 });
            Assert.Single(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy");
            Assert.Equal(1, battle.Items.Single(item => item.Id == 107).Count);
            Publish("completed-before-host-mode");
            // This visual component has no enemy AI; select one real host poll after releasing the Limit.
            battle.SetMode(BattleAtbMode.Normal);
            CompareNext("completed");
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", CompletionHostMode = battle.Mode, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", before.Frame, NativeCallback = before.NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }

        void Until(Func<bool> complete)
        {
            for (int callback = 0; callback < 4096 && !complete(); callback++)
            {
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(complete());
        }

        void Publish(string name)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, battle.Capture(), scene.Render(battle, 320, 224).ToArray());
        }

        void CompareNext(string name)
        {
            Publish(name);
            BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, name + ".battle.json")))!);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            before = battle.Capture();
            battle.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
            Publish(name + "-next");
        }
    }
}
