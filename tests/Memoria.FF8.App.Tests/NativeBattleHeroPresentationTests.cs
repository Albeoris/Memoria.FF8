using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed record NativeBattleHeroStackFixture(NativeBattleExecutionStack ExecutionStack, NativeBattleHeroCrtStackContract HeroCrtStackContract);

public sealed class NativeBattleHeroPresentationTests
{
    [Fact]
    public void Selected_original_Hero_component_completes_controlled_contact_and_cold_boundaries()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? selection = Environment.GetEnvironmentVariable("MEMORIA_FF8_HERO_EFFECT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_HERO_EVIDENCE");
        string? stackFile = Environment.GetEnvironmentVariable("MEMORIA_FF8_HERO_STACK_INPUT");
        Assert.SkipUnless(directory is not null && selection is not null && output is not null && stackFile is not null, "Select installed resources, one Hero effect, distinct evidence output and an explicitly authenticated execution-stack input.");
        ushort effectId = ushort.Parse(selection!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(effectId, new ushort[] { 309, 310 });
        string? targetSelection = Environment.GetEnvironmentVariable("MEMORIA_FF8_HERO_TARGETS");
        Assert.True(targetSelection is null or "0" or "0,3", "Select the default single target or the explicit controlled0,3 case.");
        bool multiTarget = targetSelection == "0,3";
        Assert.True(!multiTarget || effectId == 310, "The explicit two-target component requires Holy War310.");
        int expectedContacts = multiTarget ? 2 : 1;
        List<int> childBirthCallbacks = [];
        List<int> contactCallbacks = [];
        bool distinctLiveChildren = false;
        Assert.True(Directory.Exists(directory));
        Assert.True(File.Exists(stackFile));
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        BattleSession? battle = null;
        BattleSnapshot? prior = null;
        int callbacks = 0;
        int heroCallbacks = 0;
        int contacts = 0;
        int completions = 0;
        int glyphs = 0;
        int visibleChangedRgb = 0;
        int measuredPacketFrames = 0;
        BattleSnapshot? peakSnapshot = null;
        BattleSnapshot? peakWithout = null;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        object? metadataPeak = null;
        bool initialized = false;
        bool live = false;
        string boundary = "external-stack-input";
        List<string> restoredBoundaries = [];
        try
        {
            NativeBattleHeroStackFixture stack = JsonSerializer.Deserialize<NativeBattleHeroStackFixture>(File.ReadAllText(stackFile!)) ?? throw new InvalidDataException("The explicit Hero stack input is empty.");
            Assert.NotNull(stack.ExecutionStack);
            Assert.NotNull(stack.HeroCrtStackContract);
            stack.ExecutionStack.Validate();
            stack.HeroCrtStackContract.Validate(stack.ExecutionStack);
            using GameAssets game = new(directory!);
            using BattleAssets assets = new(directory!);
            BattleNativeAction action = assets.NativeActions.Where(row => row.Command == 4 && row.MagicId == effectId).OrderBy(row => row.ActionId).First();
            NativeBattleEffectBinding original = game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1];
            NativeBattleHeroProfile profile = NativeBattleHeroProfile.ForEffect(effectId);
            string fixture = Path.Combine(output!, "fixture");
            boundary = "export-fresh-controlled-component";
            NativeEffectFixtureRequest request = new($"hero{effectId}", effectId, action.Command, action.ActionId, 0, 800, 240);
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory!, fixture);
            File.WriteAllText(Path.Combine(output!, "fixture-result.json"), JsonSerializer.Serialize(exported));
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
            BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
            Assert.Null(Selected(source));
            NativeBattleEffectMemory memory = source.NativeEffectMemory ?? throw new InvalidDataException("The fresh exporter omitted effect memory.");
            memory = memory with { ExecutionStack = stack.ExecutionStack, HeroCrtStack = stack.HeroCrtStackContract };
            NativeBattleRetainedMemory retained = source.Definition.NativeRetainedMemory ?? throw new InvalidDataException("The fresh exporter omitted its retained definition mirror.");
            source = source with { NativeEffectMemory = memory, Definition = source.Definition with { NativeRetainedMemory = retained with { EffectMemory = memory } } };
            prior = source;
            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[effectId - 1]));
            Assert.True(exported.Selection!.OriginalKernelEffectReference);
            Assert.Equal(effectId, exported.Selection.ShellEffect);
            int descriptor = Assert.Single(source.NativeGraphics!.Descriptors.Queue, index => source.NativeGraphics.Descriptors.Packets[index].Command == 104);
            uint headerOffset = checked(source.NativeGraphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u);
            Assert.Equal(0u, headerOffset % 20);
            NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(headerOffset / 20))];
            Assert.Equal(action.Command, header.ReadByte(1));
            Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
            Assert.Equal(effectId, NativeBattleActionGraphics.Word(header, 6));
            if (multiTarget)
            {
                Assert.Equal((ushort)20, action.ActionId);
                Assert.Equal((byte)4, action.Command);
                Assert.NotNull(source.NativeGraphics!.ActorIds[3]);
                Assert.Equal((byte)2, (byte)(source.NativeGraphics.Models[3].Flags0 & 6));
                Assert.Null(source.ActiveAction);
                Assert.Empty(source.QueuedActions);
                Assert.Null(source.NativeActionPackets.ActiveHeaderIndex);
                Assert.Equal((byte)1, source.NativeActionPackets.ActionCount);
                Assert.Equal((byte)1, source.NativeActionPackets.ContactCount);
                NativeBattleContactPacket firstContact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
                BattleCombatantSnapshot enemy = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 3);
                NativeBattleStatusSnapshot status = enemy.NativeStatus ?? throw new InvalidDataException("The second controlled target lacks its native status owner.");
                byte remainingHits = NativeBattleGuardianFixtureInputs.ReadColdPhysicalThreeRemainingHits(game);
                enemy = enemy with { NativeParticipant = enemy.NativeParticipant! with { RemainingHits = remainingHits } };
                NativeBattleContactPacket secondContact = new(new(3, action.DamagePresentation, 1, 0, status.Saved, 7, status.Battle), firstContact.Secondary);
                NativeBattleActionPacketsState packets = source.NativeActionPackets.ResetCounts();
                int prepared;
                (packets, prepared) = packets.Prepare(new(header.ReadByte(0), header.ReadByte(1), header.ReadByte(2), unchecked((sbyte)header.ReadByte(3)), NativeBattleActionGraphics.Word(header, 4), NativeBattleActionGraphics.Word(header, 6), header.Data));
                Assert.Equal(checked((int)(headerOffset / 20)), prepared);
                (packets, _) = packets.AppendContact(firstContact);
                (packets, _) = packets.AppendContact(secondContact);
                (packets, _) = packets.Publish(2);
                packets.Validate();
                source = source with
                {
                    NativeActionPackets = packets,
                    Combatants = source.Combatants.Select(actor => actor.Id == enemy.Id ? enemy : actor).ToArray(),
                    Definition = source.Definition with { NativeRetainedMemory = source.Definition.NativeRetainedMemory! with { ActionPackets = packets } }
                };
                header = packets.Headers[prepared];
                prior = source;
            }
            Assert.Equal(checked((byte)expectedContacts), header.ReadByte(16));
            NativeBattleContactPacket[] contactPackets = source.NativeActionPackets!.ReadHeaderContacts506BA0(checked((int)(headerOffset / 20))).ToArray();
            Assert.Equal(expectedContacts, contactPackets.Length);
            Assert.Equal(multiTarget ? new byte[] { 0, 3 } : new byte[] { 0 }, contactPackets.Select(packet => packet.Primary.ActorIndex).ToArray());
            if (multiTarget)
            {
                Assert.Equal((ushort)1, contactPackets[1].Primary.ContactFlags);
            }
            NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
            Assert.Equal(0, contact.Primary.ActorIndex);
            Assert.Equal((ushort)7, contact.Primary.Amount6);
            Dictionary<int, NativeBattleEffectTask> expectedTexts = [];
            foreach (NativeBattleContactPacket packet in contactPackets)
            {
                NativeBattleEffectTask? text = NativeBattleEffectsState.Clear().PrependDamageText(packet.Primary.ActorIndex, packet.Primary.ContactFlags, packet.Primary.Amount6).Slots.SingleOrDefault(task => (task.Flags0 & 1) != 0);
                if (text is not null)
                {
                    expectedTexts.Add(packet.Primary.ActorIndex, text);
                }
            }
            Dictionary<string, int> logicalHp = source.Combatants.ToDictionary(actor => actor.Id, actor => actor.CurrentHp);
            int initialSounds = source.Events.Count(item => item.NativeModuleSound?.Sound == original.HeroResources!.Sound);
            battle = Restore(source);
            Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
            BattleScenePresentation scene = new(assets, source.Definition, true);
            ColdNext("queued", source);
            // This is a diagnostic liveness guard, not an inferred native effect duration.
            for (; callbacks < 4096; callbacks++)
            {
                boundary = "native-callback";
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                NativeBattleOriginalEffectState? after = Selected(current);
                if (before is { ReturnedDescriptor: > 0 } && after is not null && before.Pools[0].Slots[0].ReadDword(16) != after.Pools[0].Slots[0].ReadDword(16))
                {
                    heroCallbacks++;
                }
                foreach (BattleCombatantSnapshot actor in current.Combatants)
                {
                    Assert.Equal(logicalHp[actor.Id], actor.CurrentHp);
                }
                if (after is { ReturnedDescriptor: > 0, Pools.Length: > 1 })
                {
                    NativeBattleModulePoolTask[] children = after.Pools[1].Slots.ToArray();
                    for (int index = 0; index < children.Length; index++)
                    {
                        bool wasLive = before is { Pools.Length: > 1 } && (before.Pools[1].Slots[index].Flags & 1) != 0;
                        if ((children[index].Flags & 1) != 0 && !wasLive)
                        {
                            childBirthCallbacks.Add(heroCallbacks);
                        }
                    }
                    if (multiTarget && children.Take(2).All(task => (task.Flags & 1) != 0))
                    {
                        Assert.Equal(0x1D972C0u, children[0].ReadDword(16));
                        Assert.Equal(0x1D972C0u + 156u * 3, children[1].ReadDword(16));
                        Assert.Equal(5, unchecked((short)children[0].ReadWord(12)) - unchecked((short)children[1].ReadWord(12)));
                        if (!distinctLiveChildren)
                        {
                            distinctLiveChildren = true;
                            ColdNext("two-live", current);
                        }
                    }
                }
                if (!initialized && after?.ReturnedDescriptor is > 0)
                {
                    Assert.Equal(profile.Entry, after.Binding.Entry);
                    Assert.Equal(profile.Root, after.Pools[0].Slots[0].Callback);
                    Assert.Equal(effectId, State(current).Effect);
                    initialized = true;
                    ColdNext("initialized", current);
                }
                if (before is { ReturnedDescriptor: > 0, Pools.Length: > 1 })
                {
                    int reached = before.Pools[1].Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == profile.Child && unchecked((short)task.ReadWord(12)) == 32);
                    for (int index = 0; index < reached; index++)
                    {
                        contacts++;
                        contactCallbacks.Add(heroCallbacks);
                        ColdNext("contact-" + contacts, current);
                    }
                }
                for (int slot = 0; slot < current.NativeGraphics!.Effects.Slots.Length; slot++)
                {
                    NativeBattleEffectTask text = current.NativeGraphics.Effects.Slots[slot];
                    if ((text.Flags0 & 1) != 0 && text.Kind == NativeBattleEffectKind.DamageText5069B0 && contactPackets.Any(packet => packet.Primary.ActorIndex == text.ModelIndex) && (prior.NativeGraphics!.Effects.Slots[slot].Flags0 & 1) == 0)
                    {
                        Assert.True(expectedTexts.TryGetValue(text.ModelIndex, out NativeBattleEffectTask? expectedText));
                        Assert.NotNull(expectedText);
                        Assert.Equal(expectedText.Glyphs.ToArray(), text.Glyphs.ToArray());
                        Assert.Equal(expectedText.Color, text.Color);
                        glyphs++;
                    }
                }
                if (after is { ReturnedDescriptor: > 0 } && State(current).PacketCursor is int end)
                {
                    NativeBattleHeroState state = State(current);
                    int start = checked(state.WorkingArenaOffset + profile.FirstDrawOffset + (after.Pools[0].Slots[0].ReadDword(16) != 0 ? 0x10000 : 0));
                    Assert.InRange(end, start, NativeBattleEffectArena.Length);
                    NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = frame.LastCommands.Where(command => command.Command.Packet.Bank == "20DFAB8" && command.Command.Packet.Buffer == 0 && command.Command.Packet.ByteOffset >= start && command.Command.Packet.ByteOffset < end).Select(command => command.Command.Packet).ToImmutableHashSet();
                    if (!owned.IsEmpty)
                    {
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
                        byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(Restore(without), 320, 224).ToArray();
                        measuredPacketFrames++;
                        int changed = CountRgbChanges(pixels, plain);
                        if (changed > visibleChangedRgb)
                        {
                            visibleChangedRgb = changed;
                            peakSnapshot = current;
                            peakWithout = without;
                            peakPixels = pixels;
                            peakPlain = plain;
                            metadataPeak = new { ChangedRgb = changed, current.Frame, NativeCallback = current.NativeGraphics.Callback, HarnessCallback = callbacks, Bank = "20DFAB8", Buffer = 0, Start = start, End = end, Packets = owned };
                        }
                        if (!live && changed > 0)
                        {
                            live = true;
                            ColdNext("live", current);
                        }
                    }
                }
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                if (initialized && Released(current))
                {
                    break;
                }
            }
            boundary = "acceptance";
            BattleSnapshot completed = battle.Capture();
            Assert.True(initialized);
            Assert.True(live, "No visible contribution from the exact Hero private packet cursor reached cold continuation.");
            Assert.True(visibleChangedRgb > 0);
            Assert.Equal(expectedContacts, contacts);
            Assert.Equal(expectedTexts.Count, glyphs);
            Assert.Equal(expectedContacts, childBirthCallbacks.Count);
            if (multiTarget)
            {
                Assert.True(distinctLiveChildren);
                Assert.Equal(5, childBirthCallbacks[1] - childBirthCallbacks[0]);
                Assert.Equal(5, contactCallbacks[1] - contactCallbacks[0]);
            }
            Assert.Equal(1, completions);
            NativeBattleModuleSound[] sounds = completed.Events.Where(item => item.NativeModuleSound?.Sound == original.HeroResources!.Sound).Skip(initialSounds).Select(item => item.NativeModuleSound!).ToArray();
            Assert.Equal(expectedContacts, sounds.Length);
            Assert.All(sounds, sound =>
            {
                Assert.Equal(0u, sound.Flags);
                Assert.Equal((byte)128, sound.Pan);
                Assert.NotNull(sound.Allocation);
            });
            Assert.True(Released(completed), "The selected original module did not release within the diagnostic callback budget.");
            Assert.NotNull(peakSnapshot);
            Assert.NotNull(peakWithout);
            Assert.NotNull(peakPixels);
            Assert.NotNull(peakPlain);
            NativeBattleRestorationPresentationTests.Publish(output!, "visible", peakSnapshot, peakPixels);
            NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", peakWithout, peakPlain);
            File.WriteAllText(Path.Combine(output!, "visible-packet-owner.json"), JsonSerializer.Serialize(metadataPeak));
            ColdNext("peak", peakSnapshot);
            ColdNext("completed", completed);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Fresh original-resource controlled seven-HP component; no ordinary Item formula, inventory or target-policy claim", Effect = effectId, TargetActors = contactPackets.Select(packet => packet.Primary.ActorIndex), childBirthCallbacks, contactCallbacks, distinctLiveChildren, exported.Selection, callbacks, heroCallbacks, DiagnosticMaximumCallbacks = 4096, contacts, glyphs, completions, visibleChangedRgb, measuredPacketFrames, metadataPeak, RestoredBoundaries = restoredBoundaries, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));

            BattleSession Restore(BattleSnapshot snapshot)
            {
                BattleSession restored = BattleSession.Restore(snapshot);
                restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                return restored;
            }

            void ColdNext(string name, BattleSnapshot snapshot)
            {
                boundary = "cold-" + name;
                NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, new BattleScenePresentation(assets, source.Definition, true).Render(Restore(snapshot), 320, 224).ToArray());
                BattleSession cold = Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + ".battle.json")));
                BattleSession direct = Restore(snapshot);
                Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
                cold.Tick();
                direct.Tick();
                Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
                byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray();
                Assert.Equal(new BattleScenePresentation(assets, source.Definition, true).Render(direct, 320, 224).ToArray(), pixels);
                NativeBattleRestorationPresentationTests.Publish(output!, name + "-next", cold.Capture(), pixels);
                restoredBoundaries.Add(name);
            }
        }
        catch (Exception error)
        {
            if (prior is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", prior, null);
            }
            if (battle is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            }
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Scenario = "Controlled original-resource Hero component", Effect = effectId, boundary, callbacks, heroCallbacks, contacts, completions, visibleChangedRgb, measuredPacketFrames, metadataPeak, RestoredBoundaries = restoredBoundaries, Error = error.ToString() }));
            throw;
        }

        NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { } selected && selected.Binding.Effect == effectId ? selected : null;
        NativeBattleHeroState State(BattleSnapshot snapshot) => effectId == 309 ? snapshot.NativeEffectMemory!.Hero309! : snapshot.NativeEffectMemory!.Hero310!;
    }

    private static bool Released(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeEffectMemory!.Active == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104) && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
    }

    private static int CountRgbChanges(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int changed = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (!first.AsSpan(offset, 3).SequenceEqual(second.AsSpan(offset, 3)))
            {
                changed++;
            }
        }
        return changed;
    }
}
