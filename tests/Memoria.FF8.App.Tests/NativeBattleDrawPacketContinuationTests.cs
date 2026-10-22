using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDrawPacketContinuationTests
{
    [Fact]
    public void Named_standalone_Draw_completes_its_original_packet_stream()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DRAW_PACKET_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_RENZOKUKEN_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select a standalone Draw save, installed resources and a distinct evidence directory.");
        GameState source = GameSession.Load(input!);
        BattleSession battle = BattleSession.Restore(Assert.IsType<BattleSnapshot>(source.Battle));
        using GameAssets game = new(directory!);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        string output = Path.Combine(evidence!, "draw-packets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Draw packet evidence: " + output);
        BattleSnapshot before = battle.Capture();
        try
        {
            for (int callback = 0; callback < 4096 && !battle.Combatants.Single(actor => actor.Id == "party-0").IsReady; callback++)
            {
                battle.AdvanceTurn();
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.Combatants.Single(actor => actor.Id == "party-0").IsReady);
            battle.QueueAction("party-0", "enemy-0", BattleAbility.Draw, 40);
            bool initialized = false;
            for (int callback = 0; callback < 4096; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                initialized |= battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 15 };
                if (initialized && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: 0 })
                {
                    Publish("completed");
                    return;
                }
            }
            Assert.Fail("Draw must release its original effect within 4096 callbacks.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
            throw;
        }

        void Publish(string name)
        {
            BattleSnapshot snapshot = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, null);
        }
    }
}
