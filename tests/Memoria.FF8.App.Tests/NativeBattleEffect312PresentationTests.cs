using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect312PresentationTests
{
    [Fact]
    public void Original_Effect312_declared_component_preserves_current_models_particles_and_native_lifecycle()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT312_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT312_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, the current Effect312 component source and a distinct output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Equal(794, source.Definition.EncounterId);
        Assert.Equal(65, source.Definition.StageId);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        Assert.All(source.NativeGraphics.Models.Skip(3).Take(3), model => Assert.Null(model.ReadLinkedModelIndex140()));
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
        Assert.Equal(105, caster.ModelId);
        Assert.Equal((byte)121, graphics.Models[3].ModelId4);
        Assert.Equal(0, caster.SceneSlot);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(caster);
        NativeBattlePoseSource pose = NativeBattleEnemyResourceView.Resolve(original.NativeModelCpuResources!, original.NativeFileStaging!, graphics.Models[3].ActiveResourceRow100!.Value, resource).PoseSource!;
        Assert.Equal(44, NativeBattleBodyBoneMatrices.Require(graphics.Models[3], pose).Length);
        BattleNativeAction action = Assert.Single(assets.NativeActions, item => item.Command == 8 && item.ActionId == 285 && item.MagicId == 312 && item.KernelSection == 3 && item.KernelIndex == 285);
        Assert.Equal((byte)18, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, int header) = packets.Prepare(new(3, 8, 18, cameraChoice, 285, 312, null));
        BattleCombatantSnapshot[] contactActors = original.Combatants.ToArray();
        for (int actor = 0; actor < 3; actor++)
        {
            string id = graphics.ActorIds[actor]!;
            int index = Array.FindIndex(contactActors, item => item.Id == id);
            NativeBattleStatusSnapshot status = contactActors[index].NativeStatus!;
            contactActors[index] = contactActors[index] with { NativeStatus = status.Presentation.Saved is null ? status with { Presentation = new(status.Saved, status.Battle) } : status };
            (packets, _) = packets.AppendContact(new(new(checked((byte)actor), action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(3);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([312])[311];
        BattleSnapshot prepared = original with { Combatants = contactActors, Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(311, binding) }, NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(prepared.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(original.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), prepared.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
        return prepared;
    }

    internal static BattleSnapshot RunPrepared(BattleSnapshot source, GameAssets game, BattleAssets assets, string output)
    {
        BattleSession battle = Restore(source, game);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        HashSet<string> restored = [];
        NativeBattleEffect312PresentationObserver observer = new();
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
                observer.Observe(prior, current, Cold);
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: Effect, ReturnedDescriptor: > 0 } && graphics.OriginalEffect is { Binding.Effect: Effect, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                if (graphics.OriginalEffect is { Binding.Effect: Effect } effect)
                {
                    NativeBattleLogicalCommand[] commands = NativeBattleEffect312PresentationObserver.CurrentCommands(current);
                    if (commands.Length > 0)
                    {
                        ImmutableHashSet<NativeBattlePrimitiveReference> owned = commands.Select(command => command.Packet).ToImmutableHashSet();
                        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame!;
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
                        if (processedAge >= 0 && (processedAge < 6 || processedAge > 18) && (processedAge < 51 || processedAge > 57) && changed > maximumGeometryRgb)
                        {
                            maximumGeometryRgb = changed;
                            geometryPeak = current;
                            geometryPixels = pixels;
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
            Assert.True(callbacks < 2400, "Effect312 did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(maximumRgb > 100, "Effect312 geometry did not change rendered RGB.");
            Assert.True(maximumGeometryRgb > 100, "Effect312 geometry outside flash frames did not change rendered RGB.");
            observer.Accept(completed, output);
            Cold("visible-peak", peak);
            Cold("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak!, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect, Scenario = "Original105 command8/action285/sequence18 with intact companions and three party contacts; declared amount7 presentation component; ordinary AI and damage formula excluded", Callbacks = callbacks + 1, Completions = completions, ChangedRgb = maximumRgb, GeometryChangedRgb = maximumGeometryRgb, RestoredBoundaries = restored.Order(), completed.Frame }));
            return completed;
        }
        catch (Exception error)
        {
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

    internal static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 312, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;

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
