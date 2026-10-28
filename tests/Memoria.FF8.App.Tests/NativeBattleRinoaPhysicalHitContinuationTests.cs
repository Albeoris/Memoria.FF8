using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRinoaPhysicalHitContinuationTests
{
    [Fact]
    public void Standalone_attack_runs_original_projectile_contact_and_independent_json_continuation()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_RINOA_PHYSICAL_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(source is not null && File.Exists(source) && directory is not null && Directory.Exists(directory), "Select the original resources and the standalone before the physical attack.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot input = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(source!))!;
        BattleSession battle = Restore(input);
        BattleScenePresentation scene = new(assets, input.Definition, true);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_RINOA_PHYSICAL_EVIDENCE") ?? Path.GetTempPath(), "rinoa-physical-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Physical attack evidence: " + output);
        bool root = false;
        bool projectile = false;
        bool sparks = false;
        bool ribbon = false;
        bool restoredFlight = false;
        BattleSnapshot before = input;
        int initialHp = battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp;
        try
        {
            for (int wait = 0; wait < 400 && !battle.CanChoosePlayerCommand("party"); wait++)
            {
                battle.Tick();
            }
            Assert.True(battle.CanChoosePlayerCommand("party"));
            battle.QueueAttack("party", "enemy");
            Save("queued");
            for (int callback = 0; callback < 600; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                NativeBattleModuleState modules = battle.NativeGraphics!.ModuleEffects;
                root |= modules.Slots.Any(task => task.Callback == 0x56F9C0 && (task.Flags & 1) != 0);
                projectile |= modules.Slots.Any(task => task.Callback == 0x56FE10 && (task.Flags & 1) != 0);
                sparks |= modules.Slots.Any(task => task.Callback == 0x56FCD0 && (task.Flags & 1) != 0);
                ribbon |= battle.NativeGraphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Opcode == 0x3A);
                NativeBattleModuleTask? flying = modules.Slots.FirstOrDefault(task => task.Callback == 0x56FE10 && (task.Flags & 1) != 0 && BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan()) >= 15);
                if (!restoredFlight && flying is not null)
                {
                    CompareNext("flight");
                    restoredFlight = true;
                }
                if (root && modules.Head is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Capture().NativeClock!.ActionPause == 0)
                {
                    Assert.True(projectile);
                    Assert.True(sparks);
                    Assert.True(ribbon);
                    Assert.True(restoredFlight);
                    Assert.True(battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp < initialHp);
                    Assert.Equal(input.NativeGraphics!.Models[0].Flags0 & 0x200, battle.NativeGraphics.Models[0].Flags0 & 0x200);
                    CompareNext("completed");
                    for (int wait = 0; wait < 400 && !battle.CanChoosePlayerCommand("party"); wait++)
                    {
                        battle.Tick();
                    }
                    Assert.True(battle.CanChoosePlayerCommand("party"));
                    battle.QueueAttack("party", "enemy");
                    bool repeatStarted = false;
                    bool repeatCompleted = false;
                    for (int wait = 0; wait < 600; wait++)
                    {
                        before = battle.Capture();
                        battle.Tick();
                        repeatStarted |= battle.NativeGraphics!.ModuleEffects.Slots.Any(task => task.Callback == 0x56F9C0 && (task.Flags & 1) != 0);
                        if (repeatStarted && battle.NativeGraphics.ModuleEffects.Head is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Capture().NativeClock!.ActionPause == 0)
                        {
                            CompareNext("repeat-completed");
                            repeatCompleted = true;
                            break;
                        }
                    }
                    Assert.True(repeatCompleted);
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", battle.Frame, Callback = battle.NativeGraphics.Callback, root, projectile, sparks, ribbon, restoredFlight }));
                    return;
                }
            }
            Assert.Fail("The original physical attack did not release its action and shared callbacks.");
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            Save("failed-live");
            throw;
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }
        void Save(string name) => NativeBattleRestorationPresentationTests.Publish(output, name, battle.Capture(), scene.Render(battle, 320, 224).ToArray());
        void CompareNext(string name)
        {
            Save(name);
            BattleSession clone = Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            battle.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, input.Definition, true).Render(clone, 320, 224).ToArray());
            Save(name + "-next");
        }
    }
}
