using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleModelMaterialRenderingTests
{
    private const ushort Clut = 0x4000;

    [Fact]
    public void PetrifiedActorChangesColorWithoutChangingItsSharedTexture()
    {
        NativeBattlePrimitiveDrawMode mode = FullWindowMode();
        NativeBattleLogicalResolvedCommand first = Quad(10, null, mode);
        NativeBattleLogicalResolvedCommand second = Quad(40, null, mode);
        first = first with { Command = first.Command.ToCommand() with { PresentationActorId = "first" } };
        second = second with { Command = second.Command.ToCommand() with { PresentationActorId = "second" } };
        NativeBattlePreparedPrimitiveFrame normal = NativeBattlePreparedPrimitiveFrame.From(Frame([first, second, Quad(70, null, mode)]));
        NativeBattlePreparedPrimitiveFrame petrified = normal with { PetrifiedEnemyActors = ImmutableHashSet.Create("first") };
        NativeBattleEffectTextureState textures = Texture(0x001F);
        BattleRenderer renderer = new(320, 216);

        AssertPixel(renderer.RenderNativePrimitiveFrame(normal, textures), 20, 255, 0, 0);
        byte[] stone = renderer.RenderNativePrimitiveFrame(petrified, textures);
        AssertPixel(stone, 20, 90, 90, 90);
        AssertPixel(stone, 50, 255, 0, 0);
        AssertPixel(stone, 80, 255, 0, 0);
        AssertPixel(renderer.RenderNativePrimitiveFrame(normal, textures), 20, 255, 0, 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ActorMaterialsKeepTheirOwnPalettesAcrossFrames(bool useFrameSnapshot)
    {
        NativeBattleEffectTextureState global = Texture(0x7C00);
        uint window = NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new(0, 0, 8, 8)) & 0xFFFFFF;
        NativeBattlePcTextureWindowCache prepared = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(0, Clut).Prepare464BD0().Cache;
        var resolved = prepared.Resolve465CE0(0, Clut, window);
        NativeBattlePcTextureWindowSelection selection = resolved.Selection with { UsesRetainedPixelWords = true };
        NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(selection.Row, new(selection.X, selection.Y, selection.Width, selection.Height), global);
        NativeBattlePcTextureWindowRow row = resolved.Cache.Rows[selection.Row];
        NativeBattlePcTextureWindowRegion region = row.Regions[selection.Slot] with { CurrentPixels = false, RetainedPixels = retained };
        NativeBattlePcTextureWindowCache cache = resolved.Cache with { Rows = resolved.Cache.Rows.SetItem(selection.Row, row with { Regions = row.Regions.SetItem(selection.Slot, region) }) };
        NativeBattlePrimitiveDrawMode mode = new(0) { TextureWindowBits = window, TextureSelection = selection, TexturePixelSnapshot = useFrameSnapshot ? 0 : null };
        NativeBattlePrimitiveFrame frame = Frame([Quad(10, new("first"), mode), Quad(40, new("second"), mode), Quad(70, null, mode), Quad(100, new("first"), mode), Quad(130, new("second"), mode)]);
        if (useFrameSnapshot)
        {
            frame = frame with { LastTextureSnapshots = [retained] };
        }

        NativeBattleEffectTextureState textures = global.WithPresentationTexture("first", new(Texture(0x001F), cache)).WithPresentationTexture("second", new(Texture(0x03E0), cache));
        BattleRenderer renderer = new(320, 216) { PilotDiagnosticsEnabled = true };
        byte[] first = renderer.RenderNativePrimitiveFrame(frame, textures, textureWindowCache: cache).ToArray();
        AssertPixel(first, 20, 255, 0, 0);
        AssertPixel(first, 50, 0, 255, 0);
        AssertPixel(first, 80, 0, 0, 255);
        AssertPixel(first, 110, 255, 0, 0);
        AssertPixel(first, 140, 0, 255, 0);
        Assert.Equal(2, renderer.TextureDecoding!.Value.RawPaletteMismatches);
        Assert.Equal(2, renderer.TextureDecoding!.Value.FullFinalCurrentHits);

        byte[] repeated = renderer.RenderNativePrimitiveFrame(frame, textures, textureWindowCache: cache);
        Assert.Equal(first, repeated);
        Assert.Equal(3, renderer.TextureDecoding!.Value.FullFinalPreviousHits);

        NativeBattleEffectTextureState updated = textures.WithPresentationTexture("first", new(Texture(0x03FF), cache));
        byte[] changed = renderer.RenderNativePrimitiveFrame(frame, updated, textureWindowCache: cache);
        AssertPixel(changed, 20, 255, 255, 0);
        AssertPixel(changed, 50, 0, 255, 0);
        AssertPixel(changed, 80, 0, 0, 255);
        AssertPixel(changed, 110, 255, 255, 0);
        AssertPixel(changed, 140, 0, 255, 0);
        Assert.Equal(2, renderer.TextureDecoding!.Value.FullFinalPreviousHits);

        NativeBattleEffectTextureState missing = updated with { PresentationTextures = updated.PresentationTextures!.Remove("first") };
        Assert.Throws<InvalidDataException>(() => renderer.RenderNativePrimitiveFrame(frame, missing, textureWindowCache: cache));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrInvalidatedActorMaterialCannotUseGlobalVram(bool invalidated)
    {
        NativeBattleModelTextureOwner owner = invalidated ? NativeBattleModelTextureOwner.InvalidatedOwner : new("unbound");
        NativeBattlePrimitiveFrame frame = Frame([Quad(10, owner, FullWindowMode())]);
        BattleRenderer renderer = new(320, 216);
        Assert.Throws<InvalidDataException>(() => renderer.RenderNativePrimitiveFrame(frame, Texture(0x7C00)));
    }

    [Fact]
    public void ActorMaterialRejectsGlobalScreenSampling()
    {
        NativeBattleLogicalResolvedCommand command = Quad(10, new("first"), FullWindowMode());
        command = command with { Command = command.Command.ToCommand() with { SceneCapture = new(BattleSceneVisibleArea.Native, NativeBattleSceneCaptureKind.ScreenSample) } };
        Assert.Throws<NotSupportedException>(() => new BattleRenderer(320, 216).RenderNativePrimitiveFrame(Frame([command]), Texture(0x7C00)));
    }

    [Fact]
    public void UnownedTextureRetainsTheOriginalGlobalSamplingPath()
    {
        NativeBattlePrimitiveFrame frame = Frame([Quad(10, null, FullWindowMode())]);
        byte[] pixels = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, Texture(0x7C00));
        AssertPixel(pixels, 20, 0, 0, 255);
    }

    private static NativeBattlePrimitiveFrame Frame(NativeBattleResolvedCommandList commands)
    {
        return new(NativeBattleLogicalOrdering.Clear(), commands, [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
    }

    private static NativeBattlePrimitiveDrawMode FullWindowMode()
    {
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(0, Clut).Prepare464BD0().Cache;
        return new(0) { TextureWindowBits = 0, TextureSelection = cache.Resolve465CE0(0, Clut, 0).Selection };
    }

    private static NativeBattleLogicalResolvedCommand Quad(int x, NativeBattleModelTextureOwner? owner, NativeBattlePrimitiveDrawMode mode)
    {
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, x * 4), 1, 0x2C, [0x2C808080, Point(x, 10), (uint)Clut << 16, Point(x + 20, 10), 7, Point(x, 30), 7 << 8, Point(x + 20, 30), 7 << 8 | 7], 1, [], null)
        {
            OrderingBucket = 50,
            ModelTextureOwner = owner
        };
        return new(command, mode, 1);
    }

    private static uint Point(int x, int y) => (ushort)x | unchecked((uint)y << 16);

    private static NativeBattleEffectTextureState Texture(ushort color)
    {
        byte[] palette = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(2), color);
        return NativeBattleEffectTextureState.FromColdProcess()
            .Upload45BD30(0, 0, 2, 8, Enumerable.Repeat((byte)0x11, 32).ToArray())
            .Upload45BD30(0, 256, 16, 1, palette);
    }

    private static void AssertPixel(byte[] pixels, int x, byte red, byte green, byte blue)
    {
        Assert.Equal(new byte[] { red, green, blue, 255 }, pixels.AsSpan(4 * (20 * 320 + x), 4).ToArray());
    }
}
