namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect232DispatchTests
{
    [Theory]
    [MemberData(nameof(SelectedSlots))]
    public void Selected_slot_requires_its_actual_handler_and_current_individual_kernel(string table, int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(232, table, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(232, table, index, handler ^ 1));
    }

    [Fact]
    public void Unselected_slots_reject_even_when_their_handler_is_a_selected_shared_kernel()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(232, "vm_table", 15, 0xA627E0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(232, "vm_table", 6, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(232, "draw_table", 45, 0xA5D730));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(232, "prim_table", 38, 0xA5F610));
    }

    [Fact]
    public void Profile_preserves_actual_directory_ownership_and_unused_async_callback_boundary()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(232);
        Assert.True(NativeBattleCinematicProfile.IsSupported(232));
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(0x1868C88));
        Assert.Equal(0xA5C2E0u, profile.Entry);
        Assert.Equal(0xA5C2B0u, profile.Initializer);
        Assert.Equal(0x2796A58u, profile.QueueAddress);
        Assert.Equal(0x2796A70u, profile.BankAddress);
        Assert.Equal(0xA5C950u, profile.Callback);
        Assert.Equal(0u, profile.LoadCallback);
        Assert.Equal(0x2796A6Cu, profile.File0Publication);
        Assert.Equal(0x2796A68u, profile.File1Publication);
        Assert.Equal(0x1869118u, profile.InstructionTable);
        Assert.Equal(0x1868D88u, profile.BoneTable);
        Assert.Equal(0x1868DD0u, profile.PrimitiveTable);
        Assert.Equal(0x1868F14u, profile.ParticleInstructionTable);
        Assert.Equal(0x1868F74u, profile.DrawTable);
        Assert.Equal(4, profile.TextureCount);
        Assert.Equal(0x9AC, profile.StaticLength);
        Assert.Equal(0x233, profile.SourceBase);
        Assert.Equal(0x232, profile.LastSource);
        Assert.Equal("mag231_b.00", profile.InitialFile);
        Assert.False(profile.HasVibrationDirectory);
        Assert.False(profile.Lit);
    }

    public static IEnumerable<object[]> SelectedSlots()
    {
        yield return ["vm_table", 0, 0xA66060u, 0xB2FEB0u];
        yield return ["vm_table", 1, 0xA65CD0u, 0xB2FB20u];
        yield return ["vm_table", 2, 0xA61190u, 0xB2AE30u];
        yield return ["vm_table", 5, 0xA62390u, 0xB2C030u];
        yield return ["vm_table", 9, 0xA66030u, 0xB2FE80u];
        yield return ["vm_table", 10, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 11, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 12, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 13, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 14, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 16, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 17, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 18, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 19, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 21, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 22, 0xA627E0u, 0xB2C480u];
        yield return ["vm_table", 26, 0xA633D0u, 0xB2D070u];
        yield return ["vm_table", 32, 0xA64650u, 0xB2E2F0u];
        yield return ["vm_table", 33, 0xA617B0u, 0xB2B450u];
        yield return ["vm_table", 34, 0xA61890u, 0xB2B530u];
        yield return ["vm_table", 35, 0xA64300u, 0xB2DFA0u];
        yield return ["vm_table", 36, 0xA63E50u, 0xB2DAF0u];
        yield return ["vm_table", 38, 0xA63820u, 0xB2D4C0u];
        yield return ["vm_table", 39, 0xA648A0u, 0xB2E540u];
        yield return ["vm_table", 42, 0xA638F0u, 0xB2D590u];
        yield return ["vm_table", 43, 0xA5C710u, 0xB25BB0u];
        yield return ["vm_table", 47, 0xA62C30u, 0xB2C8D0u];
        yield return ["vm_table", 50, 0xA63970u, 0xB2D610u];
        yield return ["vm_table", 51, 0xA61380u, 0xB2B020u];
        yield return ["vm_table", 57, 0xA61AB0u, 0xB2B750u];
        yield return ["vm_table", 60, 0xA65740u, 0xB2F3E0u];
        yield return ["vm_table", 64, 0xA64090u, 0xB2DD30u];
        yield return ["vm_table", 68, 0xA61510u, 0xB2B1B0u];
        yield return ["vm_table", 82, 0xA64920u, 0xB2E5C0u];
        yield return ["vm_table", 88, 0xA60210u, 0xB29D70u];
        yield return ["vm_table", 89, 0xA60280u, 0xB29DE0u];
        yield return ["vm_table", 91, 0xA653F0u, 0xB2F090u];
        yield return ["vm_table", 94, 0xA62BD0u, 0xB2C870u];
        yield return ["vm_table", 95, 0xA62AE0u, 0xB2C780u];
        yield return ["vm_table", 96, 0xA63160u, 0xB2CE00u];
        yield return ["vm_table", 97, 0xA63020u, 0xB2CCC0u];
        yield return ["vm_table", 100, 0xA61440u, 0xB2B0E0u];
        yield return ["vm_table", 103, 0xA64CB0u, 0xB2E950u];
        yield return ["vm_table", 104, 0xA65010u, 0xB2ECB0u];
        yield return ["vm_table", 114, 0xA62B80u, 0xB2C820u];
        yield return ["vm_table", 116, 0xA626C0u, 0xB2C360u];
        yield return ["vm_table", 125, 0xA621F0u, 0xB2BE90u];
        yield return ["vm_table", 139, 0xA63880u, 0xB2D520u];
        yield return ["vm_table", 145, 0xA630B0u, 0xB2CD50u];
        yield return ["vm_table", 146, 0xA649D0u, 0xB2E670u];
        yield return ["vm_table", 177, 0xA614B0u, 0xB2B150u];
        yield return ["vm_table", 211, 0xA64190u, 0xB2DE30u];
        yield return ["vm_table", 212, 0xA64130u, 0xB2DDD0u];
        yield return ["vm_table", 242, 0xA62F30u, 0xB2CBD0u];
        yield return ["bone_table", 0, 0xA5CE40u, 0xB262E0u];
        yield return ["bone_table", 1, 0xA5CE40u, 0xB262E0u];
        yield return ["bone_table", 10, 0xA5D100u, 0xA5D100u];
        yield return ["draw_table", 2, 0xA5DE00u, 0xB27050u];
        yield return ["draw_table", 6, 0xA603C0u, 0xB29F20u];
        yield return ["draw_table", 44, 0xA5D730u, 0xA5D730u];
        yield return ["pop_table", 0, 0xA60960u, 0xB2A4C0u];
        yield return ["pop_table", 1, 0xA60990u, 0xB2A4F0u];
        yield return ["pop_table", 4, 0xA60A10u, 0xB2A570u];
        yield return ["pop_table", 5, 0xA60AD0u, 0xB2A630u];
        yield return ["pop_table", 6, 0xA60B90u, 0xB2A6F0u];
        yield return ["pop_table", 7, 0xA60CC0u, 0xB2A820u];
        yield return ["pop_table", 11, 0xA60D30u, 0xB2A890u];
        yield return ["ptype_table", 1, 0xA60780u, 0xB2A2E0u];
        yield return ["prim_table", 17, 0xA5ECE0u, 0xB28B00u];
        yield return ["prim_table", 18, 0xA5EFB0u, 0xB28DD0u];
        yield return ["prim_table", 19, 0xA5F290u, 0xB290B0u];
        yield return ["prim_table", 58, 0xA5F610u, 0xAAAE60u];
    }
}
