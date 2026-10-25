namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect239DispatchTests
{
    [Theory]
    [MemberData(nameof(SelectedSlots))]
    public void Selected_slot_accepts_only_its_native_handler_and_current_kernel(string table, int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(239, table, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, table, index, handler ^ 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, table, index, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, table, index, 0xA07720));
    }

    [Fact]
    public void Unselected_indices_reject_zero_selected_native_and_canonical_handlers()
    {
        Dictionary<string, int> extents = new()
        {
            ["vm_table"] = 512,
            ["bone_table"] = 256,
            ["prim_table"] = 60,
            ["spr_table"] = 256,
            ["ptype_table"] = 128,
            ["pop_table"] = 256,
            ["draw_table"] = 256
        };
        HashSet<(string Table, int Index)> selected = SelectedSlots().Select(row => ((string)row[0], (int)row[1])).ToHashSet();
        uint[] handlers = [0, 0xA1C180, 0xA16E80, 0xB2C480, 0xA07720];
        foreach ((string table, int count) in extents)
        {
            for (int index = table == "ptype_table" ? -128 : 0; index < count; index++)
            {
                if (selected.Contains((table, index)))
                {
                    continue;
                }
                foreach (uint handler in handlers)
                {
                    Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, table, index, handler));
                }
            }
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, table, -1, 0));
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, table, count, 0));
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, table, int.MaxValue, 0));
        }
    }

    [Fact]
    public void Local_camera_case_and_self_draw_are_not_global_or_sibling_slot_aliases()
    {
        Assert.Equal(0xA16E80u, NativeBattleCinematicDispatch.RequireSlot(239, "draw_table", 32, 0xA16E80));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, "draw_table", 0x32, 0xA16E80));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, "vm_table", 0x39, 0xA1B485));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, "camera_table", 0, 0xA1B485));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, "camera_table", 1, 0xA1B485));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, "spr_table", 9, 0xA04C10));
    }

    [Fact]
    public void Profile_joins_native_entry_queue_files_and_empty_extra_range()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(239);
        Assert.True(NativeBattleCinematicProfile.IsSupported(239));
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(0x1864760));
        Assert.Equal(0xA15A30u, profile.Entry);
        Assert.Equal(0xA15A00u, profile.Initializer);
        Assert.Equal(0x1864760u, profile.InitializationAddress);
        Assert.Equal(0x1864864u, profile.OwnerDirectoryAddress);
        Assert.Equal(0x27968D8u, profile.QueueAddress);
        Assert.Equal(0x27968E8u, profile.BankAddress);
        Assert.Equal(0xA160A0u, profile.Callback);
        Assert.Equal(0xA1B840u, profile.LoadCallback);
        Assert.Equal(0x27968D4u, profile.File0Publication);
        Assert.Equal(0x27968D0u, profile.File1Publication);
        Assert.Equal(0x1864C1Cu, profile.InstructionTable);
        Assert.Equal(0x186488Cu, profile.BoneTable);
        Assert.Equal(0x18648D4u, profile.PrimitiveTable);
        Assert.Equal(0x1864A18u, profile.ParticleInstructionTable);
        Assert.Equal(0x1864A04u, profile.PlacementTable);
        Assert.Equal(0x1864A78u, profile.DrawTable);
        Assert.Equal(5, profile.TextureCount);
        Assert.Equal(0x233, profile.SourceBase);
        Assert.Equal(0x232, profile.LastSource);
        Assert.Empty(Enumerable.Range(profile.SourceBase, Math.Max(0, profile.LastSource - profile.SourceBase + 1)));
        Assert.Equal("mag238_b.00", profile.InitialFile);
        Assert.False(profile.Lit);
        Assert.False(profile.HasVibrationDirectory);
    }

    public static IEnumerable<object[]> SelectedSlots()
    {
        yield return ["vm_table", 0x0, 0xA1F980u, 0xB2FEB0u];
        yield return ["vm_table", 0x1, 0xA1F5F0u, 0xB2FB20u];
        yield return ["vm_table", 0x2, 0xA1AB30u, 0xB2AE30u];
        yield return ["vm_table", 0x3, 0xA1AB50u, 0xB2AE50u];
        yield return ["vm_table", 0x4, 0xA1ABB0u, 0xB2AEB0u];
        yield return ["vm_table", 0x5, 0xA1BD30u, 0xB2C030u];
        yield return ["vm_table", 0x6, 0xA1B710u, 0xB2BA10u];
        yield return ["vm_table", 0x8, 0xA1B8D0u, 0xB2BBD0u];
        yield return ["vm_table", 0x9, 0xA1F950u, 0xB2FE80u];
        yield return ["vm_table", 0xA, 0xA1C180u, 0xB2C480u];
        yield return ["vm_table", 0xB, 0xA1C180u, 0xB2C480u];
        yield return ["vm_table", 0xC, 0xA1C180u, 0xB2C480u];
        yield return ["vm_table", 0xD, 0xA1C180u, 0xB2C480u];
        yield return ["vm_table", 0xE, 0xA1C180u, 0xB2C480u];
        yield return ["vm_table", 0x10, 0xA1C180u, 0xB2C480u];
        yield return ["vm_table", 0x1E, 0xA1AC00u, 0xB2AF00u];
        yield return ["vm_table", 0x1F, 0xA1AC80u, 0xB2AF80u];
        yield return ["vm_table", 0x20, 0xA1DFF0u, 0xB2E2F0u];
        yield return ["vm_table", 0x21, 0xA1B150u, 0xB2B450u];
        yield return ["vm_table", 0x22, 0xA1B230u, 0xB2B530u];
        yield return ["vm_table", 0x23, 0xA1DCA0u, 0xB2DFA0u];
        yield return ["vm_table", 0x24, 0xA1D7F0u, 0xB2DAF0u];
        yield return ["vm_table", 0x26, 0xA1D1C0u, 0xB2D4C0u];
        yield return ["vm_table", 0x27, 0xA1E240u, 0xB2E540u];
        yield return ["vm_table", 0x29, 0xA1E140u, 0xB2E440u];
        yield return ["vm_table", 0x2B, 0xA15E60u, 0xB25BB0u];
        yield return ["vm_table", 0x2F, 0xA1C5D0u, 0xB2C8D0u];
        yield return ["vm_table", 0x32, 0xA1D310u, 0xB2D610u];
        yield return ["vm_table", 0x33, 0xA1AD20u, 0xB2B020u];
        yield return ["vm_table", 0x39, 0xA1B450u, 0xB2B750u];
        yield return ["vm_table", 0x3B, 0xA1B680u, 0xB2B980u];
        yield return ["vm_table", 0x3C, 0xA1F0E0u, 0xB2F3E0u];
        yield return ["vm_table", 0x43, 0xA1AF20u, 0xB2B220u];
        yield return ["vm_table", 0x44, 0xA1AEB0u, 0xB2B1B0u];
        yield return ["vm_table", 0x45, 0xA1B980u, 0xB2BC80u];
        yield return ["vm_table", 0x51, 0xA1D840u, 0xB2DB40u];
        yield return ["vm_table", 0x52, 0xA1E2C0u, 0xB2E5C0u];
        yield return ["vm_table", 0x58, 0xA19BB0u, 0xB29D70u];
        yield return ["vm_table", 0x59, 0xA19C20u, 0xB29DE0u];
        yield return ["vm_table", 0x5D, 0xA1D4F0u, 0xB2D7F0u];
        yield return ["vm_table", 0x5E, 0xA1C570u, 0xB2C870u];
        yield return ["vm_table", 0x60, 0xA1CB00u, 0xB2CE00u];
        yield return ["vm_table", 0x64, 0xA1ADE0u, 0xB2B0E0u];
        yield return ["vm_table", 0x67, 0xA1E650u, 0xB2E950u];
        yield return ["vm_table", 0x68, 0xA1E9B0u, 0xB2ECB0u];
        yield return ["vm_table", 0x6E, 0xA1CC70u, 0xB2CF70u];
        yield return ["vm_table", 0x74, 0xA1C060u, 0xB2C360u];
        yield return ["vm_table", 0x7D, 0xA1BB90u, 0xB2BE90u];
        yield return ["vm_table", 0x8B, 0xA1D220u, 0xB2D520u];
        yield return ["vm_table", 0xB1, 0xA1AE50u, 0xB2B150u];
        yield return ["vm_table", 0xC4, 0xA1E770u, 0xB2EA70u];
        yield return ["vm_table", 0xD0, 0xA1D900u, 0xB2DC00u];
        yield return ["vm_table", 0xD3, 0xA1DB30u, 0xB2DE30u];
        yield return ["draw_table", 0x2, 0xA17D60u, 0xB27050u];
        yield return ["draw_table", 0x6, 0xA19D60u, 0xB29F20u];
        yield return ["draw_table", 0x15, 0xA16970u, 0xB266C0u];
        yield return ["draw_table", 0x20, 0xA16E80u, 0xA16E80u];
        yield return ["bone_table", 0x0, 0xA16590u, 0xB262E0u];
        yield return ["prim_table", 0x8, 0xA18BB0u, 0xB28110u];
        yield return ["prim_table", 0x13, 0xA18EF0u, 0xB290B0u];
        yield return ["pop_table", 0x0, 0xA1A300u, 0xB2A4C0u];
        yield return ["pop_table", 0x1, 0xA1A330u, 0xB2A4F0u];
        yield return ["pop_table", 0x4, 0xA1A3B0u, 0xB2A570u];
        yield return ["pop_table", 0x5, 0xA1A470u, 0xB2A630u];
        yield return ["pop_table", 0x7, 0xA1A660u, 0xB2A820u];
        yield return ["pop_table", 0x9, 0xA1A590u, 0xB2A750u];
        yield return ["pop_table", 0xA, 0xA1A600u, 0xB2A7C0u];
        yield return ["pop_table", 0xB, 0xA1A6D0u, 0xB2A890u];
        yield return ["pop_table", 0xC, 0xA1A710u, 0xB2A8D0u];
        yield return ["pop_table", 0xF, 0xA1A7B0u, 0xB2A970u];
        yield return ["ptype_table", 0x1, 0xA1A120u, 0xB2A2E0u];
        yield return ["ptype_table", 0x2, 0xA1A170u, 0xB2A330u];
    }
}
