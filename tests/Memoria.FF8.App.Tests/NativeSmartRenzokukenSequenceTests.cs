using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeSmartRenzokukenSequenceTests
{
    [Theory]
    [InlineData("enemy-0", false)]
    [InlineData("enemy-1", false)]
    [InlineData("enemy-0", true)]
    [InlineData("enemy-1", true)]
    public void Smart_magic_then_Renzokuken_then_Smart_magic_preserves_shadow_commands(string target, bool successfulTriggers)
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_SMART_RENZOKUKEN_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_SMART_RENZOKUKEN_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select a standalone Smart Attack save, installed resources and a distinct evidence directory.");
        BattleSnapshot source = input!.EndsWith(".battle.json", StringComparison.OrdinalIgnoreCase) ? JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(input))! : Assert.IsType<BattleSnapshot>(GameSession.Load(input).Battle);
        BattleSession battle = BattleSession.Restore(source);
        using GameAssets game = new(directory!);
        Attach(battle);
        string output = Path.Combine(evidence!, $"smart-renzokuken-{target}-{successfulTriggers}-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Smart Attack Renzokuken sequence evidence: " + output);
        BattleSnapshot before = battle.Capture();
        string phase = "before";
        int callbacks = 0;
        int triggerCount = 0;
        int limitRefreshes = 0;
        bool midpointCompared = false;
        List<BattleSmartAttackPreview> choices = [];
        List<int> completedSpellEffects = [];
        HashSet<byte> triggeredContacts = [];
        try
        {
            Save("before");
            Ready("party-3");
            Smart("first-smart", "enemy-0");
            Ready("party-0");
            byte crisis = 0;
            for (; limitRefreshes < 256 && crisis == 0; limitRefreshes++)
            {
                crisis = battle.RefreshNativeLimit("party-0");
            }
            Assert.True(crisis > 0, "Select an available Limit through the same native refresh used by the command menu.");
            phase = "renzokuken";
            uint?[] shadows = battle.NativeGraphics!.Models.Select(model => model.Color44).ToArray();
            Assert.Contains(shadows, color => color is not null);
            battle.QueueNativePlayerCommand("party-0", target, 5, 0);
            Save("renzokuken-queued");
            bool started = false;
            bool released = false;
            for (int count = 0; count < 4096; count++)
            {
                if (successfulTriggers && battle.NativeRenzokukenTriggerWindowOpen && battle.NativeGraphics?.TriggerWidget is { } widget && triggeredContacts.Add(widget.Input.ContactIndex))
                {
                    Assert.True(battle.TriggerNativeRenzokuken());
                    triggerCount++;
                }
                if (!midpointCompared && count >= 24 && battle.GetNativeRenzokuken("party-0") is not null)
                {
                    Save("renzokuken-midpoint");
                    BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
                    Attach(clone);
                    Tick();
                    clone.Tick();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                    midpointCompared = true;
                }
                else
                {
                    Tick();
                }
                Assert.Equal(shadows, battle.NativeGraphics!.Models.Select(model => model.Color44).ToArray());
                if (!started && battle.GetNativeRenzokuken("party-0") is not null)
                {
                    started = true;
                    Save("renzokuken-initialized");
                }
                if (started && battle.GetNativeRenzokuken("party-0") is null && Released())
                {
                    released = true;
                    break;
                }
            }
            Assert.True(started);
            Assert.True(released, "Renzokuken must release within its callback budget.");
            Assert.True(midpointCompared);
            if (successfulTriggers)
            {
                Assert.NotEmpty(triggeredContacts);
            }
            Save("renzokuken-completed");
            Ready("party-3");
            Smart("second-smart", "enemy-1");
            Save("completed");
            Result("completed", null);
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            Save("failed-standalone");
            Result("failed", error.ToString());
            throw;
        }

        void Attach(BattleSession current)
        {
            current.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        }

        bool Released() => battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: 0 };

        void Tick()
        {
            before = battle.Capture();
            battle.Tick();
            callbacks++;
        }

        void Ready(string actorId)
        {
            phase = "ready-" + actorId;
            for (int count = 0; count < 4096 && !battle.Combatants.Single(actor => actor.Id == actorId).IsReady; count++)
            {
                battle.AdvanceTurn();
                Tick();
            }
            Assert.True(battle.Combatants.Single(actor => actor.Id == actorId).IsReady);
        }

        void Smart(string name, string smartTarget)
        {
            phase = name;
            BattleSmartAttackPreview choice = battle.PreviewSmartAttack("party-3", smartTarget);
            Assert.True(choice.Ability is BattleAbility.Magic or BattleAbility.Draw, "The supplied save must select a spell or Draw cast through Smart Attack.");
            choices.Add(choice);
            int spellEffect = Assert.IsType<BattleNativeAction>(battle.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == choice.ActionId).NativeAction).MagicId;
            Assert.True(spellEffect > 0);
            battle.QueueSmartAttack(choice);
            Save(name + "-queued");
            bool actualChoice = false;
            bool spellInitialized = false;
            bool released = false;
            for (int count = 0; count < 4096; count++)
            {
                Tick();
                if (battle.ActiveAction is { ActorId: "party-3" } action && action.SmartAttack is { } actual)
                {
                    Assert.Equal(choice.Ability, actual.Ability);
                    Assert.Equal(choice.ActionId, actual.ActionId);
                    Assert.Equal(choice.TargetId, actual.TargetId);
                    actualChoice = true;
                }
                if (!spellInitialized && battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: > 0 } effect && effect.Binding.Effect == spellEffect)
                {
                    spellInitialized = true;
                    Save(name + "-spell-initialized");
                }
                if (actualChoice && spellInitialized && Released())
                {
                    released = true;
                    break;
                }
            }
            Assert.True(actualChoice, "Observe the queued Smart Attack selection on the actual action.");
            Assert.True(spellInitialized, "Observe initialization of the selected spell resource, including after Draw.");
            Assert.True(released, "Smart Attack must finish the selected spell within its callback budget.");
            completedSpellEffects.Add(spellEffect);
            Save(name + "-completed");
        }

        void Save(string name)
        {
            File.WriteAllText(Path.Combine(output, name + ".battle.json"), JsonSerializer.Serialize(battle.Capture()));
        }

        void Result(string status, string? error)
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { status, phase, target, successfulTriggers, callbacks, triggerCount, limitRefreshes, midpointCompared, choices, completedSpellEffects, battle.Frame, error }));
        }
    }
}
