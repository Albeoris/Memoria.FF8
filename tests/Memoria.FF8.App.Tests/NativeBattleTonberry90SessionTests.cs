using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
    [Fact]
    public void Installed_Tonberry_ordinary_entry_and_boot_preserve_independent_disk_state()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, 14, 90);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_GUARDIAN_EVIDENCE") ?? Path.GetTempPath(), "tonberry-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        BattleSnapshot before = battle.CaptureForContinuation();
        try
        {
            for (int tick = 0; tick < 400 && (!battle.NativeEntryReady || !battle.Combatants.Single(actor => actor.Id == "party").IsReady); tick++)
            {
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.NativeEntryReady);
            battle.QueueAction("party", "enemy", BattleAbility.GuardianForce, 14);
            bool entered = false;
            for (int tick = 0; tick < 400; tick++)
            {
                before = battle.Capture();
                battle.Tick();
                if (battle.Presentation?.Graphics?.OriginalEffect is { ReturnedDescriptor: > 0, Binding.Effect: 90 })
                {
                    entered = true;
                    break;
                }
            }
            Assert.True(entered);
            BattleSnapshot entry = battle.CaptureForContinuation();
            Assert.Equal(7, entry.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Pools.Length);
            Assert.NotNull(entry.ReadFixturePresentation().NativeEffectMemory!.Tonberry90);
            Publish("entry", entry);
            BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, "entry.battle.json")))!);
            Assert.Equal(JsonSerializer.Serialize(entry), JsonSerializer.Serialize(clone.Capture()));
            bool booted = false;
            TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
            for (int tick = 0; tick < 10; tick++)
            {
                before = battle.Capture();
                battle.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                clone.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                NativeBattleTonberry90State shared = battle.Presentation!.Memory.Tonberry90!;
                uint director = shared.DirectorAddress1547168!.Value;
                if (shared.ReadWord(director + 0x42) == 1 && shared.ReadWord(director + 0x40) == 0)
                {
                    booted = true;
                    break;
                }
            }
            Assert.True(booted);
            BattleSnapshot boot = battle.CaptureForContinuation();
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition).Render(clone, 320, 224).ToArray());
            Assert.Equal(0x762DA0u, boot.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Pools[4].Slots[0].Callback);
            Assert.Equal(0x762850u, boot.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Pools[6].Slots[0].Callback);
            Assert.NotNull(boot.ReadFixturePresentation().NativeEffectMemory!.SharedEffectView2793E58);
            Publish("boot", boot);
            bool capturedGround = false;
            bool drewActor = false;
            string boundary = "";
            for (int tick = 0; tick < 512; tick++)
            {
                before = battle.Capture();
                try
                {
                    battle.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                }
                catch (NotSupportedException error) when (error.Message == "Native Tonberry creature initialization requires its complete selected actor contract.")
                {
                    NotSupportedException independent = Assert.Throws<NotSupportedException>(() => clone.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
                    Assert.Equal(error.Message, independent.Message);
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                    Assert.True(capturedGround, "The actual stage-ground capture must precede the next creature initialization boundary.");
                    boundary = "phase-two-creature-initialization";
                    Publish("phase-one-before-creature", before);
                    break;
                }
                clone.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                BattleSnapshot actual = battle.CaptureForContinuation();
                Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(clone.Capture()));
                capturedGround |= actual.ReadFixturePresentation().NativeEffectMemory!.GroundCapture is { Statics.Regions.IsEmpty: false };
                drewActor = actual.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Pools[3].Slots.Any(task => task.Callback == 0x763EB0 && task.ReadDword(0x98) > 0);
                if (drewActor)
                {
                    Assert.True(capturedGround);
                    boundary = "phase-one-first-animated-draw";
                    Publish("phase-one-first-draw", actual);
                    break;
                }
            }
            Assert.True(drewActor || boundary == "phase-two-creature-initialization", "Tonberry must reach its actual animated actor draw or the next explicit creature contract boundary.");
            string name = drewActor ? "phase-one-first-draw" : "phase-one-before-creature";
            BattleSnapshot retained = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, name + ".battle.json")))!;
            BattleSession restored = BattleSession.Restore(retained);
            Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(BattleSession.Restore(retained), 320, 224).ToArray(), new BattleScenePresentation(assets, definition).Render(restored, 320, 224).ToArray());
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "entry-boot-and-phase-one-validated", retained.Frame, NativeCallback = retained.ReadFixturePresentation().NativeGraphics!.Callback, CapturedGround = capturedGround, DrewActor = drewActor, Boundary = boundary }));
            Console.WriteLine($"Tonberry phase-one standalone evidence: {output}");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", before.Frame, NativeCallback = before.ReadFixturePresentation().NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }

        void Publish(string name, BattleSnapshot snapshot) => NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, scene.Render(BattleSession.Restore(snapshot), 320, 224).ToArray());
    }

}
