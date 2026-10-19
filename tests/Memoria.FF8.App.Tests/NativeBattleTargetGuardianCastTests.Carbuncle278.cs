using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
    [Fact]
    public void Installed_Carbuncle_ordinary_entry_and_first_master_callbacks_restore_before_model_construction()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, 6, 278);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int tick = 0; tick < 400 && (!battle.NativeEntryReady || !battle.Combatants.Single(actor => actor.Id == "party").IsReady); tick++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.True(battle.Combatants.Single(actor => actor.Id == "party").IsReady);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_GUARDIAN_EVIDENCE") ?? Path.GetTempPath(), "carbuncle278-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Ordinary Carbuncle entry evidence: {output}");
        NativeBattleRestorationPresentationTests.Publish(output, "before-command", battle.Capture(), null);
        BattleActionDefinition action = definition.ActionDefinitions.Single(item => item.Ability == BattleAbility.GuardianForce && item.Id == 6);
        battle.QueueAction("party", action.Allies ? "party" : "enemy", BattleAbility.GuardianForce, 6);
        NativeBattleRestorationPresentationTests.Publish(output, "queued", battle.Capture(), null);
        bool entered = false;
        for (int tick = 0; tick < 4096; tick++)
        {
            BattleSnapshot before = battle.Capture();
            try
            {
                battle.Tick();
            }
            catch (Exception entryError)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
                NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
                File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "entry-blocked", before.Frame, Error = entryError.ToString() }));
                throw;
            }
            if (battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 278, ReturnedDescriptor: NativeBattleCarbuncle278Entry.MainDescriptor })
            {
                entered = true;
                break;
            }
        }
        Assert.True(entered);
        BattleSnapshot source = battle.Capture();
        ushort initialCounter = Counter(source);
        Assert.InRange(initialCounter, (ushort)0, (ushort)1);
        Assert.NotEqual(0u, source.NativeEffectMemory!.Carbuncle278!.ModelBuffer81F8);
        BattleScenePresentation scene = new(assets, definition, true);
        NativeBattleRestorationPresentationTests.Publish(output, "entry", source, scene.Render(battle, 320, 224).ToArray());
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        while (Counter(first.Capture()) < 2)
        {
            first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            BattleSnapshot current = first.Capture();
            Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, definition, true).Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, definition, true).Render(second, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(output, Counter(current) == 2 ? "before-model-boundary" : "master-" + Counter(current), current, pixels);
        }
        BattleSnapshot boundary = first.Capture();
        Exception? error = Record.Exception(() => first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
        Assert.NotNull(error);
        Assert.Contains("6D6070", error.ToString());
        NativeBattleRestorationPresentationTests.Publish(output, "failed-live", first.Capture(), null);
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "model-boundary", Effect = 278, InitialCounter = initialCounter, boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Counter = Counter(boundary), Error = error.ToString() }));

        static ushort Counter(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleCarbuncle278Entry.MainDescriptor).Slots[0].ReadWord(0xC);
    }
}
