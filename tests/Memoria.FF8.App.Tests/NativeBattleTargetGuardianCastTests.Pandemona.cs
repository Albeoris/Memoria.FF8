using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
    [Fact]
    public void Installed_Pandemona_ordinary_entry_and_constructor_restore_at_actual_timeline_boundary()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, 8, 291);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int tick = 0; tick < 400 && (!battle.NativeEntryReady || !battle.Combatants.Single(actor => actor.Id == "party").IsReady); tick++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.True(battle.Combatants.Single(actor => actor.Id == "party").IsReady);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_GUARDIAN_EVIDENCE") ?? Path.GetTempPath(), "pandemona291-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Ordinary Pandemona entry evidence: {output}");
        NativeBattleRestorationPresentationTests.Publish(output, "before-command", battle.Capture(), null);
        BattleActionDefinition action = definition.ActionDefinitions.Single(item => item.Ability == BattleAbility.GuardianForce && item.Id == 8);
        battle.QueueAction("party", action.Allies ? "party" : "enemy", BattleAbility.GuardianForce, 8);
        NativeBattleRestorationPresentationTests.Publish(output, "queued", battle.Capture(), null);
        bool entered = false;
        for (int tick = 0; tick < 4096; tick++)
        {
            BattleSnapshot before = battle.Capture();
            try
            {
                battle.Tick();
            }
            catch (Exception error)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "before-entry-boundary", before, null);
                NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
                File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "entry-blocked", before.Frame, Error = error.ToString() }));
                throw;
            }
            if (battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 291, ReturnedDescriptor: NativeBattlePandemona291Entry.MainDescriptor })
            {
                entered = true;
                break;
            }
        }
        Assert.True(entered);
        BattleScenePresentation scene = new(assets, definition, true);
        BattleSnapshot source = battle.Capture();
        Assert.InRange(Counter(source), (ushort)0, (ushort)1);
        Assert.Equal(0x20DFAB8u, source.NativeEffectMemory!.Pandemona291!.ModelBuffer25562F4);
        NativeBattleRestorationPresentationTests.Publish(output, "entry", source, scene.Render(battle, 320, 224).ToArray());
        BattleSession live = BattleSession.Restore(source);
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, "entry.battle.json")))!);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(independent.Capture()));
        TimeSpan callback = NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(1) + TimeSpan.FromTicks(1);
        while (Counter(live.Capture()) < 2)
        {
            live.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            independent.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            Assert.Equal(JsonSerializer.Serialize(live.Capture()), JsonSerializer.Serialize(independent.Capture()));
            Assert.Equal(scene.Render(live, 320, 224).ToArray(), scene.Render(independent, 320, 224).ToArray());
        }
        BattleSnapshot boundary = live.Capture();
        NativeBattleRestorationPresentationTests.Publish(output, "before-timeline", boundary, scene.Render(live, 320, 224).ToArray());
        for (int opening = 0; opening < 40; opening++)
        {
            live.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            independent.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            Assert.Equal(JsonSerializer.Serialize(live.Capture()), JsonSerializer.Serialize(independent.Capture()));
            Assert.Equal(scene.Render(live, 320, 224).ToArray(), scene.Render(independent, 320, 224).ToArray());
        }
        boundary = live.Capture();
        Assert.Equal((ushort)40, boundary.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25562A8).Slots[0].ReadWord(0xC));
        NativeBattleRestorationPresentationTests.Publish(output, "before-next-scene", boundary, scene.Render(live, 320, 224).ToArray());
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "entry-constructor-opening-surface-and-disk-restore-validated", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Counter = Counter(boundary), Boundary = "before-vortex" }));

        static ushort Counter(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattlePandemona291Entry.MainDescriptor).Slots[0].ReadWord(0xC);
    }
}
