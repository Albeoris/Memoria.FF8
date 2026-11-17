using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect76PresentationTests
{
    [Fact]
    public void Original_Effect76_declared_self_component_preserves_current_geometry_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([76], "MEMORIA_FF8_EFFECT76");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition);
        NativeBattleEffect76PresentationObserver observer = new();
        HashSet<string> restored = [];
        BattleSnapshot prior = source;
        BattleSnapshot peak = source;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        BattleSnapshot geometryPeak = source;
        byte[]? geometryPixels = null;
        byte[]? geometryPlain = null;
        int callbacks = 0;
        int completions = 0;
        int changedRgb = 0;
        int geometryChangedRgb = 0;
        bool cameraChanged = false;
        bool stageChanged = false;
        string boundary = "queued-component";
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-source", source, null);
        try
        {
            ColdNext("before-worker", source);
            for (; callbacks < 1800; callbacks++)
            {
                boundary = "callback-" + callbacks;
                battle.Tick();
                BattleSnapshot current = battle.CaptureForContinuation();
                NativeBattleGraphicsState currentGraphics = current.ReadFixturePresentation().NativeGraphics ?? throw new InvalidDataException("Effect76 lost its graphics state.");
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());

                cameraChanged |= JsonSerializer.Serialize(source.ReadFixturePresentation().NativeCamera) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeCamera);
                stageChanged |= JsonSerializer.Serialize(source.ReadFixturePresentation().NativeStage) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeStage);
                if (prior.ReadFixturePresentation().NativeGraphics!.OriginalEffect is { Binding.Effect: 76, ReturnedDescriptor: > 0 } && currentGraphics.OriginalEffect is { Binding.Effect: 76, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                observer.Observe(prior, current, ColdNext);
                NativeBattleLogicalCommand[] commands = observer.CurrentCommands;
                NativeBattleReachedCaptureAssertions.Moving(76, current, commands.Where(command => !observer.CurrentFadeCommands.Contains(command)));
                if (commands.Length > 0)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = commands.Select(command => command.Packet).ToImmutableHashSet();
                    NativeBattlePrimitiveFrame primitive = currentGraphics.PrimitiveFrame!;
                    BattleSnapshot without = current.WithFixturePresentation(facts => facts with { NativeGraphics = currentGraphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } });
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    byte[] plain = new BattleScenePresentation(assets, source.Definition).Render(BattleSession.Restore(without), 320, 224).ToArray();
                    int changed = CountRgb(pixels, plain);
                    if (changed > changedRgb)
                    {
                        changedRgb = changed;
                        peak = current;
                        peakPixels = pixels;
                        peakPlain = plain;
                    }
                    if (observer.CurrentFadeCommands.Length == 0 && changed > geometryChangedRgb)
                    {
                        geometryChangedRgb = changed;
                        geometryPeak = current;
                        geometryPixels = pixels;
                        geometryPlain = plain;
                    }
                }
                prior = current;
                if (Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.CaptureForContinuation();
            Assert.True(callbacks < 1800, "The original effect76 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(cameraChanged);
            Assert.True(stageChanged);
            Assert.True(changedRgb > 100, "No actual effect76 primitive changed the rendered RGB output.");
            Assert.True(geometryChangedRgb > 100, "No effect76 geometry changed rendered RGB outside its fullviewport fade.");
            Assert.Equal(1, observer.Contacts);
            Assert.Equal(1, observer.ContactGlyphs);
            observer.Accept(completed, output);
            ColdNext("effect76-visible-peak", peak);
            ColdNext("effect76-geometry-visible-peak", geometryPeak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "geometry-without-effect.rgba"), geometryPlain!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared original126 command8/action355/sequence14 post-formula Amount0 self renderer and lifecycle; current stage replacement, particles, meshes, native camera and text; ordinary AI and formula admission excluded", Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, ChangedRgb = changedRgb, GeometryChangedRgb = geometryChangedRgb, CameraChanged = cameraChanged, StageChanged = stageChanged, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.ReadFixturePresentation().NativeGraphics!.Callback }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Boundary = boundary, Callbacks = callbacks, Error = error.ToString() }));
            throw;
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession session = BattleSession.Restore(snapshot);
            session.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return session;
        }

        void ColdNext(string name, BattleSnapshot snapshot)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, null);
            BattleSnapshot json = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + ".battle.json"));
            BattleSession cold = Restore(json);
            BattleSession direct = Restore(snapshot);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            cold.Tick();
            direct.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, source.Definition).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, source.Definition).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output, name + "-next", cold.Capture(), pixels);
            restored.Add(name);
        }
    }

    private static BattleSnapshot PrepareSource(GameAssets game, BattleAssets assets, string output)
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT76_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select the concrete current formation981 source for effect76.");
        return PrepareFromEntry(game, assets, output, NativeBattleEffectFamilyRunner.LoadSnapshot(path));
    }

    internal static BattleSnapshot PrepareFromEntry(GameAssets game, BattleAssets assets, string output, BattleSnapshot original)
    {
        Assert.Equal(981, original.Definition.EncounterId);
        Assert.Equal(142, original.Definition.StageId);
        BattleSession restored = BattleSession.Restore(original);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(restored.Capture()));
        Assert.True(restored.NativeEntryReady);
        NativeBattleGraphicsState graphics = original.ReadFixturePresentation().NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.ReadFixturePresentation().NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect76State.FromColdProcess())), JsonSerializer.Serialize(original.ReadFixturePresentation().NativeEffectMemory!.Effect76));
        Assert.Equal(0x193A998u, original.ReadFixturePresentation().NativeStage!.SourceAddress1D9A820);
        Assert.Equal(0u, original.ReadFixturePresentation().NativeStage!.ReloadColorSeed1D9A828);
        Assert.NotNull(original.Definition.NativeStageSource!.PhysicalSource);
        string casterId = ReadOriginalModelActorId(graphics, 3) ?? throw new InvalidDataException("Effect76 requires its actual original caster.");
        BattleCombatantDefinition caster = restored.GetCombatantDefinition(casterId);
        Assert.Equal(126, caster.ModelId);
        Assert.Equal((byte)142, ReadOriginalModelCell(graphics, 3).ModelId4);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource ?? throw new InvalidDataException("Effect76 requires its current skeleton.");
        Assert.Equal(71, NativeBattleBodyBoneMatrices.Require(ReadOriginalModelCell(graphics, 3), pose).Length);
        Assert.NotNull(ReadOriginalModelCell(graphics, 3).GeometryBounds52);
        Assert.NotNull(ReadOriginalModelCell(graphics, 3).GeometryWorldMatrix);
        Assert.NotNull(graphics.UiServices);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 355 && row.MagicId == 76 && row.KernelSection == 3 && row.KernelIndex == 355);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(126, 1), action));
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        int priorActions = packets.ActionCount;
        int priorContacts = packets.ContactCount;
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(3), 8, 14, -1, 355, 76, null));
        BattleCombatantSnapshot target = original.Combatants.Single(actor => actor.Id == casterId);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("Effect76 requires its current self status.");
        BattleCombatantSnapshot preparedTarget = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattleContactPacket contact = new(new(packets.ModelAliases.DecodeLiteral(3), action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), null);
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorActions, header.Ordinal);
        Assert.Equal(priorContacts + 1, packets.ContactCount);
        Assert.Equal((byte)1, packets.ReadHeader(header).ReadContactCount());
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.EnqueueAction(128, header);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([76])[75];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Same(game.ReadNativeEffect76Resources(), binding.Effect76Resources);
        BattleSnapshot source = (original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(75, binding) },Combatants = original.Combatants.Select(actor => actor.Id == casterId ? preparedTarget : actor).ToArray(),NativeActionPackets = packets}).WithFixturePresentation(facts => facts with { NativeGraphics = graphics with { Descriptors = descriptors } });
        Assert.Equal(original.Combatants.Select(actor => actor.CurrentHp), source.Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(graphics)), JsonSerializer.Serialize(ReadOriginalModelCells(source.ReadFixturePresentation().NativeGraphics!)));
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeEffectMemory), JsonSerializer.Serialize(source.ReadFixturePresentation().NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeModelCpuResources), JsonSerializer.Serialize(source.ReadFixturePresentation().NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(original.NativeClock), JsonSerializer.Serialize(source.NativeClock));
        File.WriteAllText(Path.Combine(output, "declared-component.json"), JsonSerializer.Serialize(new { Scenario = "Declared original126 command8/action355/sequence14 Amount0 self component; ordinary AI and formula excluded", ActualCasterId = casterId, PhysicalCaster = 3, PhysicalTarget = 3, Action = action, header = packets.ReadHeader(header), PriorActionCount = priorActions, PriorContactCount = priorContacts, ContactCount = 1, Contact = contact, StatusBefore = status.Presentation, StatusAfter = preparedTarget.NativeStatus!.Presentation, StatusCache = "Declared47E2D0 projection copies actual current Saved/Battle into presentation cache on the prepared component copy; HP/status preserved", Stage = "Actual current physical source and corrected allocation reservation; index1 loads actual current file38 bytes" }));
        return source;
    }
    private static bool Released(BattleSnapshot snapshot) => snapshot.ReadFixturePresentation().NativeGraphics?.OriginalEffect is { Binding.Effect: 76, ReturnedDescriptor: 0 } effect && snapshot.ReadFixturePresentation().NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Queue.Any(index => snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;
    private static int CountRgb(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int count = 0;
        for (int index = 0; index < first.Length; index += 4)
        {
            if (first[index] != second[index] || first[index + 1] != second[index + 1] || first[index + 2] != second[index + 2])
            {
                count++;
            }
        }
        return count;
    }
}
