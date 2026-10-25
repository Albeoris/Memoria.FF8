using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect251PresentationTests
{
    [Fact]
    public void Original_Effect251_declared_component_preserves_current_models_particles_and_native_lifecycle()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT251_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT251_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, the current Effect251 component source and a distinct output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Equal(119, source.Definition.EncounterId);
        Assert.Equal(33, source.Definition.StageId);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        Assert.Equal(0, source.Frame);
        RunPrepared(QueueComponent(source, game, assets), game, assets, output!);
    }

    internal static BattleSnapshot QueueComponent(BattleSnapshot original, GameAssets game, BattleAssets assets)
    {
        Assert.Equal(119, original.Definition.EncounterId);
        Assert.Equal(33, original.Definition.StageId);
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        BattleSession session = BattleSession.Restore(original);
        for (int index = 0; index < 2; index++)
        {
            int physical = index + 3;
            BattleCombatantDefinition actor = session.GetCombatantDefinition(graphics.ActorIds[physical]!);
            Assert.Equal(index == 0 ? 86 : 82, actor.ModelId);
            Assert.Equal(index, actor.SceneSlot);
            BattleCombatantSnapshot combatant = Assert.Single(original.Combatants, item => item.Id == actor.Id);
            Assert.True(combatant.NativeParticipant!.Active);
            Assert.False(combatant.NativeParticipant.Hidden);
            Assert.Equal(index == 1, combatant.NativeParticipant.Suspended);
            Assert.Equal(index == 1, combatant.NativeParticipant.Untargetable);
            NativeBattleGraphicsModel model = graphics.Models[physical];
            Assert.Equal((byte)(actor.ModelId + 16), model.ModelId4);
            NativeBattleGraphicsResource current = NativeBattleEnemyResourceView.Resolve(original.NativeModelCpuResources!, original.NativeFileStaging!, model.ActiveResourceRow100!.Value, assets.ReadNativeGraphicsResource(actor));
            Assert.Equal(index == 0 ? 41 : 2, NativeBattleBodyBoneMatrices.Require(model, current.PoseSource!).Length);
            model.GeometryWorldMatrix!.Validate();
        }
        BattleCombatantDefinition caster = session.GetCombatantDefinition(graphics.ActorIds[3]!);
        BattleNativeAction action = Assert.Single(assets.NativeActions, item => item.Command == 8 && item.ActionId == 314 && item.MagicId == 251 && item.KernelSection == 3 && item.KernelIndex == 314);
        Assert.Equal((byte)33, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        (packets, int header) = packets.Prepare(new(3, 8, 33, unchecked((sbyte)action.RawRecord[4]), 314, 251, null));
        NativeBattleStatusSnapshot status = Assert.Single(original.Combatants, actor => actor.Id == graphics.ActorIds[0]).NativeStatus!;
        NativeBattleStatusSnapshot contactStatus = status.Presentation.Saved is null ? status with { Presentation = new(status.Saved, status.Battle) } : status;
        BattleCombatantSnapshot[] contactActors = original.Combatants.Select(actor => actor.Id == graphics.ActorIds[0] ? actor with { NativeStatus = contactStatus } : actor).ToArray();
        (packets, _) = packets.AppendContact(new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([251])[250];
        BattleSnapshot prepared = original with { Combatants = contactActors, Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(250, binding) }, NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(prepared.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(original.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle, actor.NativeParticipant)), prepared.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle, actor.NativeParticipant)));
        Assert.Equal(JsonSerializer.Serialize(original.Combatants.Where(actor => actor.Id != graphics.ActorIds[0])), JsonSerializer.Serialize(prepared.Combatants.Where(actor => actor.Id != graphics.ActorIds[0])));
        Assert.Equal(status.Saved, contactStatus.Presentation.Saved);
        Assert.Equal(status.Battle, contactStatus.Presentation.Battle);
        Assert.Equal(JsonSerializer.Serialize(original.NativeCrtRandom), JsonSerializer.Serialize(prepared.NativeCrtRandom));
        return prepared;
    }

    internal static BattleSnapshot RunPrepared(BattleSnapshot source, GameAssets game, BattleAssets assets, string output)
    {
        BattleSession battle = Restore(source, game);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        HashSet<string> restored = [];
        NativeBattleEffect251PresentationObserver observer = new();
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
                    NativeBattleLogicalCommand[] commands = NativeBattleEffect251PresentationObserver.CurrentCommands(current);
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
                        if (processedAge >= 0 && (processedAge < 114 || processedAge > 117) && (processedAge < 163 || processedAge > 166) && changed > maximumGeometryRgb)
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
            Assert.True(callbacks < 2400, "Effect251 did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(maximumRgb > 100, "Effect251 geometry did not change rendered RGB.");
            Assert.True(maximumGeometryRgb > 100, "Effect251 geometry outside flash frames did not change rendered RGB.");
            observer.Accept(completed, output);
            Cold("visible-peak", peak);
            Cold("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak!, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect, Scenario = "Original86 command8/action314/sequence33 with authored suspended untargetable physical4 original82 rig; declared post-formula amount7 component; ordinary AI, formula and text-only334 prerequisite excluded", Callbacks = callbacks + 1, Completions = completions, ChangedRgb = maximumRgb, GeometryChangedRgb = maximumGeometryRgb, RestoredBoundaries = restored.Order(), completed.Frame }));
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

    internal static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 251, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;

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
