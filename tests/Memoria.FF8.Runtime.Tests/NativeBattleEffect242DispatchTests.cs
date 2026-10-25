namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect242DispatchTests
{
    [Fact]
    public void Shared_vector_alias_does_not_admit_an_unselected_slot_or_changed_handler()
    {
        Assert.Equal(0xB2C480u, NativeBattleCinematicDispatch.RequireSlot(242, "vm_table", 0x12, 0x9FE1D0));
        Assert.Equal(0xB2C480u, NativeBattleCinematicDispatch.Canonicalize(242, 0x9FE1D0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(242, "vm_table", 0xF, 0x9FE1D0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(242, "vm_table", 0x12, 0x9FE1D1));
    }

    [Fact]
    public void Direct_particle_rectangle_service_does_not_admit_an_unused_sprite_or_loader_slot()
    {
        Assert.Equal(0xB29F20u, NativeBattleCinematicDispatch.RequireSlot(242, "draw_table", 6, 0x9FBDB0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(242, "spr_table", 9, 0x9FB8D0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(242, "vm_table", 6, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(242, "draw_table", 5, 0x9FBDB0));
    }

    [Fact]
    public void Selected_profile_preserves_the_physical_table_layout_and_empty_extra_source_range()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(242);
        Assert.True(NativeBattleCinematicProfile.IsSupported(242));
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(0x18629A4));
        Assert.Equal(0x1862AC0u, profile.BoneTable);
        Assert.Equal(0x1862B08u, profile.PrimitiveTable);
        Assert.Equal(0x1862BF8u, profile.PrimitiveSizes);
        Assert.Equal(0x1862C38u, profile.PlacementTable);
        Assert.Equal(0x1862C4Cu, profile.ParticleInstructionTable);
        Assert.Equal(0x1862CACu, profile.DrawTable);
        Assert.Equal(0x1862E50u, profile.InstructionTable);
        Assert.Equal(0u, profile.LoadCallback);
        Assert.Equal(0x233, profile.SourceBase);
        Assert.Equal(0x232, profile.LastSource);
        Assert.Equal(5, profile.TextureCount);
        Assert.False(profile.HasVibrationDirectory);
        Assert.False(profile.Lit);
    }
}
