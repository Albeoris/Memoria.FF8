using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect316PresentationTests
{
    [Fact]
    public void Original_Effect316_declared_component_preserves_current_geometry_and_native_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([316], "MEMORIA_FF8_EFFECT316");
    }

    [Fact]
    public void Selected_Effect316_snapshot_continues_one_callback()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT316_CONTINUATION_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and a standalone effect316 continuation.");
        using GameAssets game = new(directory!);
        BattleSnapshot source = NativeBattleFixtureInspection.ReadLogicalSnapshot(path!);
        Assert.Equal(316, source.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Binding.Effect);
        BattleSession session = Restore(source, game);
        session.Tick();
        Assert.Equal(source.ReadFixturePresentation().NativeGraphics!.Callback + 1, session.CaptureForContinuation().ReadFixturePresentation().NativeGraphics!.Callback);
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT316_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select the current original130 component source.");
        BattleSnapshot original = NativeBattleFixtureInspection.ReadLogicalSnapshot(path);
        Assert.Equal(441, original.Definition.EncounterId);
        Assert.Equal(124, original.Definition.StageId);
        Assert.True(BattleSession.Restore(original).NativeEntryReady);
        Assert.Null(original.ReadFixturePresentation().NativeGraphics!.OriginalEffect);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect316State.FromColdProcess())), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(original.ReadFixturePresentation().NativeEffectMemory!.Effect316)));
        BattleSnapshot prepared = QueueComponent(original, game, assets);
        _ = RunPrepared(prepared, game, assets, output);
    }

    internal static BattleSnapshot QueueComponent(BattleSnapshot original, GameAssets game, BattleAssets assets)
    {
        NativeBattleGraphicsState graphics = original.ReadFixturePresentation().NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, original.ReadFixturePresentation().NativeEffectMemory!.Active);
        string casterId = ReadOriginalModelActorId(graphics, 3)!;
        string targetId = ReadOriginalModelActorId(graphics, 0)!;
        BattleCombatantDefinition caster = BattleSession.Restore(original).GetCombatantDefinition(casterId);
        Assert.Equal(130, caster.ModelId);
        Assert.Equal((byte)146, ReadOriginalModelCell(graphics, 3).ModelId4);
        Assert.Equal(0, caster.SceneSlot);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource!;
        Assert.Equal(29, NativeBattleBodyBoneMatrices.Require(ReadOriginalModelCell(graphics, 3), pose).Length);
        BattleNativeAction action = Assert.Single(assets.NativeActions, item => item.Command == 8 && item.ActionId == 203 && item.MagicId == 316 && item.KernelSection == 3 && item.KernelIndex == 203);
        Assert.Equal((byte)15, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleActionPacketsState packets = original.ReadFixturePresentation().NativeActionPackets!;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(3), 8, 15, cameraChoice, 203, 316, null));
        NativeBattleStatusSnapshot status = original.Combatants.Single(actor => actor.Id == targetId).NativeStatus!;
        (packets, _) = packets.AppendContact(new(new(packets.ModelAliases.DecodeLiteral(0), action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), null));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header.Ordinal * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([316])[315];
        BattleSnapshot prepared = ((original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(315, binding) }}).WithFixturePresentation(facts => facts with { NativeActionPackets = packets })).WithFixturePresentation(facts => facts with { NativeGraphics = graphics with { Descriptors = descriptors } });
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeEffectMemory), JsonSerializer.Serialize(prepared.ReadFixturePresentation().NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeFileStaging), JsonSerializer.Serialize(prepared.ReadFixturePresentation().NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(graphics)), JsonSerializer.Serialize(ReadOriginalModelCells(prepared.ReadFixturePresentation().NativeGraphics!)));
        Assert.Equal(original.Combatants, prepared.Combatants);
        return prepared;
    }

    internal static BattleSnapshot RunPrepared(BattleSnapshot source, GameAssets game, BattleAssets assets, string output)
    {
        BattleSession battle = Restore(source, game);
        NativeBattleEffect316PresentationObserver observer = new();
        BattleScenePresentation scene = new(assets, source.Definition);
        HashSet<string> restored = [];
        BattleSnapshot prior = source;
        BattleSnapshot peak = source;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        int maximumRgb = 0;
        int firstRgb = 0;
        int secondRgb = 0;
        int completions = 0;
        int callbacks = 0;
        bool stageChanged = false;
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-source", source, null);
        try
        {
            Cold("before-worker", source);
            for (; callbacks < 2400; callbacks++)
            {
                battle.Tick();
                BattleSnapshot current = battle.CaptureForContinuation();
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());
                stageChanged |= JsonSerializer.Serialize(source.ReadFixturePresentation().NativeStage) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeStage);
                if (prior.ReadFixturePresentation().NativeGraphics!.OriginalEffect is { Binding.Effect: 316, ReturnedDescriptor: > 0 } && current.ReadFixturePresentation().NativeGraphics!.OriginalEffect is { Binding.Effect: 316, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                observer.Observe(prior, current, Cold);
                if (observer.CurrentCommands.Length > 0)
                {
                    NativeBattleGraphicsState graphics = current.ReadFixturePresentation().NativeGraphics!;
                    ImmutableHashSet<NativeBattlePrimitiveReference> packets = observer.CurrentCommands.Select(command => command.Packet).ToImmutableHashSet();
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame!;
                    BattleSnapshot without = current.WithFixturePresentation(facts => facts with { NativeGraphics = graphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !packets.Contains(command.Command.Packet)).ToImmutableArray() } } });
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    byte[] plain = new BattleScenePresentation(assets, source.Definition).Render(BattleSession.Restore(without), 320, 224).ToArray();
                    int changed = CountRgb(pixels, plain);
                    int processedAge = NativeBattleEffect316PresentationObserver.ReadMain(prior)?.ReadWord(12) ?? -1;
                    if (processedAge is >= 20 and < 150)
                    {
                        firstRgb = Math.Max(firstRgb, changed);
                    }
                    if (processedAge is >= 150 and < 300)
                    {
                        secondRgb = Math.Max(secondRgb, changed);
                    }
                    if (changed > maximumRgb)
                    {
                        maximumRgb = changed;
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
            Assert.True(callbacks < 2400, "The original effect316 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(stageChanged);
            Assert.True(firstRgb > 100, "The first native body arm did not change rendered RGB.");
            Assert.True(secondRgb > 100, "The second native body arm did not change rendered RGB.");
            observer.Accept(completed, output);
            Cold("visible-peak", peak);
            Cold("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared original130 command8/action203/sequence15 graphics component with Amount0 party contact; full native geometry/record lifecycle; ordinary AI/formula admission excluded", Effect, Callbacks = callbacks + 1, Completions = completions, ChangedRgb = maximumRgb, FirstArmChangedRgb = firstRgb, SecondArmChangedRgb = secondRgb, StageChanged = stageChanged, RestoredBoundaries = restored.Order(), completed.Frame }));
            return completed;
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            observer.Publish(output);
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
        BattleSnapshot json = NativeBattleFixtureInspection.ReadLogicalSnapshot(Path.Combine(output, name + ".battle.json"));
        BattleSession cold = Restore(json, game);
        BattleSession direct = Restore(snapshot, game);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
        cold.Tick();
        direct.Tick();
        Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
        byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition).Render(cold, 320, 224).ToArray();
        Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition).Render(direct, 320, 224).ToArray(), pixels);
        NativeBattleRestorationPresentationTests.Publish(output, name + "-next", cold.Capture(), pixels);
    }

    internal static bool Released(BattleSnapshot snapshot) => snapshot.ReadFixturePresentation().NativeGraphics?.OriginalEffect is { Binding.Effect: 316, ReturnedDescriptor: 0 } effect && snapshot.ReadFixturePresentation().NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Queue.Any(index => snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;

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
