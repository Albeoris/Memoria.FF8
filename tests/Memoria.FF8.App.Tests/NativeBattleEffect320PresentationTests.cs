using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect320PresentationTests
{
    [Fact]
    public void Original_Effect320_declared_component_preserves_current_models_particles_and_native_lifecycle()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT320_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT320_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, the current Dark Flare component source and a distinct output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Equal(483, source.Definition.EncounterId);
        Assert.Equal(139, source.Definition.StageId);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        RunPrepared(QueueComponent(source, game, assets), game, assets, output!);
    }

    internal static BattleSnapshot QueueComponent(BattleSnapshot original, GameAssets game, BattleAssets assets)
    {
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        string casterId = graphics.ActorIds[3]!;
        string targetId = graphics.ActorIds[0]!;
        BattleCombatantDefinition caster = BattleSession.Restore(original).GetCombatantDefinition(casterId);
        Assert.Equal(132, caster.ModelId);
        Assert.Equal((byte)148, graphics.Models[3].ModelId4);
        Assert.Equal(0, caster.SceneSlot);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource!;
        Assert.Equal(44, NativeBattleBodyBoneMatrices.Require(graphics.Models[3], pose).Length);
        BattleNativeAction action = Assert.Single(assets.NativeActions, item => item.Command == 8 && item.ActionId == 305 && item.MagicId == 320 && item.KernelSection == 3 && item.KernelIndex == 305);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, int header) = packets.Prepare(new(3, 8, 12, cameraChoice, 305, 320, null));
        NativeBattleStatusSnapshot status = original.Combatants.Single(actor => actor.Id == targetId).NativeStatus!;
        // The declared post-formula party boundary publishes47E2D0's direct status copy before506690.
        NativeBattleStatusSnapshot contactStatus = status.Presentation.Saved is null ? status with { Presentation = new(status.Saved, status.Battle) } : status;
        BattleCombatantSnapshot[] contactActors = original.Combatants.Select(actor => actor.Id == targetId ? actor with { NativeStatus = contactStatus } : actor).ToArray();
        (packets, _) = packets.AppendContact(new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([320])[319];
        BattleSnapshot prepared = original with { Combatants = contactActors, Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(319, binding) }, NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(prepared.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(original.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), prepared.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
        Assert.Equal(status.Saved, contactStatus.Presentation.Saved);
        Assert.Equal(status.Battle, contactStatus.Presentation.Battle);
        return prepared;
    }

    internal static BattleSnapshot RunPrepared(BattleSnapshot source, GameAssets game, BattleAssets assets, string output)
    {
        source = NativeBattleRetainedEffectMigrationAcceptance.Prepare(source, 320, output);
        BattleSession battle = Restore(source, game);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        HashSet<string> restored = [];
        HashSet<uint> callbacksSeen = [];
        HashSet<int> packetBanks = [];
        HashSet<uint> programmes = [];
        int contacts = 0;
        HashSet<string> phases = [];
        List<object> observations = [];
        BattleSnapshot prior = source;
        BattleSnapshot peak = source;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        int maximumRgb = 0;
        int maximumGeometryRgb = 0;
        BattleSnapshot? geometryPeak = null;
        byte[]? geometryPixels = null;
        int completions = 0;
        int callbacks = 0;
        bool cameraRegistered = false;
        bool cameraPlayback = false;
        bool cameraView = false;
        bool? cameraDecision = null;
        HashSet<uint> packetCursors = [];
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-source", source, null);
        try
        {
            Cold("before-worker", source);
            for (; callbacks < 2400; callbacks++)
            {
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                NativeBattleGraphicsState graphics = current.NativeGraphics!;
                NativeBattleModulePoolTask? priorMain = prior.NativeGraphics!.OriginalEffect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => task.Callback == MainCallback && (task.Flags & 1) != 0);
                int processedAge = priorMain?.ReadWord(12) ?? -1;
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: Effect, ReturnedDescriptor: > 0 } && graphics.OriginalEffect is { Binding.Effect: Effect, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                if (graphics.OriginalEffect is { Binding.Effect: Effect } effect)
                {
                    foreach (NativeBattleEffectTaskPool pool in effect.Pools)
                    {
                        foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0))
                        {
                            callbacksSeen.Add(task.Callback);
                            int age = task.ReadWord(12);
                            bool selected = task.Callback == MainCallback && new[] { 0, 1, 15, 20, 35, 36, 37, 45, 46, 47, 48, 49, 50, 51, 52, 82, 83, 84, 97, 98 }.Contains(age) || task.Callback == RootCallback && age == 1 || task.Callback == ParticleCallback && age is 1 or 16;
                            if (selected)
                            {
                                string phase = $"task-{task.Callback:X6}-age-{age}";
                                if (phases.Add(phase))
                                {
                                    Cold(phase, current);
                                }
                            }
                            if (task.Callback == MainCallback)
                            {
                                foreach (int offset in new[] { 0x40, 0x230, 0x340 })
                                {
                                    programmes.Add(task.ReadDword(offset));
                                }
                            }
                        }
                    }
                    NativeBattleEffect320State state = current.NativeEffectMemory!.Effect320!;
                    NativeBattleEffect320Resources resources = effect.Binding.Effect320Resources!;
                    if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
                    {
                        cameraRegistered = true;
                        Assert.Equal(8, control.StreamOffset);
                        Assert.Equal(CameraModelOffset, control.ModelTableOffset);
                        Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
                        if (effect.ReturnedDescriptor > 0 && graphics.ActionGraphics?.UseActionCamera is bool decision)
                        {
                            cameraDecision = decision;
                        }
                    }
                    bool track = current.NativeCamera!.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[CameraModelOffset..]));
                    cameraPlayback |= track;
                    cameraView |= track && JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
                    NativeBattleLogicalCommand[] commands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8").DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
                    if (commands.Length > 0)
                    {
                        ImmutableHashSet<NativeBattlePrimitiveReference> owned = commands.Select(command => command.Packet).ToImmutableHashSet();
                        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame;
                        BattleSnapshot without = current with { NativeGraphics = graphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
                        int changed = CountRgb(pixels, plain);
                        if (changed > maximumRgb)
                        {
                            maximumRgb = changed;
                            peak = current;
                            peakPixels = pixels;
                            peakPlain = plain;
                        }
                        if (processedAge >= 0 && processedAge is not (36 or 46 or 47 or 48 or 49) && changed > maximumGeometryRgb)
                        {
                            maximumGeometryRgb = changed;
                            geometryPeak = current;
                            geometryPixels = pixels;
                        }
                        uint cursor = state.ReadDword(CursorGlobal);
                        uint arena = state.ReadDword(ArenaGlobal);
                        packetCursors.Add(cursor);
                        packetBanks.Add(cursor >= arena + PacketBank1Offset ? 1 : 0);
                    }
                    observations.Add(new { current.Frame, graphics.Callback, Commands = commands.Length, AnchorBound = state.MutableAnchorSourceBound, MutableAnchorX = state.ReadWord(MutableAnchorAddress), Cursor = state.ReadDword(CursorGlobal) });
                }
                if (processedAge == 83)
                {
                    contacts++;
                }
                prior = current;
                if (Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.True(callbacks < 2400, "Dark Flare did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(maximumRgb > 100, "Dark Flare geometry did not change rendered RGB.");
            Assert.True(maximumGeometryRgb > 100, "Dark Flare geometry outside flash frames did not change rendered RGB.");
            Assert.True(cameraRegistered);
            Assert.Equal(new uint[] { RootCallback, MainCallback, ParticleCallback }.Order(), callbacksSeen.Order());
            Assert.Equal(ProgrammeAddresses.Order(), programmes.Order());
            Assert.Equal(new[] { 0, 1 }, packetBanks.Order());
            Assert.Equal(1, contacts);
            Assert.True(completed.NativeEffectMemory!.Effect320!.MutableAnchorSourceBound);
            Assert.All(completed.NativeEffectMemory.Effect320.FixedBanks.Values, bank => Assert.NotNull(bank));
            Assert.All(completed.NativeEffectMemory.Effect320.CommonQueueStorage.Values, queue => Assert.NotNull(queue));
            if (cameraDecision == true)
            {
                Assert.True(cameraPlayback);
                Assert.True(cameraView);
            }
            Cold("visible-peak", peak);
            Cold("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak!, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect, Scenario = "Original132 command8/action305/sequence12 controlled7 party-cache component; ordinary AI, eligibility and damage formula excluded", Callbacks = callbacks + 1, Completions = completions, ChangedRgb = maximumRgb, GeometryChangedRgb = maximumGeometryRgb, CameraRegistered = cameraRegistered, CameraDecision = cameraDecision, CameraPlayback = cameraPlayback, CameraView = cameraView, CallbacksSeen = callbacksSeen.Order(), Programmes = programmes.Order(), PacketBanks = packetBanks.Order(), Contacts = contacts, Phases = phases.Order(), PacketCursors = packetCursors.Order(), RestoredBoundaries = restored.Order(), Observations = observations, completed.Frame }));
            return completed;
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Callbacks = callbacks, Error = error.ToString(), CallbacksSeen = callbacksSeen.Order(), Phases = phases.Order(), Observations = observations }));
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

    internal static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 320, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;

    private static int CountRgb(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int changed = 0;
        for (int index = 0; index < first.Length; index += 4)
        {
            if (first[index] != second[index] || first[index + 1] != second[index + 1] || first[index + 2] != second[index + 2])
            {
                changed++;
            }
        }
        return changed;
    }
}
