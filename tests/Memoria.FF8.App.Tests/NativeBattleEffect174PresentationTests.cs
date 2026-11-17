using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect174Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect174PresentationTests
{
    [Fact]
    public void Original_Effect174_declared_component_preserves_current_geometry_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([174], "MEMORIA_FF8_EFFECT174");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition);
        NativeBattleEffect174PresentationObserver observer = new();
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
                NativeBattleGraphicsState currentGraphics = current.ReadFixturePresentation().NativeGraphics ?? throw new InvalidDataException("Effect174 lost its graphics state.");
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());

                cameraChanged |= JsonSerializer.Serialize(source.ReadFixturePresentation().NativeCamera) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeCamera);
                stageChanged |= JsonSerializer.Serialize(source.ReadFixturePresentation().NativeStage) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeStage);
                if (prior.ReadFixturePresentation().NativeGraphics!.OriginalEffect is { Binding.Effect: 174, ReturnedDescriptor: > 0 } && currentGraphics.OriginalEffect is { Binding.Effect: 174, ReturnedDescriptor: 0 })
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
            Assert.True(callbacks < 1800, "The original effect174 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(cameraChanged);
            Assert.True(stageChanged);
            Assert.True(changedRgb > 100, "No actual effect174 primitive changed the rendered RGB output.");
            Assert.True(geometryChangedRgb > 100, "No effect174 geometry changed rendered RGB outside its fullviewport fade.");
            Assert.Equal(1, observer.Contacts);
            Assert.Equal(1, observer.ContactGlyphs);
            observer.Accept(completed, output);
            ColdNext("effect174-visible-peak", peak);
            ColdNext("effect174-geometry-visible-peak", geometryPeak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "geometry-without-effect.rgba"), geometryPlain!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared original22 command8/action72/sequence13 post-formula Amount0 party component; current historical bone matrices, target deformation, particles and camera; ordinary AI and formula admission excluded", Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, ChangedRgb = changedRgb, GeometryChangedRgb = geometryChangedRgb, CameraChanged = cameraChanged, StageChanged = stageChanged, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.ReadFixturePresentation().NativeGraphics!.Callback }));
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
            if (name == "effect174-before-cleanup-age-one")
            {
                NativeBattleOriginalEffectState effect = json.ReadFixturePresentation().NativeGraphics!.OriginalEffect!;
                NativeBattleEffectTaskPool pool = effect.Pools.Single(item => item.DescriptorAddress == RootDescriptor);
                int slot = Enumerable.Range(0, pool.Slots.Length).Single(index => pool.Slots[index].Callback == CleanupCallback && pool.Slots[index].ReadWord(12) == 1);
                NativeBattleEffectTaskPool changed = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(14, 0xCAFE)) };
                BattleSnapshot variant = json.WithFixturePresentation(facts => facts with { NativeGraphics = json.ReadFixturePresentation().NativeGraphics! with { OriginalEffect = effect with { Pools = effect.Pools.Replace(pool, changed) } } });
                BattleSession warm = Restore(variant);
                warm.Tick();
                BattleSession expected = Restore(json);
                expected.Tick();
                Assert.Equal(JsonSerializer.Serialize(expected.Capture()), JsonSerializer.Serialize(warm.Capture()));
            }
            cold.Tick();
            direct.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, source.Definition).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, source.Definition).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output, name + "-next", cold.Capture(), pixels);
            restored.Add(name);
        }
    }

    internal static NativeEffectFixtureRequest CreateOriginalRequest() => new("effect174-original22-entry", 174, 8, 72, 0, 300, 180) { EncounterId = 226, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, AdmissionProbeMaximumCallbacks = 0 };

    private static BattleSnapshot PrepareSource(GameAssets game, BattleAssets assets, string output)
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT174_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select the current formation226 original22 component source.");
        BattleSnapshot original = NativeBattleFixtureInspection.ReadLogicalSnapshot(path);
        Assert.Equal(226, original.Definition.EncounterId);
        Assert.Equal(71, original.Definition.StageId);
        BattleSession restored = BattleSession.Restore(original);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(restored.Capture()));
        Assert.True(restored.NativeEntryReady);
        NativeBattleGraphicsState graphics = original.ReadFixturePresentation().NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.ReadFixturePresentation().NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect174State.FromColdProcess()), JsonSerializer.Serialize(original.ReadFixturePresentation().NativeEffectMemory!.Effect174));
        string casterId = ReadOriginalModelActorId(graphics, 3) ?? throw new InvalidDataException("Effect174 requires its actual original caster.");
        string targetId = ReadOriginalModelActorId(graphics, 0) ?? throw new InvalidDataException("Effect174 requires its actual party recipient.");
        BattleCombatantDefinition caster = restored.GetCombatantDefinition(casterId);
        Assert.Equal(22, caster.ModelId);
        Assert.Equal((byte)38, ReadOriginalModelCell(graphics, 3).ModelId4);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource ?? throw new InvalidDataException("Effect174 requires its current original skeleton.");
        Assert.Equal(26, NativeBattleBodyBoneMatrices.Require(ReadOriginalModelCell(graphics, 3), pose).Length);
        Assert.NotNull(ReadOriginalModelCell(graphics, 3).GeometryBounds52);
        Assert.NotNull(ReadOriginalModelCell(graphics, 3).GeometryWorldMatrix);
        Assert.NotNull(graphics.UiServices);
        NativeBattleGraphicsModel recipient = ReadOriginalModelCell(graphics, 0);
        NativeBattleGraphicsBinding recipientBinding = original.ReadFixtureGraphicsDefinition()!.Bindings.Single(binding => binding.ActorId == targetId);
        NativeBattlePoseSource recipientPose = recipientBinding.Resource.PoseSource!;
        Assert.NotEmpty(NativeBattleBodyBoneMatrices.Require(recipient, recipientPose));
        ValidateCurrentGeometry(recipient.ActiveResourceRow100!.Value, recipientPose.BoneCount, recipient.SequenceState!.MeshMask124);
        if ((recipient.Flags0 & 0x200) == 0 && recipient.LinkedTextureResourceIndex is int linked)
        {
            ValidateCurrentGeometry(linked, recipientBinding.Resource.LinkedPoseSource!.BoneCount, uint.MaxValue);
        }

        void ValidateCurrentGeometry(int row, int bones, uint mask)
        {
            NativeBattleModelCpuResources cpu = original.ReadFixturePresentation().NativeModelCpuResources!;
            NativeBattleModelCpuReference resource = cpu.Rows[row].Require(16);
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => NativeBattleModelEnemyHeap.Read(cpu, original.ReadFixturePresentation().NativeFileStaging!, resource with { Offset = checked(resource.Offset + offset) }, length), bones, (_, counts, _, _) =>
            {
                Assert.Equal((ushort)0, counts[2]);
                Assert.Equal((ushort)0, counts[3]);
            }, mask);
            Assert.Contains(meshes, mesh => !mesh.Polygons.IsEmpty);
        }
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 72 && row.MagicId == 174 && row.KernelSection == 3 && row.KernelIndex == 72);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(22, 1), action));
        NativeBattleActionPacketsState packets = original.ReadFixturePresentation().NativeActionPackets!;
        int priorActions = packets.ActionCount;
        int priorContacts = packets.ContactCount;
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(3), 8, 13, -1, 72, 174, null));
        BattleCombatantSnapshot target = original.Combatants.Single(actor => actor.Id == targetId);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("Effect174 requires its current target status.");
        BattleCombatantSnapshot preparedTarget = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattleContactPacket contact = new(new(packets.ModelAliases.DecodeLiteral(0), action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), null);
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorActions, header.Ordinal);
        Assert.Equal(priorContacts + 1, packets.ContactCount);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.EnqueueAction(128, header);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([174])[173];
        Assert.Same(game.ReadNativeEffect174Resources(), binding.Effect174Resources);
        BattleSnapshot source = ((original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(173, binding) },Combatants = original.Combatants.Select(actor => actor.Id == targetId ? preparedTarget : actor).ToArray()}).WithFixturePresentation(facts => facts with { NativeActionPackets = packets })).WithFixturePresentation(facts => facts with { NativeGraphics = graphics with { Descriptors = descriptors } });
        Assert.Equal(original.Combatants.Select(actor => actor.CurrentHp), source.Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(graphics)), JsonSerializer.Serialize(ReadOriginalModelCells(source.ReadFixturePresentation().NativeGraphics!)));
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeEffectMemory), JsonSerializer.Serialize(source.ReadFixturePresentation().NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeModelCpuResources), JsonSerializer.Serialize(source.ReadFixturePresentation().NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(original.NativeClock), JsonSerializer.Serialize(source.NativeClock));
        File.WriteAllText(Path.Combine(output, "declared-component.json"), JsonSerializer.Serialize(new { Scenario = "Declared original22 command8/action72/sequence13 Amount0 party component; ordinary AI and formula excluded", ActualCasterId = casterId, ActualTargetId = targetId, PhysicalCaster = 3, PhysicalTarget = 0, Action = action, header = packets.ReadHeader(header), PriorActionCount = priorActions, PriorContactCount = priorContacts, ContactCount = 1, Contact = contact, StatusBefore = status.Presentation, StatusAfter = preparedTarget.NativeStatus!.Presentation, StatusCache = "Declared47E2D0 projection copies actual current Saved/Battle on the prepared component copy; logical HP/status preserved", Source = "Fresh original formation226/stage71 resource component; no gameplay replay or substituted carrier" }));
        return source;
    }
    private static bool Released(BattleSnapshot snapshot) => snapshot.ReadFixturePresentation().NativeGraphics?.OriginalEffect is { Binding.Effect: 174, ReturnedDescriptor: 0 } effect && snapshot.ReadFixturePresentation().NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Queue.Any(index => snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;
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
