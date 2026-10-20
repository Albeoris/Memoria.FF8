using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMeteor223PresentationTests
{
    [Fact]
    public void Ordinary_original_Meteor_preserves_ten_produced_contacts_paced_text_pixels_and_disk_restores()
    {
        string? manifest = Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR223_CASES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifest), "An external Meteor ordinary Magic case manifest is required.");
        string id = Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR223_CASE") ?? throw new InvalidDataException("One external Meteor case identity is required.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR223_OUTPUT") ?? throw new InvalidDataException("A distinct Meteor output directory is required.");
        NativeBattleOrdinaryMagicCase item = JsonSerializer.Deserialize<NativeBattleOrdinaryMagicCases>(File.ReadAllBytes(manifest!))!.Cases.Single(row => row.Id == id);
        Assert.Equal((ushort)223, item.Effect);
        Assert.True(item.EntryMaximumCallbacks > 0 && item.ProducerMaximumCallbacks > 0 && item.MaximumTicks > 0 && item.ProcessTimeoutSeconds > 0);
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 2 && row.MagicId == 223 && (item.ActionId is null || row.ActionId == item.ActionId));
        NativeBattleEffectBinding original = game.ReadNativeEffectCatalogForEffects([223])[222];
        BattleSession? battle = null;
        BattleSnapshot? prior = null;
        BattleSnapshot? peak = null;
        byte[]? peakPixels = null;
        int ticks = 0;
        int textCount = 0;
        int completions = 0;
        int peakChanged = 0;
        int pacingChecks = 0;
        int maximumTextureQueue = 0;
        List<long> textCallbacks = [];
        List<string> restored = [];
        NativeBattleContactPacket[] contacts = [];
        try
        {
            string? supplied = Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR223_SNAPSHOT");
            string sourcePath;
            if (string.IsNullOrWhiteSpace(supplied))
            {
                NativeEffectFixtureRequest request = new(item.Id, 223, 2, action.ActionId, 3, item.EntryMaximumCallbacks, item.ProcessTimeoutSeconds) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = item.ProducerMaximumCallbacks };
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
            BattleSnapshot beforeInput = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, "before-ordinary-input.battle.json"));
            prior = source;
            NativeBattleGraphicsState graphics = source.NativeGraphics!;
            int descriptor = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
            uint headerOffset = unchecked(graphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u);
            Assert.True(headerOffset % 20 == 0 && headerOffset / 20 < NativeBattleActionPacketsState.HeaderCapacity);
            NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(headerOffset / 20))];
            Assert.Equal((byte)2, header.ReadByte(1));
            Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
            Assert.Equal((ushort)223, NativeBattleActionGraphics.Word(header, 6));
            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[222]));
            int first = header.FirstContactIndex ?? throw new InvalidDataException("Meteor's ordinary producer omitted its contact pointer.");
            contacts = Enumerable.Range(0, header.ReadByte(0x10)).Select(index => source.NativeActionPackets.ReadContact(first + index)).ToArray();
            Assert.Equal(10, contacts.Length);
            Assert.All(contacts, contact =>
            {
                Assert.Equal((byte)3, contact.Primary.ActorIndex);
                Assert.Equal((byte)255, contact.Secondary.ActorIndex);
                Assert.True(contact.Primary.Amount6 > 0);
            });
            string target = graphics.ActorIds[3]!;
            string caster = graphics.ActorIds[0]!;
            int hp = beforeInput.Combatants.Single(actor => actor.Id == target).CurrentHp;
            int maximumHp = source.Definition.Combatants.Single(actor => actor.Id == target).MaximumHp;
            foreach (NativeBattleContactPacket contact in contacts)
            {
                hp = (contact.Primary.HitFlags3 & 1) != 0 ? Math.Min(maximumHp, hp + contact.Primary.Amount6) : Math.Max(0, hp - contact.Primary.Amount6);
            }
            Assert.Equal(hp, source.Combatants.Single(actor => actor.Id == target).CurrentHp);
            Assert.True(source.NativeGlobalRandomCalls > beforeInput.NativeGlobalRandomCalls);
            Assert.Equal(2, Stock(beforeInput, caster, action.ActionId));
            Assert.Equal(1, Stock(source, caster, action.ActionId));
            NativeBattleEffectTask[] expectedTexts = contacts.Select(contact => Assert.Single(NativeBattleEffectsState.Clear().PrependDamageText(contact.Primary.ActorIndex, contact.Primary.ContactFlags, contact.Primary.Amount6).Slots, task => (task.Flags0 & 1) != 0)).ToArray();
            battle = BattleSession.Restore(source);
            Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
            BattleScenePresentation scene = new(assets, source.Definition, true);
            DiskNext("produced", source, false);
            bool initialized = false;
            bool midpoint = false;
            bool gridPacketsSeen = false;
            for (; ticks < item.MaximumTicks; ticks++)
            {
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                Pacing? pacingBefore = ReadPacing(prior);
                if (!midpoint && textCount > 0 && textCount < contacts.Length && before?.ReturnedDescriptor is > 0)
                {
                    DiskNext("midpoint", prior, true);
                    midpoint = true;
                }
                else
                {
                    battle.Tick();
                }
                BattleSnapshot current = battle.Capture();
                Assert.Equal(hp, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                Assert.Equal(1, Stock(current, caster, action.ActionId));
                int newTexts = 0;
                for (int slot = 0; slot < current.NativeGraphics!.Effects.Slots.Length; slot++)
                {
                    NativeBattleEffectTask text = current.NativeGraphics.Effects.Slots[slot];
                    if ((text.Flags0 & 1) == 0 || text.Kind != NativeBattleEffectKind.DamageText5069B0 || text.ModelIndex != 3 || (prior.NativeGraphics!.Effects.Slots[slot].Flags0 & 1) != 0)
                    {
                        continue;
                    }
                    Assert.InRange(textCount, 0, expectedTexts.Length - 1);
                    NativeBattleEffectTask expected = expectedTexts[textCount++];
                    Assert.Equal(expected.Glyphs.ToArray(), text.Glyphs.ToArray());
                    Assert.Equal(expected.Color, text.Color);
                    Assert.Equal(expected.HalfWidth, text.HalfWidth);
                    textCallbacks.Add(current.NativeGraphics.Callback);
                    newTexts++;
                }
                Assert.InRange(newTexts, 0, 1);
                Pacing? pacingAfter = ReadPacing(current);
                if (pacingBefore is { } pacing && pacingAfter is { } next && pacing.Slot == next.Slot && next.Age != pacing.Age)
                {
                    bool due = pacing.Index < pacing.Count && unchecked((pacing.Index + 1) * pacing.Duration) / (pacing.Count + 1) <= pacing.Age;
                    Assert.Equal(pacing.Index + (due ? 1 : 0), next.Index);
                    Assert.Equal(due ? 1 : 0, newTexts);
                    pacingChecks++;
                }
                NativeBattleOriginalEffectState? after = Selected(current);
                initialized |= after?.ReturnedDescriptor is > 0;
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                HashSet<NativeBattlePrimitiveReference> gridPackets = frame.LastCommands.GroupBy(command => command.Command.Packet).Where(group => group.Any(command => command.Command.WordOffset == 3 && (command.Command.Opcode & 0xFC) == 0x3C) && group.Any(command => command.Command.WordOffset == 1 && command.Command.Opcode == 0xE2) && group.Any(command => command.Command.WordOffset == 15 && command.Command.Opcode == 0xE2)).Select(group => group.Key).ToHashSet();
                bool packets = gridPackets.Count > 0;
                gridPacketsSeen |= packets;
                maximumTextureQueue = Math.Max(maximumTextureQueue, current.NativeEffectMemory?.Textures?.Count ?? 0);
                if (peakChanged == 0 && packets)
                {
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame removed = frame with { LastCommands = frame.LastCommands.Where(command => !gridPackets.Contains(command.Command.Packet)).ToImmutableArray() };
                    BattleSession plain = BattleSession.Restore(current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = removed } });
                    peakChanged = DifferentRgb(pixels, scene.Render(plain, 320, 224));
                    if (peakChanged > 0)
                    {
                        peak = current;
                        peakPixels = pixels;
                    }
                }
                if (after?.ReturnedDescriptor == 0 && current.NativeEffectMemory!.Active == 0 && !current.NativeGraphics.Descriptors.Queue.Any(index => current.NativeGraphics.Descriptors.Packets[index].Command == 104) && current.ActiveAction is null && current.QueuedActions.Length == 0)
                {
                    break;
                }
            }
            Assert.True(ticks < item.MaximumTicks, "The diagnostic callback budget is not a native duration.");
            Assert.True(initialized);
            Assert.True(midpoint);
            Assert.True(gridPacketsSeen);
            Assert.True(peakChanged > 0);
            Assert.True(pacingChecks > 0);
            Assert.Equal(contacts.Length, textCount);
            Assert.Equal(textCallbacks.Count, textCallbacks.Distinct().Count());
            Assert.Equal(1, completions);
            NativeBattleOriginalEffectState finished = Selected(battle.Capture())!;
            Assert.Equal(0u, finished.ReturnedDescriptor);
            Assert.All(finished.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
            Assert.Single(battle.Events, row => row.Kind == "ActionComplete" && row.ActorId == caster);
            DiskNext("completed", battle.Capture(), true);
            Assert.Single(battle.Events, row => row.Kind == "ActionComplete" && row.ActorId == caster);
            NativeBattleRestorationPresentationTests.Publish(output, "visible-contribution", peak!, peakPixels);
            Report("checked", null);

            void DiskNext(string name, BattleSnapshot snapshot, bool advanceLive)
            {
                BattleSession live = advanceLive ? battle! : BattleSession.Restore(snapshot);
                NativeBattleRestorationPresentationTests.Publish(output, name + "-before", snapshot, scene.Render(live, 320, 224));
                BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + "-before.battle.json")));
                Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
                Assert.Equal(scene.Render(live, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                live.Tick();
                cold.Tick();
                Assert.Equal(JsonSerializer.Serialize(live.Capture()), JsonSerializer.Serialize(cold.Capture()));
                byte[] pixels = scene.Render(live, 320, 224).ToArray();
                Assert.Equal(pixels, new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray());
                NativeBattleRestorationPresentationTests.Publish(output, name + "-after", live.Capture(), pixels);
                restored.Add(name);
            }
        }
        catch (Exception error)
        {
            if (prior is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            }
            Report("blocked", error.ToString());
            throw;
        }

        NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { } selected && selected.Binding.Effect == 223 ? selected : null;

        void Report(string status, string? blocker)
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = status, FirstBlocker = blocker, item.Id, Effect = 223, action.Command, action.ActionId, Ticks = ticks, DiagnosticMaximumTicks = item.MaximumTicks, ProducedContacts = contacts.Select(contact => new { contact.Primary.ActorIndex, contact.Primary.Amount6, contact.Primary.HitFlags3 }), DamageTextContacts = textCount, DamageTextCallbacks = textCallbacks, PacingChecks = pacingChecks, Completions = completions, VisibleChangedRgbPixels = peakChanged, MaximumObservedPendingTextures = maximumTextureQueue, RestoredBoundaries = restored, BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), ManifestSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest!))), Pending = new[] { "Whole-entry native pixel differential", "Story-route continuation", "Other spell targets and stocks" } }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static Pacing? ReadPacing(BattleSnapshot snapshot)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: 223, ReturnedDescriptor: > 0 } || snapshot.NativeEffectMemory is not { ScriptedEffects: { } core, Arena: { } arena })
        {
            return null;
        }
        uint manager = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x27973EC, 4));
        uint basis = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(manager + 0x90, 4));
        foreach (byte index in core.Read(0x2797554, 128).TakeWhile(index => index != 255))
        {
            int offset = checked((int)(basis - 0x20DFAB8u) + (index & 127) * 0x100);
            if (arena.Read(offset + 0x1C, 1)[0] != 28)
            {
                continue;
            }
            int age = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 0xBC, 4));
            if (age == 0)
            {
                return null;
            }
            return new(offset, age, BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 0xC0, 4)), BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 0xC4, 4)), BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 0xB8, 4)));
        }
        return null;
    }

    private sealed record Pacing(int Slot, int Age, int Index, int Count, int Duration);
    private static int Stock(BattleSnapshot snapshot, string actor, ushort action) => snapshot.Combatants.Single(row => row.Id == actor).NativeMagicInventory!.Single(stock => stock.SpellId == action).Count;

    private static int DifferentRgb(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int changed = 0;
        for (int index = 0; index < first.Length; index += 4)
        {
            changed += first[index] != second[index] || first[index + 1] != second[index + 1] || first[index + 2] != second[index + 2] ? 1 : 0;
        }
        return changed;
    }
}
