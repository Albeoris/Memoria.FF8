using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDuel112CastTests
{
    [Fact]
    public void Capture_named_Duel112_standalone_at_target_phase()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_OUTPUT");
        Assert.SkipUnless(input is not null && File.Exists(input) && output is not null, "Select the standalone input and diagnostic output directory.");
        int phase = int.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_TARGET_PHASE") ?? "45");
        Assert.InRange(phase, 1, 151);
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(input!))!;
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(effectId => game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1]);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        Directory.CreateDirectory(output!);
        BattleSnapshot before = source;
        try
        {
            for (int callback = 0; callback < 512; callback++)
            {
                if (battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 112, ReturnedDescriptor: > 0, Pools.Length: 5 } effect && effect.Pools[1].Slots[0].ReadWord(12) >= phase)
                {
                    BattleSnapshot selected = battle.Capture();
                    NativeBattleRestorationPresentationTests.Publish(output!, "selected", selected, scene.Render(battle, 320, 224).ToArray());
                    BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output!, "selected.battle.json")))!);
                    clone.AttachNativeEffectResourceResolver(effectId => game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1]);
                    Assert.Equal(JsonSerializer.Serialize(selected), JsonSerializer.Serialize(clone.Capture()));
                    before = selected;
                    battle.Tick();
                    clone.Tick();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(clone, 320, 224).ToArray());
                    NativeBattleRestorationPresentationTests.Publish(output!, "selected-next", battle.Capture(), scene.Render(battle, 320, 224).ToArray());
                    File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "passed", Input = input, TargetPhase = phase, ActualPhase = selected.NativeGraphics!.OriginalEffect!.Pools[1].Slots[0].ReadWord(12), selected.Frame, selected.NativeGraphics.Callback }));
                    return;
                }
                before = battle.Capture();
                battle.Tick();
            }
            Assert.Fail("The selected standalone did not reach the requested active Duel target phase.");
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            throw;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Duel112_completes_original_action_and_independent_standalones(bool duelCommandRoute)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleNativeAction action = assets.NativeActions.First(action => action.Command == 239 && action.MagicId == 112);
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 1, 112);
        BattleCombatantDefinition actor = source.Combatants[0] with { Name = "Zell" };
        if (!duelCommandRoute)
        {
            actor = actor with { NativeCommands = actor.NativeCommands.Add(new(239, action.Name ?? "Duel", action.TargetFlags!.Value, checked((byte)action.ActionId))) };
        }
        BattleDefinition definition = source with { Combatants = [actor, source.Combatants[1]] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.AttachNativeEffectResourceResolver(effectId => game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1]);
        BattleScenePresentation scene = new(assets, definition, true);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "duel-112-" + (duelCommandRoute ? "command-" : "raw-") + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Duel112 evidence: " + output);
        BattleSnapshot before = battle.Capture();
        bool queuedChoice = false;
        bool selected = false;
        bool drawn = false;
        bool midpoint = false;
        int duelInputs = 0;
        try
        {
            for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
            {
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.CanChoosePlayerCommand("party"));
            if (duelCommandRoute)
            {
                battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("party", 1)] });
                battle.AttachNativeEffectResourceResolver(effectId => game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1]);
            }
            Publish("before-command");
            battle.QueueNativePlayerCommand("party", "enemy", duelCommandRoute ? (byte)11 : (byte)239, duelCommandRoute ? (ushort)0 : action.ActionId);
            CompareNext("queued");
            for (int callback = 0; callback < 1024; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                if (duelCommandRoute && !queuedChoice && battle.GetNativeDuel("party") is { Phase: BattleNativeDuelPhase.Ready })
                {
                    BattleNativeDuelChoice choice = NextChoice();
                    Publish("before-input-" + duelInputs);
                    foreach (ushort button in choice.Buttons)
                    {
                        battle.AdvanceNativeDuelPresentation(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30 + 1), 0);
                        battle.AdvanceNativeDuelPresentation(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30 + 1), button);
                    }
                    Assert.Equal(choice.Finisher ? BattleNativeDuelPhase.Finisher : BattleNativeDuelPhase.AwaitingEffect, battle.GetNativeDuel("party")!.Phase);
                    queuedChoice = choice.ActionId == action.ActionId;
                    File.WriteAllText(Path.Combine(output, "duel-input-" + duelInputs + ".json"), JsonSerializer.Serialize(choice));
                    CompareNext("duel-input-" + duelInputs++);
                }
                NativeBattleGraphicsState graphics = battle.NativeGraphics!;
                bool active = graphics.OriginalEffect is { Binding.Effect: 112, ReturnedDescriptor: > 0 } && (!duelCommandRoute || queuedChoice);
                if (active)
                {
                    selected = true;
                    if (!drawn && graphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                    {
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        NativeBattlePrimitiveFrame withoutEffect = graphics.PrimitiveFrame with { LastCommands = graphics.PrimitiveFrame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                        BattleSession background = BattleSession.Restore(battle.Capture() with { NativeGraphics = graphics with { PrimitiveFrame = withoutEffect } });
                        byte[] backgroundPixels = new BattleScenePresentation(assets, definition, true).Render(background, 320, 224).ToArray();
                        drawn = Enumerable.Range(0, pixels.Length / 4).Any(index => !pixels.AsSpan(index * 4, 3).SequenceEqual(backgroundPixels.AsSpan(index * 4, 3)));
                    }
                    if (!midpoint && graphics.OriginalEffect!.Pools.Length == 5 && graphics.OriginalEffect.Pools[1].Slots[0].ReadWord(12) >= 70)
                    {
                        CompareNext("midpoint");
                        midpoint = true;
                    }
                }
                if (selected && Released() && (!duelCommandRoute || battle.GetNativeDuel("party") is null))
                {
                    Assert.True(drawn);
                    Assert.True(midpoint);
                    Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy");
                    battle.SetMode(BattleAtbMode.Normal);
                    CompareNext("completed");
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", duelCommandRoute, duelInputs, queuedChoice, selected, drawn, midpoint, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback, Mode = battle.Mode.ToString() }));
                    return;
                }
            }
            Assert.Fail("Duel112 did not release its actual module/action owners within the diagnostic bound; the saved boundary identifies any remaining command-control producer.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", duelCommandRoute, duelInputs, queuedChoice, selected, drawn, midpoint, before.Frame, NativeCallback = before.NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }

        BattleNativeDuelChoice NextChoice()
        {
            BattleNativeLimitDefinition rules = definition.Combatants[0].NativeLimit!;
            BattleNativeDuelState state = battle.GetNativeDuel("party")!;
            Queue<(byte MoveSet, byte First)> pending = new();
            HashSet<byte> visited = [state.MoveSetId];
            pending.Enqueue((state.MoveSetId, byte.MaxValue));
            while (pending.TryDequeue(out var current))
            {
                for (int index = 1; index <= 3; index++)
                {
                    byte next = rules.DuelMoveSets[4 * current.MoveSet + index];
                    if (next == byte.MaxValue || !visited.Add(next))
                    {
                        continue;
                    }
                    byte first = current.First == byte.MaxValue ? next : current.First;
                    if (NativeBattleDuelRules.ActionId(rules, next) == action.ActionId)
                    {
                        //Manual native input compares button edges even when the learned flag hides the choice label.
                        return battle.GetNativeDuelChoices("party").Single(choice => choice.MoveSetId == first);
                    }
                    pending.Enqueue((next, first));
                }
            }
            throw new InvalidDataException("The original Duel move graph has no route from the current move set to the selected finisher.");
        }
        bool Released()
        {
            NativeBattleGraphicsState graphics = battle.NativeGraphics!;
            return battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Capture().NativeClock!.ActionPause == 0 && graphics.Descriptors.Queue.IsEmpty && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
        }
        void Publish(string name)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, battle.Capture(), scene.Render(battle, 320, 224).ToArray());
        }
        void CompareNext(string name)
        {
            Publish(name);
            BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, name + ".battle.json")))!);
            clone.AttachNativeEffectResourceResolver(effectId => game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1]);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            before = battle.Capture();
            battle.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
            Publish(name + "-next");
        }
    }
}
