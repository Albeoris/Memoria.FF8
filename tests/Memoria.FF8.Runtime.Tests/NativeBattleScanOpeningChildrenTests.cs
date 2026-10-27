using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanOpeningChildrenTests
{
    [Theory]
    [InlineData(0, -144, -96, 0x15D1718u)]
    [InlineData(1, 144, -96, 0x15D1748u)]
    [InlineData(2, -144, 96, 0x15D1778u)]
    [InlineData(3, 144, 96, 0x15D17A8u)]
    public void Corner_initialization_does_not_draw_and_terminal_active_callback_draws_before_completion(ushort corner, short x, short y, uint resource)
    {
        var fixture = Fixture(0x84DDD0);
        NativeBattleScanReticleFrame frame = Modify(fixture.Frame, fixture.Address, task => task.WriteWord(0x74, corner));
        List<NativeBattleModulePacketBatch> draws = [];
        NativeBattleScanOpeningInputs inputs = Inputs(fixture.Packets, draws);
        frame = NativeBattleScanOpeningChildren.Advance84DDD0(frame, fixture.Address, inputs).Frame;
        Assert.Empty(draws);
        Assert.Equal(resource, Task(frame, fixture.Address).ReadDword(0x50));
        Assert.Equal(unchecked((ushort)x), Task(frame, fixture.Address).ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)y), Task(frame, fixture.Address).ReadWord(0x1E));
        NativeBattleScanReticleStep terminal = NativeBattleScanOpeningChildren.Advance84DDD0(frame with { Highlight269AA90 = 0 }, fixture.Address, inputs);
        Assert.Single(draws);
        Assert.Equal(2, terminal.Result);
        Assert.Equal((ushort)2, Task(terminal.Frame, fixture.Address).ReadWord(0x24));
    }

    [Fact]
    public void Out_of_range_signed_corner_preserves_actual_pointer_and_coordinates()
    {
        var fixture = Fixture(0x84DDD0);
        NativeBattleScanReticleFrame frame = Modify(fixture.Frame, fixture.Address, task => task.WriteWord(0x74, 0xFFFF).WriteDword(0x50, 0x12345678).WriteDword(0x1C, 0x8899AABB));
        frame = NativeBattleScanOpeningChildren.Advance84DDD0(frame, fixture.Address, Inputs(fixture.Packets, [])).Frame;
        Assert.Equal(0x12345678u, Task(frame, fixture.Address).ReadDword(0x50));
        Assert.Equal(0x8899AABBu, Task(frame, fixture.Address).ReadDword(0x1C));
        Assert.Equal((ushort)4096, Task(frame, fixture.Address).ReadWord(0x64));
    }

    [Fact]
    public void Bracket_copies_real_parent_dwords_then_runs_inclusive_thirty_one_and_five_draws()
    {
        var fixture = Fixture(0x84E010);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Scan.Effect.Pools);
        uint parent = Task(fixture.Frame, fixture.Address).ReadDword(0x18);
        hierarchy = hierarchy.Write(parent, hierarchy.Read(parent).WriteDword(0x1C, 0x00220011).WriteDword(0x20, 0xBEEF0123));
        NativeBattleScanReticleFrame frame = fixture.Frame with { Scan = fixture.Frame.Scan with { Effect = fixture.Frame.Scan.Effect with { Pools = hierarchy.Pools } } };
        List<NativeBattleModulePacketBatch> draws = [];
        NativeBattleScanOpeningInputs inputs = Inputs(fixture.Packets, draws);
        frame = NativeBattleScanOpeningChildren.Advance84E010(frame, fixture.Address, inputs).Frame;
        Assert.Empty(draws);
        Assert.Equal(0u, Task(frame, fixture.Address).ReadDword(0x1C));
        Assert.Equal(0xBEEF0123u, Task(frame, fixture.Address).ReadDword(0x20));
        for (int callback = 0; callback < 31; callback++)
        {
            frame = NativeBattleScanOpeningChildren.Advance84E010(frame, fixture.Address, inputs).Frame;
        }
        Assert.Equal(31, draws.Count);
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)4, Task(frame, fixture.Address).ReadWord(0x66));
        Assert.Equal(0x00220011u, Task(frame, fixture.Address).ReadDword(0x1C));
        for (int callback = 0; callback < 5; callback++)
        {
            frame = NativeBattleScanOpeningChildren.Advance84E010(frame, fixture.Address, inputs).Frame;
        }
        Assert.Equal(36, draws.Count);
        Assert.Equal((byte)3, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)12, Task(frame, fixture.Address).ReadWord(0x1C));
        Assert.Equal((ushort)29, Task(frame, fixture.Address).ReadWord(0x1E));
        Assert.Equal((ushort)0x2400, Task(frame, fixture.Address).ReadWord(0x64));
        Assert.Equal(0xBEEF0123u, Task(frame, fixture.Address).ReadDword(0x20));
        NativeBattleScanReticleStep completed = NativeBattleScanOpeningChildren.Advance84E010(frame with { Highlight269AA90 = 0 }, fixture.Address, inputs);
        Assert.Equal(37, draws.Count);
        Assert.Equal(2, completed.Result);
    }

    [Fact]
    public void Line_packets_use_negative_Q12_truncation_WORD_wrap_and_C7_zero_precision_proxies()
    {
        var fixture = Fixture(0x84DDD0);
        NativeBattleModulePoolTask task = Task(fixture.Frame, fixture.Address).WriteWord(0x1C, (ushort)short.MaxValue).WriteWord(0x1E, 0x8000).WriteWord(0x64, 1);
        NativeBattleModulePacketBatch batch = NativeBattleScanOpeningChildren.Lines84DF00(task, Records());
        Assert.Equal(32, batch.AllocationBytes);
        NativeBattleAllocatedPrimitive first = batch.Primitives[0];
        Assert.Equal((ushort)0, first.Flags20);
        Assert.Equal(new[] { 0, 0, 0, 0 }, first.Primitive.VertexPayload4);
        Assert.Equal(0x40563412u, first.Primitive.Words[1]);
        Assert.Equal(unchecked((ushort)(short.MaxValue + 160)), (ushort)first.Primitive.Words[2]);
        Assert.Equal(unchecked((ushort)(short.MinValue + 108)), (ushort)(first.Primitive.Words[2] >> 16));
        Assert.Equal((ushort)1, Assert.Single(NativeBattleScanReticleLines.Ring84D6D0(0, 0, 1, 0, 1).Packets.Primitives).Flags20);
    }

    [Fact]
    public void Ring_grows_four_segments_per_callback_then_adds_diagonal_before_sprite_and_horizontal_only_next_phase()
    {
        var fixture = Fixture(0x84E3D0);
        NativeBattleScanReticleFrame frame = fixture.Frame;
        List<NativeBattleModulePacketBatch> draws = [];
        NativeBattleScanOpeningInputs inputs = Inputs(fixture.Packets, draws);
        frame = NativeBattleScanOpeningChildren.Advance84E3D0(frame, fixture.Address, inputs).Frame;
        Assert.Empty(draws);
        for (int callback = 1; callback <= 6; callback++)
        {
            frame = NativeBattleScanOpeningChildren.Advance84E3D0(frame, fixture.Address, inputs).Frame;
            Assert.Equal(callback * 4, draws[^1].Primitives.Length);
        }
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        draws.Clear();
        frame = NativeBattleScanOpeningChildren.Advance84E3D0(frame, fixture.Address, inputs).Frame;
        Assert.Equal(new[] { 24, 1 }, draws.Select(draw => draw.Primitives.Length));
        Assert.Equal(0x84E510u, frame.Scan.Effect.Pools[3].Slots[0].Callback);
        Assert.Equal(fixture.Address, frame.Scan.Effect.Pools[3].Slots[0].ReadDword(0x18));
        Assert.Equal((byte)1, Task(frame, fixture.Address).ReadByte(0x28));
        draws.Clear();
        NativeBattleScanReticleStep terminal = NativeBattleScanOpeningChildren.Advance84E3D0(frame with { Highlight269AA90 = 0 }, fixture.Address, inputs);
        Assert.Equal(new[] { 24, 1, 1 }, draws.Select(draw => draw.Primitives.Length));
        Assert.Equal(0, terminal.Result);
        Assert.Equal((byte)4, Task(terminal.Frame, fixture.Address).ReadByte(0x29));
        uint start = draws[2].Primitives[0].Primitive.Words[2];
        uint end = draws[2].Primitives[0].Primitive.Words[3];
        Assert.Equal(unchecked((ushort)((ushort)start + 88)), (ushort)end);
        Assert.Equal(start >> 16, end >> 16);
    }

    [Fact]
    public void Sprite_draws_frame_zero_on_init_keeps_frame_31_then_switches_resource_immediately_and_draws_terminal()
    {
        var fixture = Fixture(0x84E510);
        List<NativeBattleModulePacketBatch> draws = [];
        NativeBattleScanOpeningInputs inputs = Inputs(fixture.Packets, draws);
        NativeBattleScanReticleFrame frame = NativeBattleScanOpeningChildren.Advance84E510(fixture.Frame, fixture.Address, inputs).Frame;
        Assert.Single(draws);
        Assert.NotEmpty(draws[0].Primitives);
        Assert.Equal((ushort)0, Task(frame, fixture.Address).ReadWord(0x50));
        for (int callback = 1; callback <= 31; callback++)
        {
            frame = NativeBattleScanOpeningChildren.Advance84E510(frame, fixture.Address, inputs).Frame;
        }
        Assert.Equal((ushort)31, Task(frame, fixture.Address).ReadWord(0x50));
        Assert.Equal(0x15D07D4u, Task(frame, fixture.Address).ReadDword(0x4C));
        frame = NativeBattleScanOpeningChildren.Advance84E510(frame, fixture.Address, inputs).Frame;
        Assert.Equal(0x15D0AF8u, Task(frame, fixture.Address).ReadDword(0x4C));
        Assert.Equal((ushort)0, Task(frame, fixture.Address).ReadWord(0x50));
        Assert.Equal((byte)0, (byte)(Task(frame, fixture.Address).ReadByte(0x26) & 4));
        int before = draws.Count;
        NativeBattleScanReticleStep terminal = NativeBattleScanOpeningChildren.Advance84E510(frame with { Highlight269AA90 = 0 }, fixture.Address, inputs);
        Assert.Equal(before + 1, draws.Count);
        Assert.Equal(2, terminal.Result);
        Assert.Equal((ushort)1, Task(terminal.Frame, fixture.Address).ReadWord(0x50));
    }

    [Fact]
    public void Sprite_uses_direct_matrix_and_actual_roll_with_no_battle_view_composition()
    {
        var fixture = Fixture(0x84E510);
        NativeBattleModulePoolTask task = Task(fixture.Frame, fixture.Address).WriteWord(0x1C, 400).WriteWord(0x1E, 192).WriteWord(0x20, 2304);
        NativeBattleCameraProjection first = new(NativeBattleCuragaMatrices.Identity8DD770(), 216, 160, 108);
        NativeBattleCameraProjection differentView = first with { View = NativeBattleCuragaMatrices.Identity8DD770() with { X = 20000, Z = -10000 } };
        NativeBattleModuleResource resource = Sprite(0x15D07D4);
        NativeBattleModulePacketBatch expected = NativeBattleScanOpeningChildren.Sprite84E580(task, resource, first, 0, 1024);
        Assert.NotEmpty(expected.Primitives);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(NativeBattleScanOpeningChildren.Sprite84E580(task, resource, differentView, 0, 1024)));
        Assert.NotEqual(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(NativeBattleScanOpeningChildren.Sprite84E580(task, resource, first, 1024, 1024)));
    }

    [Fact]
    public void Opening_child_JSON_continuation_keeps_parent_aliases_packets_and_terminal_order()
    {
        var fixture = Fixture(0x84E010);
        NativeBattleScanReticleFrame first = NativeBattleScanOpeningChildren.Advance84E010(fixture.Frame, fixture.Address, Inputs(fixture.Packets, [])).Frame;
        NativeBattleScanReticleFrame clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleScanReticleFrame>(NativeBattleTestSnapshots.Json(first))!;
        List<NativeBattleModulePacketBatch> leftDraws = [];
        List<NativeBattleModulePacketBatch> rightDraws = [];
        NativeBattleScanReticleStep left = NativeBattleScanOpeningChildren.Advance84E010(first, fixture.Address, Inputs(fixture.Packets, leftDraws));
        NativeBattleScanReticleStep right = NativeBattleScanOpeningChildren.Advance84E010(clone, fixture.Address, Inputs(fixture.Packets, rightDraws));
        Assert.Equal(NativeBattleTestSnapshots.Json(left), NativeBattleTestSnapshots.Json(right));
        Assert.Equal(NativeBattleTestSnapshots.Json(leftDraws), NativeBattleTestSnapshots.Json(rightDraws));
    }

    private static NativeBattleScanOpeningInputs Inputs(NativeBattleActionPacketsState packets, List<NativeBattleModulePacketBatch> draws) => new(packets, _ => Records(), Sprite, new(NativeBattleCuragaMatrices.Identity8DD770(), 216, 160, 108), 0, (frame, batch) =>
    {
        draws.Add(batch);
        return frame with { Scan = frame.Scan with { State = frame.Scan.State with { CurrentDrawOffset = (frame.Scan.State.CurrentDrawOffset ?? 0) + batch.AllocationBytes } } };
    });

    private static ImmutableArray<byte> Records()
    {
        byte[] bytes = new byte[48];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), -1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), -1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 10);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 10);
        bytes[12] = 0x12;
        bytes[13] = 0x34;
        bytes[14] = 0x56;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 0xFF);
        return bytes.ToImmutableArray();
    }

    private static NativeBattleModuleResource Sprite(uint address)
    {
        int count = address == 0x15D07D4 ? 32 : 4;
        int data = 12 + count * 2;
        byte[] bytes = new byte[data + 24];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)count);
        for (int frame = 0; frame < count; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)data);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(data - 2), -1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(data), 1);
        bytes[data + 4] = 2;
        bytes[data + 5] = 128;
        bytes[data + 6] = 2;
        bytes[data + 7] = 0x2C;
        return new(0, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }

    private static NativeBattleModulePoolTask Task(NativeBattleScanReticleFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Scan.Effect.Pools).Read(address);
    private static NativeBattleScanReticleFrame Modify(NativeBattleScanReticleFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        hierarchy = hierarchy.Write(address, change(hierarchy.Read(address)));
        return frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
    }

    private static (NativeBattleScanReticleFrame Frame, uint Address, NativeBattleActionPacketsState Packets) Fixture(uint callback)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess(), effect, packets);
        NativeBattleEffectHierarchy hierarchy = new(scan.Effect.Pools);
        int pool = callback == 0x84E510 ? 3 : 2;
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(pool, callback, callback == 0x84E510 ? 0x58 : 0x78, hierarchy.Address(0, 0), (_, _, _) => new(2, 4));
        scan = scan with { Effect = scan.Effect with { Pools = child.Hierarchy.Pools } };
        return (new(scan, 1), child.Address, packets);
    }
}
