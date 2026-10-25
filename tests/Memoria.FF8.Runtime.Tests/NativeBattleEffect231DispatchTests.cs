namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect231DispatchTests
{
    [Fact]
    public void Between_update_requires_its_actual_selected_handler_and_slot()
    {
        Assert.Equal(0xA671C0u, NativeBattleCinematicDispatch.RequireSlot(231, "bone_table", 4, 0xA671C0));
        Assert.Equal(0xA671C0u, NativeBattleCinematicDispatch.Canonicalize(231, 0xA671C0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(231, "bone_table", 5, 0xA671C0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(231, "bone_table", 4, 0xA671C1));
    }

    [Fact]
    public void Flagged_quad_uses_decimal_variant58_and_unselected_paths_remain_closed()
    {
        Assert.Equal(0xAAAE60u, NativeBattleCinematicDispatch.RequireSlot(231, "prim_table", 58, 0xA69D60));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(231, "prim_table", 0x58, 0xA69D60));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(231, "pop_table", 9, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(231, "pop_table", 10, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(231, "ptype_table", 2, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(231, "spr_table", 0, 0xA6A630));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(231, "vm_table", 6, 0));
    }

    [Fact]
    public void Profile_preserves_physical_tables_and_the_selected_resource_policy()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(231);
        Assert.True(NativeBattleCinematicProfile.IsSupported(231));
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(0x1869650));
        Assert.Equal(0xA664A0u, profile.Entry);
        Assert.Equal(0xA66470u, profile.Initializer);
        Assert.Equal(0x186973Cu, profile.OwnerDirectoryAddress);
        Assert.Equal(0x2796A90u, profile.QueueAddress);
        Assert.Equal(0x2796AA8u, profile.BankAddress);
        Assert.Equal(0xA66B10u, profile.Callback);
        Assert.Equal(0u, profile.LoadCallback);
        Assert.Equal(0x2796AA4u, profile.File0Publication);
        Assert.Equal(0x2796AA0u, profile.File1Publication);
        Assert.Equal(0x1869764u, profile.BoneTable);
        Assert.Equal(0x18697ACu, profile.PrimitiveTable);
        Assert.Equal(0x186989Cu, profile.PrimitiveSizes);
        Assert.Equal(0x18698DCu, profile.PlacementTable);
        Assert.Equal(0x18698F0u, profile.ParticleInstructionTable);
        Assert.Equal(0x1869950u, profile.DrawTable);
        Assert.Equal(0x1869AF4u, profile.InstructionTable);
        Assert.Equal(4, profile.TextureCount);
        Assert.Equal(0x233, profile.SourceBase);
        Assert.Equal(0x232, profile.LastSource);
        Assert.Equal("mag230_b.00", profile.InitialFile);
        Assert.False(profile.HasVibrationDirectory);
        Assert.False(profile.Lit);
    }
}
