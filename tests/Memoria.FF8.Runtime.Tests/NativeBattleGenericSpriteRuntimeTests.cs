using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGenericSpriteRuntimeTests
{
    [Fact]
    public void Cold_control_is_explicit_and_reset_retains_unknown_or_warm_values()
    {
        NativeBattlePcTextureWindowCache cold = NativeBattlePrimitiveProcess.FromColdProcess().TextureWindowCache!;
        Assert.Equal<uint?>(0, cold.SpriteTileCacheControl1CCFD90);
        Assert.Null(NativeBattlePcTextureWindowCache.Reset4647A0(null).SpriteTileCacheControl1CCFD90);
        NativeBattlePcTextureWindowCache warm = cold with { SpriteTileCacheControl1CCFD90 = 7 };
        NativeBattlePcTextureWindowCache restored = JsonSerializer.Deserialize<NativeBattlePcTextureWindowCache>(JsonSerializer.Serialize(warm))!;
        Assert.Equal<uint?>(7, NativeBattlePcTextureWindowCache.Reset4647A0(restored).SpriteTileCacheControl1CCFD90);
        Assert.Throws<NotSupportedException>(() => restored.PreprocessSprite463120(0x64, 0, 0));
    }

    [Theory]
    [InlineData(0x64)]
    [InlineData(0x66)]
    public void Direct_color_does_not_consume_unknown_tile_control_and_keeps_alternate_clut(byte opcode)
    {
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).PreprocessSprite463120(opcode, 0x140, 0xFFFF);
        Assert.Null(cache.SpriteTileCacheControl1CCFD90);
        Assert.Equal(1u, cache.Rows[64].UsedMask);
        Assert.Equal(1u, cache.Rows[64].AlternateMask);
        Assert.Equal((ushort)0x7FFF, cache.Rows[64].AlternateClut);
        cache = cache.Prepare464BD0().Cache;
        NativeBattlePcTextureWindowResolve resolved = cache.ResolveSprite462DF0(opcode, 0x140, 0xFFFF, 0);
        Assert.Equal(64, resolved.Selection.Row);
        Assert.False(resolved.Selection.Windowed);
        Assert.Equal(1f / 256, resolved.Selection.ScaleU);
    }

    [Fact]
    public void Indexed_control_is_required_before_marking_or_selecting_a_cache_row()
    {
        NativeBattlePcTextureWindowCache unknown = NativeBattlePcTextureWindowCache.Reset4647A0(null);
        Assert.Throws<NotSupportedException>(() => unknown.PreprocessSprite463120(0x64, 0x80, 0));
        Assert.Throws<NotSupportedException>(() => unknown.ResolveSprite462DF0(0x66, 0x80, 0, 0));
        NativeBattlePcTextureWindowCache prepared = (unknown with { SpriteTileCacheControl1CCFD90 = 0 }).PreprocessSprite463120(0x66, 0x80, 0).Prepare464BD0().Cache;
        Assert.Equal(32, prepared.ResolveSprite462DF0(0x66, 0x80, 0, 0).Selection.Row);
        Assert.All(unknown.Rows, row => Assert.Equal(0u, row.UsedMask));
    }

    [Theory]
    [InlineData(0x64, 1)]
    [InlineData(0x66, 2)]
    public void Normal_sprite_prepares_pointer_aliases_and_retains_viewport_and_selected_pixels(byte opcode, byte pass)
    {
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).PreprocessSprite463120(opcode, 0x100, 0xFFFF).Prepare464BD0().Cache;
        NativeBattlePcTextureWindowResolve selected = cache.ResolveSprite462DF0(opcode, 0x100, 0xFFFF, 0);
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(new(576, 256, 895, 487, 576, 256, 0, 0, 0, null));
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer() with { Environment = environment, ViewportInputs = new(640, 480, 3, -7) };
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 256, 256, new byte[256 * 256 * 2]);
        NativeBattleSoftwarePrimitiveExecution execution = new(state, textures, new(new uint[256].ToImmutableArray()), true, selected.Cache);
        NativeBattleLogicalCommand command = new(new("sprite", 0, 0), 1, opcode, [(uint)opcode << 24 | 0x123456, 0x200010, 0xFFFF0000, 0x200020], 0, [], null);
        execution.ExecuteNormal(command, new(0x100), pass);
        Assert.Equal<ushort?>(0x7FFF, execution.State.Texture.EncodedClut);
        Assert.Equal<int?>(1048544, execution.State.Texture.PaletteByteOffset);
        Assert.Equal<int?>(2, execution.State.Texture.Depth);
        NativeBattleLogicalResolvedCommand resolved = execution.RetainTexturePixels(new(command, new(0x100) { TextureSelection = selected.Selection }, pass));
        Assert.Equal(NativeSoftwareNormalViewport.Calculate45B6F0(environment, state.ViewportInputs!), resolved.DrawMode.NormalViewport);
        Assert.Equal(state.ViewportInputs, resolved.DrawMode.ViewportInputs);
        Assert.Equal<int?>(0, resolved.DrawMode.TexturePixelSnapshot);
        Assert.Single(execution.TextureSnapshots);
        Assert.Equal(JsonSerializer.Serialize(resolved), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleLogicalResolvedCommand>(JsonSerializer.Serialize(resolved))));
    }
}
