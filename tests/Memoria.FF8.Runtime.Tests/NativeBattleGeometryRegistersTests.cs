using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGeometryRegistersTests
{
    [Fact]
    public void Unknown_geometry_has_exact_widths_and_preserves_unknown_lanes_through_JSON()
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown();
        geometry.Validate();
        Assert.All(geometry.Rotation20, value => Assert.Null(value));
        Assert.All(geometry.Translation, value => Assert.Null(value));
        Assert.All(geometry.V1, value => Assert.Null(value));
        Assert.All(geometry.V2, value => Assert.Null(value));
        Assert.All(geometry.Sxy, value => Assert.Null(value));
        Assert.All(geometry.Sz, value => Assert.Null(value));
        Assert.All(geometry.AnchorScratch8, value => Assert.Null(value));
        Assert.Null(geometry.Mac0);
        Assert.Null(geometry.Otz);
        NativeBattleGeometryRegisters restored = JsonSerializer.Deserialize<NativeBattleGeometryRegisters>(JsonSerializer.Serialize(geometry))!;
        Assert.Equal(JsonSerializer.Serialize(geometry), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (geometry with { Rotation20 = new byte?[18].ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (geometry with { Sz = [null, null, null] }).Validate());
    }

    [Fact]
    public void Matrix_load_masks_write_rotation_padding_or_translation_without_reloading_the_other_half()
    {
        NativeBattleGeometryRegisters original = Seed().Geometry;
        NativeBattleActionMatrix matrix = Identity() with { X = 123, Y = -456, Z = int.MinValue };
        NativeBattleGeometryRegisters rotation = NativeBattleGeometry.LoadMatrix(original, matrix, true, false, null);
        Assert.Equal(original.Translation, rotation.Translation);
        Assert.Null(rotation.Rotation20[18]);
        Assert.Null(rotation.Rotation20[19]);
        Assert.Equal(original.Sxy, rotation.Sxy);
        Assert.Equal(original.Sz, rotation.Sz);
        Assert.Equal(original.Mac0, rotation.Mac0);
        NativeBattleGeometryRegisters translation = NativeBattleGeometry.LoadMatrix(rotation, matrix, false, true, 1234);
        Assert.Equal(rotation.Rotation20, translation.Rotation20);
        Assert.Equal(new int?[] { 123, -456, int.MinValue }, translation.Translation);
        NativeBattleGeometryRegisters padded = NativeBattleGeometry.LoadMatrix(translation, matrix, true, false, unchecked((short)0xBEEF));
        Assert.Equal((byte)0xEF, padded.Rotation20[18]);
        Assert.Equal((byte)0xBE, padded.Rotation20[19]);
        Assert.Same(padded, NativeBattleGeometry.LoadMatrix(padded, matrix, false, false, null));
    }

    [Fact]
    public void Vector_load_replaces_unknown_W_and_keeps_V0_in_the_existing_color_owner_only()
    {
        NativeBattleGeometryRegisterStep original = Seed();
        NativeBattleGeometryRegisterStep loaded = NativeBattleGeometry.LoadVector(original, 0, new(1, -2, 3), null);
        Assert.Equal(new byte[] { 1, 0, 0xFE, 0xFF, 3, 0 }, loaded.Colors.NormalInput!.Value.Take(6));
        Assert.Equal(new[] { true, true, true, true, true, true, false, false }, loaded.Colors.NormalInputKnown!.Value);
        Assert.Equal(original.Colors.Material, loaded.Colors.Material);
        Assert.Equal(original.Colors.Flags, loaded.Colors.Flags);
        Assert.Same(original.Geometry, loaded.Geometry);
        loaded = NativeBattleGeometry.LoadVector(loaded, 1, new(4, 5, 6), unchecked((short)0xABCD));
        Assert.Equal((byte)0xCD, loaded.Geometry.V1[6]);
        Assert.Equal((byte)0xAB, loaded.Geometry.V1[7]);
        Assert.Equal(original.Geometry.V2, loaded.Geometry.V2);
        Assert.Equal(original.Colors.Rgb, loaded.Colors.Rgb);
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGeometry.LoadVector(loaded, 3, new(0, 0, 0), null));
    }

    [Fact]
    public void Transform_publishes_full_MAC_signed_IR_and_exact_flags_without_touching_other_registers()
    {
        NativeBattleGeometryRegisterStep original = Seed();
        original = original with { Geometry = NativeBattleGeometry.LoadMatrix(original.Geometry, Identity() with { X = 40000, Y = -40000, Z = 40000 }, true, true, null) };
        original = NativeBattleGeometry.LoadVector(original, 0, new(10, -20, 30), null);
        NativeBattleGeometryRegisterStep transformed = NativeBattleGeometry.Transform(original);
        Assert.Equal(new int?[] { 40010, -40020, 40030 }, transformed.Colors.Mac);
        Assert.Equal(new int?[] { 32767, -32768, 32767 }, transformed.Colors.Ir);
        Assert.Equal(0x81C00000u, transformed.Colors.Flags);
        Assert.Same(original.Geometry, transformed.Geometry);
        Assert.Equal(original.Colors.Ir0, transformed.Colors.Ir0);
        Assert.Equal(original.Colors.Rgb, transformed.Colors.Rgb);
        Assert.Equal(original.Colors.NormalInput, transformed.Colors.NormalInput);
        Assert.Equal(original.Colors.Material, transformed.Colors.Material);
    }

    [Fact]
    public void Rotation_only_transform_ignores_unknown_translation_and_IR_input_uses_signed_low_WORDs()
    {
        NativeBattleGeometryRegisterStep original = new(NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity(), true, false, null), NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleGeometryRegisterStep loaded = NativeBattleGeometry.LoadIrColumn(original, 0xFFFF, 0x8000, 1);
        Assert.Equal(new int?[] { 65535, 32768, 1 }, loaded.Colors.Ir);
        NativeBattleGeometryRegisterStep transformed = NativeBattleGeometry.Transform(loaded, false, true);
        Assert.Equal(new int?[] { -1, -32768, 1 }, transformed.Colors.Mac);
        Assert.Equal(0u, transformed.Colors.Flags);
        Assert.Null(transformed.Colors.NormalInput);
        Assert.Null(transformed.Colors.Ir0);
        Assert.Throws<NotSupportedException>(() => NativeBattleGeometry.Transform(loaded, true, true));
    }

    [Fact]
    public void Single_projection_shifts_partial_FIFOs_and_always_publishes_cue_MAC0_IR0()
    {
        NativeBattleGeometryRegisterStep original = Seed();
        original = original with { Geometry = original.Geometry with { Sxy = [1, null, 3], Sz = [1, null, 3, 4] } };
        original = NativeBattleGeometry.LoadVector(original, 0, new(4, 8, 200), null);
        NativeBattleGeometryRegisterStep projected = NativeBattleGeometry.ProjectSingle(original, Projection(100), 2, 4096);
        Assert.Equal(new uint?[] { null, 3, Packed(16, 32) }, projected.Geometry.Sxy);
        Assert.Equal(new uint?[] { null, 3, 4, 200 }, projected.Geometry.Sz);
        Assert.Equal(69632u, projected.Geometry.Mac0);
        Assert.Equal(17u, projected.Colors.Ir0);
        Assert.Equal(new int?[] { 4, 8, 200 }, projected.Colors.Mac);
        Assert.Equal(0u, projected.Colors.Flags);
        Assert.Equal(original.Geometry.Otz, projected.Geometry.Otz);
        Assert.Equal(original.Geometry.V1, projected.Geometry.V1);
        Assert.Equal(original.Geometry.V2, projected.Geometry.V2);
        Assert.Equal(original.Colors.Rgb, projected.Colors.Rgb);
        Assert.Equal(original.Colors.Material, projected.Colors.Material);
    }

    [Fact]
    public void Triple_projection_keeps_only_final_vertex_IR_saturation_but_accumulates_screen_flags()
    {
        NativeBattleGeometryRegisterStep original = Seed();
        NativeBattleActionMatrix matrix = new([8192, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        original = original with { Geometry = NativeBattleGeometry.LoadMatrix(original.Geometry, matrix, true, true, null) };
        original = NativeBattleGeometry.LoadVector(original, 0, new(20000, 0, 10000), 17);
        original = NativeBattleGeometry.LoadVector(original, 1, new(-20000, 0, 10000), 18);
        original = NativeBattleGeometry.LoadVector(original, 2, new(1, 2, 10000), 19);
        NativeBattleGeometryRegisterStep projected = NativeBattleGeometry.ProjectTriple(original, Projection(100), 0, 0);
        Assert.Equal(new int?[] { 2, 2, 10000 }, projected.Colors.Mac);
        Assert.Equal(new int?[] { 2, 2, 10000 }, projected.Colors.Ir);
        Assert.Equal(0u, projected.Colors.Flags);
        Assert.Equal(new uint?[] { 44, 10000, 10000, 10000 }, projected.Geometry.Sz);
        Assert.Equal(original.Colors.NormalInput, projected.Colors.NormalInput);
        original = NativeBattleGeometry.LoadVector(original, 0, new(30000, 0, 1000), null);
        projected = NativeBattleGeometry.ProjectTriple(original, Projection(100), 0, 0);
        Assert.Equal(0x80004000u, projected.Colors.Flags);
        Assert.Equal((short)8184, unchecked((short)projected.Geometry.Sxy[0]!.Value));
    }

    [Fact]
    public void Triple_then_fourth_projection_establishes_all_depths_and_average_publishes_shifted_MAC0()
    {
        NativeBattleGeometryRegisterStep step = Seed() with { Geometry = NativeBattleGeometryRegisters.Unknown() };
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, Identity(), true, true, null) };
        step = NativeBattleGeometry.LoadVector(step, 0, new(0, 0, 1000), null);
        step = NativeBattleGeometry.LoadVector(step, 1, new(0, 0, 2000), null);
        step = NativeBattleGeometry.LoadVector(step, 2, new(0, 0, 3000), null);
        step = NativeBattleGeometry.ProjectTriple(step, Projection(100), 0, 0);
        Assert.Null(step.Geometry.Sz[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleGeometry.Average45E610(step, 256));
        step = NativeBattleGeometry.LoadVector(step, 0, new(0, 0, 4000), null);
        step = NativeBattleGeometry.ProjectSingle(step, Projection(100), 0, 0);
        Assert.Equal(new uint?[] { 1000, 2000, 3000, 4000 }, step.Geometry.Sz);
        NativeBattleGeometryRegisterStep averaged = NativeBattleGeometry.Average45E610(step, 256);
        Assert.Equal(625u, averaged.Geometry.Mac0);
        Assert.Equal(625u, averaged.Geometry.Otz);
        Assert.Equal(step.Colors.Mac, averaged.Colors.Mac);
        Assert.Equal(step.Colors.Ir, averaged.Colors.Ir);
        Assert.Equal(step.Colors.Ir0, averaged.Colors.Ir0);
        Assert.Equal(0u, averaged.Colors.Flags);
    }

    [Fact]
    public void Projection_negative_cue_clamps_IR0_and_preserves_wrapped_full_MAC0()
    {
        NativeBattleGeometryRegisterStep step = NativeBattleGeometry.LoadVector(Seed(), 0, new(0, 0, 200), null);
        NativeBattleGeometryRegisterStep projected = NativeBattleGeometry.ProjectSingle(step, Projection(100), 0, -4096);
        Assert.Equal(0xFFFFF000u, projected.Geometry.Mac0);
        Assert.Equal(0u, projected.Colors.Ir0);
        Assert.Equal(0x1000u, projected.Colors.Flags);
        projected = NativeBattleGeometry.ProjectSingle(step, Projection(100), 32767, 0);
        Assert.Equal(4095u, projected.Colors.Ir0);
        Assert.Equal(0x1000u, projected.Colors.Flags);
    }

    [Fact]
    public void Clip_uses_signed_screen_WORDs_wraps_determinant_and_clears_FLAG_only()
    {
        NativeBattleGeometryRegisterStep step = Seed();
        step = step with { Geometry = step.Geometry with { Sxy = [Packed(-32768, -32768), Packed(32767, -32768), Packed(-32768, 32767)] } };
        NativeBattleGeometryRegisterStep clipped = NativeBattleGeometry.Clip45EE10(step);
        Assert.Equal(0xFFFE0001u, clipped.Geometry.Mac0);
        Assert.Equal(step.Geometry.Otz, clipped.Geometry.Otz);
        Assert.Equal(step.Geometry.Sxy, clipped.Geometry.Sxy);
        Assert.Equal(step.Geometry.Sz, clipped.Geometry.Sz);
        Assert.Equal(step.Colors with { Flags = 0 }, clipped.Colors);
    }

    [Fact]
    public void Average_reads_low_depth_WORDs_and_logically_shifts_wrapped_signed_product()
    {
        NativeBattleGeometryRegisterStep step = Seed();
        step = step with { Geometry = step.Geometry with { Sz = [0x1234FFFF, 0xABCDFFFF, 0xFFFFFFFF, 0x0000FFFF] } };
        NativeBattleGeometryRegisterStep averaged = NativeBattleGeometry.Average45E610(step, -1);
        Assert.Equal(0xFFFC0u, averaged.Geometry.Mac0);
        Assert.Equal(0xFFFC0u, averaged.Geometry.Otz);
        Assert.Equal(step.Geometry.Sz, averaged.Geometry.Sz);
        Assert.Equal(step.Geometry.Sxy, averaged.Geometry.Sxy);
        Assert.Equal(step.Colors with { Flags = 0 }, averaged.Colors);
    }

    [Fact]
    public void Anchor_Z_store_preserves_unknown_XY_and_W_and_full_vector_load_does_not_fill_them()
    {
        NativeBattleGeometryRegisterStep original = Seed() with { Geometry = NativeBattleGeometryRegisters.Unknown() };
        NativeBattleGeometryRegisters stored = NativeBattleGeometry.LoadAnchorScratchZ(original.Geometry, -1234);
        Assert.All(stored.AnchorScratch8.Take(4), value => Assert.Null(value));
        Assert.Equal((byte)0x2E, stored.AnchorScratch8[4]);
        Assert.Equal((byte)0xFB, stored.AnchorScratch8[5]);
        Assert.Null(stored.AnchorScratch8[6]);
        NativeBattleGeometryRegisterStep loaded = NativeBattleGeometry.LoadAnchorVector(original with { Geometry = stored });
        Assert.Equal(new[] { false, false, false, false, true, true, false, false }, loaded.Colors.NormalInputKnown!.Value);
        loaded = loaded with { Geometry = NativeBattleGeometry.LoadMatrix(loaded.Geometry, Identity(), true, true, null) };
        Assert.Throws<NotSupportedException>(() => NativeBattleGeometry.Transform(loaded));
        stored = NativeBattleGeometry.LoadAnchorScratchXYZ(stored, new(1, 2, 3));
        loaded = NativeBattleGeometry.LoadAnchorVector(loaded with { Geometry = stored with { Rotation20 = loaded.Geometry.Rotation20, Translation = loaded.Geometry.Translation } });
        Assert.Equal(new int?[] { 1, 2, 3 }, NativeBattleGeometry.Transform(loaded).Colors.Mac);
        Assert.False(loaded.Colors.NormalInputKnown!.Value[6]);
    }

    [Fact]
    public void Projection_requires_actual_controls_inputs_and_positive_native_denominator()
    {
        NativeBattleGeometryRegisterStep step = NativeBattleGeometry.LoadVector(Seed(), 0, new(0, 0, 0), null);
        Assert.Throws<InvalidDataException>(() => NativeBattleGeometry.ProjectSingle(step, Projection(0), 0, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleGeometry.ProjectSingle(step, new(null, null, 0, 0), 0, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleGeometry.ProjectSingle(step with { Geometry = NativeBattleGeometryRegisters.Unknown() }, Projection(100), 0, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleGeometry.ProjectTriple(step, Projection(100), 0, 0));
    }

    private static NativeBattleGeometryRegisterStep Seed()
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity(), true, true, 1234) with { Sxy = [11, 22, 33], Sz = [11, 22, 33, 44], Mac0 = 0xAABBCCDD, Otz = 0x12345678 };
        NativeBattleScriptedColorRegisters colors = new([101, 102, 103], [201, 202, 203], [null, 0x12345678, 0xABCDEF01], uint.MaxValue) { Ir0 = 0x87654321, Material = 0x11223344, NormalInput = new byte[8].ToImmutableArray() };
        return new(geometry, colors);
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection(ushort h) => new(null, h, 0, 0);
    private static uint Packed(short x, short y) => unchecked((ushort)x | ((uint)(ushort)y << 16));
}
