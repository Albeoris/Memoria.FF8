using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect65PresentationTests
{
    [Fact]
    public void Original_Effect65_declared_allocated_component_preserves_current_geometry_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([65], "MEMORIA_FF8_EFFECT65");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition);
        NativeBattleEffect65PresentationObserver observer = new();
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
                NativeBattleGraphicsState currentGraphics = current.ReadFixturePresentation().NativeGraphics ?? throw new InvalidDataException("Effect65 lost its graphics state.");
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());

                cameraChanged |= JsonSerializer.Serialize(source.ReadFixturePresentation().NativeCamera) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeCamera);
                stageChanged |= JsonSerializer.Serialize(source.ReadFixturePresentation().NativeStage) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeStage);
                if (prior.ReadFixturePresentation().NativeGraphics!.OriginalEffect is { Binding.Effect: 65, ReturnedDescriptor: > 0 } && currentGraphics.OriginalEffect is { Binding.Effect: 65, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                observer.Observe(prior, current, ColdNext);
                NativeBattleLogicalCommand[] commands = observer.CurrentCommands;
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
            Assert.True(callbacks < 1800, "The original effect65 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(cameraChanged);
            Assert.True(stageChanged);
            Assert.True(changedRgb > 100, "No actual effect65 primitive changed the rendered RGB output.");
            Assert.True(geometryChangedRgb > 100, "No effect65 geometry changed rendered RGB outside its fullviewport fade.");
            Assert.Equal(1, observer.Contacts);
            Assert.Equal(1, observer.ContactGlyphs);
            observer.Accept(completed, output);
            ColdNext("effect65-visible-peak", peak);
            ColdNext("effect65-geometry-visible-peak", geometryPeak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "geometry-without-effect.rgba"), geometryPlain!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared allocated original124 command8/action360/sequence13 post-formula Amount0 renderer and lifecycle; current loaded ACT, generated strips, particles and fullviewport fade; ordinary AI and formula admission excluded", Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, ChangedRgb = changedRgb, GeometryChangedRgb = geometryChangedRgb, CameraChanged = cameraChanged, StageChanged = stageChanged, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.ReadFixturePresentation().NativeGraphics!.Callback }));
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
            BattleSnapshot json = NativeBattleFixtureInspection.ReadLogicalSnapshot(Path.Combine(output, name + ".battle.json"));
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
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT65_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select the concrete original allocated formation511 source for effect65.");
        BattleSnapshot original = NativeBattleFixtureInspection.ReadLogicalSnapshot(path);
        Assert.Equal(511, original.Definition.EncounterId);
        Assert.Equal(142, original.Definition.StageId);
        BattleSession restored = BattleSession.Restore(original);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(restored.Capture()));
        NativeBattleGraphicsState graphics = original.ReadFixturePresentation().NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.ReadFixturePresentation().NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect65State.FromColdProcess()), JsonSerializer.Serialize(original.ReadFixturePresentation().NativeEffectMemory!.Effect65));
        string casterId = ReadOriginalModelActorId(graphics, 5) ?? throw new InvalidDataException("Effect65 requires its actual allocated caster.");
        NativeBattleEnemyAllocation allocation = Assert.Single(original.NativeAllocations, item => item.Id == casterId);
        Assert.Equal(3, allocation.SceneSlot);
        Assert.Equal(1, allocation.Generation);
        BattleCombatantDefinition caster = restored.GetCombatantDefinition(casterId);
        Assert.Equal(124, caster.ModelId);
        Assert.Equal((byte)140, ReadOriginalModelCell(graphics, 5).ModelId4);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource ?? throw new InvalidDataException("Effect65 requires its current allocated skeleton.");
        Assert.Equal(71, NativeBattleBodyBoneMatrices.Require(ReadOriginalModelCell(graphics, 5), pose).Length);
        Assert.NotNull(ReadOriginalModelCell(graphics, 5).GeometryBounds52);
        Assert.NotNull(ReadOriginalModelCell(graphics, 5).GeometryWorldMatrix);
        Assert.Equal((ushort)1448, original.ReadFixturePresentation().NativeModelCpuResources!.ScratchMaximum);
        Assert.NotNull(original.ReadFixturePresentation().NativeStage!.FinalStage142);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 360 && row.MagicId == 65 && row.KernelSection == 3 && row.KernelIndex == 360);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(124, 1), action));
        NativeBattleActionPacketsState packets = original.ReadFixturePresentation().NativeActionPackets!;
        int priorActions = packets.ActionCount;
        int priorContacts = packets.ContactCount;
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(5), 8, 13, -1, 360, 65, null));
        string targetId = ReadOriginalModelActorId(graphics, 0) ?? throw new InvalidDataException("Effect65 requires its actual party recipient.");
        BattleCombatantSnapshot target = original.Combatants.Single(actor => actor.Id == targetId);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("Effect65 requires its current recipient status.");
        BattleCombatantSnapshot preparedTarget = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattleContactPacket contact = new(new(packets.ModelAliases.DecodeLiteral(0), action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), null);
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorActions, header.Ordinal);
        Assert.Equal(priorContacts + 1, packets.ContactCount);
        Assert.Equal((byte)1, packets.ReadHeader(header).ReadContactCount());
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.EnqueueAction(128, header);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([65])[64];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Same(game.ReadNativeEffect65Resources(), binding.Effect65Resources);
        BattleSnapshot source = ((original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(64, binding) },Combatants = original.Combatants.Select(actor => actor.Id == targetId ? preparedTarget : actor).ToArray()}).WithFixturePresentation(facts => facts with { NativeActionPackets = packets })).WithFixturePresentation(facts => facts with { NativeGraphics = graphics with { Descriptors = descriptors } });
        Assert.Equal(original.Combatants.Select(actor => actor.CurrentHp), source.Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(graphics)), JsonSerializer.Serialize(ReadOriginalModelCells(source.ReadFixturePresentation().NativeGraphics!)));
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeEffectMemory), JsonSerializer.Serialize(source.ReadFixturePresentation().NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeModelCpuResources), JsonSerializer.Serialize(source.ReadFixturePresentation().NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(original.NativeClock), JsonSerializer.Serialize(source.NativeClock));
        File.WriteAllText(Path.Combine(output, "declared-component.json"), JsonSerializer.Serialize(new { Scenario = "Declared allocated original124 command8/action360/sequence13 declared post-formula renderer and lifecycle with one Amount0 contact; ordinary AI and formula admission excluded", ActualCasterId = casterId, PhysicalCaster = 5, Allocation = allocation, Action = action, header = packets.ReadHeader(header), PriorActionCount = priorActions, PriorContactCount = priorContacts, ContactCount = 1, Contact = contact, StatusBefore = status.Presentation, StatusAfter = preparedTarget.NativeStatus!.Presentation, SourceNativeClock = original.NativeClock, SourceScratchMaximum = original.ReadFixturePresentation().NativeModelCpuResources!.ScratchMaximum, SourceStageRotation = original.ReadFixturePresentation().NativeStage!.Rotation, SourceSavedRotation = original.ReadFixturePresentation().NativeStage!.FinalStage142!.SavedRotation, StatusCache = "Declared47E2D0 projection copies actual current Saved/Battle into presentation cache on the prepared component copy; engine producer not executed; HP/status preserved" }));
        return source;
    }

    private static bool Released(BattleSnapshot snapshot) => snapshot.ReadFixturePresentation().NativeGraphics?.OriginalEffect is { Binding.Effect: 65, ReturnedDescriptor: 0 } effect && snapshot.ReadFixturePresentation().NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Queue.Any(index => snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;
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
