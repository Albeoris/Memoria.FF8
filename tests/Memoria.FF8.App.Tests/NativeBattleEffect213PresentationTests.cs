using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect213PresentationTests
{
    [Fact]
    public void Selected_Effect213_saved_boundary_attributes_current_source_stream_packets()
    {
        string? priorPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT213_OBSERVATION_PRIOR");
        string? currentPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT213_OBSERVATION_CURRENT");
        Assert.SkipUnless(priorPath is not null && currentPath is not null, "Select two standalone effect213 observations at a visible source-mesh boundary.");
        BattleSnapshot prior = NativeBattleEffectFamilyRunner.LoadSnapshot(priorPath!);
        BattleSnapshot current = NativeBattleEffectFamilyRunner.LoadSnapshot(currentPath!);
        NativeBattleEffect213PresentationObserver observer = new();
        observer.Observe(prior, current, (_, _) =>
        {
        });
        Assert.NotEmpty(observer.CurrentMeshCommands);
        Assert.All(observer.CurrentMeshCommands, command => Assert.Equal("20DFAB8", command.Packet.Bank));
    }

    [Fact]
    public void Selected_Effect213_snapshot_continues_one_callback()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT213_CONTINUATION_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and a standalone effect213 continuation snapshot.");
        using GameAssets game = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Equal(213, source.NativeGraphics!.OriginalEffect!.Binding.Effect);
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        battle.Tick();
        Assert.Equal(source.NativeGraphics.Callback + 1, battle.Capture().NativeGraphics!.Callback);
    }

    [Fact]
    public void Original_Effect213_declared_component_preserves_current_geometry_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([213], "MEMORIA_FF8_EFFECT213");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleEffect213PresentationObserver observer = new();
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
                NativeBattleGraphicsState currentGraphics = current.NativeGraphics ?? throw new InvalidDataException("Effect213 lost its graphics state.");
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());

                cameraChanged |= JsonSerializer.Serialize(source.NativeCamera) != JsonSerializer.Serialize(current.NativeCamera);
                stageObserved |= JsonSerializer.Serialize(source.NativeStage) != JsonSerializer.Serialize(current.NativeStage);
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 213, ReturnedDescriptor: > 0 } && currentGraphics.OriginalEffect is { Binding.Effect: 213, ReturnedDescriptor: 0 })
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
            Assert.True(callbacks < 2400, "The original effect213 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(cameraChanged);
            Assert.True(changedRgb > 100, "No actual effect213 primitive changed the rendered RGB output.");
            Assert.True(geometryChangedRgb > 100, "No effect213 geometry changed rendered RGB outside its fullviewport fade.");
            Assert.Equal(1, observer.Contacts);
            Assert.Equal(1, observer.ContactGlyphs);
            observer.Accept(completed, output);
            ColdNext("effect213-visible-peak", peak);
            ColdNext("effect213-geometry-visible-peak", geometryPeak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "geometry-without-effect.rgba"), geometryPlain!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared original131 command8/action233/sequence14 Amount0 party component; current mutable stream, geometry, camera and native lifecycle; ordinary AI/formula admission excluded", Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, ChangedRgb = changedRgb, GeometryChangedRgb = geometryChangedRgb, CameraChanged = cameraChanged, StageChanged = stageObserved, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));
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
            byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, source.Definition, true).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output, name + "-next", cold.Capture(), pixels);
            restored.Add(name);
        }
    }

    internal static NativeEffectFixtureRequest CreateOriginalRequest() => new("effect213-original131-source", 213, 8, 233, 0, 1200, 180) { EncounterId = 791, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, AdmissionProbeMaximumCallbacks = 0, ControlledContactValue = 0 };

    private static BattleSnapshot PrepareSource(GameAssets game, BattleAssets assets, string output)
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT213_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select the current original131 component source.");
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(path);
        Assert.Equal(791, original.Definition.EncounterId);
        Assert.Equal(66, original.Definition.StageId);
        BattleSession restored = BattleSession.Restore(original);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(restored.Capture()));
        Assert.True(restored.NativeEntryReady);
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect213State.FromColdProcess()), JsonSerializer.Serialize(original.NativeEffectMemory.Effect213));
        string casterId = graphics.ActorIds[3] ?? throw new InvalidDataException("Effect213 requires its current caster.");
        string targetId = graphics.ActorIds[0] ?? throw new InvalidDataException("Effect213 requires its current party recipient.");
        BattleCombatantDefinition caster = restored.GetCombatantDefinition(casterId);
        Assert.Equal(131, caster.ModelId);
        Assert.Equal((byte)147, graphics.Models[3].ModelId4);
        Assert.False(caster.InitialHidden);
        Assert.False(caster.InitialSuspended);
        Assert.False(caster.InitialUntargetable);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource ?? throw new InvalidDataException("Effect213 requires its actual current skeleton.");
        Assert.Equal(54, pose.BoneCount);
        Assert.Equal(54, NativeBattleBodyBoneMatrices.Require(graphics.Models[3], pose).Length);
        Assert.NotNull(graphics.Models[3].GeometryWorldMatrix);
        Assert.NotNull(graphics.Models[3].ActiveResourceRow100);
        Assert.NotNull(original.NativeModelCpuResources);
        Assert.NotNull(original.NativeFileStaging);
        Assert.True(graphics.Models[0].Transform!.Scale38 > 0);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 233 && row.MagicId == 213 && row.KernelSection == 3 && row.KernelIndex == 233);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        byte priorActions = packets.ActionCount;
        byte priorContacts = packets.ContactCount;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, int header) = packets.Prepare(new(3, 8, 14, cameraChoice, 233, 213, null));
        BattleCombatantSnapshot target = original.Combatants.Single(actor => actor.Id == targetId);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("Effect213 requires its current target status.");
        BattleCombatantSnapshot preparedTarget = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattleContactPacket contact = new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0));
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorActions, header);
        Assert.Equal(priorContacts + 1, packets.ContactCount);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([213])[212];
        Assert.Same(game.ReadNativeEffect213Resources(), binding.Effect213Resources);
        BattleSnapshot source = original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(212, binding) }, Combatants = original.Combatants.Select(actor => actor.Id == targetId ? preparedTarget : actor).ToArray(), NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(original.Combatants.Select(actor => actor.CurrentHp), source.Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(source.NativeGraphics!.Models));
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(source.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.NativeFileStaging), JsonSerializer.Serialize(source.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(original.NativeModelCpuResources), JsonSerializer.Serialize(source.NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(original.NativeClock), JsonSerializer.Serialize(source.NativeClock));
        File.WriteAllText(Path.Combine(output, "declared-component.json"), JsonSerializer.Serialize(new { Scenario = "Declared original131 command8/action233/sequence14 Amount0 contact; ordinary AI/formula excluded", ActualCasterId = casterId, ActualTargetId = targetId, PhysicalCaster = 3, PhysicalTarget = 0, EnemyLevel = caster.Level, Action = action, CameraChoice = cameraChoice, CameraSelector = action.RawRecord[4] & 127, Header = packets.Headers[header], PriorActionCount = priorActions, PriorContactCount = priorContacts, ContactCount = 1, Contact = contact, StatusBefore = status.Presentation, StatusAfter = preparedTarget.NativeStatus!.Presentation, StatusCache = "Declared47E2D0 projection copies current Saved/Battle on the prepared component copy; logical HP/status preserved" }));
        return source;
    }

    private static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 213, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;
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
