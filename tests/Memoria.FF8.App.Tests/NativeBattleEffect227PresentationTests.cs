using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect227PresentationTests
{
    [Fact]
    public void Selected_Effect227_saved_boundary_attributes_current_source_packets()
    {
        string? priorPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT227_OBSERVATION_PRIOR");
        string? currentPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT227_OBSERVATION_CURRENT");
        Assert.SkipUnless(priorPath is not null && currentPath is not null, "Select two standalone effect227 observations at a visible source-mesh boundary.");
        BattleSnapshot prior = NativeBattleEffectFamilyRunner.LoadSnapshot(priorPath!);
        BattleSnapshot current = NativeBattleEffectFamilyRunner.LoadSnapshot(currentPath!);
        NativeBattleEffect227PresentationObserver observer = new();
        observer.Observe(prior, current, (_, _) =>
        {
        });
        Assert.NotEmpty(observer.CurrentMeshCommands);
        Assert.All(observer.CurrentMeshCommands, command => Assert.Equal("20DFAB8", command.Packet.Bank));
    }

    [Fact]
    public void Selected_Effect227_snapshot_continues_one_callback()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT227_CONTINUATION_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and a standalone effect227 continuation snapshot.");
        using GameAssets game = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Equal(227, source.NativeGraphics!.OriginalEffect!.Binding.Effect);
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        battle.Tick();
        Assert.Equal(source.NativeGraphics.Callback + 1, battle.Capture().NativeGraphics!.Callback);
    }

    [Fact]
    public void Original_Effect227_declared_component_preserves_current_geometry_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([227], "MEMORIA_FF8_EFFECT227");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleEffect227PresentationObserver observer = new();
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
        int captured29Frames = 0;
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
                NativeBattleGraphicsState currentGraphics = current.NativeGraphics ?? throw new InvalidDataException("Effect227 lost its graphics state.");
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());

                cameraChanged |= JsonSerializer.Serialize(source.NativeCamera) != JsonSerializer.Serialize(current.NativeCamera);
                stageObserved |= JsonSerializer.Serialize(source.NativeStage) != JsonSerializer.Serialize(current.NativeStage);
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 227, ReturnedDescriptor: > 0 } && currentGraphics.OriginalEffect is { Binding.Effect: 227, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                observer.Observe(prior, current, ColdNext);
                if (NativeBattleCinematicCaptureAssertions.AssertFrame(current, Effect) > 0)
                {
                    captured29Frames++;
                }
                NativeBattleCinematicCaptureAssertions.AssertCommands(current, Effect, observer.CurrentCommands);
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
            Assert.True(callbacks < 2400, "The original effect227 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(captured29Frames > 1);
            Assert.True(cameraChanged);
            Assert.True(changedRgb > 100, "No actual effect227 primitive changed the rendered RGB output.");
            Assert.True(geometryChangedRgb > 100, "No effect227 geometry changed rendered RGB outside its fullviewport fade.");
            Assert.Equal(0, observer.Contacts);
            Assert.Equal(0, observer.ContactGlyphs);
            observer.Accept(completed, output);
            ColdNext("effect227-visible-peak", peak);
            ColdNext("effect227-geometry-visible-peak", geometryPeak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "geometry-without-effect.rgba"), geometryPlain!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared original121 command8/action339/sequence13 zero-contact component; actual texts, vibration, programmes, particles and native lifecycle; original123 companion preserved; ordinary AI/formula admission excluded", Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, ChangedRgb = changedRgb, GeometryChangedRgb = geometryChangedRgb, CameraChanged = cameraChanged, StageChanged = stageObserved, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));
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
            NativeBattleCinematicCaptureAssertions.AssertFrame(direct.Capture(), Effect);
            NativeBattleCinematicCaptureAssertions.AssertFrame(cold.Capture(), Effect);
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, source.Definition, true).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output, name + "-next", cold.Capture(), pixels);
            restored.Add(name);
        }
    }

    internal static NativeEffectFixtureRequest CreateOriginalRequest() => new("effect227-original121-source", 227, 8, 339, 0, 1800, 240) { EncounterId = 511, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, AdmissionProbeMaximumCallbacks = 0, ControlledContactValue = 0 };

    private static BattleSnapshot PrepareSource(GameAssets game, BattleAssets assets, string output)
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT227_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select the current original121 component source.");
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(path);
        Assert.Equal(511, original.Definition.EncounterId);
        Assert.Equal(142, original.Definition.StageId);
        BattleSession restored = BattleSession.Restore(original);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(restored.Capture()));
        Assert.True(restored.NativeEntryReady);
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect227State.FromColdProcess())), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(original.NativeEffectMemory.Effect227)));
        string casterId = graphics.ActorIds[3] ?? throw new InvalidDataException("Effect227 requires its current caster.");
        string companionId = graphics.ActorIds[4] ?? throw new InvalidDataException("Effect227 requires its original companion.");
        BattleCombatantDefinition caster = restored.GetCombatantDefinition(casterId);
        BattleCombatantDefinition companion = restored.GetCombatantDefinition(companionId);
        Assert.Equal(121, caster.ModelId);
        Assert.Equal(123, companion.ModelId);
        Assert.Equal((byte)137, graphics.Models[3].ModelId4);
        Assert.Equal((byte)139, graphics.Models[4].ModelId4);
        Assert.False(caster.InitialHidden);
        Assert.False(caster.InitialSuspended);
        Assert.False(caster.InitialUntargetable);
        Assert.True(companion.InitialHidden);
        Assert.True(companion.InitialSuspended);
        Assert.True(companion.InitialUntargetable);
        BattleCombatantSnapshot companionSnapshot = original.Combatants.Single(actor => actor.Id == companionId);
        Assert.True(companionSnapshot.NativeParticipant!.Hidden);
        Assert.True(companionSnapshot.NativeParticipant.Suspended);
        Assert.True(companionSnapshot.NativeParticipant.Untargetable);
        foreach ((int physical, BattleCombatantDefinition definition, int count) in new[] { (3, caster, 69), (4, companion, 2) })
        {
            NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(definition).PoseSource ?? throw new InvalidDataException("Effect227 requires its actual current skeletons.");
            Assert.Equal(count, pose.BoneCount);
            Assert.Equal(count, NativeBattleBodyBoneMatrices.Require(graphics.Models[physical], pose).Length);
            Assert.NotNull(graphics.Models[physical].GeometryWorldMatrix);
            Assert.NotNull(graphics.Models[physical].ActiveResourceRow100);
        }
        NativeBattleGraphicsDefinition originalGraphicsDefinition = original.Definition.NativeGraphics!;
        int bindingIndex = Enumerable.Range(0, originalGraphicsDefinition.Bindings.Length).Single(index => originalGraphicsDefinition.Bindings[index].ActorId == casterId);
        NativeBattleGraphicsBinding originalCasterBinding = originalGraphicsDefinition.Bindings[bindingIndex];
        NativeBattleGraphicsResource installed = assets.ReadNativeGraphicsResource(caster);
        Assert.NotNull(installed.TextBank);
        NativeBattleGraphicsResource preparedResource = originalCasterBinding.Resource.TextBank is null ? originalCasterBinding.Resource with { TextBank = installed.TextBank } : originalCasterBinding.Resource;
        Assert.Equal(installed.TextBank!.Value.ToArray(), preparedResource.TextBank!.Value.ToArray());
        Assert.Equal(JsonSerializer.Serialize(originalCasterBinding.Resource), JsonSerializer.Serialize(preparedResource with { TextBank = originalCasterBinding.Resource.TextBank }));
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(installed.ReadText91(index), preparedResource.ReadText91(index));
        }
        NativeBattleGraphicsDefinition preparedGraphicsDefinition = originalGraphicsDefinition with { Bindings = originalGraphicsDefinition.Bindings.SetItem(bindingIndex, originalCasterBinding with { Resource = preparedResource }) };
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 339 && row.MagicId == 227 && row.KernelSection == 3 && row.KernelIndex == 339);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        byte priorActions = packets.ActionCount;
        byte priorContacts = packets.ContactCount;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, int header) = packets.Prepare(new(3, 8, 13, cameraChoice, 339, 227, null));
        (packets, _) = packets.Publish(0);
        Assert.Equal(priorActions, header);
        Assert.Equal(priorContacts, packets.ContactCount);
        Assert.Equal((byte)0, packets.Headers[header].ReadByte(16));
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([227])[226];
        Assert.Same(game.ReadNativeEffect227Resources(), binding.Effect227Resources);
        BattleSnapshot source = original with { Definition = original.Definition with { NativeGraphics = preparedGraphicsDefinition, NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(226, binding) }, NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(original.Combatants, source.Combatants);
        Assert.Equal(JsonSerializer.Serialize(caster.NativeAi), JsonSerializer.Serialize(source.Definition.Combatants.Single(actor => actor.Id == casterId).NativeAi));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(source.NativeGraphics!.Models));
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(source.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.NativeFileStaging), JsonSerializer.Serialize(source.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(original.NativeModelCpuResources), JsonSerializer.Serialize(source.NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(original.NativeClock), JsonSerializer.Serialize(source.NativeClock));
        File.WriteAllText(Path.Combine(output, "declared-component.json"), JsonSerializer.Serialize(new { Scenario = "Declared original121 command8/action339/sequence13 zero-contact component; original123 companion preserved; ordinary AI/formula excluded", ActualCasterId = casterId, ActualCompanionId = companionId, PhysicalCaster = 3, PhysicalCompanion = 4, EnemyLevel = caster.Level, Action = action, CameraChoice = cameraChoice, CameraSelector = action.RawRecord[4] & 127, Header = packets.Headers[header], PriorActionCount = priorActions, PriorContactCount = priorContacts, ContactCount = 0, TextBankPreparation = new { Path = "Definition.NativeGraphics.Bindings[" + bindingIndex + "].Resource.TextBank", Before = originalCasterBinding.Resource.TextBank, After = preparedResource.TextBank, Hydrated = originalCasterBinding.Resource.TextBank is null, Producer = "Actor-original selected121 DAT section8 text decoder; no current pose override or AI admission", NativeAiBefore = caster.NativeAi, NativeAiAfter = caster.NativeAi }, LogicalState = "No target, HP, saved/battle status or presentation cache changed" }));
        return source;
    }

    private static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 227, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;
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
