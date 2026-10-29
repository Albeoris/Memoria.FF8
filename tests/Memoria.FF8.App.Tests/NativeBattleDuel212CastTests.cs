using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDuel212CastTests
{
    [Fact]
    public void Installed_Burning_Rave_uses_the_original_Duel_command_route()
    {
        NativeBattleDuel112CastTests.RunOriginalDuelEffect(212, true);
    }

    [Fact]
    public void Capture_named_Burning_Rave_standalone_at_target_phase()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_OUTPUT");
        Assert.SkipUnless(input is not null && File.Exists(input) && output is not null, "Select the standalone input and diagnostic output directory.");
        int phase = int.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_TARGET_PHASE") ?? "100");
        Assert.InRange(phase, 1, 181);
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
                if (battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 212, ReturnedDescriptor: > 0, Pools.Length: 4 } effect && effect.Pools[1].Slots[0].ReadWord(12) >= phase)
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
            Assert.Fail("The selected standalone did not reach the requested active Burning Rave target phase.");
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            throw;
        }
    }

    [Fact]
    public void Installed_Burning_Rave_completes_its_original_action_and_independent_standalones()
    {
        const int characterId = 1;
        const ushort effectId = 212;
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, characterId, effectId);
        BattleNativeAction originalAction = assets.NativeActions.First(action => action.Command == 239 && action.MagicId == effectId);
        Assert.Equal((ushort)6, originalAction.ActionId);
        BattleCombatantDefinition actor = source.Combatants[0] with { Name = "Zell", NativeCommands = source.Combatants[0].NativeCommands.Add(new(239, originalAction.Name ?? "Burning Rave", originalAction.TargetFlags!.Value, checked((byte)originalAction.ActionId))) };
        BattleDefinition definition = source with { Combatants = [actor, source.Combatants[1]] };
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "burning-rave-" + effectId + "-" + Guid.NewGuid().ToString("N"));
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
                        BattleSnapshot backgroundSource = battle.Capture();
                        NativeBattleGraphicsState backgroundGraphics = backgroundSource.NativeGraphics!;
                        NativeBattlePrimitiveFrame withoutEffect = backgroundGraphics.PrimitiveFrame! with { LastCommands = backgroundGraphics.PrimitiveFrame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                        BattleSession background = BattleSession.Restore(backgroundSource with { NativeGraphics = backgroundGraphics with { PrimitiveFrame = withoutEffect } });
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
                    Assert.Equal((ushort)212, battle.NativeGraphics!.OriginalEffect!.Binding.Effect);
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
