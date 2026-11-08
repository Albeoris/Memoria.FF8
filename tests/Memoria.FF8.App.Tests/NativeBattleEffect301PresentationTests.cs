using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect301PresentationTests
{
    [Fact]
    public void Selected_Effect301_snapshot_continues_one_callback()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT301_CONTINUATION_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and a standalone effect301 continuation snapshot.");
        using GameAssets game = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Equal(301, source.NativeGraphics!.OriginalEffect!.Binding.Effect);
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        battle.Tick();
        Assert.Equal(source.NativeGraphics.Callback + 1, battle.Capture().NativeGraphics!.Callback);
    }

    [Fact]
    public void Original_Effect301_declared_component_preserves_current_geometry_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([301], "MEMORIA_FF8_EFFECT301");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        source = NativeBattleRetainedEffectMigrationAcceptance.Prepare(source, 301, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleEffect301PresentationObserver observer = new();
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
        bool stageObserved = false;
        string boundary = "queued-component";
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-source", source, null);
        try
        {
            ColdNext("before-worker", source);
            for (; callbacks < 2400; callbacks++)
            {
                boundary = "callback-" + callbacks;
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                NativeBattleGraphicsState currentGraphics = current.NativeGraphics ?? throw new InvalidDataException("Effect301 lost its graphics state.");
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());

                cameraChanged |= JsonSerializer.Serialize(source.NativeCamera) != JsonSerializer.Serialize(current.NativeCamera);
                stageObserved |= JsonSerializer.Serialize(source.NativeStage) != JsonSerializer.Serialize(current.NativeStage);
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 301, ReturnedDescriptor: > 0 } && currentGraphics.OriginalEffect is { Binding.Effect: 301, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                observer.Observe(prior, current, ColdNext);
                NativeBattleLogicalCommand[] commands = observer.CurrentCommands;
                if (commands.Length > 0)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = commands.Select(command => command.Packet).ToImmutableHashSet();
                    NativeBattlePrimitiveFrame primitive = currentGraphics.PrimitiveFrame!;
                    BattleSnapshot without = current with { NativeGraphics = currentGraphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
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
            BattleSnapshot completed = battle.Capture();
            Assert.True(callbacks < 2400, "The original effect301 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(cameraChanged);
            Assert.True(changedRgb > 100, "No actual effect301 primitive changed the rendered RGB output.");
            Assert.True(geometryChangedRgb > 100, "No effect301 geometry changed rendered RGB outside its fullviewport fade.");
            Assert.Equal(0, observer.Contacts);
            Assert.Equal(0, observer.ContactGlyphs);
            observer.Accept(completed, output);
            ColdNext("effect301-visible-peak", peak);
            ColdNext("effect301-geometry-visible-peak", geometryPeak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "geometry-without-effect.rgba"), geometryPlain!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared original94 command8/action265/sequence16 zero-damage SELF-contact graphics component; current body, script records, geometry, camera and native lifecycle; ordinary AI/formula admission excluded", Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, ChangedRgb = changedRgb, GeometryChangedRgb = geometryChangedRgb, CameraChanged = cameraChanged, StageChanged = stageObserved, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            observer.Publish(output);
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
            NativeBattleRecovery32CaptureAssertions.AssertFrame(direct.Capture(), 301);
            NativeBattleRecovery32CaptureAssertions.AssertFrame(cold.Capture(), 301);
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, source.Definition, true).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output, name + "-next", cold.Capture(), pixels);
            restored.Add(name);
        }
    }

    internal static NativeEffectFixtureRequest CreateOriginalRequest() => new("effect301-original54-baseline", 45, 8, 84, 0, 1200, 180) { EncounterId = 436, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, AdmissionProbeMaximumCallbacks = 0, ControlledContactValue = 0 };

    internal static BattleSnapshot PrepareSource(GameAssets game, BattleAssets assets, string output)
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT301_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select the current original94 component source.");
        BattleSnapshot baseline = NativeBattleEffectFamilyRunner.LoadSnapshot(path);
        Assert.Equal(436, baseline.Definition.EncounterId);
        Assert.Equal(125, baseline.Definition.StageId);
        (BattleSnapshot original, int casterIndex) = NativeBattleEvilEye282FixturePreparation.Prepare(game, assets, baseline, Path.Combine(output, "native-allocation"));
        NativeBattleRestorationPresentationTests.Publish(output, "allocation-ready-source", original, null);
        Assert.Equal(436, original.Definition.EncounterId);
        Assert.Equal(125, original.Definition.StageId);
        BattleSession restored = BattleSession.Restore(original);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(restored.Capture()));
        Assert.True(restored.NativeEntryReady);
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect301State.FromColdProcess())), JsonSerializer.Serialize(original.NativeEffectMemory.Effect301));
        string casterId = graphics.ActorIds[casterIndex] ?? throw new InvalidDataException("Effect301 requires its current caster.");
        BattleCombatantDefinition caster = restored.GetCombatantDefinition(casterId);
        Assert.Equal(94, caster.ModelId);
        Assert.Equal((byte)110, graphics.Models[casterIndex].ModelId4);
        Assert.Equal(3, caster.SceneSlot);
        Assert.True(caster.InitialHidden);
        Assert.True(caster.InitialSuspended);
        Assert.True(caster.InitialUntargetable);
        Assert.Equal(new[] { 39, 40, 54, 94 }, original.Definition.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.ModelId).Order());
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource ?? throw new InvalidDataException("Effect301 requires its actual current skeleton.");
        Assert.Equal(40, pose.BoneCount);
        Assert.Equal(40, NativeBattleBodyBoneMatrices.Require(graphics.Models[casterIndex], pose).Length);
        Assert.NotNull(graphics.Models[casterIndex].GeometryWorldMatrix);
        Assert.NotNull(graphics.Models[casterIndex].ActiveResourceRow100);
        Assert.NotNull(original.NativeModelCpuResources);
        Assert.Equal(pose.Skeleton.AsSpan(0, 4).ToArray(), NativeBattleModelEnemyHeap.Read(original.NativeModelCpuResources!, original.NativeFileStaging!, original.NativeModelCpuResources.Rows[graphics.Models[casterIndex].ActiveResourceRow100!.Value].Require(12), 4));

        Assert.NotNull(original.NativeFileStaging);
        Assert.True(graphics.Models[0].Transform!.Scale38 > 0);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 265 && row.MagicId == 301 && row.KernelSection == 3 && row.KernelIndex == 265);
        Assert.Equal((byte)16, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([301])[300];
        Assert.Same(game.ReadNativeEffect301Resources(), binding.Effect301Resources);
        BattleSnapshot prepared = original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(300, binding) } };
        NativeBattleCommandPacket init = Assert.Single(prepared.NativeCommandBanks!.Banks.SelectMany(bank => bank.Packets), packet => packet.ReadByte(0) == casterIndex && packet.ReadByte(1) == 255 && packet.ReadWord(4) == 0);
        BattleSession producer = BattleSession.Restore(prepared);
        producer.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        BattleSnapshot source = NativeBattleLimitComponentSources.AwaitQueued(producer, 301);
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        int headerIndex = Assert.Single(Enumerable.Range(0, packets.ActionCount), index => NativeBattleActionGraphics.Word(packets.Headers[index], 6) == 301);
        NativeBattleActionHeaderSlot header = packets.Headers[headerIndex];
        Assert.Equal(checked((byte)casterIndex), header.ReadByte(0));
        Assert.Equal((byte)8, header.ReadByte(1));
        Assert.Equal((byte)16, header.ReadByte(2));
        Assert.Equal((ushort)265, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((byte)1, header.ReadByte(16));
        Assert.Equal((ushort)(1 << casterIndex), NativeBattleActionGraphics.Normalize(packets, headerIndex).TargetMask);
        Assert.True(source.NativeEffectMemory!.Effect301!.ScriptSourcePending);
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory.Effect301), JsonSerializer.Serialize(source.NativeEffectMemory.Effect301));
        File.WriteAllText(Path.Combine(output, "declared-component.json"), JsonSerializer.Serialize(new { Scenario = "Original94 pending INIT produces command8/action265/sequence16 SELF graphics after authentic scene3 allocation/102/113; no manually queued duplicate invocation", ActualCasterId = casterId, PhysicalCaster = casterIndex, EnemyLevel = caster.Level, Action = action, HeaderIndex = headerIndex, Header = header, ContactCount = header.ReadByte(16), InitPacket = init, AllocationCallback = graphics.Callback, SourceCallback = source.NativeGraphics!.Callback, SourceBoundary = "native-init-before-entry" }));
        return source;
    }

    private static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 301, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;
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
