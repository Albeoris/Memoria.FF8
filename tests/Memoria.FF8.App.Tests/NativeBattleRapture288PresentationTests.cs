using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRapture288PresentationTests
{
    [Fact]
    public void Original_Rapture_completes_ascent_restore_ejection_sound_pixels_and_standalone_next()
    {
        string? manifest = Environment.GetEnvironmentVariable("MEMORIA_FF8_RAPTURE_CASES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifest), "An external ordinary Magic case manifest is required.");
        string id = Environment.GetEnvironmentVariable("MEMORIA_FF8_RAPTURE_CASE") ?? throw new InvalidDataException("One external case identity is required.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RAPTURE_OUTPUT") ?? throw new InvalidDataException("A distinct external case output directory is required.");
        NativeBattleOrdinaryMagicCase item = (JsonSerializer.Deserialize<NativeBattleOrdinaryMagicCases>(File.ReadAllBytes(manifest!)) ?? throw new InvalidDataException("The case manifest is null.")).Cases.Single(item => item.Id == id);
        Assert.Equal((ushort)288, item.Effect);
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.MagicId == item.Effect && (item.ActionId is null || action.ActionId == item.ActionId));
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([item.Effect])[item.Effect - 1];
        string? supplied = Environment.GetEnvironmentVariable("MEMORIA_FF8_RAPTURE_SNAPSHOT");
        string sourcePath;
        if (string.IsNullOrWhiteSpace(supplied))
        {
            NativeEffectFixtureRequest request = new(item.Id, item.Effect, 2, action.ActionId, 3, item.EntryMaximumCallbacks, item.ProcessTimeoutSeconds) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = item.ProducerMaximumCallbacks };
            string fixture = Path.Combine(output, "fixture");
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory, fixture);
            File.WriteAllText(Path.Combine(output, "fixture-result.json"), JsonSerializer.Serialize(exported, new JsonSerializerOptions { WriteIndented = true }));
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
            sourcePath = Path.Combine(fixture, "ordinary-produced-action104.battle.json");
        }
        else
        {
            sourcePath = supplied;
        }
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath);
        BattleSnapshot beforeInput = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(Path.GetDirectoryName(sourcePath)!, "before-ordinary-input.battle.json"));
        NativeBattleGraphicsState sourceGraphics = source.NativeGraphics!;
        int descriptor = Assert.Single(sourceGraphics.Descriptors.Queue, index => sourceGraphics.Descriptors.Packets[index].Command == 104);
        uint relative = unchecked(sourceGraphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u);
        Assert.True(relative % 20 == 0 && relative / 20 < NativeBattleActionPacketsState.HeaderCapacity);
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(relative / 20))];
        Assert.Equal((byte)2, header.ReadByte(1));
        Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal(item.Effect, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal(JsonSerializer.Serialize(installed), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[item.Effect - 1]));
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.Equal(0, contact.Primary.HitFlags3 & 4);
        Assert.Equal(0, contact.Primary.Flags2 & 0x40);
        Assert.NotEqual(0u, contact.Primary.BattleStatuses8 & 0x10000);
        Assert.NotEqual(0, contact.Primary.SavedStatuses4 & 1);
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        string target = source.NativeGraphics!.ActorIds[3]!;
        string caster = source.NativeGraphics.ActorIds[0]!;
        Assert.Equal(2, Stock(beforeInput, caster, action.ActionId));
        Assert.Equal(1, Stock(source, caster, action.ActionId));
        int producedHp = source.Combatants.Single(actor => actor.Id == target).CurrentHp;
        Assert.True(beforeInput.Combatants.Single(actor => actor.Id == target).CurrentHp > 0);
        Assert.Equal(0, producedHp);
        uint projectedContact = source.Definition.NativeGraphics!.Rules.Project(contact.Primary.SavedStatuses4, contact.Primary.BattleStatuses8);
        Assert.NotEqual(0u, projectedContact);
        Assert.Equal(0u, sourceGraphics.Models[3].Flags8!.Value & projectedContact);
        BattleSession battle = BattleSession.Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        HashSet<string> pending = ["initialized", "ascent"];
        int receipts = 0;
        int completions = 0;
        int peak = 0;
        bool moved = false;
        int ticks = 0;
        BattleSnapshot? prior = null;
        try
        {
            ColdNext("produced", source, false);
            for (; ticks < item.MaximumTicks; ticks++)
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                NativeBattleModulePoolTask? previousTarget = Target(before);
                int age = previousTarget is null ? -1 : unchecked((short)previousTarget.ReadWord(12));
                string? boundary = before?.ReturnedDescriptor is > 0 && pending.Contains("initialized") ? "initialized" : age >= 40 && pending.Contains("ascent") ? "ascent" : null;
                if (boundary is not null)
                {
                    ColdNext(boundary, prior);
                    pending.Remove(boundary);
                }
                else
                {
                    battle.Tick();
                }
                BattleSnapshot current = battle.Capture();
                NativeBattleOriginalEffectState? after = Selected(current);
                NativeBattleModulePoolTask? currentTarget = Target(after);
                int nextAge = currentTarget is null ? -1 : unchecked((short)currentTarget.ReadWord(12));
                Assert.Equal(producedHp, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                Assert.Equal(1, Stock(current, caster, action.ActionId));
                if (age is >= 23 and < 63)
                {
                    Assert.NotEqual(0, current.NativeGraphics!.Models[3].Flags0 & 0x1000);
                    moved |= current.NativeGraphics.Models[3].Transform!.Position28.Y != unchecked((short)previousTarget!.ReadWord(14));
                }
                if (age == 63 && nextAge == 64)
                {
                    NativeBattleGraphicsModel model = current.NativeGraphics!.Models[3];
                    Assert.Equal(0, model.Flags0 & 0x1000);
                    Assert.NotEqual(0, model.Flags0 & 4);
                    Assert.Equal(unchecked((short)previousTarget!.ReadWord(14)), model.Transform!.Position28.Y);
                    Assert.True(model.ModelId4 >= 16);
                    Assert.Equal((byte)0, prior.NativeGraphics!.Models[3].Kind5);
                    Assert.Equal((byte)5, model.Kind5);
                    Assert.Equal(0, prior.NativeGraphics.Models[3].Flags0 & 0x10);
                    Assert.NotEqual(0, model.Flags0 & 0x10);
                    Assert.Equal(0u, prior.NativeGraphics.Models[3].Flags8!.Value & projectedContact);
                    Assert.NotEqual(0u, projectedContact & 0x400000);
                    Assert.Equal(projectedContact & ~0x400000u, model.Flags8!.Value & projectedContact);
                    receipts++;
                    File.WriteAllText(Path.Combine(output, "target63-contact-receipt.json"), JsonSerializer.Serialize(new { BeforeCallback = prior.NativeGraphics!.Callback, AfterCallback = current.NativeGraphics.Callback, SavedY = unchecked((short)previousTarget.ReadWord(14)), Model = model, Contact = contact, Observation = "Original target63 restores its exact saved WORD30, clears movement bit1000, executes matching506690 KO/Eject-status contact, consumes projected Eject into native removal service5 and removal flag10, then sets hidden bit4" }, new JsonSerializerOptions { WriteIndented = true }));
                }
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                NativeBattlePrimitiveFrame frame = current.NativeGraphics!.PrimitiveFrame!;
                if (frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    byte[] visible = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame without = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = without } });
                    int changed = DifferentRgbPixels(visible, scene.Render(plain, 320, 224));
                    if (changed > peak)
                    {
                        peak = changed;
                        NativeBattleRestorationPresentationTests.Publish(output, $"visible-peak-{current.NativeGraphics.Callback}", current, visible);
                    }
                }
                if (after?.ReturnedDescriptor == 0 && current.NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0)
                {
                    break;
                }
            }
            Assert.True(ticks < item.MaximumTicks);
            Assert.Empty(pending);
            Assert.True(moved);
            Assert.Equal(1, receipts);
            Assert.Equal(1, completions);
            Assert.True(peak > 0);
            Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == installed.Rapture288Resources!.Sound);
            Assert.Equal(0, battle.NativeGraphics!.Models[3].Flags0 & 0x1000);
            Assert.All(Selected(battle.Capture())!.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete" && entry.ActorId == caster);
            ColdNext("completed", battle.Capture());
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "checked", item.Id, item.Effect, action.ActionId, Ticks = ticks, ContactReceipts = receipts, PeakChangedPixels = peak, Completions = completions, OriginalSound = installed.Rapture288Resources!.Sound }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            if (prior is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            }
            throw;
        }

        NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { } effect && effect.Binding.Effect == item.Effect ? effect : null;
        void ColdNext(string name, BattleSnapshot snapshot, bool advanceOriginal = true)
        {
            BattleSession running = advanceOriginal ? battle : BattleSession.Restore(snapshot);
            NativeBattleRestorationPresentationTests.Publish(output, name + "-before", snapshot, scene.Render(running, 320, 224));
            BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + "-before.battle.json")));
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            Assert.Equal(scene.Render(running, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
            running.Tick();
            cold.Tick();
            Assert.Equal(JsonSerializer.Serialize(running.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = scene.Render(running, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(output, name + "-after", running.Capture(), pixels);
        }
    }

    private static NativeBattleModulePoolTask? Target(NativeBattleOriginalEffectState? effect) => effect?.Pools.SelectMany(pool => pool.Slots).SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x679510);
    private static int Stock(BattleSnapshot snapshot, string actor, ushort action) => snapshot.Combatants.Single(item => item.Id == actor).NativeMagicInventory!.Single(stock => stock.SpellId == action).Count;
    private static int DifferentRgbPixels(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int changed = 0;
        for (int index = 0; index < first.Length; index += 4)
        {
            if (first[index] != second[index] || first[index + 1] != second[index + 1] || first[index + 2] != second[index + 2])
            {
                changed++;
            }
        }
        return changed;
    }
}
