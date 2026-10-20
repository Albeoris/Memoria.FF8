using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleTornado146PresentationTests
{
    [Fact]
    public void Original_Magic_Tornado_consumes_one_stock_draws_the_target_and_completes_after_one_contact_with_cold_continuations()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TORNADO146_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "An external empty Tornado evidence output directory is required.");
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required.");
        Assert.False(Directory.Exists(output!) && Directory.EnumerateFileSystemEntries(output!).Any(), "Tornado evidence output must be empty.");
        Directory.CreateDirectory(output!);
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, candidate => candidate.Command == 2 && candidate.MagicId == 146);
        NativeEffectFixtureRequest request = new("tornado146", 146, 2, action.ActionId, 3, 400, 180) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = 500 };
        string fixture = Path.Combine(output!, "fixture");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory, fixture);
        File.WriteAllText(Path.Combine(output!, "fixture-result.json"), JsonSerializer.Serialize(exported, new JsonSerializerOptions { WriteIndented = true }));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot beforeInput = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "before-ordinary-input.battle.json"));
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "ordinary-produced-action104.battle.json"));
        BattleSession battle = BattleSession.Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        string caster = source.NativeGraphics!.ActorIds[0]!;
        string target = source.NativeGraphics.ActorIds[3]!;
        int descriptor = Assert.Single(source.NativeGraphics.Descriptors.Queue, index => source.NativeGraphics.Descriptors.Packets[index].Command == 104);
        uint offset = source.NativeGraphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u;
        Assert.Equal(0u, offset % 20);
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(offset / 20))];
        Assert.Equal((byte)2, header.ReadByte(1));
        Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)146, NativeBattleActionGraphics.Word(header, 6));
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex ?? throw new InvalidDataException("Original Tornado producer omitted its contact pointer."));
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.True(contact.Primary.Amount6 > 0);
        Assert.Equal(2, Stock(beforeInput));
        Assert.Equal(1, Stock(source));
        int expectedHp = Math.Max(0, beforeInput.Combatants.Single(actor => actor.Id == target).CurrentHp - contact.Primary.Amount6);
        Assert.Equal(expectedHp, source.Combatants.Single(actor => actor.Id == target).CurrentHp);
        Assert.True(source.NativeGlobalRandomCalls > beforeInput.NativeGlobalRandomCalls);
        NativeBattleEffectTask expectedText = Assert.Single(NativeBattleEffectsState.Clear().PrependDamageText(contact.Primary.ActorIndex, contact.Primary.ContactFlags, contact.Primary.Amount6).Slots, task => (task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0);
        HashSet<string> restored = [];
        bool initialized = false;
        bool privatePackets = false;
        bool targetLifted = false;
        bool groundHidden = false;
        NativeBattleModelTransform originalTarget = source.NativeGraphics.Models[3].Transform!;
        HashSet<string> renderedPhases = [];
        int contacts = 0;
        int completions = 0;
        int pixelChanges = 0;
        int ticks = 0;

        BattleSnapshot prior = source;
        try
        {
            ColdNext("produced");
            for (; ticks < 600; ticks++)
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                string? boundary = null;
                if (before?.ReturnedDescriptor is > 0)
                {

                    Assert.NotNull(prior.NativeEffectMemory);
                    Assert.NotNull(prior.NativeEffectMemory.Tornado146);
                    Assert.Equal(2, before.Pools.Length);
                    Assert.Equal(0x24936C8u, before.Pools[0].BankAddress);
                    Assert.Equal(0x2493760u, before.Pools[1].BankAddress);
                    initialized = true;
                    if (!restored.Contains("initialized"))
                    {
                        boundary = "initialized";
                    }
                    else if (Age(before) >= 24 && !restored.Contains("lifted"))
                    {
                        boundary = "lifted";
                    }
                    else if (Age(before) >= 55 && !restored.Contains("ground"))
                    {
                        boundary = "ground";
                    }
                    else if (Age(before) >= 84 && !restored.Contains("after-contact"))
                    {
                        boundary = "after-contact";
                    }
                }
                if (boundary is null)
                {
                    battle.Tick();
                }
                else
                {
                    ColdNext(boundary);
                }
                BattleSnapshot current = battle.Capture();
                Assert.Equal(expectedHp, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                Assert.Equal(1, Stock(current));
                for (int index = 0; index < current.NativeGraphics!.Effects.Slots.Length; index++)
                {
                    NativeBattleEffectTask text = current.NativeGraphics.Effects.Slots[index];
                    if ((text.Flags0 & 1) != 0 && text.Kind == NativeBattleEffectKind.DamageText5069B0 && text.ModelIndex == 3 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
                    {
                        Assert.Equal(expectedText.Glyphs.ToArray(), text.Glyphs.ToArray());
                        Assert.Equal(expectedText.Color, text.Color);
                        Assert.Equal(expectedText.HalfWidth, text.HalfWidth);
                        contacts++;
                    }
                }
                NativeBattleOriginalEffectState? after = Selected(current);
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                privatePackets |= current.NativeGraphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == NativeBattleTornado146State.PacketBank);
                targetLifted |= current.NativeGraphics.Models[3].Transform!.Position28.Y < originalTarget.Position28.Y - 200;
                groundHidden |= (current.NativeStage!.Groups[1].Flags & 2) == 0;
                int currentAge = Age(after);
                string? phase = currentAge is >= 10 and < 17 ? "funnel" : currentAge is >= 40 and < 65 ? "pieces-ground" : currentAge is >= 90 and < 98 ? "landing" : null;
                if (phase is not null && !renderedPhases.Contains(phase) && current.NativeGraphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == NativeBattleTornado146State.PacketBank))
                {
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame withoutEffect = current.NativeGraphics.PrimitiveFrame with { LastCommands = current.NativeGraphics.PrimitiveFrame.LastCommands.Where(command => command.Command.Packet.Bank != NativeBattleTornado146State.PacketBank).ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = withoutEffect } });
                    byte[] ordinary = scene.Render(plain, 320, 224).ToArray();
                    int changed = 0;
                    for (int index = 0; index < pixels.Length; index += 4)
                    {
                        if (pixels[index] != ordinary[index] || pixels[index + 1] != ordinary[index + 1] || pixels[index + 2] != ordinary[index + 2])
                        {
                            changed++;
                        }
                    }
                    if (changed > 0)
                    {
                        renderedPhases.Add(phase);
                        pixelChanges = Math.Max(pixelChanges, changed);
                        NativeBattleRestorationPresentationTests.Publish(output, "visible-" + phase, current, pixels);
                    }
                }
                if (after?.ReturnedDescriptor == 0 && current.NativeEffectMemory!.Active == 0 && !current.NativeGraphics.Descriptors.Queue.Any(index => current.NativeGraphics.Descriptors.Packets[index].Command == 104) && current.ActiveAction is null && current.QueuedActions.Length == 0)
                {
                    break;
                }
            }
            Assert.True(ticks < 600, "The diagnostic callback budget was exceeded.");
            Assert.True(initialized);
            Assert.True(privatePackets);
            Assert.True(targetLifted);
            Assert.True(groundHidden);
            Assert.Equal(2, battle.Capture().NativeStage!.Groups[1].Flags & 2);
            Assert.Equal(originalTarget.Position28, battle.NativeGraphics!.Models[3].Transform!.Position28);
            Assert.Equal(originalTarget.Vertical34, battle.NativeGraphics.Models[3].Transform!.Vertical34);
            Assert.Equal(source.NativeGraphics.Models[3].Flags0 & 0x1020, battle.NativeGraphics.Models[3].Flags0 & 0x1020);
            Assert.Equal(new[] { "funnel", "landing", "pieces-ground" }, renderedPhases.Order().ToArray());
            Assert.True(pixelChanges > 100);
            Assert.Equal(1, contacts);
            Assert.Equal(1, completions);
            Assert.Contains("initialized", restored);
            Assert.Contains("lifted", restored);
            Assert.Contains("ground", restored);
            Assert.Contains("after-contact", restored);
            NativeBattleOriginalEffectState finished = Selected(battle.Capture())!;
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster);
            ColdNext("completed");
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster);
            Report("checked", null);
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            Report("blocked", error.ToString());
            throw;
        }

        int Stock(BattleSnapshot snapshot) => (snapshot.Combatants.Single(actor => actor.Id == caster).NativeMagicInventory ?? throw new InvalidDataException("Original Magic stock is missing.")).Single(stock => stock.SpellId == action.ActionId).Count;
        static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 146 } effect ? effect : null;

        static int Age(NativeBattleOriginalEffectState? effect)
        {
            NativeBattleModulePoolTask? director = effect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x618560);
            return director is null ? -1 : unchecked((short)director.ReadWord(12));
        }

        void ColdNext(string name)
        {
            BattleSnapshot snapshot = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output, name + "-before", snapshot, scene.Render(battle, 320, 224));
            BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + "-before.battle.json")));
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
            battle.Tick();
            cold.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = scene.Render(battle, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(output, name + "-after", battle.Capture(), pixels);
            restored.Add(name);
        }

        void Report(string status, string? blocker)
        {
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, Effect = 146, Entry = "614D70", action.ActionId, Ticks = ticks, ContactTextTransitions = contacts, Completions = completions, PrivatePackets = privatePackets, TargetLifted = targetLifted, GroundHidden = groundHidden, ChangedRgbPixels = pixelChanges, RenderedPhases = renderedPhases.Order().ToArray(), Restored = restored.Order().ToArray(), FormulaProducer = exported.Scenario, Scope = "Fresh original ordinary Magic component; no story-route continuation or original renderer differential" }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
