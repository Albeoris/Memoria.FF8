using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleWaterDepthContinuationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);

    [Fact]
    public void Selected_standalone_continues_its_actual_water_packets_and_releases_the_action()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_WATER_DEPTH_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_WATER_DEPTH_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(input) && !string.IsNullOrWhiteSpace(evidence) && !string.IsNullOrWhiteSpace(directory), "Select a standalone battle save, installed resources and an evidence directory.");
        Assert.True(File.Exists(input));
        Assert.True(Directory.Exists(directory));
        GameState? source = input!.EndsWith(".battle.json", StringComparison.OrdinalIgnoreCase) ? null : GameSession.Load(input);
        BattleSnapshot initial = source is null ? JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(input))! : Assert.IsType<BattleSnapshot>(source.Battle);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSession battle = Restore(initial);
        BattleScenePresentation scene = new(assets, initial.Definition, true);
        string output = Path.Combine(evidence!, "water-continuation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Water continuation evidence: " + output);
        BattleSnapshot before = initial;
        bool waterSeen = HasWater(initial);
        bool comparedMidpoint = false;
        int callbacks = 0;
        try
        {
            Publish("before", initial, true);
            if (initial.ActiveAction is null && initial.QueuedActions.Length == 0)
            {
                BattleCombatant actor = battle.Combatants.First(combatant => combatant.IsPlayer && combatant.ModelId == 0);
                BattleCombatant target = battle.Combatants.First(combatant => !combatant.IsPlayer && combatant.CurrentHp > 0);
                for (int ready = 0; ready < 512 && !battle.CanChoosePlayerCommand(actor.Id); ready++)
                {
                    battle.AdvanceTurn();
                }
                BattleActionDefinition water = initial.Definition.ActionDefinitions.First(action => action.Ability == BattleAbility.Magic && action.Name == "Water");
                battle.QueueAction(actor.Id, target.Id, BattleAbility.Magic, water.Id);
            }
            using StreamWriter trace = new(Path.Combine(output, "callbacks.jsonl")) { AutoFlush = true };
            for (; callbacks < 512; callbacks++)
            {
                before = battle.Capture();
                battle.AdvanceNativeGraphicsPresentation(Callback);
                BattleSnapshot current = battle.Capture();
                if (callbacks % 16 == 0)
                {
                    trace.WriteLine(JsonSerializer.Serialize(new { callbacks, current.Frame, NativeCallback = current.NativeGraphics?.Callback, Active = current.ActiveAction?.ActionId, Queue = current.QueuedActions.Length, Water = HasWater(current) }));
                }
                waterSeen |= HasWater(current);
                if (!comparedMidpoint && HasWater(current) && callbacks >= 65)
                {
                    CompareNext("midpoint", current);
                    comparedMidpoint = true;
                }
                if (waterSeen && comparedMidpoint && Released(current))
                {
                    CompareNext("completed", current);
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", SourceFrame = initial.Frame, SourceNativeCallback = initial.NativeGraphics?.Callback, current.Frame, NativeCallback = current.NativeGraphics?.Callback, Callbacks = callbacks + 1, waterSeen, comparedMidpoint }));
                    return;
                }
                if (!waterSeen && Released(current))
                {
                    battle.AdvanceTurn();
                }
            }
            Assert.Fail("The selected standalone must produce its actual water and release its action within 512 host callbacks.");
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
            Publish("before-boundary", before, false);
            Publish("failed-live", battle.Capture(), false);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", callbacks, before.Frame, NativeCallback = before.NativeGraphics?.Callback, waterSeen, comparedMidpoint, Error = error.ToString() }));
            throw;
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }

        void CompareNext(string name, BattleSnapshot snapshot)
        {
            string path = Publish(name, snapshot, true);
            BattleSession cold = Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path))!);
            BattleSession direct = Restore(snapshot);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            direct.AdvanceNativeGraphicsPresentation(Callback);
            cold.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            Assert.Equal(new BattleScenePresentation(assets, initial.Definition, true).Render(direct, 320, 224).ToArray(), new BattleScenePresentation(assets, initial.Definition, true).Render(cold, 320, 224).ToArray());
            Publish(name + "-next", cold.Capture(), true);
        }

        string Publish(string name, BattleSnapshot snapshot, bool render)
        {
            string path = Path.Combine(output, name + ".battle.json");
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(snapshot));
            Console.WriteLine($"{name}: {path}; field {source?.FieldId}; battle frame {snapshot.Frame}; native callback {snapshot.NativeGraphics?.Callback}");
            if (render)
            {
                byte[] pixels = scene.Render(Restore(snapshot), 320, 224).ToArray();
                using var bitmap = Memoria.FF8.GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 320, 224);
                bitmap.Save(Path.Combine(output, name + ".png"));
            }
            return path;
        }
    }

    private static bool HasWater(BattleSnapshot snapshot)
    {
        return snapshot.NativeGraphics?.OriginalEffect?.Binding.Water222Resources is not null && snapshot.NativeGraphics.OriginalEffect.ReturnedDescriptor != 0;
    }

    private static bool Released(BattleSnapshot snapshot)
    {
        return !HasWater(snapshot) && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeTimedAction is null && snapshot.NativeActionScratch?.ComponentCompletion is null && snapshot.NativeGraphics is { Requests.IsEmpty: true, Descriptors.Queue.IsEmpty: true, OriginalEffect.ReturnedDescriptor: 0 };
    }
}
