using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanDecorationsTests
{
    [Theory]
    [InlineData(0, 999, 0x4000, 0x15CF6FC)]
    [InlineData(1, -1, 0xFFF0, 0x15CF6FC)]
    [InlineData(0, int.MinValue, 0, 0x15CF6FC)]
    [InlineData(2, 0, 129, 0x15CF784)]
    [InlineData(3, 0, 8256, 0x15CF80C)]
    [InlineData(4, 0, 0, 0x15CF894)]
    [InlineData(5, 0, 16384, 0x15CF91C)]
    [InlineData(6, 0, 16254, 0x15CF9A4)]
    [InlineData(7, 0, 16384, 0x15CFA2C)]
    public void Stat_initialization_uses_duplicate_HP_branch_exact_relative_stat_bytes_and_signed_HP_shift(int index, int hp, int target, uint mesh)
    {
        var fixture = Fixture(0x84F520);
        NativeBattleScanDecorationsFrame frame = Change(fixture.Frame, fixture.Address, task => task.WriteWord(0x62, (ushort)index));
        List<NativeBattleScanMeshDraw> draws = [];
        NativeBattleScanDecorationsStep step = NativeBattleScanDecorations.Advance(frame, fixture.Address, Inputs(fixture.Packets, draws, hp));
        NativeBattleModulePoolTask task = Task(step.Frame, fixture.Address);
        Assert.Equal((ushort)target, task.ReadWord(0x64));
        Assert.Equal(mesh, task.ReadDword(0x4C));
        Assert.Equal((ushort)9, task.ReadWord(0x1C));
        Assert.Equal((ushort)18, task.ReadWord(0x1E));
        Assert.Equal((ushort)30, task.ReadWord(0x20));
        Assert.Empty(draws);
        Assert.Equal((byte)1, task.ReadByte(0x29));
    }

    [Fact]
    public void Zero_delay_first_enables_draw_then_grow_clamps_to_signed_target_and_completes_after_draw()
    {
        var fixture = Fixture(0x84F520);
        List<NativeBattleScanMeshDraw> draws = [];
        NativeBattleScanDecorationsInputs inputs = Inputs(fixture.Packets, draws, -1);
        NativeBattleScanDecorationsFrame frame = NativeBattleScanDecorations.Advance(fixture.Frame, fixture.Address, inputs).Frame;
        frame = NativeBattleScanDecorations.Advance(frame, fixture.Address, inputs).Frame;
        Assert.Single(draws);
        Assert.Equal(0u, Task(frame, fixture.Address).ReadDword(0x30));
        NativeBattleScanDecorationsStep step = NativeBattleScanDecorations.Advance(frame with { Complete269CE2C = 1 }, fixture.Address, inputs);
        Assert.Equal(2, step.Result);
        Assert.Equal(0xFFFFFFF0u, Task(step.Frame, fixture.Address).ReadDword(0x30));
        Assert.Equal(2, draws.Count);
        Assert.Equal((ushort)1, Task(step.Frame, fixture.Address).Flags);
    }

    [Fact]
    public void Board_appends_eight_real_statbars_in_order_and_cannot_complete_while_children_are_active()
    {
        var fixture = Fixture(0x84F430);
        List<NativeBattleScanMeshDraw> draws = [];
        NativeBattleScanDecorationsInputs inputs = Inputs(fixture.Packets, draws);
        NativeBattleScanDecorationsFrame frame = NativeBattleScanDecorations.Advance(fixture.Frame, fixture.Address, inputs).Frame;
        frame = NativeBattleScanDecorations.Advance(frame, fixture.Address, inputs).Frame;
        NativeBattleModulePoolTask[] children = frame.Visual.Scan.Effect.Pools[4].Slots.Where(task => task.Flags != 0).Skip(1).ToArray();
        Assert.Equal(8, children.Length);
        Assert.Equal(Enumerable.Range(0, 8).Select(index => (ushort)index), children.Select(task => task.ReadWord(0x62)));
        Assert.All(children, task =>
        {
            Assert.Equal(0x84F520u, task.Callback);
            Assert.Equal(fixture.Address, task.ReadDword(0x18));
            Assert.Equal((ushort)0, task.ReadWord(0x60));
        });
        NativeBattleScanDecorationsStep step = NativeBattleScanDecorations.Advance(frame with { Complete269CE2C = 1 }, fixture.Address, inputs);
        Assert.Equal(0, step.Result);
        Assert.Equal((byte)3, Task(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(3, draws.Count);
    }

    [Fact]
    public void Spinner_rotates_during_fade_and_terminal_draw_and_requires_completion_word_exactly_one()
    {
        var fixture = Fixture(0x84F300);
        List<NativeBattleScanMeshDraw> draws = [];
        NativeBattleScanDecorationsInputs inputs = Inputs(fixture.Packets, draws);
        NativeBattleScanDecorationsFrame frame = NativeBattleScanDecorations.Advance(fixture.Frame, fixture.Address, inputs).Frame;
        Assert.Equal((ushort)96, Task(frame, fixture.Address).ReadWord(0x44));
        Assert.Equal((ushort)192, Task(frame, fixture.Address).ReadWord(0x46));
        for (int index = 0; index < 16; index++)
        {
            frame = NativeBattleScanDecorations.Advance(frame, fixture.Address, inputs).Frame;
        }
        Assert.Equal((ushort)0, Task(frame, fixture.Address).ReadWord(0x50));
        frame = NativeBattleScanDecorations.Advance(frame with { Complete269CE2C = 2 }, fixture.Address, inputs).Frame;
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        NativeBattleScanDecorationsStep step = NativeBattleScanDecorations.Advance(frame with { Complete269CE2C = 1 }, fixture.Address, inputs);
        Assert.Equal(2, step.Result);
        Assert.Equal(19, draws.Count);
        Assert.Equal((ushort)(19 * 96 & 0xFFF), Task(step.Frame, fixture.Address).ReadWord(0x44));
    }

    [Fact]
    public void Backdrop_uses_its_own_ready_word_after_eight_fade_ticks_and_still_draws_on_completion()
    {
        var fixture = Fixture(0x84F9E0);
        List<NativeBattleScanMeshDraw> draws = [];
        NativeBattleScanDecorationsInputs inputs = Inputs(fixture.Packets, draws);
        NativeBattleScanDecorationsFrame frame = fixture.Frame with { Complete269CE2C = 1 };
        for (int index = 0; index < 9; index++)
        {
            frame = NativeBattleScanDecorations.Advance(frame, fixture.Address, inputs).Frame;
        }
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(0, NativeBattleScanDecorations.Advance(frame, fixture.Address, inputs).Result);
        NativeBattleScanDecorationsStep step = NativeBattleScanDecorations.Advance(frame with { Ready269D938 = 1 }, fixture.Address, inputs);
        Assert.Equal(2, step.Result);
        Assert.Equal(11, draws.Count);
        Assert.Equal(0x15CEA34u, draws[^1].ResourceAddress);
        Assert.Equal(-3968, draws[^1].Matrix.X);
    }

    private static NativeBattleScanDecorationsInputs Inputs(NativeBattleActionPacketsState packets, List<NativeBattleScanMeshDraw> draws, int hp = 0)
    {
        byte[] table = new byte[80];
        for (int index = 0; index < 8; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(table.AsSpan(index * 10), -3);
            BinaryPrimitives.WriteInt16LittleEndian(table.AsSpan(index * 10 + 2), -5);
        }
        NativeBattleScanVisualResources resources = new([])
        {
            Sound15D0B3C = 0,
            TablesByAddress = new Dictionary<uint, ImmutableArray<byte>> { [0x15D1678] = table.ToImmutableArray(), [0x15D16C8] = table.ToImmutableArray() }.ToImmutableDictionary(),
            MeshesByAddress = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty,
            VertexBanksByAddress = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty,
            LineArrays15D1718 = [],
            Sprite15D07D4 = null!,
            Sprite15D0AF8 = null!
        };
        byte[] actor = Enumerable.Repeat((byte)255, 208).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(actor.AsSpan(0x18), hp);
        actor[0xBD] = 1;
        actor[0xBE] = 64;
        actor[0xBF] = 0;
        actor[0xC0] = 255;
        actor[0xC1] = 126;
        actor[0xC3] = 26;
        NativeBattleScanByteOwner descriptor = new(actor.ToImmutableArray(), Enumerable.Repeat(true, 208).ToImmutableArray());
        return new(packets, new(resources, (frame, draw) =>
        {
            draws.Add(draw);
            return frame;
        }), _ => descriptor);
    }

    private static NativeBattleModulePoolTask Task(NativeBattleScanDecorationsFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Visual.Scan.Effect.Pools).Read(address);

    private static NativeBattleScanDecorationsFrame Change(NativeBattleScanDecorationsFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Visual.Scan.Effect.Pools);
        hierarchy = hierarchy.Write(address, change(hierarchy.Read(address)));
        return frame with { Visual = frame.Visual with { Scan = frame.Visual.Scan with { Effect = frame.Visual.Scan.Effect with { Pools = hierarchy.Pools } } } };
    }

    private static (NativeBattleScanDecorationsFrame Frame, uint Address, NativeBattleActionPacketsState Packets) Fixture(uint callback)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess(), effect, packets);
        NativeBattleEffectHierarchy hierarchy = new(scan.Effect.Pools);
        hierarchy = hierarchy.Write(0x269AAE0, hierarchy.Read(0x269AAE0).WriteWord(0x1C, 10).WriteWord(0x1E, 20).WriteWord(0x20, 30));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(4, callback, 0x6C, 0x269AAE0, (_, _, _) => new(2, 4));
        scan = scan with { Effect = scan.Effect with { Pools = child.Hierarchy.Pools } };
        return (new(new(scan, 1, ImmutableDictionary<uint, NativeBattleScanByteOwner>.Empty), 0, 0), child.Address, packets);
    }
}
