using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDuel215CastTests
{
    [Fact]
    public void Installed_My_Final_Heaven_uses_the_original_Duel_command_route()
    {
        NativeBattleDuel112CastTests.RunOriginalDuelEffect(215, true);
    }

    [Fact]
    public void Installed_My_Final_Heaven_raw239_completes_its_original_action_and_independent_standalones()
    {
        const int characterId = 1;
        const ushort effectId = 215;
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, characterId, effectId);
        BattleNativeAction originalAction = assets.NativeActions.First(action => action.Command == 239 && action.MagicId == effectId);
        Assert.Equal((ushort)9, originalAction.ActionId);
        BattleCombatantDefinition actor = source.Combatants[0] with { Name = "Zell", NativeCommands = source.Combatants[0].NativeCommands.Add(new(239, originalAction.Name ?? "My Final Heaven", originalAction.TargetFlags!.Value, checked((byte)originalAction.ActionId))) };
        BattleDefinition definition = source with { Combatants = [actor, source.Combatants[1]] };
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "my-final-heaven-" + effectId + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Limit effect evidence: " + output);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        BattleSnapshot before = battle.Capture();
        bool selected = false;
        bool drawn = false;
        bool midpoint = false;
        try
        {
            for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
            {
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.NativeEntryReady);
            Assert.True(battle.CanChoosePlayerCommand("party"));
            Publish("before-command", battle);
            battle.QueueNativePlayerCommand("party", "enemy", 239, originalAction.ActionId);
            CompareNext("queued", battle);
            for (int callback = 0; callback < 4096; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                NativeBattleGraphicsState graphics = battle.NativeGraphics!;
                bool active = graphics.OriginalEffect is { ReturnedDescriptor: > 0 } effect && effect.Binding.Effect == effectId;
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
                    if (!midpoint && callback >= 60)
                    {
                        CompareNext("midpoint", battle);
                        midpoint = true;
                    }
                }
                if (selected && Released(battle))
                {
                    Assert.True(drawn);
                    Assert.True(midpoint);
                    Assert.Equal((ushort)215, battle.NativeGraphics!.OriginalEffect!.Binding.Effect);
                    Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy");
                    Publish("completed-before-host-mode", battle);
                    battle.SetMode(BattleAtbMode.Normal);
                    CompareNext("completed", battle);
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", characterId, effectId, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback, selected, drawn, midpoint, HostModeAfterRelease = "Normal", OriginalActionId = originalAction.ActionId }));
                    return;
                }
            }
            Assert.Fail("The selected Limit did not release its original action and task owners within the diagnostic bound.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", characterId, effectId, before.Frame, NativeCallback = before.NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }

        void Publish(string name, BattleSession current)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, current.Capture(), scene.Render(current, 320, 224).ToArray());
        }

        void CompareNext(string name, BattleSession current)
        {
            Publish(name, current);
            BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, name + ".battle.json")))!);
            Assert.Equal(JsonSerializer.Serialize(current.Capture()), JsonSerializer.Serialize(clone.Capture()));
            before = current.Capture();
            current.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(current.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(current, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
            Publish(name + "-next", current);
        }
    }

    private static bool Released(BattleSession battle)
    {
        NativeBattleGraphicsState graphics = battle.NativeGraphics!;
        return battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Capture().NativeClock!.ActionPause == 0 && graphics.Descriptors.Queue.IsEmpty && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
    }

}
