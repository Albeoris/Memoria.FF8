using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect324Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect324PresentationTests
{
    [Fact]
    public void Selected_Effect324_saved_warm_history_uses_current_source_and_native_rejection()
    {
        string? priorPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT324_HISTORY_PRIOR");
        string? currentPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT324_HISTORY_CURRENT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT324_HISTORY_EVIDENCE");
        Assert.SkipUnless(priorPath is not null && currentPath is not null && output is not null, "Select a saved warm history callback pair and distinct diagnostic output.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        BattleSnapshot prior = NativeBattleEffectFamilyRunner.LoadSnapshot(priorPath!);
        BattleSnapshot current = NativeBattleEffectFamilyRunner.LoadSnapshot(currentPath!);
        Assert.Equal(prior.NativeGraphics!.Callback + 1, current.NativeGraphics!.Callback);
        var result = NativeBattleEffect324PresentationObserver.ReproduceHistory(prior, current);
        File.WriteAllText(Path.Combine(output!, "history-rejection.json"), JsonSerializer.Serialize(new { prior.Frame, PriorNativeCallback = prior.NativeGraphics.Callback, CurrentNativeCallback = current.NativeGraphics.Callback, result.GeometryAddress, result.SkeletonAddress, result.Age, result.World, result.View, result.Draw.ProjectedBytesWritten, Packets = result.Draw.Packets.Attempts.Length, Scope = "Current CPU geometry16 and skeleton12, current captured original28bone rows, exact current savedworld and prior camera; existing native rawbody kernel with actual viewport, mask2 and depth4; no Tick, visibility reset or geometry substitution" }));
        Assert.Empty(result.Draw.Packets.Attempts);
    }

    [Fact]
    public void Selected_Effect324_saved_observations_cover_all_lifecycle_and_material_requirements()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT324_OBSERVATION_DIRECTORY");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT324_OBSERVATION_EVIDENCE");
        Assert.SkipUnless(directory is not null && output is not null, "Select a complete set of standalone observations and a distinct diagnostic output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        NativeBattleEffect324PresentationObserver observer = new();
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(directory!, "prepared-source.battle.json"));
        BattleSnapshot first = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(directory!, "effect324-before-main-age-1.battle.json"));
        observer.Observe(source, first, NoCapture);
        HashSet<long> visited = [first.NativeGraphics!.Callback];
        IEnumerable<string> paths = Directory.GetFiles(directory!, "*-next.battle.json").OrderBy(Callback);
        foreach (string currentPath in paths)
        {
            string priorPath = currentPath[..^"-next.battle.json".Length] + ".battle.json";
            if (!File.Exists(priorPath))
            {
                continue;
            }
            BattleSnapshot current = NativeBattleEffectFamilyRunner.LoadSnapshot(currentPath);
            if (!visited.Add(current.NativeGraphics!.Callback))
            {
                continue;
            }
            BattleSnapshot prior = NativeBattleEffectFamilyRunner.LoadSnapshot(priorPath);
            Assert.Equal(prior.NativeGraphics!.Callback + 1, current.NativeGraphics.Callback);
            observer.Observe(prior, current, NoCapture);
        }
        string completedPath = File.Exists(Path.Combine(directory!, "completed.battle.json")) ? Path.Combine(directory!, "completed.battle.json") : Path.Combine(directory!, "failed-live.battle.json");
        BattleSnapshot completed = NativeBattleEffectFamilyRunner.LoadSnapshot(completedPath);
        Assert.True(Released(completed));
        observer.Accept(completed, output!);

        static long Callback(string path)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.GetProperty("NativeGraphics").GetProperty("Callback").GetInt64();
        }

        static void NoCapture(string name, BattleSnapshot snapshot)
        {
        }
    }

    [Fact]
    public void Selected_Effect324_saved_pair_attributes_current_body_history_packets()
    {
        string? priorPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT324_OBSERVATION_PRIOR");
        string? currentPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT324_OBSERVATION_CURRENT");
        Assert.SkipUnless(priorPath is not null && currentPath is not null, "Select two standalone effect324 observations during current body history.");
        BattleSnapshot prior = NativeBattleEffectFamilyRunner.LoadSnapshot(priorPath!);
        BattleSnapshot current = NativeBattleEffectFamilyRunner.LoadSnapshot(currentPath!);
        NativeBattleEffect324PresentationObserver observer = new();
        observer.Observe(prior, current, (_, _) =>
        {
        });
        Assert.True(observer.HistoryGeometry);
    }

    [Fact]
    public void Selected_Effect324_snapshot_continues_one_callback()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT324_CONTINUATION_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and a standalone effect324 continuation snapshot.");
        using GameAssets game = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Equal(324, source.NativeGraphics!.OriginalEffect!.Binding.Effect);
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        battle.Tick();
        Assert.Equal(source.NativeGraphics.Callback + 1, battle.Capture().NativeGraphics!.Callback);
    }

    [Fact]
    public void Original_Effect324_declared_component_preserves_current_geometry_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([324], "MEMORIA_FF8_EFFECT324");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleEffect324PresentationObserver observer = new();
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
        bool nativeSelfKo = false;
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
                NativeBattleGraphicsState currentGraphics = current.NativeGraphics ?? throw new InvalidDataException("Effect324 lost its graphics state.");
                if (AssertComponentStatus(source, prior, current, nativeSelfKo))
                {
                    nativeSelfKo = true;
                    ColdNext("effect324-native-sequence16-opcode9D-ownKO", current);
                }

                cameraChanged |= JsonSerializer.Serialize(source.NativeCamera) != JsonSerializer.Serialize(current.NativeCamera);
                stageObserved |= JsonSerializer.Serialize(source.NativeStage) != JsonSerializer.Serialize(current.NativeStage);
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 324, ReturnedDescriptor: > 0 } && currentGraphics.OriginalEffect is { Binding.Effect: 324, ReturnedDescriptor: 0 })
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
            Assert.True(callbacks < 2400, "The original effect324 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(cameraChanged);
            Assert.True(nativeSelfKo, "The original selected sequence did not publish its native own-caster suspension/KO edge.");
            Assert.True(changedRgb > 100, "No actual effect324 primitive changed the rendered RGB output.");
            Assert.True(geometryChangedRgb > 100, "No effect324 geometry changed rendered RGB outside its fullviewport fade.");
            Assert.Equal(1, observer.Contacts);
            Assert.Equal(1, observer.ContactGlyphs);
            observer.Accept(completed, output);
            ColdNext("effect324-visible-peak", peak);
            ColdNext("effect324-geometry-visible-peak", geometryPeak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "geometry-without-effect.rgba"), geometryPlain!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared original83 command8/action225/sequence16 Amount0 party component; actual allocated current bones, particles and temporary body history, camera and native lifecycle; native sequence16 opcode9D own-caster SavedKO/participant40 edge retained; HP and other statuses preserved; ordinary AI/formula admission excluded", NativeSelfKo = nativeSelfKo, Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, ChangedRgb = changedRgb, GeometryChangedRgb = geometryChangedRgb, CameraChanged = cameraChanged, StageChanged = stageObserved, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));
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

    internal static NativeEffectFixtureRequest CreateOriginalRequest() => new("effect324-original83-source", 324, 8, 225, 0, 1200, 180) { EncounterId = 236, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, AdmissionProbeMaximumCallbacks = 0, ControlledContactValue = 0 };

    private static BattleSnapshot PrepareSource(GameAssets game, BattleAssets assets, string output)
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT324_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select the current original83 component source.");
        BattleSnapshot baseline = NativeBattleEffectFamilyRunner.LoadSnapshot(path);
        Assert.Equal(236, baseline.Definition.EncounterId);
        Assert.Equal(76, baseline.Definition.StageId);
        (BattleSnapshot original, int physicalCaster) = NativeBattleEffect324FixturePreparation.Prepare(game, assets, baseline, Path.Combine(output, "source-allocation"));
        BattleSession restored = BattleSession.Restore(original);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(restored.Capture()));
        Assert.True(restored.NativeEntryReady);
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect324State.FromColdProcess()), JsonSerializer.Serialize(original.NativeEffectMemory.Effect324));
        string casterId = graphics.ActorIds[physicalCaster] ?? throw new InvalidDataException("Effect324 requires its current caster.");
        string targetId = graphics.ActorIds[0] ?? throw new InvalidDataException("Effect324 requires its current party recipient.");
        BattleCombatantDefinition caster = restored.GetCombatantDefinition(casterId);
        Assert.Equal(83, caster.ModelId);
        Assert.Equal((byte)99, graphics.Models[physicalCaster].ModelId4);
        Assert.True(caster.InitialHidden);
        Assert.True(caster.InitialSuspended);
        Assert.True(caster.InitialUntargetable);
        BattleCombatantSnapshot allocated = original.Combatants.Single(actor => actor.Id == casterId);
        Assert.True(allocated.NativeParticipant!.Active);
        Assert.False(allocated.NativeParticipant.Hidden);
        Assert.False(allocated.NativeParticipant.Suspended);
        Assert.False(allocated.NativeParticipant.Untargetable);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource ?? throw new InvalidDataException("Effect324 requires its actual current skeleton.");
        Assert.Equal(28, pose.BoneCount);
        Assert.Equal(28, NativeBattleBodyBoneMatrices.Require(graphics.Models[physicalCaster], pose).Length);
        Assert.NotNull(graphics.Models[physicalCaster].GeometryWorldMatrix);
        Assert.NotNull(graphics.Models[physicalCaster].ActiveResourceRow100);
        Assert.NotNull(original.NativeModelCpuResources);
        Assert.NotEqual(0u, original.NativeModelCpuResources.Rows[graphics.Models[physicalCaster].ActiveResourceRow100!.Value].Require(12).PhysicalAddress);
        Assert.Contains(original.Definition.Combatants, actor => actor.ModelId == 56 && actor.SceneSlot == 1 && actor.InitialActive);
        Assert.NotNull(original.NativeFileStaging);
        Assert.True(graphics.Models[0].Transform!.Scale38 > 0);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 225 && row.MagicId == 324 && row.KernelSection == 3 && row.KernelIndex == 225);
        Assert.Equal((byte)16, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        byte priorActions = packets.ActionCount;
        byte priorContacts = packets.ContactCount;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, int header) = packets.Prepare(new(checked((byte)physicalCaster), 8, 16, cameraChoice, 225, 324, null));
        BattleCombatantSnapshot target = original.Combatants.Single(actor => actor.Id == targetId);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("Effect324 requires its current target status.");
        BattleCombatantSnapshot preparedTarget = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattleContactPacket contact = new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0));
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorActions, header);
        Assert.Equal(priorContacts + 1, packets.ContactCount);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([324])[323];
        Assert.Same(game.ReadNativeEffect324Resources(), binding.Effect324Resources);
        BattleSnapshot source = original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(323, binding) }, Combatants = original.Combatants.Select(actor => actor.Id == targetId ? preparedTarget : actor).ToArray(), NativePresentationStatusMemory = (original.NativePresentationStatusMemory ?? throw new InvalidDataException("The declared component requires its current physical presentation-status bank.")).Observe(0, preparedTarget.NativeStatus!.Presentation), NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(original.Combatants.Select(actor => actor.CurrentHp), source.Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(source.NativeGraphics!.Models));
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(source.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.NativeFileStaging), JsonSerializer.Serialize(source.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(original.NativeModelCpuResources), JsonSerializer.Serialize(source.NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(original.NativeClock), JsonSerializer.Serialize(source.NativeClock));
        File.WriteAllText(Path.Combine(output, "declared-component.json"), JsonSerializer.Serialize(new { Scenario = "Declared original83 command8/action225/sequence16 Amount0 contact; native sequence16 opcode9D own-caster SavedKO/participant40 allowed, HP/other statuses preserved; ordinary AI/formula excluded", ActualCasterId = casterId, ActualTargetId = targetId, PhysicalCaster = physicalCaster, PhysicalTarget = 0, EnemyLevel = caster.Level, Action = action, CameraChoice = cameraChoice, CameraSelector = action.RawRecord[4] & 127, Header = packets.Headers[header], PriorActionCount = priorActions, PriorContactCount = priorContacts, ContactCount = 1, Contact = contact, StatusBefore = status.Presentation, StatusAfter = preparedTarget.NativeStatus!.Presentation, StatusCache = "Declared47E2D0 projection copies current Saved/Battle on the prepared component copy; logical HP/status preserved" }));
        return source;
    }

    internal static bool AssertComponentStatus(BattleSnapshot source, BattleSnapshot prior, BattleSnapshot current, bool witnessedSelfKo)
    {
        string casterId = source.NativeAllocations.Single(allocation => allocation.SceneSlot == 0).Id;
        bool transition = false;
        foreach (BattleCombatantSnapshot original in source.Combatants)
        {
            BattleCombatantSnapshot before = prior.Combatants.Single(actor => actor.Id == original.Id);
            BattleCombatantSnapshot actual = current.Combatants.Single(actor => actor.Id == original.Id);
            Assert.Equal(original.CurrentHp, actual.CurrentHp);
            Assert.Equal(original.NativeStatus!.Battle, actual.NativeStatus!.Battle);
            if (original.Id != casterId || actual.NativeStatus.Saved == original.NativeStatus.Saved)
            {
                Assert.Equal(original.NativeStatus.Saved, actual.NativeStatus.Saved);
                continue;
            }
            Assert.Equal(unchecked((ushort)(original.NativeStatus.Saved | 1)), actual.NativeStatus.Saved);
            if (before.NativeStatus!.Saved == actual.NativeStatus.Saved)
            {
                Assert.True(witnessedSelfKo, "The current self-KO owner must have its actual observed native sequence producer.");
                continue;
            }
            Assert.Equal(original.NativeStatus.Saved, before.NativeStatus.Saved);
            int caster = current.NativeGraphics!.ActorIds.IndexOf(casterId);
            Assert.InRange(caster, 3, 6);
            NativeBattleGraphicsModel previousModel = prior.NativeGraphics!.Models[caster];
            NativeBattleGraphicsModel model = current.NativeGraphics.Models[caster];
            Assert.Equal((byte)16, previousModel.Sequence);
            Assert.Equal((byte)16, model.Sequence);
            Assert.True(previousModel.SequenceState!.Offset46 <= 117);
            Assert.Equal(120, model.SequenceState!.Offset46);
            Assert.Equal(0x40, model.ResourceFlags44!.Value & 0x40);
            BattleCombatantDefinition prototype = current.Definition.Combatants.Single(actor => !actor.IsPlayer && actor.SceneSlot == 0);
            NativeBattleGraphicsResource resource = current.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == prototype.Id).Resource;
            int start = resource.SequenceStart(16);
            Assert.Equal((byte)0x9D, resource.SequenceSection[start + 117]);
            Assert.Equal((byte)0xA3, resource.SequenceSection[start + 118]);
            Assert.Equal((byte)0xA9, resource.SequenceSection[start + 119]);
            Assert.Equal(before.NativeParticipant!.NativeCombatantFlags!.Value | 0x40u, actual.NativeParticipant!.NativeCombatantFlags!.Value);
            transition = true;
        }
        return transition;
    }

    private static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 324, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;
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
