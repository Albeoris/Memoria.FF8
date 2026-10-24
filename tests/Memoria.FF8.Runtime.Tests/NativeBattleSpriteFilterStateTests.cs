using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSpriteFilterStateTests
{
    [Fact]
    public void Declared_portable_profile_uses_the_D3D_enable_producer_without_inference()
    {
        NativeBattlePrimitiveCapabilities profile = NativeBattlePrimitiveCapabilities.PrimaryRgba8();
        Assert.Equal<uint?>(2, profile.SpriteFilterStartupControl);
        NativeBattleSpriteFilterState filter = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer().SpriteFilter!;
        Assert.Equal(new(2, uint.MaxValue), filter);
        Assert.Equal<uint?>(0, filter.Enable45B5A0(false).Current);
        Assert.Equal<uint?>(2, filter.Disable45B5C0().Enable45B5A0(true).Current);
        NativeBattlePrimitiveCapabilities unknown = new(true) { HardwareEnabled = true };
        Assert.Null(unknown.SpriteFilterStartupControl);
    }

    [Fact]
    public void Viewport_thresholds_save_disable_and_restore_the_actual_control()
    {
        NativeBattleSpriteFilterState initial = new(7, uint.MaxValue);
        Assert.Equal(initial, initial.ApplyViewport45B4C0(540, 100));
        Assert.Equal(initial, initial.ApplyViewport45B4C0(100, 380));
        NativeBattleSpriteFilterState disabled = initial.ApplyViewport45B4C0(539, 379);
        Assert.Equal(new(0, 7), disabled);
        Assert.Equal(new(7, 7), disabled.ApplyViewport45B4C0(640, 480));
        NativeBattleSpriteFilterState repeated = disabled.ApplyViewport45B4C0(320, 216);
        Assert.Equal(new(0, 0), repeated);
        Assert.Equal(new(0, 0), repeated.ApplyViewport45B4C0(640, 480));
    }

    [Fact]
    public void Unknown_imports_remain_unknown_until_an_actual_producer_replaces_them()
    {
        NativeBattleSpriteFilterState unknown = JsonSerializer.Deserialize<NativeBattleSpriteFilterState>("{\"Current\":null,\"Saved\":null}")!;
        Assert.Equal(unknown, unknown.ApplyViewport45B4C0(640, 480));
        Assert.Equal(new(0, null), unknown.ApplyViewport45B4C0(320, 216));
        Assert.Equal(new(0, null), unknown.Disable45B5C0());
        NativeBattleSoftwarePrimitiveState old = JsonSerializer.Deserialize<NativeBattleSoftwarePrimitiveState>(JsonSerializer.Serialize(NativeBattleSoftwarePrimitiveState.FromInitializedRenderer() with { SpriteFilter = null }))!;
        Assert.Null(old.SpriteFilter);
        Assert.Throws<NotSupportedException>(() => old.SetViewport45B4C0(new(640, 480, 0, 0)));
    }

    [Fact]
    public void Resolved_sprite_metadata_is_stable_when_the_current_filter_changes()
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer() with { Environment = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(new(0, 0, 319, 215, 0, 0, 0, 0, 0, null)) };
        NativeBattleSoftwarePrimitiveExecution execution = new(state, NativeBattleEffectTextureState.FromColdProcess(), new(new uint[256].ToImmutableArray()), true, NativeBattlePcTextureWindowCache.Reset4647A0(null));
        NativeBattleLogicalCommand command = new(new("sprite", 0, 0), 1, 0x64, [0x64000000, 0, 0, 0x200020], 0, [], null);
        NativeBattleLogicalResolvedCommand resolved = execution.RetainTexturePixels(new(command, new(0x100), 1));
        NativeBattleSoftwarePrimitiveState changed = state.SetViewport45B4C0(new(320, 216, 0, 0));
        Assert.Equal<uint?>(0, changed.SpriteFilter!.Current);
        Assert.Equal<uint?>(2, resolved.DrawMode.SpriteFilter!.Current);
        Assert.Equal(resolved.DrawMode.SpriteFilter, JsonSerializer.Deserialize<NativeBattleLogicalResolvedCommand>(JsonSerializer.Serialize(resolved))!.DrawMode.SpriteFilter);
    }
}
