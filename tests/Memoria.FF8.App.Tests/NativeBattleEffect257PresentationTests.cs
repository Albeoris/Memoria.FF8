using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect257PresentationTests
{
    internal const ushort Effect = 257;

    [Fact]
    public void Original_component_preserves_current_rigs_and_completes_native_presentation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT257_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT257_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, the current Effect257 source and a distinct evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        NativeBattleEffect257SourceAdmissionTests.ValidateSource(source, game, assets, true);
        BattleSnapshot prepared = QueueComponent(source, game, assets);
        NativeBattleEffect257SourceAdmissionTests.ValidateQueuedAction(source, prepared);
        RunPrepared(prepared, game, assets, output!);
    }

    internal static BattleSnapshot QueueComponent(BattleSnapshot original, GameAssets game, BattleAssets assets)
    {
        BattleSession restored = Restore(original, game);
        Assert.True(restored.NativeEntryReady);
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        BattleCombatantDefinition caster = restored.GetCombatantDefinition(graphics.ActorIds[3]!);
        Assert.Equal(67, caster.ModelId);
        Assert.Equal((byte)83, graphics.Models[3].ModelId4);
        BattleNativeAction action = Assert.Single(assets.NativeActions, item => item.Command == 8 && item.ActionId == 234 && item.MagicId == 257 && item.KernelSection == 3 && item.KernelIndex == 234);
        byte sequence = NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action);
        Assert.Equal((byte)12, sequence);
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        byte priorContacts = packets.ContactCount;
        byte retainedHeader11 = packets.Headers[packets.ActionCount].ReadByte(0x11);
        Assert.Equal((byte)0, retainedHeader11);
        (packets, int header) = packets.Prepare(new(3, action.Command, sequence, unchecked((sbyte)action.RawRecord[4]), action.ActionId, action.MagicId, null));
        Assert.Equal(retainedHeader11, packets.Headers[header].ReadByte(0x11));
        string target = graphics.ActorIds[0]!;
        BattleCombatantSnapshot recipient = Assert.Single(original.Combatants, actor => actor.Id == target);
        NativeBattleStatusSnapshot status = recipient.NativeStatus!;
        NativeBattleStatusSnapshot presentation = status.Presentation.Saved is null || status.Presentation.Battle is null ? status with { Presentation = new(status.Saved, status.Battle) } : status;
        NativeBattleContactPacket contact = new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0));
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorContacts + 1, packets.ContactCount);
        Assert.Equal(1, packets.Headers[header].ReadByte(16));
        Assert.Equal(0, packets.Headers[header].ReadByte(17));
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([257])[256];
        BattleSnapshot prepared = original with
        {
            Combatants = original.Combatants.Select(actor => actor.Id == target ? actor with { NativeStatus = presentation } : actor).ToArray(),
            Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(256, binding) },
            NativeActionPackets = packets,
            NativeGraphics = graphics with { Descriptors = descriptors }
        };
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(prepared.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(original.NativeModelCpuResources), JsonSerializer.Serialize(prepared.NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(original.NativeCrtRandom, prepared.NativeCrtRandom);
        Assert.Equal(original.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), prepared.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
        return prepared;
    }

    internal static BattleSnapshot RunPrepared(BattleSnapshot source, GameAssets game, BattleAssets assets, string output)
    {
        BattleSession battle = Restore(source, game);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleEffect257PresentationObserver observer = new(source);
        HashSet<string> restored = [];
        BattleSnapshot prior = source;
        BattleSnapshot? peak = null;
        byte[]? pixelsAtPeak = null;
        byte[]? plainAtPeak = null;
        BattleSnapshot? geometryPeak = null;
        byte[]? geometryPixels = null;
        byte[]? geometryPlain = null;
        int maximumRgb = 0;
        int maximumGeometryRgb = 0;
        int callbacks = 0;
        int completions = 0;
        int maximumCallbacks = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT257_MAX_CALLBACKS") is { } limit ? int.Parse(limit) : 2400;
        Assert.True(maximumCallbacks > 0, "A positive diagnostic callback budget is required.");
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-source", source, null);
        try
        {
            Cold("before-entry", source);
            for (; callbacks < maximumCallbacks; callbacks++)
            {
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                observer.Observe(prior, current, Cold);
                Assert.Equal(source.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: Effect, ReturnedDescriptor: > 0 } && current.NativeGraphics!.OriginalEffect is { Binding.Effect: Effect, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                NativeBattleLogicalCommand[] commands = NativeBattleEffect257PresentationObserver.CurrentCommands(current, prior.NativeEffectMemory!.PrimitiveProcess!.ByteCursor!.Value);
                if (commands.Length > 0)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = commands.Select(command => command.Packet).ToImmutableHashSet();
                    NativeBattleGraphicsState graphics = current.NativeGraphics!;
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame!;
                    BattleSnapshot without = current with { NativeGraphics = graphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
                    int changed = CountRgb(pixels, plain);
                    if (changed > maximumRgb)
                    {
                        maximumRgb = changed;
                        peak = current;
                        pixelsAtPeak = pixels;
                        plainAtPeak = plain;
                    }
                    ImmutableHashSet<NativeBattlePrimitiveReference> geometryReferences = commands.Where(NativeBattleEffect257PresentationObserver.IsGeometry).Select(command => command.Packet).ToImmutableHashSet();
                    if (!geometryReferences.IsEmpty)
                    {
                        BattleSnapshot withoutGeometry = current with { NativeGraphics = graphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !geometryReferences.Contains(command.Command.Packet)).ToImmutableArray() } } };
                        byte[] geometryBaseline = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(withoutGeometry), 320, 224).ToArray();
                        int geometryChanged = CountRgb(pixels, geometryBaseline);
                        if (geometryChanged > maximumGeometryRgb)
                        {
                            maximumGeometryRgb = geometryChanged;
                            geometryPeak = current;
                            geometryPixels = pixels;
                            geometryPlain = geometryBaseline;
                        }
                    }
                }
                prior = current;
                if (Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.True(callbacks < maximumCallbacks, "Effect257 did not return all native lifecycle owners within the diagnostic callback budget.");
            Assert.Equal(1, completions);
            Assert.True(maximumRgb > 0, "Current Effect257 packets did not change rendered RGB.");
            Assert.True(maximumGeometryRgb > 0, "Current selected Effect257 geometry did not change rendered RGB.");
            observer.Accept(completed, output);
            Cold("visible-peak", peak!);
            Cold("geometry-peak", geometryPeak!);
            Cold("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak!, pixelsAtPeak!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak!, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), plainAtPeak!);
            File.WriteAllBytes(Path.Combine(output, "geometry-without-effect.rgba"), geometryPlain!);
            for (int cleanup = 1; cleanup <= 4; cleanup++)
            {
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                Assert.True(Released(current));
                Assert.Equal(JsonSerializer.Serialize(completed.NativeEffectMemory!.Cinematic), JsonSerializer.Serialize(current.NativeEffectMemory!.Cinematic));
                Assert.Equal(completed.NativePartyPresentation!.Rows.Select(row => row?.HitPoints), current.NativePartyPresentation!.Rows.Select(row => row?.HitPoints));
                Assert.Equal(source.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
                Assert.Equal(JsonSerializer.Serialize(source.Random), JsonSerializer.Serialize(current.Random));
                Cold("caller-cleanup-" + cleanup, current);
            }
            Assert.True(battle.NativeEntryReady);
            BattleSnapshot ready = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output, "caller-ready", ready, null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect, Scenario = "Original67 command8/action234/sequence12; one declared post-formula contact7; ordinary AI/formula excluded", Callbacks = callbacks + 1, DiagnosticCallbackLimit = maximumCallbacks, Completions = completions, CleanupCallbacks = 4, ChangedRgb = maximumRgb, GeometryChangedRgb = maximumGeometryRgb, GeometryScope = "Remove only current selected geometry in actual ARENA and COMMON packet spans; retain other packets in both renders", RestoredBoundaryCount = restored.Count, RestoredBoundaries = restored.Order(), ready.Frame }));
            return ready;
        }
        catch (Exception error)
        {
            observer.Write(output);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Callbacks = callbacks, Error = error.ToString() }));
            throw;
        }

        void Cold(string name, BattleSnapshot snapshot)
        {
            if (restored.Add(name))
            {
                VerifyColdNext(name, snapshot, game, assets, output);
            }
        }
    }

    internal static BattleSession Restore(BattleSnapshot snapshot, GameAssets game)
    {
        BattleSession session = BattleSession.Restore(snapshot);
        session.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        return session;
    }

    internal static void VerifyColdNext(string name, BattleSnapshot snapshot, GameAssets game, BattleAssets assets, string output)
    {
        NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, null);
        BattleSnapshot json = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + ".battle.json"));
        BattleSession cold = Restore(json, game);
        BattleSession direct = Restore(snapshot, game);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
        cold.Tick();
        direct.Tick();
        Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
        byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(cold, 320, 224).ToArray();
        Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition, true).Render(direct, 320, 224).ToArray(), pixels);
        NativeBattleRestorationPresentationTests.Publish(output, name + "-next", cold.Capture(), pixels);
    }

    internal static bool TaskPoolDrained(NativeBattleOriginalEffectState effect)
    {
        return effect.Pools.Length == 1 && effect.Pools[0] is { DescriptorAddress: 0x2796510, BankAddress: 0x2796520, Stride: 0x10, Head: null, Tail: null } pool && pool.Slots.Length == 1 && pool.Slots.All(task => task.Flags == 0);
    }

    internal static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: Effect, ReturnedDescriptor: 0 } effect && TaskPoolDrained(effect) && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104);

    private static int CountRgb(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int count = 0;
        for (int index = 0; index < first.Length; index += 4)
        {
            count += first[index] != second[index] || first[index + 1] != second[index + 1] || first[index + 2] != second[index + 2] ? 1 : 0;
        }
        return count;
    }
}
