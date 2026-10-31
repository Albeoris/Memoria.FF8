using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSelectedEffectPresentationTests
{
    [Fact]
    public void Selected_queued_component_completes_and_restores_its_live_owners()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_SNAPSHOT");
        string? selected = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_ID");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && selected is not null && output is not null, "Select original resources, a queued standalone component, its effect and a distinct evidence directory.");
        ushort effect = ushort.Parse(selected!);
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([effect])[effect - 1];
        source = source with { Definition = source.Definition with { NativeEffectCatalog = source.Definition.NativeEffectCatalog.SetItem(effect - 1, binding) } };
        bool livePartyFollowup = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_LIVE_PARTY_FOLLOWUP") == "1";
        if (livePartyFollowup)
        {
            Assert.True(Restore(source).NativeEntryReady);
            source = QueueLivePartyFollowing(source, effect);
        }
        else if (Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_INVOCATION") is { } invocation)
        {
            Assert.True(Restore(source).NativeEntryReady);
            source = QueueFollowing(source, NativeBattleEffectFamilyRunner.LoadSnapshot(invocation), effect);
        }
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        int headerIndex;
        if (graphics.OriginalEffect is { ReturnedDescriptor: null } admitted)
        {
            Assert.Equal(effect, admitted.RequestedEffect);
            Assert.Null(admitted.ReturnedDescriptor);
            Assert.Empty(admitted.Pools);
            headerIndex = Assert.IsType<NativeBattleEffectParameters>(admitted.Parameters).HeaderIndex7C;
        }
        else
        {
            int descriptor = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
            uint argument = graphics.Descriptors.Packets[descriptor].Argument;
            Assert.Equal(0u, (argument - 0x1D280C4u) % 20);
            headerIndex = checked((int)((argument - 0x1D280C4u) / 20));
        }
        Assert.Equal(effect, NativeBattleActionGraphics.Word(source.NativeActionPackets!.Headers[headerIndex], 6));
        NativeBattleActionHeaderSlot selectedHeader = source.NativeActionPackets.Headers[headerIndex];
        Assert.NotNull(selectedHeader.FirstContactIndex);
        NativeBattleContactPacket selectedContact = Assert.IsType<NativeBattleContactPacket>(source.NativeActionPackets.Contacts[selectedHeader.FirstContactIndex.Value]);
        Assert.InRange(selectedContact.Primary.ActorIndex, (byte)0, (byte)7);
        int contactTransitions = 0;
        int expectedContactTransitions = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_EXPECTED_CONTACT_TRANSITIONS") is { } expectedContacts ? int.Parse(expectedContacts) : 1;
        Assert.InRange(expectedContactTransitions, 0, 1);
        if (selectedContact.Primary.ActorIndex >= 3)
        {
            Assert.Equal(0, expectedContactTransitions);
        }
        int maximum = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_MAX_CALLBACKS") is { } limit ? int.Parse(limit) : 2400;
        Assert.True(maximum > 0);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleSelectedCinematicObserver? observer = NativeBattleCinematicProfile.IsSupported(effect) || effect == 9 ? new(source, effect) : null;
        NativeBattleSelectedEffect314Observer? observer314 = effect == 314 ? new() : null;
        HashSet<string> boundaries = [];
        HashSet<string> restoredBoundaries = [];
        int completions = 0;
        int callbacks = 0;
        bool casting18Seen = false;
        bool casting18Tail = false;
        int casting18Frames = 0;
        int peakChanged = 0;
        int renderedCommandCount = 0;
        int renderedSamples = 0;
        int capturedOwnedFrames = 0;
        int captured218Frames = 0;
        int contact218Services = 0;
        int text218Count = 0;
        int capturedResourceFrames = 0;
        int captured30Frames = 0;
        int captured31Frames = 0;
        int captured30 = 0;
        int customComponentCapturedFrames = 0;
        HashSet<uint> capturedOwnedBanks = [];
        BattleSnapshot? peak = null;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        try
        {
            Cold("queued", source);
            BattleSnapshot prior = source;
            int recoveryCapturedGeometry = 0;
            for (; callbacks < maximum; callbacks++)
            {
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                if (effect is 265 or 266 or 267 or 268 or 269 or 270 && NativeBattleCinematicCaptureAssertions.AssertFrame(current, effect) > 0)
                {
                    captured31Frames++;
                }
                if (effect is 248 or 250 or 253 or 254 or 255 or 261 or 262 or 263 or 264 && NativeBattleCinematicCaptureAssertions.AssertFrame(current, effect) > 0)
                {
                    captured30Frames++;
                }
                if (effect is 236 or 237 or 243 or 244 or 245 or 246 or 247 && NativeBattleCinematicCaptureAssertions.AssertFrame(current, effect) > 0)
                {
                    capturedResourceFrames++;
                }
                if (effect == 276 && NativeBattleCustomComponentCaptureAssertions.AssertFrame(current, effect) > 0)
                {
                    customComponentCapturedFrames++;
                }
                if (effect == 18 && current.NativeGraphics!.OriginalEffect?.Binding.Effect == 18)
                {
                    casting18Frames += NativeBattleControl18CastingAssertions.AssertFrame(current) > 0 ? 1 : 0;
                    bool casting = NativeBattleControl18CastingAssertions.HasCasting(current);
                    casting18Seen |= casting;
                    if (casting && current.NativeGraphics.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CastingSeed574C80 && System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan(0, 2)) >= 29))
                    {
                        casting18Tail = true;
                        Cold("casting18-tail", current);
                    }
                    if (NativeBattleControl18CastingAssertions.HasCasting(prior) && !casting)
                    {
                        Cold("casting18-released", current);
                    }
                    if (casting && !boundaries.Contains("casting18-live"))
                    {
                        Cold("casting18-live", current);
                    }
                }
                NativeBattleCloudBombPresentationOwnership.AssertFrame(current, effect);
                if (effect == 7)
                {
                    recoveryCapturedGeometry += NativeBattleRecoveryCaptureAssertions.AssertOwnedGeometry(current, 7);
                }
                observer?.Observe(prior, current, Cold);
                observer314?.Observe(prior, current, Cold);
                NativeBattleOriginalEffectState? currentEffect = current.NativeGraphics!.OriginalEffect;
                if (effect == 218 && prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 218 } before218 && currentEffect is { Binding.Effect: 218 })
                {
                    NativeBattleModulePoolTask? oldMain218 = before218.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x6328A0);
                    NativeBattleModulePoolTask? newMain218 = currentEffect.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x6328A0);
                    if (oldMain218?.ReadWord(12) == 383 && newMain218?.ReadWord(12) == 384)
                    {
                        contact218Services++;
                        Cold("contact218-self", current);
                    }
                }
                if (selectedContact.Primary.ActorIndex < 3)
                {
                    int beforeHp = prior.NativePartyPresentation!.Rows[selectedContact.Primary.ActorIndex]!.HitPoints;
                    int afterHp = current.NativePartyPresentation!.Rows[selectedContact.Primary.ActorIndex]!.HitPoints;
                    if (beforeHp != afterHp)
                    {
                        if (effect == 7)
                        {
                            Assert.NotEqual(0, selectedContact.Primary.HitFlags3 & 1);
                            Assert.Equal(0, beforeHp);
                            Assert.Equal(selectedContact.Primary.Amount6, afterHp);
                            Assert.Equal(source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == selectedContact.Primary.ActorIndex).CurrentHp, afterHp);
                        }
                        else
                        {
                            Assert.Equal(Math.Max(0, beforeHp - selectedContact.Primary.Amount6), afterHp);
                        }
                        contactTransitions++;
                        Cold("contact", current);
                    }
                }
                if (currentEffect is { ReturnedDescriptor: > 0 } && currentEffect.Binding.Effect == effect)
                {
                    Cold("initialized", current);
                }
                if (prior.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: > 0 } before && before.Binding.Effect == effect && currentEffect is { ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                Assert.Equal(source.NativeGraphics!.ActorIds.ToArray(), current.NativeGraphics!.ActorIds.ToArray());
                if (livePartyFollowup)
                {
                    for (int actor = 0; actor < source.NativeGraphics.Models.Length; actor++)
                    {
                        if ((source.NativeGraphics.Models[actor].Flags0 & 8) != 0)
                        {
                            Assert.NotEqual(0, current.NativeGraphics.Models[actor].Flags0 & 8);
                        }
                    }
                }
                Assert.Equal(source.Combatants.Select(actor => (actor.Id, actor.CurrentHp)), current.Combatants.Select(actor => (actor.Id, actor.CurrentHp)));
                NativeBattlePrimitiveFrame? primitive = current.NativeGraphics.PrimitiveFrame;
                NativeBattleLogicalCommand[] ownedCommands = [];
                if (effect == 7 && currentEffect?.Recovery?.PrimaryDrawCursor is int recoveryCursor && primitive is not null)
                {
                    ownedCommands = primitive.LastCommands.Select(command => command.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= 0 && command.Packet.ByteOffset < recoveryCursor).ToArray();
                }
                else if (currentEffect is { ReturnedDescriptor: > 0 } && effect == 187)
                {
                    ownedCommands = NativeBattleSelectedEffect187Packets.CurrentCommands(current);
                }
                else if (currentEffect is { ReturnedDescriptor: > 0 } && effect == 326)
                {
                    ownedCommands = NativeBattleSelectedEffect326Packets.CurrentCommands(current);
                }
                else if (currentEffect is { ReturnedDescriptor: > 0 } && effect == 218)
                {
                    ownedCommands = NativeBattleSelectedEffect218Packets.CurrentCommands(current);
                }
                else if (currentEffect is { ReturnedDescriptor: > 0 } && NativeBattleCinematicProfile.IsSupported(effect))
                {
                    ownedCommands = NativeBattleSelectedCinematicObserver.CurrentCommands(current, prior.NativeEffectMemory!.PrimitiveProcess!.ByteCursor!.Value);
                }
                else if (currentEffect is { ReturnedDescriptor: > 0, DrawArenaOffset: int cursor } && primitive is not null)
                {
                    ownedCommands = primitive.LastCommands.Select(command => command.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= 0 && command.Packet.ByteOffset < cursor).ToArray();
                }
                if (effect == 30 && currentEffect?.Binding.Effect == 30 && currentEffect.DrawArenaOffset is int cursor30 && primitive is not null)
                {
                    NativeBattleLogicalCommand[] geometry30 = primitive.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= 0 && command.Packet.ByteOffset < cursor30 && command.Opcode is >= 0x20 and < 0x60).ToArray();
                    foreach (NativeBattleLogicalCommand command in geometry30)
                    {
                        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
                        Assert.Equal((ushort)30, geometry.Identity.Effect);
                        Assert.True(geometry.Identity.Generation > 0);
                        captured30++;
                    }
                }
                if (effect == 218)
                {
                    if (NativeBattleCinematicCaptureAssertions.AssertFrame(current, effect) > 0)
                    {
                        captured218Frames++;
                    }
                    if (current.NativeEffectMemory?.Effect218 is { CommonPacketStart: not null, CommonPacketEnd: not null })
                    {
                        NativeBattleCinematicCaptureAssertions.AssertCommands(current, effect, NativeBattleSelectedEffect218Packets.CurrentCommands(current));
                    }
                }
                if (effect is 9 or 17 or 168 or 179 or 180 or 187 or 276 && currentEffect?.Binding.Effect == effect && primitive is not null && (currentEffect.ReturnedDescriptor is > 0 || prior.NativeGraphics!.OriginalEffect?.ReturnedDescriptor is > 0))
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> appended = currentEffect.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToImmutableHashSet();
                    ownedCommands = primitive.LastCommands.Select(item => item.Command).Where(command => appended.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet))).ToArray();
                }
                if (effect == 18 && currentEffect?.Binding.Effect == 18 && prior.NativeGraphics!.OriginalEffect?.ReturnedDescriptor is > 0 && currentEffect.ReturnedDescriptor == 0)
                {
                    ownedCommands = NativeBattleSelectedCinematicObserver.CurrentCommands(current, prior.NativeEffectMemory!.PrimitiveProcess!.ByteCursor!.Value, includeCompleted: true);
                }
                if (effect is 9 or 17 or 18 or 168 or 179 or 180 or 187 && primitive is not null)
                {
                    NativeBattleLogicalCommand[] geometry = ownedCommands.Where(command => command.Opcode >= 0x20 && command.Opcode < (effect == 187 ? 0x80 : 0x40)).ToArray();
                    foreach (NativeBattleLogicalCommand command in geometry)
                    {
                        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
                        Assert.True(visual.Identity.Effect == effect, $"Effect{effect} callback{current.NativeGraphics.Callback}: packet {command.Packet.Bank}/{command.Packet.Buffer}/{command.Packet.ByteOffset}, opcode{command.Opcode:X2}, captured owner {visual.Identity}.");
                        Assert.True(visual.Identity.Generation > 0);
                        capturedOwnedBanks.Add(visual.Identity.Bank);
                    }
                    if (geometry.Length > 0)
                    {
                        capturedOwnedFrames++;
                    }
                }
                if (primitive is not null && ownedCommands.Length > 0 && (peak is null || ownedCommands.Length > renderedCommandCount))
                {
                    renderedCommandCount = Math.Max(renderedCommandCount, ownedCommands.Length);
                    renderedSamples++;
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = ownedCommands.Select(command => command.Packet).ToImmutableHashSet();
                    BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
                    byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
                    int changed = 0;
                    for (int pixel = 0; pixel < pixels.Length; pixel += 4)
                    {
                        if (pixels[pixel] != plain[pixel] || pixels[pixel + 1] != plain[pixel + 1] || pixels[pixel + 2] != plain[pixel + 2])
                        {
                            changed++;
                        }
                    }
                    if (changed > peakChanged)
                    {
                        peakChanged = changed;
                        peak = current;
                        peakPixels = pixels;
                        peakPlain = plain;
                    }
                    if (changed > 0)
                    {
                        Cold("visible", current);
                    }
                }
                prior = current;
                if (Released(current) && (effect != 18 || !NativeBattleControl18CastingAssertions.HasCasting(current)))
                {
                    break;
                }
            }
            Assert.True(callbacks < maximum, "The selected component exceeded its diagnostic callback budget.");
            Assert.Equal(1, completions);
            if (effect == 18)
            {
                Assert.True(casting18Seen, "The original18 sequence30 must reach its authored casting seed and children.");
                Assert.True(casting18Frames > 1);
                Assert.True(casting18Tail, "The original18 casting seed must reach its final native counters before release.");
                Assert.False(NativeBattleControl18CastingAssertions.HasCasting(battle.Capture()));
            }
            if (effect == 276)
            {
                Assert.True(customComponentCapturedFrames > 1, "The strict original276 component must reach owned moving geometry on multiple native callbacks.");
            }
            if (effect == 30)
            {
                Assert.True(captured30 > 0);
            }
            if (effect is 265 or 266 or 267 or 268 or 269 or 270)
            {
                Assert.True(captured31Frames > 1);
            }
            if (effect is 248 or 250 or 253 or 254 or 255 or 261 or 262 or 263 or 264)
            {
                Assert.True(captured30Frames > 1);
            }
            if (effect is 236 or 237 or 243 or 244 or 245 or 246 or 247)
            {
                Assert.True(capturedResourceFrames > 1);
            }
            if (effect == 218)
            {
                Assert.True(captured218Frames > 1);
            }
            if (effect is 9 or 17 or 18 or 168 or 179 or 180 or 187)
            {
                Assert.True(capturedOwnedFrames > 1, "The standalone component must reach captured physical geometry on multiple callbacks.");
            }
            if (effect == 9)
            {
                Assert.Contains(0x27571A0u, capturedOwnedBanks);
                Assert.Contains(0x274F628u, capturedOwnedBanks);
                Assert.Contains(0x274F230u, capturedOwnedBanks);
            }
            if (effect == 17)
            {
                Assert.Contains(0x2556FC8u, capturedOwnedBanks);
                Assert.Contains(0x2556FD8u, capturedOwnedBanks);
            }
            Assert.Equal(expectedContactTransitions, contactTransitions);
            if (!livePartyFollowup)
            {
                Assert.True(peakChanged > 0, "The selected component did not produce visible physical commands.");
            }
            if (effect == 7)
            {
                Assert.True(recoveryCapturedGeometry > 0);
            }
            BattleSnapshot completed = battle.Capture();
            if (effect == 218)
            {
                Assert.Equal(selectedHeader.ReadByte(0), selectedContact.Primary.ActorIndex);
                string textActor218 = source.NativeGraphics!.ActorIds[selectedContact.Primary.ActorIndex]!;
                BattleCombatantDefinition textDefinition218 = source.Definition.Combatants.Single(actor => actor.Id == textActor218);
                Assert.Equal(128, textDefinition218.ModelId);
                NativeBattleGraphicsResource textResource218 = assets.ReadNativeGraphicsResource(textDefinition218);
                BattleEvent[] textEvents218 = completed.Events.Where(item => item.Kind == "NativeText" && item.ActorId == textActor218).ToArray();
                Assert.Equal(new[] { 0, 1, 2, 3, 5, 6 }.Select(textResource218.ReadText91), textEvents218.Select(item => item.Message));
                Assert.Equal(new[] { 45, 45, 45, 15, 20, 45 }, textEvents218.Select(item => item.Amount));
                Assert.Equal(1, contact218Services);
                text218Count = textEvents218.Length;
            }
            if (peak is not null)
            {
                Cold("peak", peak);
                NativeBattleRestorationPresentationTests.Publish(output!, "visible-peak", peak, peakPixels!);
                File.WriteAllBytes(Path.Combine(output!, "without-effect.rgba"), peakPlain!);
            }
            Cold("completed", completed);
            Assert.True(Released(completed), "The selected component must release its actual owners at the caller boundary.");
            Cold("caller-ready", completed);
            observer?.Write(output!);
            observer314?.Accept(completed, output!);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = effect, LivePartyFollowup = livePartyFollowup, Callbacks = callbacks + 1, Completions = completions, ExpectedContactTransitions = expectedContactTransitions, ContactTransitions = contactTransitions, Casting18Seen = casting18Seen, Casting18Tail = casting18Tail, Casting18Frames = casting18Frames, Self218ContactServices = contact218Services, Original218TextMessages = text218Count, ChangedRgb = peakChanged, RenderedSamples = renderedSamples, VisualSelection = "Visible witness at increasing owned-command counts", PublishedBoundaries = boundaries.Order(), RestoredBoundaries = restoredBoundaries.Order(), SourceFrame = source.Frame, battle.Capture().Frame }));
        }
        catch (Exception error)
        {
            observer?.Write(output!);
            observer314?.Publish(output!);
            NativeBattleRestorationPresentationTests.Publish(output!, "unsupported", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output!, "error.txt"), error.ToString());
            throw;
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            return restored;
        }

        void Cold(string name, BattleSnapshot snapshot)
        {
            if (!boundaries.Add(name))
            {
                return;
            }
            NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, null);
            if (name is not ("queued" or "initialized" or "contact" or "peak" or "completed" or "caller-ready" or "draw-22-init" or "draw-22-body" or "draw-22-live" or "draw-52-init" or "draw-52-live" or "draw-5-mode-2" or "update-8" or "casting18-live" or "casting18-tail" or "casting18-released"))
            {
                return;
            }
            restoredBoundaries.Add(name);
            BattleSnapshot json = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + ".battle.json"));
            BattleSession cold = Restore(json);
            BattleSession direct = Restore(snapshot);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            cold.Tick();
            direct.Tick();
            if (effect == 276)
            {
                NativeBattleCustomComponentCaptureAssertions.AssertFrame(direct.Capture(), effect);
                NativeBattleCustomComponentCaptureAssertions.AssertFrame(cold.Capture(), effect);
            }
            NativeBattleCloudBombPresentationOwnership.AssertFrame(direct.Capture(), effect);
            NativeBattleCloudBombPresentationOwnership.AssertFrame(cold.Capture(), effect);
            if (effect == 187)
            {
                _ = NativeBattleActorLowerCaptureAssertions.AssertOwned(cold.Capture(), 187);
                _ = NativeBattleActorLowerCaptureAssertions.AssertOwned(direct.Capture(), 187);
            }
            if (effect is 265 or 266 or 267 or 268 or 269 or 270)
            {
                _ = NativeBattleCinematicCaptureAssertions.AssertFrame(cold.Capture(), effect);
                _ = NativeBattleCinematicCaptureAssertions.AssertFrame(direct.Capture(), effect);
            }
            if (effect is 248 or 250 or 253 or 254 or 255 or 261 or 262 or 263 or 264)
            {
                _ = NativeBattleCinematicCaptureAssertions.AssertFrame(cold.Capture(), effect);
                _ = NativeBattleCinematicCaptureAssertions.AssertFrame(direct.Capture(), effect);
            }
            if (effect is 236 or 237 or 243 or 244 or 245 or 246 or 247)
            {
                _ = NativeBattleCinematicCaptureAssertions.AssertFrame(cold.Capture(), effect);
                _ = NativeBattleCinematicCaptureAssertions.AssertFrame(direct.Capture(), effect);
            }
            if (effect == 218)
            {
                NativeBattleCinematicCaptureAssertions.AssertFrame(direct.Capture(), effect);
                NativeBattleCinematicCaptureAssertions.AssertFrame(cold.Capture(), effect);
                if (cold.Capture().NativeEffectMemory?.Effect218 is { CommonPacketStart: not null, CommonPacketEnd: not null })
                {
                    NativeBattleCinematicCaptureAssertions.AssertCommands(cold.Capture(), effect, NativeBattleSelectedEffect218Packets.CurrentCommands(cold.Capture()));
                    NativeBattleCinematicCaptureAssertions.AssertCommands(direct.Capture(), effect, NativeBattleSelectedEffect218Packets.CurrentCommands(direct.Capture()));
                }
            }
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            if (effect == 18 && cold.NativeGraphics?.OriginalEffect?.Binding.Effect == 18)
            {
                NativeBattleControl18CastingAssertions.AssertFrame(cold.Capture());
                NativeBattleControl18CastingAssertions.AssertFrame(direct.Capture());
            }
            Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition, true).Render(direct, 320, 224).ToArray(), new BattleScenePresentation(assets, snapshot.Definition, true).Render(cold, 320, 224).ToArray());
        }

        bool Released(BattleSnapshot snapshot)
        {
            return snapshot.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: 0 } original && original.Binding.Effect == effect && original.Pools.All(pool => pool.Head is null && pool.Tail is null && pool.Slots.All(slot => slot.Flags == 0)) && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104);
        }
    }

    private static BattleSnapshot QueueLivePartyFollowing(BattleSnapshot current, ushort effect)
    {
        Assert.True(current.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: 0 });
        Assert.NotNull(current.NativeGraphics.ActorIds[0]);
        Assert.Equal(0, current.NativeGraphics.Models[0].Flags0 & 8);
        BattleNativeAction action = Assert.Single(current.Definition.NativeActions, action => action.Command == 2 && action.ActionId == 1);
        Assert.Equal(effect, checked((ushort)action.MagicId));
        NativeBattleActionPacketsState packets = current.NativeActionPackets!;
        NativeBattleContactPacket contact = Assert.IsType<NativeBattleContactPacket>(packets.Contacts.First(contact => contact is not null));
        Assert.Equal(0, contact.Primary.ActorIndex);
        Assert.Equal(7, contact.Primary.Amount6);
        byte sequence = NativeBattleEffectFixtureExporter.SelectSequence(current.Definition.NativeCommandSequences, action.Command, action.ActionId);
        (packets, int header) = packets.Prepare(new(0, action.Command, sequence, -1, action.ActionId, effect, null));
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = current.NativeGraphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        return current with { NativeActionPackets = packets, NativeGraphics = current.NativeGraphics with { Descriptors = descriptors } };
    }

    private static BattleSnapshot QueueFollowing(BattleSnapshot current, BattleSnapshot invocation, ushort effect)
    {
        Assert.True(current.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: 0 });
        NativeBattleGraphicsState graphics = invocation.NativeGraphics!;
        int originalHeader;
        if (graphics.OriginalEffect is { ReturnedDescriptor: null, Parameters: { } parameters } admitted)
        {
            Assert.Equal(effect, admitted.RequestedEffect);
            Assert.Empty(admitted.Pools);
            originalHeader = parameters.HeaderIndex7C;
        }
        else
        {
            int descriptor = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
            uint offset = graphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u;
            Assert.Equal(0u, offset % 20);
            originalHeader = checked((int)(offset / 20));
        }
        NativeBattleActionPacketsState original = invocation.NativeActionPackets!;
        NativeBattleActionHeaderSlot header = original.Headers[originalHeader];
        Assert.Equal(effect, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal(1, header.ReadByte(0x10));
        Assert.Equal(0, header.ReadByte(0x11));
        Assert.Equal(graphics.ActorIds.ToArray(), current.NativeGraphics!.ActorIds.ToArray());
        NativeBattleActionPacketsState packets = current.NativeActionPackets!;
        (packets, int nextHeader) = packets.Prepare(new(header.ReadByte(0), header.ReadByte(1), header.ReadByte(2), unchecked((sbyte)header.ReadByte(3)), NativeBattleActionGraphics.Word(header, 4), effect, header.Data));
        Assert.NotNull(header.FirstContactIndex);
        (packets, _) = packets.AppendContact(Assert.IsType<NativeBattleContactPacket>(original.Contacts[header.FirstContactIndex.Value]));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = current.NativeGraphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)nextHeader * 20));
        return current with { NativeActionPackets = packets, NativeGraphics = current.NativeGraphics with { Descriptors = descriptors } };
    }
}
