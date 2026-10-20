using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFlare124PresentationTests
{
    [Fact]
    public void Original_Magic_Flare_consumes_one_stock_draws_the_target_and_completes_after_one_contact_with_cold_continuations()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_FLARE124_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "An external empty Flare evidence output directory is required.");
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required.");
        Assert.False(Directory.Exists(output!) && Directory.EnumerateFileSystemEntries(output!).Any(), "Flare evidence output must be empty.");
        Directory.CreateDirectory(output!);
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, candidate => candidate.Command == 2 && candidate.MagicId == 124);
        NativeEffectFixtureRequest request = new("flare124", 124, 2, action.ActionId, 3, 400, 180) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = 500 };
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
        Assert.Equal((ushort)124, NativeBattleActionGraphics.Word(header, 6));
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex ?? throw new InvalidDataException("Original Flare producer omitted its contact pointer."));
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
        bool silhouette = false;
        int contacts = 0;
        int completions = 0;
        int pixelChanges = 0;
        int ticks = 0;
        byte[] baseline = scene.Render(battle, 320, 224).ToArray();
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
                    Assert.True(prior.NativeEffectMemory!.Cursor > 0, "Original TIM loading must leave a nonzero allocation cursor for this fixed-base regression.");
                    Assert.Equal(0, before.Flare124!.BufferOffset);
                    if (before.Pools.Length == 3)
                    {
                        Assert.Equal(0x20DFAB8u, before.Pools[1].BankAddress);
                        Assert.Equal(0x20DFAB8u + 0x21CCu, before.Pools[2].BankAddress);
                    }
                    initialized = true;
                    if (!restored.Contains("initialized"))
                    {
                        boundary = "initialized";
                    }
                    else if (before.Pools.Length == 3 && before.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && unchecked((short)task.ReadWord(12)) is >= 42 and < 72) && !restored.Contains("silhouette"))
                    {
                        boundary = "silhouette";
                    }
                    else if (before.Pools.Length == 3 && before.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && unchecked((short)task.ReadWord(12)) >= 81) && !restored.Contains("after-contact"))
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
                privatePackets |= current.NativeGraphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8");
                if ((current.NativeGraphics.Models[3].Flags0 & 4) != 0)
                {
                    silhouette = true;
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    pixelChanges = Math.Max(pixelChanges, pixels.Where((value, index) => value != baseline[index]).Count());
                }
                if (after?.ReturnedDescriptor == 0 && current.NativeEffectMemory!.Active == 0 && !current.NativeGraphics.Descriptors.Queue.Any(index => current.NativeGraphics.Descriptors.Packets[index].Command == 104) && current.ActiveAction is null && current.QueuedActions.Length == 0)
                {
                    break;
                }
            }
            Assert.True(ticks < 600, "The diagnostic callback budget was exceeded.");
            Assert.True(initialized);
            Assert.True(privatePackets);
            Assert.True(silhouette);
            Assert.True(pixelChanges > 100);
            Assert.Equal(1, contacts);
            Assert.Equal(1, completions);
            Assert.Contains("initialized", restored);
            Assert.Contains("silhouette", restored);
            Assert.Contains("after-contact", restored);
            NativeBattleOriginalEffectState finished = Selected(battle.Capture())!;
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Equal(0, battle.NativeGraphics!.Models[3].Flags0 & 0x80C);
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
        static NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 124 } effect ? effect : null;

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
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, Effect = 124, Entry = "6BBDA0", action.ActionId, Ticks = ticks, ContactTextTransitions = contacts, Completions = completions, PrivatePackets = privatePackets, Silhouette = silhouette, ChangedPixelBytes = pixelChanges, Restored = restored.Order().ToArray(), FormulaProducer = exported.Scenario, Scope = "Fresh original ordinary Magic component; no story-route continuation or original renderer differential" }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
