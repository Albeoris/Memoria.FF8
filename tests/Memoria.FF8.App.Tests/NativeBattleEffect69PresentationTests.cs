using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect69PresentationTests
{
    [Fact]
    public void Original_Effect69_declared_startup_component_preserves_current_stage_reload_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([69], "MEMORIA_FF8_EFFECT69");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleEffect69PresentationObserver observer = new();
        HashSet<string> restored = [];
        BattleSnapshot prior = source;
        BattleSnapshot peak = source;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        int callbacks = 0;
        int completions = 0;
        int changedRgb = 0;
        bool cameraChanged = false;
        bool stageChanged = false;
        string boundary = "queued-component";
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-source", source, null);
        try
        {
            for (; callbacks < 1800; callbacks++)
            {
                boundary = "callback-" + callbacks;
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());
                Assert.DoesNotContain(current.NativeGraphics!.Effects.Slots, task => (task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0);
                cameraChanged |= JsonSerializer.Serialize(source.NativeCamera) != JsonSerializer.Serialize(current.NativeCamera);
                stageChanged |= JsonSerializer.Serialize(source.NativeStage) != JsonSerializer.Serialize(current.NativeStage);
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 69, ReturnedDescriptor: > 0 } && current.NativeGraphics.OriginalEffect is { Binding.Effect: 69, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                observer.Observe(prior, current, ColdNext);
                NativeBattleLogicalCommand[] commands = observer.CurrentCommands;
                NativeBattleReachedCaptureAssertions.Moving(69, current, commands);
                if (commands.Length > 0)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = commands.Select(command => command.Packet).ToImmutableHashSet();
                    NativeBattlePrimitiveFrame primitive = current.NativeGraphics.PrimitiveFrame!;
                    BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
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
                }
                prior = current;
                if (Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.True(callbacks < 1800, "The original effect69 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(cameraChanged);
            Assert.True(stageChanged);
            Assert.True(changedRgb > 0, "No actual effect69 primitive changed the rendered RGB output.");
            Assert.Contains(completed.Events, item => item.NativeModuleSound is { Sound: 0x64307 });
            observer.Accept(completed, output);
            ColdNext("effect69-visible-peak", peak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared original model122 command8/action350/sequence12 startup renderer and lifecycle in formation509/stage142; zero targets and contacts; no initializer magic TIM; current stage replacement TIM upload included; no ordinary AI admission or formula replay", Effect = Effect, Callbacks = callbacks + 1, Contacts = 0, ContactGlyphs = 0, Completions = completions, ChangedRgb = changedRgb, CameraChanged = cameraChanged, StageChanged = stageChanged, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));
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

    private static BattleSnapshot PrepareSource(GameAssets game, BattleAssets assets, string output)
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT69_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select a concrete fresh original formation509/stage142 standalone source for effect69.");
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(path);
        Assert.Equal(509, original.Definition.EncounterId);
        Assert.Equal(142, original.Definition.StageId);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(BattleSession.Restore(original).Capture()));
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect69State.FromColdProcess()), JsonSerializer.Serialize(original.NativeEffectMemory.Effect69));
        BattleCombatantDefinition definition = original.Definition.Combatants.Single(actor => actor.Id == graphics.ActorIds[4]);
        Assert.Equal(122, definition.ModelId);
        Assert.Null(definition.NativeAi);
        Assert.Equal((byte)138, graphics.Models[4].ModelId4);
        Assert.True(original.Combatants.Single(actor => actor.Id == definition.Id).NativeParticipant!.Active);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
        Assert.Equal(70, NativeBattleBodyBoneMatrices.Require(graphics.Models[4], resource.PoseSource!).Length);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 350 && row.MagicId == 69 && row.KernelSection == 3 && row.KernelIndex == 350);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(122, 1), action));
        uint stage = original.NativeStage!.SourceAddress1D9A820 ?? throw new InvalidDataException("Effect69 requires its actual loaded stage directory owner.");
        Assert.NotEqual(0u, stage);
        Assert.Equal(6, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(original.NativeFileStaging!.Read(stage, 4)));
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        byte priorActions = packets.ActionCount;
        byte priorContacts = packets.ContactCount;
        (packets, int header) = packets.Prepare(new(4, 8, 12, -1, 350, 69, null));
        (packets, _) = packets.Publish(0);
        Assert.Equal(priorActions, header);
        Assert.Equal(priorContacts, packets.ContactCount);
        Assert.Equal((byte)0, packets.Headers[header].ReadByte(16));
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([69])[68];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.True(binding.InitializerIsRet);
        Assert.Null(binding.InitializerResource);
        Assert.Null(binding.InitializerAsset);
        Assert.Same(game.ReadNativeEffect69Resources(), binding.Effect69Resources);
        BattleSnapshot source = original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(68, binding) }, NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(original.Combatants), JsonSerializer.Serialize(source.Combatants));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(source.NativeGraphics.Models));
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(source.NativeEffectMemory));
        File.WriteAllText(Path.Combine(output, "declared-startup.json"), JsonSerializer.Serialize(new { Scenario = "Declared original startup component; no target, contact, AI or formula execution", Action = action, Header = packets.Headers[header], PriorActionCount = priorActions, PriorContactCount = priorContacts, CurrentStageDirectory = stage, CasterModel = graphics.Models[4].ModelId4 }));
        return source;
    }

    private static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 69, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
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
