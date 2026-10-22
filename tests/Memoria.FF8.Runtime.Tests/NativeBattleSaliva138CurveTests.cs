using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSaliva138CurveTests
{
    private const uint ArenaAddress = 0x20DFAB8;
    private const uint WorkingAddress = ArenaAddress + 0x400;

    [Fact]
    public void Preprocess_scans_beyond_vertex_Z_and_writes_only_108_W_words_without_reseeding()
    {
        byte[] source = Mesh();
        for (int index = 0; index < 108; index++)
        {
            Put16(source, 12 + index * 8, unchecked((short)(index < 2 ? 0 : index - 2)));
            Put16(source, 14 + index * 8, -2345);
        }
        Put32(source, 0x36C, 210);
        ImmutableArray<byte> actual = NativeBattleSaliva138Controller.Preprocess6AD430(source.ToImmutableArray());
        Assert.Equal((short)2048, I16(actual, 14 + 107 * 8));
        Assert.Equal((short)0, I16(actual, 14));
        for (int index = 0; index < source.Length; index++)
        {
            bool w = index >= 14 && index < 8 + 108 * 8 && (index - 14) % 8 < 2;
            if (!w)
            {
                Assert.Equal(source[index], actual[index]);
            }
        }
        Assert.Equal(actual, NativeBattleSaliva138Controller.Preprocess6AD430(actual));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 4096)]
    [InlineData(1024, 0, 0, 4096, 0)]
    [InlineData(1024, 1024, -4096, 0, 0)]
    public void Direction_has_native_signed_trig_product_order(int first, int second, short x, short y, short z)
    {
        Assert.Equal(new NativeBattleVector(x, y, z), NativeBattleSaliva138Controller.Direction6ADFA0(first, second));
    }

    [Fact]
    public void Deformation_uses_canonical_saturated_IR_and_inclusive_table_endpoint_preserving_selected_W()
    {
        byte[] mesh = Mesh();
        for (int index = 0; index < 108; index++)
        {
            Put16(mesh, 8 + index * 8, 100);
            Put16(mesh, 10 + index * 8, 40);
            Put16(mesh, 12 + index * 8, 900);
            Put16(mesh, 14 + index * 8, 4096);
        }
        NativeBattleSaliva138Frame frame = Frame(mesh);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770() with { X = 40000 };
        frame = NativeBattleSaliva138Controller.LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(identity, 0xCAFE));
        int selected = checked((int)(WorkingAddress - ArenaAddress)) + MorphOffset;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(selected + 6, 2).Write(selected + 14, BitConverter.GetBytes((ushort)0xBEEF)) } };
        int[] table = new int[2049];
        table[2048] = 65536;
        NativeBattleSaliva138Resources resources = Resources(mesh) with { DeformationTable = table.ToImmutableArray() };
        NativeBattleSaliva138Frame actual = NativeBattleSaliva138Controller.Deform6AAC80(frame, 3, resources);
        Assert.Equal(short.MaxValue, ArenaWord(actual, selected));
        Assert.Equal((short)37, ArenaWord(actual, selected + 2));
        Assert.Equal((short)900, ArenaWord(actual, selected + 4));
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena!.Read(selected + 6, 2));
        Assert.Equal(unchecked((short)0xBEEF), ArenaWord(actual, selected + 14));
        Assert.Equal(40, actual.Memory.ScriptedColorRegisters!.Ir[1]!.Value);
        Assert.Equal(40100, actual.Memory.ScriptedColorRegisters.Mac[0]!.Value);
        Assert.Equal(frame.State.ReadMesh(), actual.State.ReadMesh());
        Assert.Equal(frame.Random, actual.Random);
        NativeBattleSaliva138Frame restored = JsonSerializer.Deserialize<NativeBattleSaliva138Frame>(JsonSerializer.Serialize(actual))!;
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Missing_current_mesh_fails_at_deformation_without_cold_hydration()
    {
        byte[] mesh = Mesh();
        NativeBattleSaliva138Frame frame = Frame(mesh);
        frame = frame with { State = frame.State with { Mesh = null, PendingColdSources = false } };
        Assert.Throws<NotSupportedException>(() => NativeBattleSaliva138Controller.Deform6AAC80(frame, 1, Resources(mesh)));
    }

    [Fact]
    public void Three_point_ribbon_uses_32_byte_layout_life_width_gray_without_shift_and_sparse_52_byte_packets()
    {
        NativeBattleSaliva138Frame frame = Frame(Mesh());
        frame = Point(frame, 0, 100, 200, 512, 200, 128, PointAddress(1));
        frame = Point(frame, 1, 116, 200, 512, 200, 64, PointAddress(2));
        frame = Point(frame, 2, 132, 200, 512, 200, 32, 0);
        string registers = JsonSerializer.Serialize(new { frame.Memory.GeometryRegisters, frame.Memory.ScriptedColorRegisters });
        var actual = NativeBattleSaliva138Controller.DrawCurveRibbon(frame, PointAddress(0), 3, Resources(Mesh()));
        Assert.Equal(104, actual.Batch.AllocationBytes);
        Assert.Equal(new[] { 0, 52 }, actual.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        NativeBattleGfGeometryAttempt packet = actual.Batch.Attempts[0];
        Assert.Equal(145, packet.Primitive!.Bucket);
        Assert.Empty(packet.Primitive.VertexPayload4);
        Assert.Equal(0x3E808080u, U32(packet.Data, 4));
        Assert.Equal(0x00404040u, U32(packet.Data, 28));
        Assert.Equal(0x3C14C008u, U32(packet.Data, 12));
        Assert.Equal(0x00BAC038u, U32(packet.Data, 24));
        Assert.Equal((ushort)0xC038, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(48)));
        Assert.Equal(0x00C70064u, U32(packet.Data, 8));
        Assert.Equal(0x00C90064u, U32(packet.Data, 20));
        for (int index = 0; index < 52; index++)
        {
            Assert.Equal(index is 38 or 39 or 50 or 51 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, packet.WriteMask[index]);
        }
        Assert.Equal(registers, JsonSerializer.Serialize(new { actual.Frame.Memory.GeometryRegisters, actual.Frame.Memory.ScriptedColorRegisters }));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(PointAddress(1), BinaryPrimitives.ReadUInt32LittleEndian(actual.Frame.Memory.Arena!.Read(PointOffset(0) + 0x1C, 4)));
    }

    [Fact]
    public void Zero_first_edge_requires_native_unobserved_previous_direction_only_when_later_edge_consumes_it()
    {
        NativeBattleSaliva138Frame frame = Frame(Mesh());
        frame = Point(frame, 0, 100, 200, 0, 200, null, PointAddress(1));
        frame = Point(frame, 1, 100, 200, 0, 200, null, PointAddress(2));
        frame = Point(frame, 2, 100, 200, 0, 200, null, 0);
        NativeBattleSaliva138Resources resources = Resources(Mesh()) with { DistanceTable = new byte[16384].ToImmutableArray() };
        Assert.Throws<NotSupportedException>(() => NativeBattleSaliva138Controller.DrawCurveRibbon(frame, PointAddress(0), 3, resources));
    }

    [Fact]
    public void Cyclic_or_wrongly_counted_curve_fails_without_replacing_retained_bytes()
    {
        NativeBattleSaliva138Frame frame = Frame(Mesh());
        frame = Point(frame, 0, 100, 200, 512, 200, 64, PointAddress(0));
        Assert.Throws<InvalidDataException>(() => NativeBattleSaliva138Controller.DrawCurveRibbon(frame, PointAddress(0), 3, Resources(Mesh())));
        frame = Point(frame, 0, 100, 200, 512, 200, 64, 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleSaliva138Controller.DrawCurveRibbon(frame, PointAddress(0), 3, Resources(Mesh())));
    }

    [Fact]
    public void First_curve_callback_observes_actual_mouth_and_does_not_read_unknown_head_or_emit()
    {
        NativeBattleSaliva138Frame frame = WithCurve(Frame(Mesh()), 0);
        uint address = WorkingAddress + CurveOffset;
        NativeBattleModulePoolTask task = NativeBattleSaliva138Controller.Read(frame, address).MarkPayloadUnknown(0x14, CurveStride - 0x14);
        frame = NativeBattleSaliva138Controller.Write(frame, address, task);
        int calls = 0;
        NativeBattleSaliva138Inputs inputs = Inputs(frame, (_, _) => throw new InvalidOperationException()) with
        {
            ReadMouth = current =>
            {
                calls++;
                return new(current with { State = current.State.WriteDword(CursorGlobal, 0x12345678) }, new(12, -34, 900));
            }
        };
        var actual = NativeBattleSaliva138Controller.AdvanceCurve(frame, address, Resources(Mesh()), inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal(1, calls);
        task = NativeBattleSaliva138Controller.Read(actual.Frame, address);
        Assert.Equal((ushort)1, task.ReadWord(0x12));
        Assert.Equal((ushort)12, task.ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)-34), task.ReadWord(0x1E));
        Assert.Equal((ushort)900, task.ReadWord(0x20));
        Assert.Equal((ushort)0, task.ReadWord(0x22));
        Assert.Equal(0x12345678u, actual.Frame.State.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => task.ReadDword(0x28));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Two_births_draw_custom_between_allocations_and_keep_callback_links_and_unknown_selected_W()
    {
        NativeBattleSaliva138Frame frame = WithCurve(Frame(Mesh()), 1);
        uint address = WorkingAddress + CurveOffset;
        NativeBattleModulePoolTask task = NativeBattleSaliva138Controller.Read(frame, address).WriteWord(12, 0).WriteWord(14, 0).WriteWord(16, 2000).WriteWord(0x14, 100).WriteWord(0x16, 100).WriteWord(0x18, 2000).WriteWord(0x1A, 1024).WriteWord(0x1C, 0).WriteWord(0x1E, 0).WriteWord(0x20, 2000).WriteWord(0x24, 0);
        frame = NativeBattleSaliva138Controller.Write(frame, address, task);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < PointCount; index++)
        {
            arena = arena.Write(PointOffset(index) + 6, BitConverter.GetBytes(ushort.MaxValue));
        }
        int selected = checked((int)(WorkingAddress - ArenaAddress)) + MorphOffset;
        arena = arena.MarkUnknown(selected, MorphBytes);
        frame = frame with { Memory = frame.Memory with { Arena = arena }, State = frame.State with { ScaleMatrix32 = frame.State.ScaleMatrix32!.MarkUnknown(18, 14) } };
        int calls = 0;
        uint child = 0;
        NativeBattleSaliva138Inputs inputs = Inputs(frame, (current, operation) =>
        {
            NativeBattleSaliva138DrawCustom92 custom = Assert.IsType<NativeBattleSaliva138DrawCustom92>(operation);
            calls++;
            Assert.Equal((short)200, ArenaWord(current, PointOffset(0) + 6));
            Assert.Equal((short)-1, ArenaWord(current, PointOffset(1) + 6));
            Assert.Equal(MeshAddress, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(custom.TemporaryAddress, 4)));
            Assert.Equal(WorkingAddress + MorphOffset, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging.Read(custom.TemporaryAddress + 4, 4)));
            Assert.Throws<NotSupportedException>(() => current.Memory.Arena!.Read(selected + 6, 2));
            Assert.Null(current.Memory.GeometryRegisters!.Rotation20[18]);
            (current, child) = NativeBattleSaliva138Controller.Append(current, 0, CurveCallback);
            return current;
        });
        var actual = NativeBattleSaliva138Controller.AdvanceCurve(frame, address, Resources(Mesh()), inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal(1, calls);
        task = NativeBattleSaliva138Controller.Read(actual.Frame, address);
        Assert.Equal((ushort)2, task.ReadWord(0x12));
        Assert.Equal((ushort)2, task.ReadWord(0x24));
        Assert.Equal(PointAddress(0), task.ReadDword(0x28));
        Assert.Equal(PointAddress(1), task.ReadDword(0x2C));
        Assert.NotEqual(0u, child);
        Assert.Equal(1, task.Next);
        Assert.Equal((short)124, ArenaWord(actual.Frame, PointOffset(0) + 0x18));
        Assert.Equal((short)124, ArenaWord(actual.Frame, PointOffset(1) + 0x18));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x22));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Terminal_trim_frees_only_old_head_and_returns_two_without_age_increment_or_projection()
    {
        NativeBattleSaliva138Frame frame = WithCurve(Frame(Mesh()), 10);
        uint address = WorkingAddress + CurveOffset;
        frame = Point(frame, 0, 0, 0, 0, 200, 32, PointAddress(1));
        frame = Point(frame, 1, 0, 0, 0, 200, 32, 0);
        frame = NativeBattleSaliva138Controller.Write(frame, address, NativeBattleSaliva138Controller.Read(frame, address).WriteWord(0x24, 2).WriteDword(0x28, PointAddress(0)).WriteDword(0x2C, PointAddress(1)));
        NativeBattleSaliva138Inputs inputs = Inputs(frame, (_, _) => throw new InvalidOperationException());
        var actual = NativeBattleSaliva138Controller.AdvanceCurve(frame, address, Resources(Mesh()), inputs);
        Assert.Equal(2, actual.Result);
        NativeBattleModulePoolTask task = NativeBattleSaliva138Controller.Read(actual.Frame, address);
        Assert.Equal((ushort)10, task.ReadWord(0x12));
        Assert.Equal((ushort)1, task.ReadWord(0x24));
        Assert.Equal(PointAddress(1), task.ReadDword(0x28));
        Assert.Equal((short)-1, ArenaWord(actual.Frame, PointOffset(0) + 6));
        Assert.Equal((short)200, ArenaWord(actual.Frame, PointOffset(1) + 6));
        Assert.Equal((short)28, ArenaWord(actual.Frame, PointOffset(1) + 0x18));
    }

    private static NativeBattleSaliva138Frame WithCurve(NativeBattleSaliva138Frame frame, ushort age)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(CurveDescriptor, WorkingAddress + CurveOffset, CurveStride, CurveCount);
        (pool, int slot) = pool.Append(CurveCallback);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0x12, age);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        return frame with { Effect = frame.Effect with { Pools = [pool] } };
    }
    private static NativeBattleSaliva138Inputs Inputs(NativeBattleSaliva138Frame frame, Func<NativeBattleSaliva138Frame, NativeBattleSaliva138Operation, NativeBattleSaliva138Frame> execute) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => throw new InvalidOperationException(), (_, _, _, _, _, _) => throw new InvalidOperationException(), execute);

    private static NativeBattleSaliva138Frame Frame(byte[] mesh)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleSaliva138State state = NativeBattleSaliva138State.FromColdProcess().WriteDword(WorkingGlobal, WorkingAddress) with { Mesh = mesh.ToImmutableArray(), PendingColdSources = false };
        state = state.WriteView(MatrixAddress, NativeBattleEffectArena.Clear().Write(0, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0).Bytes.AsSpan()), 32);
        NativeBattleOriginalEffectState effect = new(138, 137, new(138, Entry, Initializer), null, RootDescriptor);
        return new(memory, effect, new(123), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleSaliva138Resources Resources(byte[] mesh)
    {
        byte[] distances = new byte[16384];
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                distances[y * 128 + x] = unchecked((byte)Math.Sqrt(x * x + y * y));
            }
        }
        return new(new(MainActAddress, []), new(SecondaryActAddress, []), new(ImpactActAddress, []), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, MeshAddress, mesh.ToImmutableArray()), new(0, MatrixAddress, 0, 0, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0).Bytes), new("unused", []), 0, new short[18].ToImmutableArray(), new byte[8].ToImmutableArray(), new int[2049].ToImmutableArray(), distances.ToImmutableArray());
    }
    private static byte[] Mesh()
    {
        byte[] result = new byte[MeshBytes];
        Put32(result, 0, 0x368);
        Put32(result, 4, 108);
        return result;
    }
    private static NativeBattleSaliva138Frame Point(NativeBattleSaliva138Frame frame, int index, short x, short y, short depth, short life, short? intensity, uint next)
    {
        int at = PointOffset(index);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(at + 6, BitConverter.GetBytes(life)).Write(at + 8, BitConverter.GetBytes(x)).Write(at + 10, BitConverter.GetBytes(y)).Write(at + 12, BitConverter.GetBytes(depth)).Write(at + 0x1C, BitConverter.GetBytes(next));
        arena = intensity is { } value ? arena.Write(at + 0x18, BitConverter.GetBytes(value)) : arena.MarkUnknown(at + 0x18, 2);
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }
    private static int PointOffset(int index) => checked((int)(WorkingAddress - ArenaAddress)) + PointsOffset + index * 32;
    private static uint PointAddress(int index) => checked(WorkingAddress + (uint)PointsOffset + (uint)(index * 32));
    private static short ArenaWord(NativeBattleSaliva138Frame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(offset, 2));
    private static short I16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static void Put16(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void Put32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
