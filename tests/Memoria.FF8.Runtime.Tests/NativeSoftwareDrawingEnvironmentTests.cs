using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareDrawingEnvironmentTests
{
    [Theory]
    [InlineData(639, 239, 640, 480, 1)]
    [InlineData(640, 239, 319, 216, 2)]
    [InlineData(640, 239, 320, 216, 3)]
    [InlineData(704, 256, 128, 128, 2)]
    [InlineData(576, 256, 320, 232, 3)]
    [InlineData(-1, -1, 320, 232, 1)]
    public void Mode_selection_uses_actual_signed_top_left_and_signed_dimensions(int x, int y, int width, int height, int expected)
    {
        NativeSoftwareDrawingEnvironmentState state = Inputs((short)x, (short)y, unchecked((uint)(x + width - 1)), unchecked((uint)(y + height - 1)), 0, 0);
        NativeSoftwareEnvironmentDerived derived = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(state).Derived!;
        Assert.Equal((uint)expected, derived.Mode);
        Assert.Equal((ushort)width, derived.Width);
        Assert.Equal((ushort)height, derived.Height);
        Assert.Equal(new NativeSoftwareDrawingRectangle((short)x, (short)y, (short)width, (short)height), derived.Rectangle);
    }

    [Fact]
    public void E3_E4_decode_their_unsigned_fields_and_E4_requests_invalidation_without_pixel_writes()
    {
        NativeSoftwareDrawingEnvironmentState state = Inputs(0, 0, 1023, 511, 7, -9);
        state = NativeSoftwareDrawingEnvironment.WriteE345BA30(state, 0xE3000000 | 704u | (256u << 10));
        Assert.Equal((short)704, state.TopLeftX);
        Assert.Equal((short)256, state.TopLeftY);
        Assert.Equal((ushort)320, state.Derived!.Width);
        NativeSoftwareEnvironmentUpdate updated = NativeSoftwareDrawingEnvironment.WriteE445BA60(state, 0xE4000000 | 831u | (383u << 10));
        Assert.Equal(831u, updated.State.RightX);
        Assert.Equal(383u, updated.State.BottomY);
        Assert.Equal(new NativeSoftwareTextureCacheInvalidation(704, 256, 831, 383), updated.Invalidation);
        Assert.Equal(new NativeSoftwareClipBounds(704 << 16, 256, 832 << 16, 384), updated.State.Derived!.ClipBounds);
        Assert.Equal((byte)0, NativeSoftwarePolygonClip.Outcode466320(832 << 16, 384, updated.State.Derived.ClipBounds));
        Assert.Equal((byte)12, NativeSoftwarePolygonClip.Outcode466320((832 << 16) + 1, 385, updated.State.Derived.ClipBounds));
        NativeSoftwareEnvironmentUpdate normal = NativeSoftwareDrawingEnvironment.WriteE445BA60(Inputs(0, 0, 10, 10, 0, 0), 0xE4000000 | 9u | (9u << 10));
        Assert.Null(normal.Invalidation);
        Assert.Equal(1u, normal.State.Derived!.Mode);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1023, -1024)]
    [InlineData(-1, 1)]
    [InlineData(-1024, 1023)]
    public void E5_sign_extends_both_eleven_bit_offsets_and_forms_software_fixed_coordinates(int x, int y)
    {
        uint command = 0xE5000000u | ((uint)x & 0x7FF) | (((uint)y & 0x7FF) << 11);
        NativeSoftwareDrawingEnvironmentState state = NativeSoftwareDrawingEnvironment.WriteE545BAD0(Inputs(704, 256, 831, 383, 0, 0), command);
        Assert.Equal((short)x, state.OffsetX);
        Assert.Equal((short)y, state.OffsetY);
        Assert.Equal(x, state.Derived!.DrawPositionX);
        Assert.Equal(y, state.Derived.DrawPositionY);
        Assert.Equal(unchecked(x << 16), state.Derived.DrawOffsetX);
        Assert.Equal(unchecked(y << 16), state.Derived.DrawOffsetY);
    }

    [Fact]
    public void Origin_set_and_batch_reset_preserve_stale_derived_fields_until_the_next_environment_write()
    {
        NativeSoftwareDrawingEnvironmentState initial = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(Inputs(704, 256, 831, 383, 7, -9) with { Debug = 0x80 });
        NativeSoftwareDrawingEnvironmentState origin = NativeSoftwareDrawingEnvironment.SetOrigin45B5E0(initial, 11, -13);
        Assert.Equal(0x81u, origin.Debug);
        Assert.Same(initial.Derived, origin.Derived);
        NativeSoftwareDrawingEnvironmentState recalculated = NativeSoftwareDrawingEnvironment.WriteE545BAD0(origin, 0xE5000000 | 7u | (unchecked((uint)-9) & 0x7FF) << 11);
        Assert.Equal(new NativeSoftwareDrawingRectangle(715, 243, 128, 128), recalculated.Derived!.Rectangle);
        Assert.Equal(18, recalculated.Derived.DrawPositionX);
        Assert.Equal(-22, recalculated.Derived.DrawPositionY);
        Assert.Equal((short)704, recalculated.TopLeftX);
        Assert.Equal((short)256, recalculated.TopLeftY);
        NativeSoftwareDrawingEnvironmentState ended = NativeSoftwareDrawingEnvironment.EndBatch45D080(recalculated);
        Assert.Equal(0, ended.OriginX);
        Assert.Equal(0, ended.OriginY);
        Assert.Equal(0u, ended.Debug);
        Assert.Same(recalculated.Derived, ended.Derived);
        NativeSoftwareDrawingEnvironmentState next = NativeSoftwareDrawingEnvironment.WriteE345BA30(ended, 0xE3000000 | 704u | (256u << 10));
        Assert.Equal(new NativeSoftwareDrawingRectangle(704, 256, 128, 128), next.Derived!.Rectangle);
        Assert.Equal(7 << 16, next.Derived.DrawOffsetX);
        Assert.Equal(-9 << 16, next.Derived.DrawOffsetY);
    }

    [Fact]
    public void Origin_DWORD_additions_and_RECT_WORD_additions_have_separate_wrap_boundaries()
    {
        NativeSoftwareDrawingEnvironmentState state = Inputs(700, 250, 827, 377, 1, -1) with { OriginX = int.MaxValue, OriginY = int.MinValue, Debug = 1 };
        NativeSoftwareEnvironmentDerived derived = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(state).Derived!;
        Assert.Equal(int.MinValue, derived.DrawPositionX);
        Assert.Equal(int.MaxValue, derived.DrawPositionY);
        Assert.Equal((short)699, derived.Rectangle.X);
        Assert.Equal((short)250, derived.Rectangle.Y);
        Assert.Equal(0, derived.DrawOffsetX);
        Assert.Equal(-65536, derived.DrawOffsetY);
        Assert.Equal((short)700, state.TopLeftX);
    }

    [Fact]
    public void Width_and_height_are_low_WORDs_and_later_signed_dimensions_are_not_clamped()
    {
        NativeSoftwareDrawingEnvironmentState state = Inputs(-1, 32767, 65535, 0, 0, 0);
        NativeSoftwareEnvironmentDerived derived = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(state).Derived!;
        Assert.Equal((ushort)1, derived.Width);
        Assert.Equal((ushort)32770, derived.Height);
        Assert.Equal((short)-32766, derived.Rectangle.Height);
        Assert.Equal(2u, derived.Mode);
        NativeSoftwareEnvironmentUpdate zero = NativeSoftwareDrawingEnvironment.WriteE445BA60(Inputs(704, 256, 0, 0, 0, 0), 703u | (255u << 10));
        Assert.Equal((ushort)0, zero.State.Derived!.Width);
        Assert.Equal((ushort)0, zero.State.Derived.Height);
        Assert.Equal(new NativeSoftwareTextureCacheInvalidation(704, 256, 703, 255), zero.Invalidation);
    }

    [Fact]
    public void Cold_process_establishes_only_origin_and_debug_and_missing_warm_inputs_fail_explicitly()
    {
        NativeSoftwareDrawingEnvironmentState cold = NativeSoftwareDrawingEnvironmentState.FromColdProcess();
        Assert.Equal(0, cold.OriginX);
        Assert.Equal(0, cold.OriginY);
        Assert.Equal(0u, cold.Debug);
        Assert.Null(cold.TopLeftX);
        Assert.Null(cold.RightX);
        Assert.Null(cold.OffsetX);
        Assert.Null(cold.Derived);
        Assert.Throws<NotSupportedException>(() => NativeSoftwareDrawingEnvironment.WriteE345BA30(cold, 0));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareDrawingEnvironment.Recalculate45B6F0(Inputs(0, 0, 0, 0, 0, 0) with { OriginX = null }));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareDrawingEnvironment.SetOrigin45B5E0(cold with { Debug = null }, 1, 2));
    }

    [Fact]
    public void JSON_restore_preserves_transient_stale_outputs_and_the_next_ordered_E4_receipt()
    {
        NativeSoftwareDrawingEnvironmentState state = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(Inputs(576, 256, 895, 487, -10, 20));
        state = NativeSoftwareDrawingEnvironment.SetOrigin45B5E0(state, 100, -50);
        string json = JsonSerializer.Serialize(state);
        NativeSoftwareDrawingEnvironmentState restored = JsonSerializer.Deserialize<NativeSoftwareDrawingEnvironmentState>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        uint command = 0xE4000000 | 895u | (487u << 10);
        NativeSoftwareEnvironmentUpdate next = NativeSoftwareDrawingEnvironment.WriteE445BA60(state, command);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(NativeSoftwareDrawingEnvironment.WriteE445BA60(restored, command)));
        Assert.Equal(new NativeSoftwareTextureCacheInvalidation(676, 206, 995, 437), next.Invalidation);
        Assert.Equal(new NativeSoftwareClipBounds(676 << 16, 206, 996 << 16, 438), next.State.Derived!.ClipBounds);
    }

    private static NativeSoftwareDrawingEnvironmentState Inputs(short x, short y, uint right, uint bottom, short offsetX, short offsetY) => new(x, y, right, bottom, offsetX, offsetY, 0, 0, 0, null);
}
