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
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? selection = Environment.GetEnvironmentVariable("MEMORIA_FF8_CINEMATIC_EFFECT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CINEMATIC_EVIDENCE");
        Assert.SkipUnless(directory is not null && selection is not null && output is not null, "Select installed original resources, one cinematic effect and a distinct evidence directory.");
        ushort effectId = ushort.Parse(selection!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(effectId, new ushort[] { 75, 77, 208, 211, 219, 220 });
        Assert.True(Directory.Exists(directory));
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        BattleSession? battle = null;
        BattleSnapshot? prior = null;
        BattleScenePresentation? scene = null;
        int callbacks = 0;
        int contacts = 0;
        int completions = 0;
        int visibleChangedRgb = 0;
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
            int encounter = effectId switch { 77 => 981, 219 => 462, _ => 94 };
            NativeEffectFixtureRequest request = new($"cinematic{effectId}", effectId, action.Command, action.ActionId, 0, 800, 240) { ControlledShellEffect = originalAction is null ? action.MagicId : null, EncounterId = encounter, CasterActorIndex = caster };
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
            Assert.Equal((byte)0, contact.Primary.ActorIndex);
            Assert.Equal((ushort)7, contact.Primary.Amount6);
            Assert.Equal((byte)255, contact.Secondary.ActorIndex);
            NativeBattleEffectTask expectedText = Assert.Single(NativeBattleEffectsState.Clear().PrependDamageText(contact.Primary.ActorIndex, contact.Primary.ContactFlags, contact.Primary.Amount6).Slots, task => (task.Flags0 & 1) != 0);
            string target = source.NativeGraphics!.ActorIds[0]!;
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
                NativeBattleOriginalEffectState? before = Selected(prior);
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                NativeBattleOriginalEffectState? after = Selected(current);
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
                    if ((text.Flags0 & 1) == 0 || text.Kind != NativeBattleEffectKind.DamageText5069B0 || text.ModelIndex != 0 || (prior.NativeGraphics!.Effects.Slots[slot].Flags0 & 1) != 0)
                    {
                        continue;
                    }
                    Assert.Equal(expectedText.Glyphs.ToArray(), text.Glyphs.ToArray());
                    Assert.Equal(expectedText.Color, text.Color);
                    Assert.Equal(expectedText.HalfWidth, text.HalfWidth);
                    contacts++;
                    ColdNext("contact-" + contacts, current);
                }
                if (visibleChangedRgb == 0 && after?.ReturnedDescriptor is > 0 && current.NativeEffectMemory?.Cinematic?.Effect == effectId)
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
                        visibleChangedRgb = CountRgbChanges(pixels, plain);
                        if (visibleChangedRgb > 0)
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
            Assert.Equal(1, contacts);
            Assert.Equal(1, completions);
            Assert.True(completed.Events.Count(item => item.NativeModuleSound is not null) > initialSounds, "The selected component published no original module sound.");
            Assert.True(Released(completed), "The original returned task, active effect and action104 descriptor must release through native execution.");
            ColdNext("completed", completed);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Controlled post-formula seven-HP component; no ordinary action or formula admission", Effect = effectId, exported.Selection, action.Command, action.ActionId, callbacks, DiagnosticMaximumCallbacks = 4096, contacts, completions, visibleChangedRgb, cameraChanged, RestoredBoundaries = restoredBoundaries, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));

            NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot)
            {
                return snapshot.NativeGraphics?.OriginalEffect is { } selected && selected.Binding.Effect == effectId ? selected : null;
            }

            void ColdNext(string name, BattleSnapshot snapshot)
            {
                boundary = "cold-" + name;
                prior = snapshot;
                NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, scene.Render(BattleSession.Restore(snapshot), 320, 224).ToArray());
                BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + ".battle.json")));
                BattleSession direct = BattleSession.Restore(snapshot);
                cold.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                direct.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
                cold.Tick();
                direct.Tick();
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
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Scenario = "Controlled post-formula component", Effect = effectId, boundary, callbacks, contacts, completions, visibleChangedRgb, initialized, liveRestored, RestoredBoundaries = restoredBoundaries, Error = error.ToString() }));
            throw;
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
