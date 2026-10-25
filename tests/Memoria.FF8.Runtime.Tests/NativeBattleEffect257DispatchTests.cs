namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect257DispatchTests
{
    [Fact]
    public void Shared_vector_handler_requires_the_selected_slot_and_original_address()
    {
        Assert.Equal(0xB2C480u, NativeBattleCinematicDispatch.RequireSlot(257, "vm_table", 0x15, 0x9757C0));
        Assert.Equal(0xB2C480u, NativeBattleCinematicDispatch.Canonicalize(257, 0x9757C0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(257, "vm_table", 0x14, 0x9757C0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(257, "vm_table", 0x15, 0x9757C1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(257, "vm_table", 0x38, 0x9757C0));
    }

    [Fact]
    public void Quad_variant_and_direct_particle_draw_preserve_their_distinct_dispatch_owners()
    {
        Assert.Equal(0xAAAE60u, NativeBattleCinematicDispatch.RequireSlot(257, "prim_table", 58, 0x9725F0));
        Assert.Equal(0xB29F20u, NativeBattleCinematicDispatch.RequireSlot(257, "draw_table", 6, 0x9733A0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(257, "prim_table", 47, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(257, "prim_table", 18, 0x9725F0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(257, "spr_table", 9, 0x9733A0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(257, "draw_table", 1, 0x9707F0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(257, 0));
    }

    [Fact]
    public void Profile_preserves_the_original_physical_layout_and_common_file_completion()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(257);
        Assert.True(NativeBattleCinematicProfile.IsSupported(257));
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(0x185A064));
        Assert.Equal(0x96F290u, profile.Entry);
        Assert.Equal(0x96F260u, profile.Initializer);
        Assert.Equal(0x185A1ACu, profile.OwnerDirectoryAddress);
        Assert.Equal(0x2796510u, profile.QueueAddress);
        Assert.Equal(0x2796520u, profile.BankAddress);
        Assert.Equal(0x96F900u, profile.Callback);
        Assert.Equal(0x974E80u, profile.LoadCallback);
        Assert.Equal(0x279650Cu, profile.File0Publication);
        Assert.Equal(0x2796508u, profile.File1Publication);
        Assert.Equal(0x185A1D4u, profile.BoneTable);
        Assert.Equal(0x185A21Cu, profile.PrimitiveTable);
        Assert.Equal(0x185A30Cu, profile.PrimitiveSizes);
        Assert.Equal(0x185A320u, profile.SpriteTable);
        Assert.Equal(0x185A34Cu, profile.PlacementTable);
        Assert.Equal(0x185A360u, profile.ParticleInstructionTable);
        Assert.Equal(0x185A3C0u, profile.DrawTable);
        Assert.Equal(0x185A564u, profile.InstructionTable);
        Assert.Equal(8, profile.TextureCount);
        Assert.Equal(0x233, profile.SourceBase);
        Assert.Equal(0x232, profile.LastSource);
        Assert.Equal("mag256_b.00", profile.InitialFile);
        Assert.False(profile.HasVibrationDirectory);
        Assert.False(profile.Lit);
    }
}
