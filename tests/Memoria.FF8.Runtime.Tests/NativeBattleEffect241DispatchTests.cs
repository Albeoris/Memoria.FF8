namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect241DispatchTests
{
    [Theory]
    [MemberData(nameof(SelectedSlots))]
    public void Selected_slot_requires_its_actual_handler_and_current_individual_kernel(string table, int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(241, table, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(241, table, index, handler ^ 1));
    }

    [Fact]
    public void Unselected_slots_reject_shared_handlers_and_unused_async_callback()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(241, "vm_table", 6, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(241, "vm_table", 3, 0xA07720));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(241, "draw_table", 6, 0xA04CB0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(241, "spr_table", 0, 0xA04C10));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(241, "prim_table", 48, 0xA041A0));
    }

    [Fact]
    public void Profile_retains_actual_publications_directories_and_unlit_selected_scope()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(241);
        Assert.True(NativeBattleCinematicProfile.IsSupported(241));
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(0x1863388));
        Assert.Equal(0xA01E10u, profile.Entry);
        Assert.Equal(0xA01DE0u, profile.Initializer);
        Assert.Equal(0x1863388u, profile.InitializationAddress);
        Assert.Equal(0x1863478u, profile.OwnerDirectoryAddress);
        Assert.Equal(0x2796860u, profile.QueueAddress);
        Assert.Equal(0x2796878u, profile.BankAddress);
        Assert.Equal(0xA02480u, profile.Callback);
        Assert.Equal(0u, profile.LoadCallback);
        Assert.Equal(0x2796874u, profile.File0Publication);
        Assert.Equal(0x2796870u, profile.File1Publication);
        Assert.Equal(0x1863830u, profile.InstructionTable);
        Assert.Equal(0x18634A0u, profile.BoneTable);
        Assert.Equal(0x18634E8u, profile.PrimitiveTable);
        Assert.Equal(0x18635ECu, profile.SpriteTable);
        Assert.Equal(0x186368Cu, profile.DrawTable);
        Assert.Equal(5, profile.TextureCount);
        Assert.Equal(0x9C4, profile.StaticLength);
        Assert.Equal(0x233, profile.SourceBase);
        Assert.Equal(0x232, profile.LastSource);
        Assert.Equal("mag240_b.00", profile.InitialFile);
        Assert.False(profile.Lit);
        Assert.False(profile.HasVibrationDirectory);
    }

    public static IEnumerable<object[]> SelectedSlots()
    {
        yield return ["vm_table", 0x0, 0xA0AF20u, 0xB2FEB0u];
        yield return ["vm_table", 0x1, 0xA0AB90u, 0xB2FB20u];
        yield return ["vm_table", 0x2, 0xA060D0u, 0xB2AE30u];
        yield return ["vm_table", 0x5, 0xA072D0u, 0xB2C030u];
        yield return ["vm_table", 0x9, 0xA0AEF0u, 0xB2FE80u];
        yield return ["vm_table", 0xA, 0xA07720u, 0xB2C480u];
        yield return ["vm_table", 0xD, 0xA07720u, 0xB2C480u];
        yield return ["vm_table", 0xE, 0xA07720u, 0xB2C480u];
        yield return ["vm_table", 0x10, 0xA07720u, 0xB2C480u];
        yield return ["vm_table", 0x12, 0xA07720u, 0xB2C480u];
        yield return ["vm_table", 0x21, 0xA066F0u, 0xB2B450u];
        yield return ["vm_table", 0x22, 0xA067D0u, 0xB2B530u];
        yield return ["vm_table", 0x23, 0xA09240u, 0xB2DFA0u];
        yield return ["vm_table", 0x24, 0xA08D90u, 0xB2DAF0u];
        yield return ["vm_table", 0x25, 0xA08D40u, 0xB2DAA0u];
        yield return ["vm_table", 0x26, 0xA08760u, 0xB2D4C0u];
        yield return ["vm_table", 0x27, 0xA097E0u, 0xB2E540u];
        yield return ["vm_table", 0x2B, 0xA02240u, 0xB25BB0u];
        yield return ["vm_table", 0x32, 0xA088B0u, 0xB2D610u];
        yield return ["vm_table", 0x33, 0xA062C0u, 0xB2B020u];
        yield return ["vm_table", 0x39, 0xA069F0u, 0xB2B750u];
        yield return ["vm_table", 0x3B, 0xA06C20u, 0xB2B980u];
        yield return ["vm_table", 0x3C, 0xA0A680u, 0xB2F3E0u];
        yield return ["vm_table", 0x52, 0xA09860u, 0xB2E5C0u];
        yield return ["vm_table", 0x5F, 0xA07A20u, 0xB2C780u];
        yield return ["vm_table", 0x64, 0xA06380u, 0xB2B0E0u];
        yield return ["vm_table", 0x68, 0xA09F50u, 0xB2ECB0u];
        yield return ["vm_table", 0x72, 0xA07AC0u, 0xB2C820u];
        yield return ["vm_table", 0x74, 0xA07600u, 0xB2C360u];
        yield return ["vm_table", 0x7D, 0xA07130u, 0xB2BE90u];
        yield return ["vm_table", 0x8B, 0xA087C0u, 0xB2D520u];
        yield return ["vm_table", 0xB1, 0xA063F0u, 0xB2B150u];
        yield return ["vm_table", 0xD4, 0xA09070u, 0xB2DDD0u];
        yield return ["vm_table", 0x104, 0xA0A050u, 0xB2EDB0u];
        yield return ["vm_table", 0x105, 0xA0A0E0u, 0xB2EE40u];
        yield return ["bone_table", 0x0, 0xA02970u, 0xB262E0u];
        yield return ["bone_table", 0x1, 0xA02970u, 0xB262E0u];
        yield return ["draw_table", 0x2, 0xA03260u, 0xB27050u];
        yield return ["draw_table", 0x5, 0xA04CB0u, 0xB298D0u];
        yield return ["spr_table", 0x9, 0xA04C10u, 0xB29830u];
        yield return ["prim_table", 0x8, 0xA041A0u, 0xB28110u];
        yield return ["prim_table", 0x12, 0xA044D0u, 0xB28DD0u];
    }
}
