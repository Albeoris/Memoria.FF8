namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCureActorMatrixTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0, 0, 100, 0, 0, 4096)]
    [InlineData(0, -100, 0, 0, -4096, 0)]
    [InlineData(3, 4, 0, 2458, 3277, 0)]
    public void Long_vector_normalization_preserves_native_rounding_and_zero_without_a_fabricated_direction(int x, int y, int z, short expectedX, short expectedY, short expectedZ)
    {
        Assert.Equal(new(expectedX, expectedY, expectedZ), NativeBattlePoseMatrices.NormalizeLong56BD20(x, y, z));
    }

    [Fact]
    public void Actor_camera_stays_on_the_original_matrix_while_the_effect_offset_changes_only_the_overlay_projection()
    {
        NativeBattleActionMatrix world = Identity with { X = 10, Y = 20, Z = 30 };
        NativeBattleCureActorMatrices result = NativeBattlePoseMatrices.CureActorMatrices8D7110(world, Identity with { Z = 1024 }, new(0, 32, 0), 1, new(10, 52, -70), 8192);
        Assert.Equal((10, 52, 30), (result.MovedModelWorld.X, result.MovedModelWorld.Y, result.MovedModelWorld.Z));
        Assert.Equal((10, 20, 1054), (result.MainCamera.X, result.MainCamera.Y, result.MainCamera.Z));
        Assert.Equal((0, -32, 8192), (result.OverlayCamera.X, result.OverlayCamera.Y, result.OverlayCamera.Z));
        Assert.Equal(Identity.Rotation.ToArray(), result.OverlayCamera.Rotation.ToArray());
        Assert.Equal((10, 20, 30), (world.X, world.Y, world.Z));
    }

    [Fact]
    public void Local_translation_mode_rotates_through_the_original_basis_before_the_signed_WORD_addition()
    {
        NativeBattleActionMatrix world = Identity with { Rotation = [0, 0, 4096, 0, 4096, 0, -4096, 0, 0], X = 10, Y = 20, Z = 30 };
        NativeBattleCureActorMatrices result = NativeBattlePoseMatrices.CureActorMatrices8D7110(world, Identity, new(100, 0, 0), 0, new(10, 20, -170), 8192);
        Assert.Equal((10, 20, -70), (result.MovedModelWorld.X, result.MovedModelWorld.Y, result.MovedModelWorld.Z));
        Assert.Equal((0, 0, 8292), (result.OverlayCamera.X, result.OverlayCamera.Y, result.OverlayCamera.Z));
        Assert.Equal(world.Rotation.ToArray(), result.OverlayCamera.Rotation.ToArray());
        NativeBattleCureActorMatrices unchanged = NativeBattlePoseMatrices.CureActorMatrices8D7110(world, Identity, new(100, 0, 0), 2, new(10, 20, -70), 8192);
        Assert.Equal(world.Rotation.ToArray(), unchanged.MovedModelWorld.Rotation.ToArray());
        Assert.Equal((world.X, world.Y, world.Z, world.Scale), (unchanged.MovedModelWorld.X, unchanged.MovedModelWorld.Y, unchanged.MovedModelWorld.Z, unchanged.MovedModelWorld.Scale));
    }

    [Fact]
    public void Overlay_origin_and_GTE_translation_use_original_WORD_stores_even_when_the_model_DWORD_position_overflows_them()
    {
        NativeBattleActionMatrix world = Identity with { X = 40000, Y = -40000, Z = 65536 };
        NativeBattleCureActorMatrices result = NativeBattlePoseMatrices.CureActorMatrices8D7110(world, Identity, default, 1, new(-25536, 25536, -100), 8192);
        Assert.Equal((0, 0, 8192), (result.OverlayCamera.X, result.OverlayCamera.Y, result.OverlayCamera.Z));
        Assert.Equal((40000, -40000, 65536), (result.MainCamera.X, result.MainCamera.Y, result.MainCamera.Z));
        NativeBattleCureActorMatrices zero = NativeBattlePoseMatrices.CureActorMatrices8D7110(Identity, Identity, default, 1, default, 8192);
        Assert.Equal(new short[] { 0, 0, 0, 0, 4096, 0, 0, 0, 0 }, zero.OverlayCamera.Rotation);
    }

    [Fact]
    public void Overlay_rotation_uses_the_clamped_GTE_column_result_instead_of_a_wrapped_matrix_product()
    {
        NativeBattleActionMatrix world = Identity with { Rotation = [32767, 32767, 32767, 32767, 32767, 32767, 32767, 32767, 32767] };
        NativeBattleCureActorMatrices result = NativeBattlePoseMatrices.CureActorMatrices8D7110(world, Identity, default, 1, new(-100, 0, -100), 8192);
        Assert.Equal(new short[] { 0, 0, 0, 32767, 32767, 32767, 32767, 32767, 32767 }, result.OverlayCamera.Rotation);
    }
}
