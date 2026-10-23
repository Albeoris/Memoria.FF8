using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleStare45Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStare45ProgrammeTests
{
    [Theory]
    [InlineData(0, 0x15)]
    [InlineData(1, 0x16)]
    public void Caster_initialization_uses_actual_bone_selector_and_draws_without_a_delay(int side, int selector)
    {
        var frame = Frame(3);
        frame = Write(frame, 0x2654730, Read(frame, 0x2654730).WriteWord(0x8A, (ushort)side).WriteWord(0x22, 0xBEEF).WriteWord(0x94 + 452, 0xCAFE));
        int calls = 0;
        var actual = InitializeCasterProgramme836DC0(frame, 0x2654730, Resources(), Inputs() with
        {
            ReadBone502170 = (current, actor, bone, distance) =>
            {
                calls++;
                Assert.Equal(3, actor);
                Assert.Equal(selector, bone);
                Assert.Equal(0, distance);
                return new(current, PointView(new(100, 200, 300)));
            }
        });
        var task = Read(actual, 0x2654730);
        Assert.Equal(1, calls);
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((ushort)0xF00, task.ReadWord(0x60));
        Assert.Equal((ushort)100, task.ReadWord(0x1C));
        Assert.Equal((ushort)200, task.ReadWord(0x1E));
        Assert.Equal((ushort)236, task.ReadWord(0x20));
        Assert.Equal((ushort)0xBEEF, task.ReadWord(0x22));
        Assert.Equal((ushort)0xCAFE, task.ReadWord(0x94 + 452));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Continuation_reads_live_bone_and_returns_remaining_without_duplicating_upper_done()
    {
        var frame = Frame(3);
        var resources = Resources();
        byte[] source = resources.ProgrammeA.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(256), 0);
        resources = resources with { ProgrammeA = resources.ProgrammeA with { Data = source.ToImmutableArray() } };
        frame = InitializeCasterProgramme836DC0(frame, 0x2654730, resources, Inputs());
        frame = Write(frame, 0x2654730, Read(frame, 0x2654730).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0));
        NativeBattleGeometryRegisterStep expected = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters!);
        expected = expected with { Geometry = NativeBattleGeometry.LoadMatrix(expected.Geometry, NativeBattleCuragaMatrices.Identity8DD770(), true, false, null) };
        foreach ((ushort x, ushort y, ushort z) in new (ushort, ushort, ushort)[] { (4096, 0, 0), (0, 4096, 0), (0, 0, 4096) })
        {
            expected = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadIrColumn(expected, x, y, z), false, true);
        }
        var actual = AdvanceCasterProgramme8394C0(frame, 0x2654730, resources, Inputs((_, _) => throw new InvalidOperationException("The inactive programme has no drawable nodes.")) with { ReadBone502170 = (current, _, _, _) => new(current, PointView(new(1000, 2000, 3000))) });
        Assert.Equal(0, actual.Remaining);
        Assert.Equal((ushort)1000, Read(actual.Frame, 0x2654730).ReadWord(0x1C));
        Assert.Equal((ushort)2936, Read(actual.Frame, 0x2654730).ReadWord(0x20));
        Assert.Equal(Read(frame, 0x2654730).ReadByte(0x29), Read(actual.Frame, 0x2654730).ReadByte(0x29));
        Assert.Equal(JsonSerializer.Serialize(expected.Geometry), JsonSerializer.Serialize(actual.Frame.Memory.GeometryRegisters));
        Assert.Equal(JsonSerializer.Serialize(expected.Colors), JsonSerializer.Serialize(actual.Frame.Memory.ScriptedColorRegisters));
        var restored = JsonSerializer.Deserialize<NativeBattleStare45Frame>(JsonSerializer.Serialize(actual.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(actual.Frame), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Node_shape_interpolation_preserves_scratch_W_and_retained_temporary_color_when_fade_zero()
    {
        var frame = Frame(3, 20);
        frame = frame with { State = frame.State.WriteWord(0x265542E, 0xBEEF) };
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, NativeBattleTemporaryMemory.Address + 8, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0xCAFEBABEu)), 4) };
        var resources = Resources();
        var node = Node(ratio: 1024);
        var actual = DrawProgrammeNode45(frame, resources.ProgrammeA, node, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), -128, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleStare45DrawMesh104>(operation);
            Assert.Equal(0x2655428u, draw.VertexAddress);
            Assert.Equal((ushort)150, current.State.ReadWord(0x2655428));
            Assert.Equal((ushort)0xBEEF, current.State.ReadWord(0x265542E));
            Assert.Equal(0xCAFEBABEu, U(Context(current, draw.TemporaryAddress, 104), 8));
            return current;
        }));
        Assert.Equal(1024u, actual.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4096)]
    public void Endpoint_shape_selection_uses_original_rows_without_touching_scratch(int ratio)
    {
        var frame = Frame(3, 20);
        frame = frame with { State = frame.State.WriteDword(0x2655428, 0x12345678) };
        var resources = Resources();
        var actual = DrawProgrammeNode45(frame, resources.ProgrammeA, Node(ratio), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), 0, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleStare45DrawMesh104>(operation);
            Assert.Equal(0x17C4DA0u + 32u + 12u + (ratio == 0 ? 0u : 32u), draw.VertexAddress);
            return current;
        }));
        Assert.Equal(0x12345678u, actual.State.ReadDword(0x2655428));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x200)]
    [InlineData(0x1000)]
    public void Position_branch_selects_actual_current_basis_or_direct_point_and_always_adds_caller_translation(uint header)
    {
        var frame = Frame(3);
        var resources = Resources();
        var currentBasis = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 1024);
        frame = frame with { State = frame.State.WriteView(0x2655758, PackedArena(NativeBattlePackedMatrix32.FromMatrix(currentBasis, 0xBEEF)), 32) };
        var caller = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 };
        var point = new NativeBattleVector(20, 30, 40);
        var actual = DrawProgrammeNode45(frame, resources.ProgrammeA, Node(0, header, point), NativeBattlePackedMatrix32.FromMatrix(caller, null), 0, resources, Inputs((current, _) =>
        {
            var expected = header == 0x1000 ? NativeBattleGfMatrices.RotateShort56C4F0(currentBasis, point) : point;
            Assert.Equal(100 + expected.X, current.Memory.GeometryRegisters!.Translation[0]);
            Assert.Equal(200 + expected.Y, current.Memory.GeometryRegisters.Translation[1]);
            Assert.Equal(300 + expected.Z, current.Memory.GeometryRegisters.Translation[2]);
            short[] expectedRotation = header == 0x1000 ? currentBasis.Rotation.ToArray() : caller.Rotation.ToArray();
            for (int index = 0; index < 9; index++)
            {
                Assert.Equal(expectedRotation[index], BitConverter.ToInt16(current.Memory.GeometryRegisters.Rotation20.Skip(index * 2).Take(2).Select(value => value!.Value).ToArray()));
            }
            return current;
        }));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Invisible_node_exits_before_any_resource_or_temporary_read()
    {
        byte[] data = new byte[44];
        Assert.Null(DrawProgrammeNode45(null!, null!, new(0, 0, data.ToImmutableArray()), null!, 0, null!, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x40000)]
    public void Alternate_Euler_header_selects_7015B0_axis_order(uint header)
    {
        var frame = Frame(3);
        var resources = Resources();
        var angles = new NativeBattleVector(128, 256, 512);
        var actual = DrawProgrammeNode45(frame, resources.ProgrammeA, Node(0, header, angles: angles), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), 0, resources, Inputs());
        NativeBattleActionMatrix expected = header == 0 ? NativeBattleGfMatrices.Euler701310(angles) : NativeBattleGfMatrices.Euler7015B0(angles);
        for (int index = 0; index < 9; index++)
        {
            Assert.Equal(expected.Rotation[index], BitConverter.ToInt16(actual.Memory.GeometryRegisters!.Rotation20.Skip(index * 2).Take(2).Select(value => value!.Value).ToArray()));
        }
    }

    internal static NativeBattleStare45Frame Frame(int poolIndex, ushort age = 0)
    {
        var pools = NativeBattleStare45Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        var pool = pools[poolIndex];
        (pool, _) = pool.Append(poolIndex == 3 ? 0x836D60u : 0x839590u);
        pools = pools.SetItem(poolIndex, pool);
        var effect = new NativeBattleOriginalEffectState(45, 44, new(45, 0x836590, 0x836570), null, 0x2654588) { Pools = pools };
        var frame = new NativeBattleStare45Frame(NativeBattleStare45MeshContext104Tests.Memory(2000), effect, new(123), NativeBattleStare45State.FromColdProcess() with { InactiveBanks = Enumerable.Repeat<NativeBattleEffectArena?>(null, 6).ToImmutableArray() }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        var task = Read(frame, pool.BankAddress).WriteWord(0x24, age);
        if (poolIndex == 3)
        {
            task = task.WriteDword(0x74, 0x17C4DA0).WriteDword(0x78, 452).WriteByte(0x2C, 3).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
        }
        return Write(frame, pool.BankAddress, task);
    }

    internal static NativeBattleStare45Inputs Inputs(Func<NativeBattleStare45Frame, NativeBattleStare45Operation, NativeBattleStare45Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, actor) => new(actor, 0) { Rotation14 = 0 }, (current, _, _, _) => new(current, PointView(new(0, 0, 0))), _ => NativeBattleActionPacketsState.Fresh(), execute ?? ((current, _) => current));
    internal static NativeBattleStare45Resources Resources() => new(Source(0x17C4DA0), Source(0x17C6A14), null!, 0);
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleGfAnimationNode Node(int ratio, uint header = 0, NativeBattleVector point = default, NativeBattleVector angles = default)
    {
        byte[] data = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), point.X);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), point.Z);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16), angles.X);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(18), angles.Y);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20), angles.Z);
        for (int at = 24; at <= 28; at += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(at), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), (short)ratio);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
        return new(0, 0, data.ToImmutableArray());
    }
    private static NativeBattleGfAnimationSource Source(uint address)
    {
        byte[] data = new byte[288];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 256);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8 + index * 4), 32);
        }
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(32), 76);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(36), 4);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(40), 2);
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 4; vertex++)
            {
                int at = 44 + (shape * 4 + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(at), (short)(100 + shape * 200));
            }
        }
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(120), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(124), 0x2E112233);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(128 + corner * 2), (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(256), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(260), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(264), 16);
        return new(address, data.ToImmutableArray());
    }
}
