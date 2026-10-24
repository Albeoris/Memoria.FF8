using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleZombieContinuationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);

    [Fact]
    public void Selected_standalone_continues_its_actual_zombie_packets_and_releases_the_action()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_ZOMBIE_CONTINUATION_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_ZOMBIE_CONTINUATION_EVIDENCE");
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
        string output = Path.Combine(evidence!, "zombie-continuation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Zombie continuation evidence: " + output);
        BattleSnapshot before = initial;
        bool zombieSeen = HasZombie(initial);
        bool comparedMidpoint = false;
        int callbacks = 0;
        try
        {
            Publish("before", initial, true);
            if (!zombieSeen && initial.QueuedActions.Length == 0 && initial.NativeCommandBanks?.BoundCommands.Any() != true)
            {
                for (int pending = 0; pending < 512 && !Released(battle.Capture()); pending++)
                {
                    battle.AdvanceNativeGraphicsPresentation(Callback);
                }
                Assert.True(Released(battle.Capture()), "The preceding action must release before admission.");
                BattleCombatant actor = battle.Combatants.First(combatant => combatant.IsPlayer && battle.GetAvailableMagicCasts(combatant.Id, initial.Definition.ActionDefinitions.First(action => action.Name == "Zombie").Id) > 0);
                BattleCombatant target = battle.Combatants.First(combatant => !combatant.IsPlayer && combatant.CurrentHp > 0);
                for (int ready = 0; ready < 512 && !battle.CanChoosePlayerCommand(actor.Id); ready++)
                {
                    battle.AdvanceTurn();
                    battle.AdvanceNativeGraphicsPresentation(Callback);
                }
                BattleActionDefinition zombie = initial.Definition.ActionDefinitions.First(action => action.Ability == BattleAbility.Magic && action.Name == "Zombie");
                Assert.True(battle.CanChoosePlayerCommand(actor.Id));
                battle.SelectPlayerCommandActor(actor.Id);
                battle.QueueAction(actor.Id, target.Id, BattleAbility.Magic, zombie.Id);
                Publish("admitted", battle.Capture(), false);
            }
            using StreamWriter trace = new(Path.Combine(output, "callbacks.jsonl")) { AutoFlush = true };
            for (; callbacks < 512; callbacks++)
            {
                before = battle.Capture();
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                battle.AdvanceNativeGraphicsPresentation(Callback);
                Console.WriteLine($"callback {callbacks}: {System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds:F2}ms; {(GC.GetAllocatedBytesForCurrentThread() - allocated) / 1048576.0:F2}MB; effect {battle.NativeGraphics?.OriginalEffect?.Binding.Effect}");
                BattleSnapshot current = battle.Capture();
                if (callbacks % 16 == 0)
                {
                    trace.WriteLine(JsonSerializer.Serialize(new { callbacks, current.Frame, NativeCallback = current.NativeGraphics?.Callback, Active = current.ActiveAction?.ActionId, Queue = current.QueuedActions.Length, Zombie = HasZombie(current) }));
                }
                zombieSeen |= HasZombie(current);
                if (!comparedMidpoint && HasZombie(current) && callbacks >= 95)
                {
                    CompareNext("midpoint", current);
                    comparedMidpoint = true;
                }
                if (zombieSeen && comparedMidpoint && Released(current))
                {
                    CompareNext("completed", current);
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", SourceFrame = initial.Frame, SourceNativeCallback = initial.NativeGraphics?.Callback, current.Frame, NativeCallback = current.NativeGraphics?.Callback, Callbacks = callbacks + 1, zombieSeen, comparedMidpoint }));
                    return;
                }
                if (!zombieSeen && Released(current))
                {
                    battle.AdvanceTurn();
                }
            }
            Assert.Fail("The selected standalone must produce its actual zombie and release its action within 512 host callbacks.");
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
            Publish("before-boundary", before, false);
            Publish("failed-live", battle.Capture(), false);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", callbacks, before.Frame, NativeCallback = before.NativeGraphics?.Callback, zombieSeen, comparedMidpoint, Error = error.ToString() }));
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

    private static bool HasZombie(BattleSnapshot snapshot)
    {
        return snapshot.NativeGraphics?.OriginalEffect?.Binding.ZombieResources is not null && snapshot.NativeGraphics.OriginalEffect.ReturnedDescriptor != 0;
    }

    private static bool Released(BattleSnapshot snapshot)
    {
        return !HasZombie(snapshot) && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeCommandBanks?.BoundCommands.Any() != true && snapshot.NativeTimedAction is null && snapshot.NativeActionScratch?.ComponentCompletion is null && snapshot.NativeGraphics is { Requests.IsEmpty: true, Descriptors.Queue.IsEmpty: true, OriginalEffect.ReturnedDescriptor: 0 };
    }
}
