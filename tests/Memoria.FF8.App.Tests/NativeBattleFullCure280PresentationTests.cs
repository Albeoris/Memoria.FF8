using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFullCure280PresentationTests
{
    [Fact]
    public void Original_Full_cure_heals_its_produced_party_contact_and_releases_all_original_visual_tasks_through_JSON()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_FULLCURE280_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Select a distinct Full-cure evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, candidate => candidate.Command == 2 && candidate.MagicId == 280);
        NativeEffectFixtureRequest request = new("full-cure280", 280, 2, action.ActionId, 0, 500, 120) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = 500 };
        string fixture = Path.Combine(output!, "fixture");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory, fixture);
        File.WriteAllText(Path.Combine(output!, "fixture-result.json"), JsonSerializer.Serialize(exported));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "ordinary-produced-action104.battle.json"));
        BattleSnapshot beforeInput = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "before-ordinary-input.battle.json"));
        Assert.Equal(100, beforeInput.Combatants.Single(actor => actor.Id == "party").CurrentHp);
        Assert.Equal(1000, source.Combatants.Single(actor => actor.Id == "party").CurrentHp);
        Assert.Equal(2, Assert.Single(beforeInput.Combatants.Single(actor => actor.Id == "party").NativeMagicInventory!, stock => stock.SpellId == action.ActionId).Count);
        Assert.Equal(1, Assert.Single(source.Combatants.Single(actor => actor.Id == "party").NativeMagicInventory!, stock => stock.SpellId == action.ActionId).Count);
        BattleSession battle = BattleSession.Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        BattleSnapshot prior = source;
        bool initialized = false;
        bool liveRestore = false;
        bool completed = false;
        int maximumChangedPixels = 0;
        int presentedHealings = 0;
        BattleSnapshot? visiblePeak = null;
        byte[]? visiblePixels = null;
        try
        {
            CompareNext("produced");
            for (int tick = 0; tick < 1000; tick++)
            {
                prior = battle.Capture();
                if (!liveRestore && prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 280, ReturnedDescriptor: > 0 } selected && selected.Pools.Any(pool => pool.DescriptorAddress == 0x2507FF8 && pool.Slots.Any(task => (task.Flags & 1) != 0 && task.ReadWord(22) >= 12)))
                {
                    CompareNext("live");
                    liveRestore = true;
                }
                else
                {
                    battle.Tick();
                }
                BattleSnapshot current = battle.Capture();
                Assert.Equal(1000, current.Combatants.Single(actor => actor.Id == "party").CurrentHp);
                Assert.Equal(1, Assert.Single(current.Combatants.Single(actor => actor.Id == "party").NativeMagicInventory!, stock => stock.SpellId == action.ActionId).Count);
                if (prior.NativePartyPresentation!.Rows[0]!.HitPoints != current.NativePartyPresentation!.Rows[0]!.HitPoints)
                {
                    Assert.Equal((ushort)100, prior.NativePartyPresentation.Rows[0]!.HitPoints);
                    Assert.Equal((ushort)1000, current.NativePartyPresentation.Rows[0]!.HitPoints);
                    presentedHealings++;
                }
                NativeBattleGraphicsState graphics = current.NativeGraphics!;
                if (graphics.OriginalEffect is { Binding.Effect: 280 } effect)
                {
                    if (effect.FullCure280 is { } entered)
                    {
                        Assert.True(current.NativeEffectMemory!.Cursor > 0, "Original TIM loading must leave a nonzero allocation cursor for this fixed-base regression.");
                        Assert.Equal(0, entered.BufferOffset);
                        foreach (NativeBattleEffectTaskPool pool in effect.Pools.Where(pool => pool.DescriptorAddress is 0x2508008 or 0x2507FF8 or 0x2507FE8))
                        {
                            uint offset = pool.DescriptorAddress == 0x2508008 ? 7124u : pool.DescriptorAddress == 0x2507FF8 ? 320u : 0u;
                            Assert.Equal(0x20DFAB8u + offset, pool.BankAddress);
                        }
                    }
                    initialized = true;
                    if (graphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                    {
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        NativeBattlePrimitiveFrame hidden = graphics.PrimitiveFrame with { LastCommands = graphics.PrimitiveFrame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                        BattleSession plain = BattleSession.Restore(current with { NativeGraphics = graphics with { PrimitiveFrame = hidden } });
                        byte[] background = new BattleScenePresentation(assets, source.Definition, true).Render(plain, 320, 224).ToArray();
                        int changed = Enumerable.Range(0, pixels.Length / 4).Count(index => !pixels.AsSpan(index * 4, 3).SequenceEqual(background.AsSpan(index * 4, 3)));
                        if (changed > maximumChangedPixels)
                        {
                            maximumChangedPixels = changed;
                            visiblePeak = current;
                            visiblePixels = pixels;
                        }
                    }
                    if (effect.ReturnedDescriptor == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104))
                    {
                        Assert.All(effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
                        completed = true;
                        break;
                    }
                }
            }
            Assert.True(initialized && liveRestore && completed);
            Assert.Equal(1, presentedHealings);
            Assert.True(maximumChangedPixels > 0);
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == "party");
            Assert.Single(battle.Events, item => item.NativeModuleSound?.Sound == game.ReadNativeFullCure280Resources().Sound);
            NativeBattleRestorationPresentationTests.Publish(output!, "visible-peak", visiblePeak!, visiblePixels);
            CompareNext("completed");
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "passed", battle.Frame, NativeCallback = battle.NativeGraphics!.Callback, maximumChangedPixels, presentedHealings }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", prior, null);
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Error = error.ToString() }));
            throw;
        }

        void CompareNext(string name)
        {
            BattleSnapshot current = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output!, name, current, scene.Render(battle, 320, 224));
            BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + ".battle.json")));
            Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(cold.Capture()));
            battle.Tick();
            cold.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = scene.Render(battle, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(output!, name + "-next", battle.Capture(), pixels);
        }
    }
}
