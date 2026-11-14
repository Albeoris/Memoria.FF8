using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelMaterialExecutionTests
{
    private static readonly NativeBattleModelTextureOwner First = new("model-a");
    private static readonly NativeBattleModelTextureOwner Second = new("model-b");
    private static readonly ImmutableArray<uint> TriangleWords = [0x07000000, 0x240000FF, 0x00040000, 0x01800000, 0x00040004, 0x00800000, 0x00060000, 0];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Logical_passes_retain_selected_actor_and_global_pixels(bool restore)
    {
        NativeBattleSceneCapture copy = new(new(0, 0, 320, 216), NativeBattleSceneCaptureKind.Copy) { ReplaySourceId = 1 };
        NativeBattleEffectTextureState child = Pixels(2, 0x1234) with { SceneCopies = [new(copy, 0, 0, 1, 1, 0, 0)] };
        NativeBattleEffectTextureState textures = Pixels(1, 0x001F)
            .WithPresentationTexture(First.RequireActorId(), new(child, EmptyCache()))
            .WithPresentationTexture(Second.RequireActorId(), new(Pixels(3, 0x4567), EmptyCache()));
        if (restore)
        {
            textures = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(NativeBattleTestSnapshots.Json(textures));
        }
        NativeBattleSoftwarePrimitiveExecution execution = Execution(textures);
        NativeBattleLogicalOrdering ordering = Add(Add(Add(NativeBattleLogicalOrdering.Clear(), 0, First), 1, Second), 2, null);

        var result = NativeBattleLogicalPasses.Execute(ordering, Tables(), new(0) { TextureWindowBits = 1 }, execution);

        Assert.Equal(9, result.Commands.Length);
        Assert.Equal(3, execution.TextureSnapshots.Length);
        foreach (NativeBattleLogicalResolvedCommand command in result.Commands.Where(command => command.Pass != 0))
        {
            byte expected = command.Command.ModelTextureOwner == First ? (byte)2 : command.Command.ModelTextureOwner == Second ? (byte)3 : (byte)1;
            int index = Assert.IsType<int>(command.DrawMode.TexturePixelSnapshot);
            Assert.Equal(expected, execution.TextureSnapshots[index].Data[0]);
            if (expected == 2)
            {
                Assert.Equal((ushort)0x1234, command.DrawMode.SceneCopyPalette!.Value[2]);
            }
            else
            {
                Assert.Null(command.DrawMode.SceneCopyPalette);
            }
        }
        Assert.Equal(2, execution.Textures.RequirePresentationTexture(First.RequireActorId()).Cache.Rows[32].Count);
        Assert.Equal(2, execution.Textures.RequirePresentationTexture(Second.RequireActorId()).Cache.Rows[32].Count);
        Assert.Equal(2, result.Cache!.Rows[32].Count);
        Assert.Equal<float?>(1f / 248, result.Cache.ScaleU);
    }

    [Fact]
    public void Identical_pixels_only_share_snapshots_with_matching_scene_copy_provenance()
    {
        NativeBattleModelTextureOwner third = new("model-c");
        NativeBattleSceneCapture copy = new(new(0, 0, 320, 216), NativeBattleSceneCaptureKind.Copy) { ReplaySourceId = 1 };
        NativeBattleSceneCopyRegion region = new(copy, 0, 0, 1, 1, 0, 0);
        NativeBattleEffectTextureState a = Pixels(2, 0x1234) with { SceneCopies = [region] };
        NativeBattleEffectTextureState b = Pixels(2, 0x1234) with { SceneCopies = [region with { Capture = copy with { ReplaySourceId = 2 } }] };
        NativeBattleEffectTextureState c = Pixels(2, 0x1234) with { SceneCopies = [region] };
        NativeBattleEffectTextureState textures = Pixels(1, 0x001F)
            .WithPresentationTexture(First.RequireActorId(), new(a, EmptyCache()))
            .WithPresentationTexture(Second.RequireActorId(), new(b, EmptyCache()))
            .WithPresentationTexture(third.RequireActorId(), new(c, EmptyCache()));
        NativeBattleSoftwarePrimitiveExecution execution = Execution(textures);
        NativeBattleLogicalOrdering ordering = Add(Add(Add(NativeBattleLogicalOrdering.Clear(), 0, First), 1, Second), 2, third);

        var result = NativeBattleLogicalPasses.Execute(ordering, Tables(), new(0) { TextureWindowBits = 1 }, execution);

        Assert.Equal(2, execution.TextureSnapshots.Length);
        Dictionary<NativeBattleModelTextureOwner, int> indices = result.Commands.Where(command => command.Pass == 1).ToDictionary(command => command.Command.ModelTextureOwner!, command => command.DrawMode.TexturePixelSnapshot!.Value);
        Assert.Equal(indices[First], indices[third]);
        Assert.NotEqual(indices[First], indices[Second]);
        Assert.Equal<long?>(1, Assert.Single(execution.TextureSnapshots[indices[First]].SceneCopies!.Value).Capture.ReplaySourceId);
        Assert.Equal<long?>(2, Assert.Single(execution.TextureSnapshots[indices[Second]].SceneCopies!.Value).Capture.ReplaySourceId);
    }

    [Fact]
    public void Selected_caches_share_live_controls_and_prepare_and_flush_their_own_rows()
    {
        NativeBattleEffectTextureState textures = Pixels(1, 1)
            .WithPresentationTexture(First.RequireActorId(), new(Pixels(2, 2), EmptyCache().Preprocess462F90(0x80, 0)))
            .WithPresentationTexture(Second.RequireActorId(), new(Pixels(3, 3), EmptyCache().Preprocess462F90(0x81, 0)));
        NativeBattleSoftwarePrimitiveExecution execution = Execution(textures);
        NativeBattleLogicalCommand a = Command(First);
        NativeBattleLogicalCommand b = Command(Second);
        NativeBattleLogicalCommand global = Command(null);
        execution.SetTextureCache(global, EmptyCache() with { ScaleU = 0.125f, ScaleV = 0.25f, SpriteTileCacheControl1CCFD90 = 1 });
        Assert.Equal<float?>(0.125f, execution.GetTextureCache(a)!.ScaleU);
        Assert.Equal<uint?>(1u, execution.GetTextureCache(b)!.SpriteTileCacheControl1CCFD90);
        execution.BeginPass(1);
        NativeBattlePcTextureWindowCache aResolved = execution.GetTextureCache(a)!.Resolve465CE0(0x80, 0, 1).Cache;
        execution.SetTextureCache(a, aResolved with { SpriteTileCacheControl1CCFD90 = 2 });
        Assert.Equal<float?>(1f / 248, execution.GetTextureCache(b)!.ScaleU);
        Assert.Equal<float?>(1f / 256, execution.GetTextureCache(global)!.ScaleV);
        Assert.Equal<uint?>(2u, execution.GetTextureCache(b)!.SpriteTileCacheControl1CCFD90);
        NativeBattlePcTextureWindowCache bResolved = execution.GetTextureCache(b)!.Resolve465CE0(0x81, 0, 2).Cache;
        execution.SetTextureCache(b, bResolved);
        Assert.Equal<float?>(1f / 240, execution.GetTextureCache(a)!.ScaleU);
        Assert.Equal<float?>(1f / 240, execution.Cache!.ScaleU);
        Assert.Equal(0, execution.Cache.Rows[32].Count);
        Assert.Equal(0, execution.GetTextureCache(a)!.Rows[33].Count);
        Assert.Equal(0, execution.GetTextureCache(b)!.Rows[32].Count);

        execution.EndPass(1);

        foreach ((NativeBattleLogicalCommand command, int row) in new[] { (a, 32), (b, 33) })
        {
            Assert.Equal(2, execution.GetTextureCache(command)!.Rows[row].Count);
            Assert.Equal(0u, execution.GetTextureCache(command)!.Rows[row].UsedMask);
        }
    }

    [Theory]
    [InlineData(false, 0x001F)]
    [InlineData(true, 0x1234)]
    public void Software_triangle_writes_global_framebuffer_and_keeps_child_pixels(bool model, ushort expected)
    {
        NativeBattleEffectTextureState child = Pixels(2, 0x1234);
        NativeBattleSoftwarePrimitiveExecution execution = Execution(Pixels(1, 0x001F).WithPresentationTexture(First.RequireActorId(), new(child, EmptyCache())));

        execution.ExecuteSoftware(Command(model ? First : null));
        execution.EndBatch();

        Assert.Equal(expected, execution.Textures.ReadVramWord(0, 4));
        Assert.Same(child.VramPublicationIdentity, execution.Textures.RequirePresentationTexture(First.RequireActorId()).Textures.VramPublicationIdentity);
        Assert.Equal((ushort)0, child.ReadVramWord(0, 4));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalidated")]
    [InlineData("no global cache")]
    [InlineData("screen sample")]
    public void Invalid_or_ambiguous_material_fails_before_native_dispatch(string problem)
    {
        NativeBattleEffectTextureState textures = Pixels(1, 0x001F).WithPresentationTexture(First.RequireActorId(), new(Pixels(2, 0x1234), EmptyCache()));
        NativeBattleSoftwarePrimitiveExecution execution = Execution(textures);
        NativeBattleModelTextureOwner owner = problem switch
        {
            "missing" => Second,
            "invalidated" => NativeBattleModelTextureOwner.InvalidatedOwner,
            _ => First
        };
        if (problem == "no global cache")
        {
            execution.Cache = null;
        }
        NativeBattleSceneCapture? capture = problem == "screen sample" ? new(new(0, 0, 320, 216), NativeBattleSceneCaptureKind.ScreenSample) : null;
        NativeBattleLogicalOrdering ordering = Add(Add(NativeBattleLogicalOrdering.Clear(), 0, owner, capture), 1, null);
        Action run = () => NativeBattleLogicalPasses.Execute(ordering, Tables(), new(0), execution);

        if (problem is "screen sample" or "no global cache")
        {
            Assert.Throws<NotSupportedException>(run);
        }
        else
        {
            Assert.Throws<InvalidDataException>(run);
        }
        Assert.Same(textures, execution.Textures);
        Assert.Null(execution.State.Raster.PublishedClippedDescriptor);
        Assert.Empty(execution.TextureSnapshots);
    }

    private static NativeBattleSoftwarePrimitiveExecution Execution(NativeBattleEffectTextureState textures)
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer();
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(state.Environment with { TopLeftX = 0, TopLeftY = 0, RightX = 319, BottomY = 215, OffsetX = 0, OffsetY = 0 });
        return new(state with { Environment = environment }, textures, new(Tables().PreprocessingHandlers), true, EmptyCache());
    }

    private static NativeBattleLogicalOrdering Add(NativeBattleLogicalOrdering ordering, int slot, NativeBattleModelTextureOwner? owner, NativeBattleSceneCapture? capture = null)
    {
        return ordering.Prepend(17, new("off_B6D070", 0, 32 * slot), TriangleWords, 1, [], sceneCapture: capture, modelTextureOwner: owner);
    }

    private static NativeBattleLogicalCommand Command(NativeBattleModelTextureOwner? owner) => new(new("off_B6D070", 0, 0), 1, 0x24, TriangleWords.RemoveAt(0), 1, [], null) { ModelTextureOwner = owner };

    private static NativeBattleEffectTextureState Pixels(byte texel, ushort color)
    {
        byte[] paletteWord = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(paletteWord, color);
        return NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [texel, 0]).Upload45BD30(texel, 6, 1, 1, paletteWord);
    }

    private static NativeBattlePcTextureWindowCache EmptyCache() => NativeBattlePcTextureWindowCache.Reset4647A0(null);

    private static NativeBattlePrimitiveTables Tables()
    {
        ImmutableArray<byte> lengths = new byte[256].ToImmutableArray().SetItem(0x24, 7);
        ImmutableArray<uint> handlers = new uint[256].ToImmutableArray().SetItem(0x24, 0x4669F0);
        return new(lengths, new byte[256].ToImmutableArray(), handlers, handlers, handlers);
    }
}
