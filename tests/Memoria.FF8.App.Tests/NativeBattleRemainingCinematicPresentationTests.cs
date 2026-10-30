using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRemainingCinematicPresentationTests
{
    [Fact]
    public void Selected_original_cinematic_component_completes_controlled_contact_and_cold_boundaries()
    {
        RunSelected([75, 77, 208, 211, 219, 220], "MEMORIA_FF8_CINEMATIC");
    }

    internal static void RunSelected(ushort[] supportedEffects, string variablePrefix, Func<BattleAssets, ushort, NativeEffectFixtureRequest>? selectFixture = null, Func<BattleSnapshot, BattleSnapshot, bool>? contactGate = null, Action<BattleSnapshot, BattleSnapshot>? observeCallback = null, bool capturePeak = false)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? selection = Environment.GetEnvironmentVariable(variablePrefix + "_EFFECT");
        string? output = Environment.GetEnvironmentVariable(variablePrefix + "_EVIDENCE");
        Assert.SkipUnless(directory is not null && selection is not null && output is not null, "Select installed original resources, one cinematic effect and a distinct evidence directory.");
        ushort effectId = ushort.Parse(selection!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(effectId, supportedEffects);
        Assert.True(Directory.Exists(directory));
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        BattleSession? battle = null;
        BattleSnapshot? prior = null;
        BattleScenePresentation? scene = null;
        int callbacks = 0;
        int contacts = 0;
        int glyphs = 0;
        int completions = 0;
        int captured28Frames = 0;
        int visibleChangedRgb = 0;
        int peakChangedRgb = 0;
        BattleSnapshot? peakSnapshot = null;
        byte[]? peakPixels = null;
        BattleSnapshot? peakWithout = null;
        byte[]? peakPlain = null;
        bool initialized = false;
        bool liveRestored = false;
        bool cameraChanged = false;
        List<string> restoredBoundaries = [];
        string boundary = "original-resources";
        try
        {
            using GameAssets game = new(directory!);
            using BattleAssets assets = new(directory!);
            // One unchanged installed kernel row supplies identity and sequence; this component does not execute its formula.
            BattleNativeAction? originalAction = assets.NativeActions.Where(row => row.MagicId == effectId).OrderBy(row => row.Command).ThenBy(row => row.ActionId).FirstOrDefault();
            // A missing kernel reference requires explicit direct-module selection, never a renamed native action.
            BattleNativeAction action = originalAction ?? Assert.Single(assets.NativeActions, row => row.Command == 2 && row.ActionId == 1);
            NativeBattleEffectBinding original = game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1];
            string fixture = Path.Combine(output!, "fixture");
            boundary = "export-controlled-component";
            byte caster = effectId is 77 or 219 ? (byte)3 : (byte)0;
            byte contactActor = effectId == 77 ? caster : (byte)0;
            int encounter = effectId switch { 77 => 981, 219 => 462, _ => 94 };
            NativeEffectFixtureRequest request = selectFixture is null ? new($"cinematic{effectId}", effectId, action.Command, action.ActionId, contactActor, 800, 240) { ControlledShellEffect = originalAction is null ? action.MagicId : null, EncounterId = encounter, CasterActorIndex = caster } : selectFixture(assets, effectId);
            caster = request.CasterActorIndex;
            contactActor = request.TargetActorIndex;
            if (selectFixture is not null)
            {
                Assert.Equal((byte)21, NativeBattleEffectFixtureExporter.SelectSequence(game.ReadNativeBattleCommandSequences(), request.Command, request.ActionId));
                Assert.Equal(action.Command, request.Command);
                Assert.Equal(action.ActionId, request.ActionId);
            }
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory!, fixture);
            File.WriteAllText(Path.Combine(output!, "fixture-result.json"), JsonSerializer.Serialize(exported));
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
            BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
            prior = source;
            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[effectId - 1]));
            int descriptor = Assert.Single(source.NativeGraphics!.Descriptors.Queue, index => source.NativeGraphics.Descriptors.Packets[index].Command == 104);
            uint headerOffset = checked(source.NativeGraphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u);
            Assert.Equal(0u, headerOffset % 20);
            NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(headerOffset / 20))];
            Assert.Equal(caster, header.ReadByte(0));
            Assert.Equal(action.Command, header.ReadByte(1));
            Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
            Assert.Equal(effectId, NativeBattleActionGraphics.Word(header, 6));
            Assert.Equal(originalAction is not null, exported.Selection!.OriginalKernelEffectReference);
            Assert.Equal(action.MagicId, exported.Selection.ShellEffect);
            NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
            Assert.Equal((byte)1, source.NativeActionPackets.ContactCount);
            Assert.Equal(contactActor, contact.Primary.ActorIndex);
            Assert.Equal((ushort)7, contact.Primary.Amount6);
            Assert.Equal((byte)255, contact.Secondary.ActorIndex);
            if (selectFixture is not null)
            {
                NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == contactActor).NativeStatus!;
                Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
                Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
                File.WriteAllText(Path.Combine(output!, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Explicit seven-HP post-formula presentation with original Blue Magic command/action/sequence and retained logical HP/status; no formula, ordinary command or AI admission", Contact = contact }));
            }
            NativeBattleEffectTask? expectedText = NativeBattleEffectsState.Clear().PrependDamageText(contact.Primary.ActorIndex, contact.Primary.ContactFlags, contact.Primary.Amount6).Slots.SingleOrDefault(task => (task.Flags0 & 1) != 0);
            string target = source.NativeGraphics!.ActorIds[contactActor]!;
            int logicalHp = source.Combatants.Single(actor => actor.Id == target).CurrentHp;
            int initialSounds = source.Events.Count(item => item.NativeModuleSound is not null);
            string initialCamera = JsonSerializer.Serialize(source.NativeCamera);
            battle = BattleSession.Restore(source);
            battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
            scene = new(assets, source.Definition, true);
            ColdNext("queued", source);
            // This is a diagnostic liveness guard, never a claim about an effect's native duration.
            for (; callbacks < 4096; callbacks++)
            {
                boundary = "native-callback";
                prior = battle.Capture();
                BattleSnapshot callbackBefore = prior;
                NativeBattleOriginalEffectState? before = Selected(prior);
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                if (effectId is 208 or 211 or 219 && NativeBattleCinematicCaptureAssertions.AssertFrame(current, effectId) > 0)
                {
                    captured28Frames++;
                }
                NativeBattleOriginalEffectState? after = Selected(current);
                observeCallback?.Invoke(prior, current);
                Assert.Equal(logicalHp, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                Assert.NotNull(current.NativeCamera?.Projection?.View);
                cameraChanged |= JsonSerializer.Serialize(current.NativeCamera) != initialCamera;
                if (!initialized && after?.ReturnedDescriptor is > 0)
                {
                    Assert.Equal(effectId, current.NativeEffectMemory!.Cinematic!.Effect);
                    initialized = true;
                    ColdNext("initialized", current);
                }
                for (int slot = 0; slot < current.NativeGraphics!.Effects.Slots.Length; slot++)
                {
                    NativeBattleEffectTask text = current.NativeGraphics.Effects.Slots[slot];
                    if ((text.Flags0 & 1) == 0 || text.Kind != NativeBattleEffectKind.DamageText5069B0 || text.ModelIndex != contactActor || (prior.NativeGraphics!.Effects.Slots[slot].Flags0 & 1) != 0)
                    {
                        continue;
                    }
                    Assert.NotNull(expectedText);
                    Assert.Equal(expectedText.Glyphs.ToArray(), text.Glyphs.ToArray());
                    Assert.Equal(expectedText.Color, text.Color);
                    Assert.Equal(expectedText.HalfWidth, text.HalfWidth);
                    glyphs++;
                    if (contactGate is null)
                    {
                        contacts++;
                        ColdNext("contact-" + contacts, current);
                    }
                    else
                    {
                        ColdNext("glyph-" + glyphs, current);
                    }
                }
                if (contactGate is not null && contactGate(callbackBefore, current))
                {
                    contacts++;
                    ColdNext("contact-" + contacts, current);
                }
                if (effectId == 77 && after?.ReturnedDescriptor is > 0)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> smoothOwned = NativeBattleTargetGuardianCastTests.GuardianPacketReferences(prior, current, effectId, game);
                    foreach (NativeBattleLogicalCommand command in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => smoothOwned.Contains(command.Packet) && command.Opcode is >= 0x20 and < 0x80))
                    {
                        Assert.True(command.PilotGeometry is not null, $"Effect77 uncaptured pass at frame {current.Frame}: {command.Packet}, opcode {command.Opcode:X2}, word {command.WordOffset}.");
                        Assert.True(command.PilotGeometry!.Identity.Effect == effectId, $"Effect77 identity mismatch at frame {current.Frame}: {command.Packet}, opcode {command.Opcode:X2}, word {command.WordOffset}, identity {command.PilotGeometry.Identity}.");
                    }
                }
                if ((capturePeak || visibleChangedRgb == 0) && after?.ReturnedDescriptor is > 0 && current.NativeEffectMemory?.Cinematic?.Effect == effectId)
                {
                    // The shared cinematic owner publishes its private common-bank prefix through the manager cursor.
                    // The established packet selector includes only that proved prefix and current effect-arena packets.
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = NativeBattleTargetGuardianCastTests.GuardianPacketReferences(prior, current, effectId, game);
                    if (!owned.IsEmpty)
                    {
                        NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        NativeBattlePrimitiveFrame withoutFrame = frame with { LastCommands = frame.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() };
                        BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = withoutFrame } };
                        byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
                        int changedRgb = CountRgbChanges(pixels, plain);
                        if (capturePeak && changedRgb > peakChangedRgb)
                        {
                            peakChangedRgb = changedRgb;
                            peakSnapshot = current;
                            peakPixels = pixels;
                            peakWithout = without;
                            peakPlain = plain;
                        }
                        if (capturePeak)
                        {
                            visibleChangedRgb = Math.Max(visibleChangedRgb, changedRgb);
                        }
                        else
                        {
                            visibleChangedRgb = changedRgb;
                        }
                        if (!liveRestored && changedRgb > 0)
                        {
                            NativeBattleRestorationPresentationTests.Publish(output!, "visible", current, pixels);
                            NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", without, plain);
                            ColdNext("live", current);
                            liveRestored = true;
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
            Assert.True(initialized, "The original cinematic never initialized its selected controller.");
            Assert.True(liveRestored, "No significant original effect packet contribution reached cold continuation.");
            Assert.True(visibleChangedRgb > 0);
            Assert.Equal(contactGate is null && expectedText is null ? 0 : 1, contacts);
            Assert.Equal(1, completions);
            if (effectId is 208 or 211 or 219)
            {
                Assert.True(captured28Frames > 1);
            }
            Assert.True(completed.Events.Count(item => item.NativeModuleSound is not null) > initialSounds, "The selected component published no original module sound.");
            Assert.True(Released(completed), "The original returned task, active effect and action104 descriptor must release through native execution.");
            if (capturePeak)
            {
                Assert.NotNull(peakSnapshot);
                Assert.NotNull(peakPixels);
                Assert.NotNull(peakWithout);
                Assert.NotNull(peakPlain);
                Assert.True(peakChangedRgb > 0);
                ColdNext("peak", peakSnapshot, peakPixels);
                NativeBattleRestorationPresentationTests.Publish(output!, "peak-without-effect", peakWithout, peakPlain);
            }
            ColdNext("completed", completed);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Controlled post-formula seven-HP component; no ordinary action or formula admission", Effect = effectId, exported.Selection, action.Command, action.ActionId, callbacks, DiagnosticMaximumCallbacks = 4096, contacts, glyphs, completions, visibleChangedRgb, peakChangedRgb, PeakFrame = peakSnapshot?.Frame, PeakNativeCallback = peakSnapshot?.NativeGraphics?.Callback, cameraChanged, RestoredBoundaries = restoredBoundaries, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));

            NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot)
            {
                return snapshot.NativeGraphics?.OriginalEffect is { } selected && selected.Binding.Effect == effectId ? selected : null;
            }

            void ColdNext(string name, BattleSnapshot snapshot, byte[]? savedPixels = null)
            {
                boundary = "cold-" + name;
                prior = snapshot;
                NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, savedPixels ?? scene.Render(BattleSession.Restore(snapshot), 320, 224).ToArray());
                BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + ".battle.json")));
                BattleSession direct = BattleSession.Restore(snapshot);
                cold.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                direct.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
                cold.Tick();
                direct.Tick();
                if (effectId is 208 or 211 or 219)
                {
                    NativeBattleCinematicCaptureAssertions.AssertFrame(direct.Capture(), effectId);
                    NativeBattleCinematicCaptureAssertions.AssertFrame(cold.Capture(), effectId);
                }
                Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
                byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray();
                Assert.Equal(scene.Render(direct, 320, 224).ToArray(), pixels);
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
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Scenario = "Controlled post-formula component", Effect = effectId, boundary, callbacks, contacts, glyphs, completions, visibleChangedRgb, initialized, liveRestored, RestoredBoundaries = restoredBoundaries, Error = error.ToString() }));
            throw;
        }
    }

    [Fact]
    public void Resume_the_selected_cinematic_standalone_through_native_completion()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_CINEMATIC_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CINEMATIC_EVIDENCE");
        Assert.SkipUnless(directory is not null && input is not null && output is not null, "Select installed resources, a concrete cinematic standalone and a distinct output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(input!);
        ushort effect = source.NativeGraphics!.OriginalEffect!.Binding.Effect;
        Assert.True(NativeBattleCinematicProfile.IsSupported(effect));
        Assert.True(source.NativeGraphics.OriginalEffect.ReturnedDescriptor > 0);
        BattleSession battle = Restore(source);
        Dictionary<string, int> hitPoints = source.Combatants.ToDictionary(actor => actor.Id, actor => actor.CurrentHp);
        int completions = 0;
        int callbacks = 0;
        BattleSnapshot current = source;
        try
        {
            CompareNext("before", source);
            for (; callbacks < 1024 && !Released(current); callbacks++)
            {
                bool active = current.NativeGraphics!.OriginalEffect!.ReturnedDescriptor > 0;
                battle.Tick();
                current = battle.Capture();
                Assert.Equal(effect, current.NativeGraphics!.OriginalEffect!.Binding.Effect);
                foreach (BattleCombatantSnapshot actor in current.Combatants)
                {
                    Assert.Equal(hitPoints[actor.Id], actor.CurrentHp);
                }
                if (active && current.NativeGraphics.OriginalEffect.ReturnedDescriptor == 0)
                {
                    completions++;
                }
            }
            Assert.Equal(1, completions);
            Assert.True(Released(current), "The retained cinematic task and action104 descriptor must complete through native execution.");
            CompareNext("completed", current);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = effect, Scenario = "Continuation of a concrete controlled cinematic standalone; no replay or formula admission", SourceFrame = source.Frame, SourceCallback = source.NativeGraphics!.Callback, callbacks, completions, current.Frame, NativeCallback = current.NativeGraphics!.Callback }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", current, null);
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Effect = effect, callbacks, completions, Error = error.ToString() }));
            throw;
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            return restored;
        }

        void CompareNext(string name, BattleSnapshot snapshot)
        {
            byte[] originalPixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(Restore(snapshot), 320, 224).ToArray();
            NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, originalPixels);
            BattleSession direct = Restore(snapshot);
            BattleSession cold = Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + ".battle.json")));
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            direct.Tick();
            cold.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition, true).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output!, name + "-next", cold.Capture(), pixels);
        }
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
