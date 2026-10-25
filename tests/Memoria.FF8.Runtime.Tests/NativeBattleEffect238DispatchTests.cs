namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect238DispatchTests
{
    [Theory]
    [InlineData(0x0C)]
    [InlineData(0x0F)]
    [InlineData(0x14)]
    [InlineData(0x15)]
    [InlineData(0x17)]
    public void Shared_vector_handler_does_not_admit_an_unselected_opcode(int opcode)
    {
        Assert.Equal(0xB2C480u, NativeBattleCinematicDispatch.RequireSlot(238, "vm_table", 0x0A, 0xA25F70));
        Assert.Equal(0xB2C480u, NativeBattleCinematicDispatch.RequireSlot(238, "vm_table", 0x16, 0xA25F70));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "vm_table", opcode, 0xA25F70));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "vm_table", opcode, 0xB2C480));
    }

    [Fact]
    public void Both_position_update_slots_share_the_kernel_without_admitting_the_next_slot()
    {
        Assert.Equal(0xB262E0u, NativeBattleCinematicDispatch.RequireSlot(238, "bone_table", 0, 0xA20920));
        Assert.Equal(0xB262E0u, NativeBattleCinematicDispatch.RequireSlot(238, "bone_table", 1, 0xA20920));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "bone_table", 2, 0xA20920));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "bone_table", 0, 0xB262E0));
    }

    [Theory]
    [InlineData(7, 0xA22250u, 0xB27E90u)]
    [InlineData(8, 0xA224D0u, 0xB28110u)]
    [InlineData(16, 0xA227E0u, 0xB105B0u)]
    [InlineData(17, 0xA22A50u, 0xB28B00u)]
    [InlineData(18, 0xA22D20u, 0xB28DD0u)]
    public void Base_mesh_primitives_do_not_admit_flagged_alternate_slots(int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(238, "prim_table", index, handler));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(238, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "prim_table", index + 20, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "prim_table", index + 40, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "prim_table", index, canonical));
    }

    [Theory]
    [InlineData(7, 1, 0xA23310u, 0xB296E0u)]
    [InlineData(9, 0, 0xA23460u, 0xB29830u)]
    public void Sprite_mode_selects_its_handler_independently_of_the_resource_index(int mode, int resourceIndex, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(238, "spr_table", mode, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "spr_table", resourceIndex, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "spr_table", mode, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(238, 0));
    }

    [Theory]
    [InlineData("draw_table", 2, 0xA21320u, 0xB27050u)]
    [InlineData("draw_table", 4, 0xA210F0u, 0xB26E20u)]
    [InlineData("draw_table", 5, 0xA23500u, 0xB298D0u)]
    [InlineData("draw_table", 6, 0xA23B50u, 0xB29F20u)]
    [InlineData("draw_table", 21, 0xA20D00u, 0xB266C0u)]
    [InlineData("pop_table", 0, 0xA240F0u, 0xB2A4C0u)]
    [InlineData("pop_table", 1, 0xA24120u, 0xB2A4F0u)]
    [InlineData("pop_table", 4, 0xA241A0u, 0xB2A570u)]
    [InlineData("pop_table", 5, 0xA24260u, 0xB2A630u)]
    [InlineData("pop_table", 7, 0xA24450u, 0xB2A820u)]
    [InlineData("ptype_table", 1, 0xA23F10u, 0xB2A2E0u)]
    [InlineData("vm_table", 0x8B, 0xA27090u, 0xB2D520u)]
    [InlineData("vm_table", 0x116, 0xA28680u, 0xB2EB10u)]
    public void Selected_handlers_require_their_actual_table_and_physical_address(string table, int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(238, table, index, handler));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(238, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, table, index, handler + 1));
        string otherTable = table == "vm_table" ? "draw_table" : "vm_table";
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, otherTable, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(238, canonical));
    }

    [Fact]
    public void Range_branch_keeps_its_selected_variant_identity()
    {
        Assert.Equal(0xA26E20u, NativeBattleCinematicDispatch.RequireSlot(238, "vm_table", 0x94, 0xA26E20));
        Assert.False(NativeBattleCinematicDispatch.IsNoOp(238, "vm_table", 0x94, 0xA26E20));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(238, 0xA26E20));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "vm_table", 0x94, 0xA77C10));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "vm_table", 0x93, 0xA26E20));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(238, "draw_table", 0x94, 0xA26E20));
    }

    [Fact]
    public void Profile_binds_the_selected_dispatch_directories()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(238);
        Assert.Equal(0xA20430u, profile.Callback);
        Assert.Equal(0xA25630u, profile.LoadCallback);
        Assert.Equal(0x18652A0u, profile.BoneTable);
        Assert.Equal(0x18652E8u, profile.PrimitiveTable);
        Assert.Equal(0x18653ECu, profile.SpriteTable);
        Assert.Equal(0x1865418u, profile.PlacementTable);
        Assert.Equal(0x186542Cu, profile.ParticleInstructionTable);
        Assert.Equal(0x186548Cu, profile.DrawTable);
        Assert.Equal(0x1865630u, profile.InstructionTable);
        Assert.Equal(7, profile.TextureCount);
        Assert.False(profile.Lit);
        Assert.False(profile.HasVibrationDirectory);
    }
}
