using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRenzokukenDrawSequenceTests
{
    [Theory]
    [InlineData("enemy-0", "enemy-0", false)]
    [InlineData("enemy-0", "enemy-1", false)]
    [InlineData("enemy-1", "enemy-0", false)]
    [InlineData("enemy-1", "enemy-1", false)]
    [InlineData("enemy-0", "enemy-0", true)]
    [InlineData("enemy-0", "enemy-1", true)]
    [InlineData("enemy-1", "enemy-0", true)]
    [InlineData("enemy-1", "enemy-1", true)]
    public void Named_standalone_Renzokuken_then_Draw_preserves_packet_stream(string renzokukenTarget, string drawTarget, bool successfulTriggers)
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_RENZOKUKEN_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_RENZOKUKEN_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select a standalone Renzokuken save, installed resources and a distinct evidence directory.");
        GameState source = GameSession.Load(input!);
        BattleSession battle = BattleSession.Restore(Assert.IsType<BattleSnapshot>(source.Battle));
        using GameAssets game = new(directory!);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        string output = Path.Combine(evidence!, $"sequence-{renzokukenTarget}-{drawTarget}-{successfulTriggers}-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Renzokuken Draw sequence evidence: " + output);
        BattleSnapshot before = battle.Capture();
        string phase = "queue-renzokuken";
        int triggerCount = 0;
        int renzokukenCallbacks = 0;
        int readinessCallbacks = 0;
        int drawCallbacks = 0;
        HashSet<byte> triggeredContacts = [];
        try
        {
            battle.QueueNativePlayerCommand("party-0", renzokukenTarget, 5, 0);
            bool started = false;
            bool released = false;
            phase = "renzokuken";
            for (; renzokukenCallbacks < 4096; renzokukenCallbacks++)
            {
                if (successfulTriggers && battle.NativeRenzokukenTriggerWindowOpen && battle.NativeGraphics?.TriggerWidget is { } widget && triggeredContacts.Add(widget.Input.ContactIndex))
                {
                    Assert.True(battle.TriggerNativeRenzokuken());
                    triggerCount++;
                }
                before = battle.Capture();
                battle.Tick();
                started |= battle.GetNativeRenzokuken("party-0") is not null;
                if (started && battle.GetNativeRenzokuken("party-0") is null && Released())
                {
                    released = true;
                    break;
                }
            }
            Assert.True(started);
            Assert.True(released, "Renzokuken must release within its bounded callback budget.");
            if (successfulTriggers)
            {
                Assert.NotEmpty(triggeredContacts);
            }
            phase = "readiness";
            for (; readinessCallbacks < 4096 && !battle.Combatants.Single(actor => actor.Id == "party-0").IsReady; readinessCallbacks++)
            {
                battle.AdvanceTurn();
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.Combatants.Single(actor => actor.Id == "party-0").IsReady);
            phase = "queue-draw";
            battle.QueueAction("party-0", drawTarget, BattleAbility.Draw, 40);
            bool initialized = false;
            released = false;
            phase = "draw";
            for (; drawCallbacks < 4096; drawCallbacks++)
            {
                before = battle.Capture();
                battle.Tick();
                initialized |= battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 15, ReturnedDescriptor: > 0 };
                if (initialized && Released())
                {
                    released = true;
                    break;
                }
            }
            Assert.True(initialized);
            Assert.True(released, "Draw must release within its bounded callback budget.");
            Result("completed", null);
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            Publish("failed-live");
            File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
            Result("failed", error.ToString());
            throw;
        }

        bool Released() => battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: 0 };

        void Publish(string name)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, battle.Capture(), null);
        }

        void Result(string status, string? error)
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { status, phase, renzokukenTarget, drawTarget, successfulTriggers, triggerCount, renzokukenCallbacks, readinessCallbacks, drawCallbacks, battle.Frame, error }));
        }
    }
}
