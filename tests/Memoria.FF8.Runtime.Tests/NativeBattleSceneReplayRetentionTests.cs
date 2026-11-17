using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSceneReplayRetentionTests
{
    [Fact]
    public void Copied_pixels_and_their_transitive_sources_survive_battle_return_json_and_next_entry()
    {
        BattleSession first = Session(NativeBattleRetainedMemory.FromColdProcess());
        NativeBattleEffectMemory effects = WithCopiedPixels(first.NativeEffectMemory!);
        NativeBattleSceneReplaySource leaf = new(new([], []), NativeBattleEffectTextureState.FromColdProcess(), BattleSceneVisibleArea.Native);
        NativeBattleSceneReplaySource parent = leaf with { Textures = leaf.Textures with { SceneCopies = [Region(3)] } };
        var pool = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(3, leaf).Add(7, parent);
        BattleSnapshot snapshot = NativeBattleStorageSnapshots.Capture(first.Capture() with
        {
            NativeEffectMemory = effects,
            NativeGraphics = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 4, 1) { PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() with { SceneReplaySources = pool } }
        });
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.Capture(snapshot, snapshot.Definition.NativeRetainedMemory!.ColdExecutableSha256);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(retained))!;
        BattleSession next = Session(restored);
        NativeBattlePrimitiveFrame frame = InitializeFrame(next);

        Assert.Equal(new long[] { 3, 7 }, frame.SceneReplaySources!.Keys.Order().ToArray());
        Assert.Equal(7, Assert.Single(next.NativeEffectMemory!.Textures!.SceneCopies!.Value).Capture.ReplaySourceId);
        var pruned = NativeBattleSceneCopyProvenance.PruneSources(frame, next.NativeEffectMemory.Textures, next.NativeEffectMemory.PrimitiveProcess!.TextureWindowCache);
        Assert.Equal(new long[] { 3, 7 }, pruned.Keys.Order().ToArray());
        Assert.Empty(frame.LastCommands);
        Assert.Null(frame.SceneSources);

        NativeBattleEffectTextureState overwritten = next.NativeEffectMemory.Textures with { SceneCopies = null };
        Assert.Equal(2, NativeBattleSceneCopyProvenance.PruneSources(frame, overwritten, next.NativeEffectMemory.PrimitiveProcess.TextureWindowCache).Count);
        Assert.Empty(NativeBattleSceneCopyProvenance.PruneSources(frame, overwritten));

        MethodInfo retain = typeof(BattleSession).GetMethod("RetainNativeSceneReplaySource", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using IDisposable visible = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        NativeBattleGraphicsState graphics = new([], [], 4, 1) { PrimitiveFrame = frame };
        graphics = (NativeBattleGraphicsState)retain.Invoke(next, [graphics])!;
        Assert.Equal(9, graphics.PrimitiveFrame!.NextSceneReplaySourceId);
        Assert.Contains(8L, graphics.PrimitiveFrame.SceneReplaySources!.Keys);
        graphics = (NativeBattleGraphicsState)retain.Invoke(next, [graphics with { Callback = 2 }])!;
        Assert.Equal(10, graphics.PrimitiveFrame!.NextSceneReplaySourceId);
        Assert.Contains(9L, graphics.PrimitiveFrame.SceneReplaySources!.Keys);

    }

    [Fact]
    public void Legacy_retained_save_recovers_raw_pixels_without_replaying_gameplay_or_mutating_the_save()
    {
        BattleSession first = Session(NativeBattleRetainedMemory.FromColdProcess());
        NativeBattleRetainedMemory legacy = NativeBattleStorageSnapshots.Capture(first.Capture().Definition.NativeRetainedMemory! with { EffectMemory = WithCopiedPixels(first.NativeEffectMemory!) });
        string json = JsonSerializer.Serialize(legacy);
        BattleSession restored = Session(JsonSerializer.Deserialize<NativeBattleRetainedMemory>(json)!);
        NativeBattleEffectMemory effects = restored.NativeEffectMemory!;
        NativeBattlePcTextureWindowRegion cached = effects.PrimitiveProcess!.TextureWindowCache!.Rows[64].Regions[0];

        Assert.Null(effects.Textures!.SceneCopies);
        Assert.Null(cached.RetainedPixels!.SceneCopies);
        Assert.Equal((ushort)0x1234, effects.Textures.ReadVramWord(0, 0));
        Assert.Equal((byte)0x34, cached.RetainedPixels.Data[0]);
        Assert.False(cached.CurrentPixels);
        Assert.Equal(1u, effects.PrimitiveProcess.TextureWindowCache.Rows[64].DirtyMask);
        Assert.Empty(InitializeFrame(restored).SceneReplaySources!);
        Assert.Equal(json, JsonSerializer.Serialize(legacy));
        Assert.NotNull(legacy.EffectMemory!.Textures!.SceneCopies);
        Assert.Equal(JsonSerializer.Serialize(first.NativeActionPackets), JsonSerializer.Serialize(restored.NativeActionPackets));
    }

    [Fact]
    public void Explicitly_present_but_incomplete_pool_remains_corruption()
    {
        BattleSession first = Session(NativeBattleRetainedMemory.FromColdProcess());
        NativeBattleRetainedMemory retained = NativeBattleStorageSnapshots.Capture(first.Capture().Definition.NativeRetainedMemory! with
        {
            EffectMemory = WithCopiedPixels(first.NativeEffectMemory!),
            SceneReplaySources = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty
        });
        BattleSession restored = Session(retained);
        Assert.Throws<InvalidDataException>(() => NativeBattleSceneCopyProvenance.PruneSources(InitializeFrame(restored), restored.NativeEffectMemory!.Textures!));
    }

    private static BattleSession Session(NativeBattleRetainedMemory memory)
    {
        return new(new BattleDefinition
        {
            NativeRetainedMemory = memory,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
    }

    private static NativeBattlePrimitiveFrame InitializeFrame(BattleSession session)
    {
        FieldInfo field = typeof(BattleSession).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!;
        BattleDefinition definition = (BattleDefinition)field.GetValue(session)!;
        field.SetValue(session, definition with { NativeGraphics = new(new("", [], []), [], []) { PrimitiveTables = new([], [], [], [], []) } });
        MethodInfo initialize = typeof(BattleSession).GetMethod("InitializeNativePrimitiveFrame500400", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((NativeBattleGraphicsState)initialize.Invoke(session, [new NativeBattleGraphicsState([], [], 4, 0)])!).PrimitiveFrame!;
    }

    private static NativeBattleSceneCopyRegion Region(long id)
    {
        NativeBattleSceneCapture capture = new(BattleSceneVisibleArea.Native, NativeBattleSceneCaptureKind.Copy) { ReplaySourceId = id };
        return new(capture, 0, 0, 1, 1, 0, 0);
    }

    private static NativeBattleEffectMemory WithCopiedPixels(NativeBattleEffectMemory effects)
    {
        NativeBattleEffectTextureState textures = effects.Textures!.PublishVramSpan(0, new byte[] { 0x34, 0x12 }) with { SceneCopies = [Region(7)] };
        NativeBattlePcTextureWindowCache cache = effects.PrimitiveProcess!.TextureWindowCache!;
        NativeBattlePcTextureWindowPixels pixels = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 256, 256), textures);
        NativeBattlePcTextureWindowRow row = new([new(0, new(0, 0, 256, 256), false) { RetainedPixels = pixels }], 0, 0, 1, 0);
        return effects with { Textures = textures, PrimitiveProcess = effects.PrimitiveProcess with { TextureWindowCache = cache with { Rows = cache.Rows.SetItem(64, row) } } };
    }
}
