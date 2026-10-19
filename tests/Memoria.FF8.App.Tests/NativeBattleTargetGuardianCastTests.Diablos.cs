using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
    [Fact]
    public void Installed_Diablos_ordinary_entry_and_opening_programme_window_restores_before_paired_wings()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, 5, 325);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int tick = 0; tick < 400 && (!battle.NativeEntryReady || !battle.Combatants.Single(actor => actor.Id == "party").IsReady); tick++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.True(battle.Combatants.Single(actor => actor.Id == "party").IsReady);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_GUARDIAN_EVIDENCE") ?? Path.GetTempPath(), "diablos325-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Ordinary Diablos entry evidence: {output}");
        NativeBattleRestorationPresentationTests.Publish(output, "before-command", battle.Capture(), null);
        BattleActionDefinition action = definition.ActionDefinitions.Single(item => item.Ability == BattleAbility.GuardianForce && item.Id == 5);
        battle.QueueAction("party", action.Allies ? "party" : "enemy", BattleAbility.GuardianForce, 5);
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
            if (battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 325, ReturnedDescriptor: NativeBattleDiablos325Entry.MainDescriptor })
            {
                entered = true;
                break;
            }
        }
        Assert.True(entered);
        BattleSnapshot source = battle.Capture();
        ushort initialCounter = Counter(source);
        Assert.InRange(initialCounter, (ushort)0, (ushort)1);
        Assert.NotEqual(0u, source.NativeEffectMemory!.Diablos325!.ModelBuffer5208);
        BattleScenePresentation scene = new(assets, definition, true);
        NativeBattleRestorationPresentationTests.Publish(output, "entry", source, scene.Render(battle, 320, 224).ToArray());
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        while (Counter(first.Capture()) < 105)
        {
            first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            Assert.Equal(scene.Render(first, 320, 224).ToArray(), scene.Render(second, 320, 224).ToArray());
        }
        BattleSnapshot boundary = first.Capture();
        byte[] boundaryPixels = scene.Render(first, 320, 224).ToArray();
        NativeBattleRestorationPresentationTests.Publish(output, "before-particle-bank", boundary, boundaryPixels);
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
        NotSupportedException independentError = Assert.Throws<NotSupportedException>(() => second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
        Assert.Contains("6548B9", error.Message);
        Assert.Equal(error.Message, independentError.Message);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "entry-and-opening-programme-validated", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Counter = Counter(boundary), Boundary = error.Message }));

        static ushort Counter(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDiablos325Entry.MainDescriptor).Slots[0].ReadWord(0xC);
    }
}
