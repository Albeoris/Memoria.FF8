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
        BattleScenePresentation scene = new(assets, definition, true);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_GUARDIAN_EVIDENCE") ?? Path.GetTempPath(), "tonberry-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        BattleSnapshot before = battle.Capture();
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
                if (battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: > 0, Binding.Effect: 90 })
                {
                    entered = true;
                    break;
                }
            }
            Assert.True(entered);
            BattleSnapshot entry = battle.Capture();
            Assert.Equal(7, entry.NativeGraphics!.OriginalEffect!.Pools.Length);
            Assert.NotNull(entry.NativeEffectMemory!.Tonberry90);
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
                NativeBattleTonberry90State shared = battle.NativeEffectMemory!.Tonberry90!;
                uint director = shared.DirectorAddress1547168!.Value;
                if (shared.ReadWord(director + 0x42) == 1 && shared.ReadWord(director + 0x40) == 0)
                {
                    booted = true;
                    break;
                }
            }
            Assert.True(booted);
            BattleSnapshot boot = battle.Capture();
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
            Assert.Equal(0x762DA0u, boot.NativeGraphics!.OriginalEffect!.Pools[4].Slots[0].Callback);
            Assert.Equal(0x762850u, boot.NativeGraphics.OriginalEffect.Pools[6].Slots[0].Callback);
            Assert.NotNull(boot.NativeEffectMemory!.SharedEffectView2793E58);
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
                BattleSnapshot actual = battle.Capture();
                Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(clone.Capture()));
                capturedGround |= actual.NativeEffectMemory!.GroundCapture is { Statics.Regions.IsEmpty: false };
                drewActor = actual.NativeGraphics!.OriginalEffect!.Pools[3].Slots.Any(task => task.Callback == 0x763EB0 && task.ReadDword(0x98) > 0);
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
            Assert.Equal(scene.Render(BattleSession.Restore(retained), 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(restored, 320, 224).ToArray());
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "entry-boot-and-phase-one-validated", retained.Frame, NativeCallback = retained.NativeGraphics!.Callback, CapturedGround = capturedGround, DrewActor = drewActor, Boundary = boundary }));
            Console.WriteLine($"Tonberry phase-one standalone evidence: {output}");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", before.Frame, NativeCallback = before.NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }

        void Publish(string name, BattleSnapshot snapshot) => NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, scene.Render(BattleSession.Restore(snapshot), 320, 224).ToArray());
    }

    [Fact]
    public void Continue_the_named_Tonberry_checkpoint_to_its_next_boundary()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_TONBERRY_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TONBERRY_EVIDENCE");
        Assert.SkipUnless(source is not null && output is not null, "Select a standalone Tonberry snapshot and a distinct evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        Assert.Equal((ushort)90, snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = BattleSession.Restore(snapshot);
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(independent.Capture()));
        battle.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        independent.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        BattleSnapshot before = snapshot;
        try
        {
            for (int tick = 0; tick < 4096; tick++)
            {
                before = battle.Capture();
                try
                {
                    battle.Tick();
                }
                catch (Exception error)
                {
                    Exception? mirrored = Record.Exception(independent.Tick);
                    Assert.NotNull(mirrored);
                    Assert.Equal(error.GetType(), mirrored.GetType());
                    Assert.Equal(error.Message, mirrored.Message);
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
                    throw;
                }
                independent.Tick();
                if (tick == 0 || (tick & 31) == 0)
                {
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
                }
                if (battle.NativeGraphics?.OriginalEffect is not { ReturnedDescriptor: > 0 })
                {
                    BattleSnapshot completed = battle.Capture();
                    Assert.Equal(JsonSerializer.Serialize(completed), JsonSerializer.Serialize(independent.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, snapshot.Definition, true).Render(independent, 320, 224).ToArray());
                    NativeBattleRestorationPresentationTests.Publish(output!, "completed", completed, scene.Render(battle, 320, 224).ToArray());
                    File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", completed.Frame, NativeCallback = completed.NativeGraphics?.Callback }));
                    return;
                }
            }
            Assert.Fail("Tonberry did not complete within its diagnostic callback bound.");
        }
        catch (Exception error)
        {
            BattleSession retained = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(retained.Capture()));
            NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", before, scene.Render(retained, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", before.Frame, NativeCallback = before.NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }
    }
}
