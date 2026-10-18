using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterMeshPreparationTests
{
    private const uint ArenaBase = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Manager = 0x2797690;
    private const int File0 = 0x10000;
    private const int File1 = 0x40000;
    private const int Shape = File0 + 0x1000;
    private const int Work = 0x80000;
    private static readonly NativeBattleWaterSlotBank Bank = new(File1, 0x26258, 8, File1 + 0x58);
    private static readonly NativeBattleEffectHeapAllocation Map = new("map", new byte[16].ToImmutableArray());
    private static readonly NativeBattleWater222Resources Resources = ResourcesFixture();

    [Fact]
    public void Type1_selects_current_ID_matrix_focal_override_and_full_MAC_translation_without_IR_clamping()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        short[] rotation = [4096, 1024, 512, 2048, 4096, 0, -1024, 256, 4096];
        frame = Matrix(frame, 2, rotation, 50000, -60000, 100000, -123);
        frame = SlotWord(frame, 0, 0x94, 30000);
        frame = SlotWord(frame, 0, 0x96, unchecked((ushort)-20000));
        frame = SlotWord(frame, 0, 0x98, 10000);
        NativeBattleWaterMeshPrepared result = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, Bank, 0, 1, 216);
        Assert.Equal((short)-123, result.Focal);
        Assert.Equal(rotation, result.Matrix.Rotation);
        Assert.Equal(76250, result.Matrix.X);
        Assert.Equal(-65000, result.Matrix.Y);
        Assert.Equal(101250, result.Matrix.Z);
        Assert.Equal(new byte[12], result.Frame.ReadTemporary(0x40, 12));
        Assert.Equal(frame.Core.Read(0x27979E8 + 64, 32), result.Frame.Core.Read(0x27979E8 + 64, 32));
    }

    [Fact]
    public void Type2_scales_columns_after_unscaled_translation_and_preserves_raw_base_focal_padding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        short[] rotation = [4096, 1024, 512, 2048, 4096, 0, -1024, 256, 4096];
        frame = Matrix(frame, 2, rotation, 50000, -60000, 100000, 0x1234);
        frame = SlotWord(frame, 0, 0x94, 30000);
        frame = SlotWord(frame, 0, 0x96, unchecked((ushort)-20000));
        frame = SlotWord(frame, 0, 0x98, 10000);
        frame = SlotWord(frame, 0, 0x8C, 256);
        frame = SlotWord(frame, 0, 0x8E, unchecked((ushort)-128));
        frame = SlotWord(frame, 0, 0x90, 512);
        NativeBattleWaterMeshPrepared result = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, Bank, 0, 2, 216);
        Assert.Equal(new short[] { 4096, -512, 1024, 2048, -2048, 0, -1024, -128, 8192 }, result.Matrix.Rotation);
        Assert.Equal((76250, -65000, 101250), (result.Matrix.X, result.Matrix.Y, result.Matrix.Z));
        Assert.Equal((ushort)0x1234, result.Frame.ReadTemporaryWord(0xF2));
        Assert.Equal(50000u, result.Frame.ReadTemporaryDword(0xF4));
        Assert.Equal(4096u, result.Frame.ReadTemporaryDword(0xD4));
        Assert.Equal(unchecked((uint)-2048), result.Frame.ReadTemporaryDword(0xD8));
        Assert.Equal(8192u, result.Frame.ReadTemporaryDword(0xDC));
    }

    [Fact]
    public void Missing_ID_falls_back_to_matrix_row0_and_zero_override_uses_global_focal()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = Matrix(frame, 0, [4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 31, 42, 53, 0);
        frame = CoreWord(frame, 0x2797204 + 4, 17);
        NativeBattleWaterMeshPrepared result = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, Bank, 0, 1, -7);
        Assert.Equal((short)-7, result.Focal);
        Assert.Equal((31, 42, 53), (result.Matrix.X, result.Matrix.Y, result.Matrix.Z));
    }

    [Theory]
    [InlineData(0, 750)]
    [InlineData(0x20, 65529)]
    [InlineData(0x40, 743)]
    public void Ordinary_projection_keeps_actual_FULL_FLAG_bytes_and_separate_raw_depth_with_current_bucket_policy(byte mode, ushort expectedBucket)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(Bank.SlotBaseOffset + 0xDE, [mode]).MarkUnknown(Shape + 0x30 + 6, 2) };
        frame = SlotWord(frame, 0, 0xC0, unchecked((ushort)-7));
        NativeBattleWaterMeshPrepared prepared = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, Bank, 0, 1, 216);
        NativeBattleEffectArena depths = NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleWaterMeshPreparation.DepthLength).Write(0x2A4, [0xDD, 0xCC, 0xBB, 0xAA]);
        NativeBattleWaterMeshProjected projected = NativeBattleWaterMeshPreparation.MorphProject(prepared, Bank, 0, Resources, Map, 0x218, Projection(), 0, -4096, depths);
        NativeBattleModuleProjectedVertex expected = NativeBattleModuleProjection.Project45FAA0(prepared.Matrix, new(1, 2, 3000), Projection(), 0, -4096);
        Assert.Equal(1d, projected.ProxyScale);
        Assert.Equal(expected.Screen, Dword(projected.Frame.Arena, Work));
        Assert.Equal(expectedBucket, Word(projected.Frame.Arena, Work + 4));
        Assert.Equal(unchecked(expected.Flags << 4) & 0xFFFF0000, Dword(projected.Frame.Arena, Work + 4) & 0xFFFF0000);
        Assert.Equal(3000u, BinaryPrimitives.ReadUInt32LittleEndian(projected.DepthOwner.Read(0, 4)));
        Assert.Equal(new byte[] { 0xDD, 0xCC, 0xBB, 0xAA }, projected.DepthOwner.Read(0x2A4, 4));
        Assert.Throws<NotSupportedException>(() => projected.DepthOwner.Read(24 * 4, 4));
        Assert.Throws<NotSupportedException>(() => projected.Frame.Arena.Read(Shape + 0x30 + 6, 2));
        Assert.Equal(new byte[] { 1, 0, 2, 0, 0xB8, 0x0B }, projected.Frame.Arena.Read(Shape + 0x30, 6));
    }

    [Fact]
    public void Projection_reads_each_current_XYZ_before_overlapping_record_writes_instead_of_snapshotting_vertices()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = CoreDword(frame, Manager + 0x74, ArenaBase + Shape + 0x38);
        NativeBattleWaterMeshPrepared prepared = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, Bank, 0, 1, 216);
        NativeBattleWaterMeshProjected result = NativeBattleWaterMeshPreparation.MorphProject(prepared, Bank, 0, Resources, Map, 0x218, Projection(), 0, 0, Depths());
        NativeBattleModuleProjectedVertex first = NativeBattleModuleProjection.Project45FAA0(prepared.Matrix, new(1, 2, 3000), Projection(), 0, 0);
        NativeBattleVector modifiedSecond = new(unchecked((short)first.Screen), unchecked((short)(first.Screen >> 16)), (short)(first.Depth >> 2));
        NativeBattleModuleProjectedVertex second = NativeBattleModuleProjection.Project45FAA0(prepared.Matrix, modifiedSecond, Projection(), 0, 0);
        Assert.Equal(first.Screen, Dword(result.Frame.Arena, Shape + 0x38));
        Assert.Equal(second.Screen, Dword(result.Frame.Arena, Shape + 0x40));
        Assert.NotEqual(NativeBattleModuleProjection.Project45FAA0(prepared.Matrix, new(2, 3, 3000), Projection(), 0, 0).Screen, second.Screen);
    }

    [Fact]
    public void Morph_uses_independent_signed_factors_preserves_unknown_W_and_current_directory_data()
    {
        NativeBattleWaterExecutionFrame frame = MorphFixture();
        frame = SlotWord(frame, 1, 0x94, unchecked((ushort)-256));
        frame = SlotWord(frame, 1, 0x96, 128);
        frame = SlotWord(frame, 1, 0x98, 512);
        for (int vertex = 0; vertex < 170; vertex++)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(Work + vertex * 8 + 6, 2).MarkUnknown(File0 + 0x5048 + 0x30 + vertex * 8 + 6, 2).MarkUnknown(File0 + 0x55C8 + 0x30 + vertex * 8 + 6, 2) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleWaterMeshPreparation.MorphSelected(frame, Bank, 0, Resources, Map, 170);
        Assert.Equal((short)-100, Signed(result.Arena, Work));
        Assert.Equal((short)51, Signed(result.Arena, Work + 2));
        Assert.Equal((short)202, Signed(result.Arena, Work + 4));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Work + 6, 2));
        Assert.Equal(0xDEADBEEFu, Dword(result.Arena, Work + 170 * 8));
        Assert.Equal(ArenaBase + Work, result.ReadTemporaryDword(0x74));
        NativeBattleWaterExecutionFrame changed = frame with { Arena = frame.Arena.Write(File0 + 0x55C8 + 0x30, new byte[] { 0xC8, 0 }) };
        NativeBattleWaterExecutionFrame next = NativeBattleWaterMeshPreparation.MorphSelected(changed, Bank, 0, Resources, Map, 170);
        Assert.Equal((short)-200, Signed(next.Arena, Work));
        frame = frame with { Arena = frame.Arena.Write(File0 + 0x5048 + 0x30, new byte[] { 0, 0x80 }).Write(File0 + 0x55C8 + 0x30, new byte[] { 0xFF, 0x7F }) };
        frame = SlotWord(frame, 1, 0x94, 32767);
        NativeBattleWaterExecutionFrame wrapped = NativeBattleWaterMeshPreparation.MorphSelected(frame, Bank, 0, Resources, Map, 170);
        Assert.Equal(unchecked((short)(-32768 + ((65535 * 32767) >> 8))), Signed(wrapped.Arena, Work));
    }

    [Fact]
    public void Four_template_closed_loop_and_JSON_next_keep_only_actual_XYZ_writes()
    {
        NativeBattleWaterExecutionFrame frame = MorphFixture();
        for (int lane = 0; lane < 3; lane++)
        {
            frame = SlotWord(frame, 1, 0x94 + lane * 2, 256);
        }
        int[] starts = [5, 6, 7, 8];
        int[] ends = [6, 7, 8, 5];
        for (int step = 0; step < 4; step++)
        {
            frame = Tuple(frame, starts[step], ends[step]);
            NativeBattleWaterExecutionFrame cold = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
            NativeBattleWaterExecutionFrame actual = NativeBattleWaterMeshPreparation.MorphSelected(frame, Bank, 0, Resources, Map, 170);
            NativeBattleWaterExecutionFrame restored = NativeBattleWaterMeshPreparation.MorphSelected(cold, Bank, 0, Resources, Map, 170);
            Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(restored));
            Assert.Equal((short)((ends[step] - 5) * 100), Signed(actual.Arena, Work));
            Assert.Equal((ushort)0xA5A5, Word(actual.Arena, Work + 6));
            frame = actual;
        }
        Assert.Equal((short)0, Signed(frame.Arena, Work));
    }

    [Fact]
    public void Prepared_groups_submit_current_output_and_update_only_manager_cursor_with_independent_JSON_next()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        byte[] corners = new byte[32];
        short[] x = [0, 100, 0, 100];
        short[] y = [0, 0, 100, 100];
        for (int corner = 0; corner < 4; corner++)
        {
            WriteWord(corners, corner * 8, unchecked((ushort)x[corner]));
            WriteWord(corners, corner * 8 + 2, unchecked((ushort)y[corner]));
            WriteWord(corners, corner * 8 + 4, 3000);
        }
        frame = frame with { Arena = frame.Arena.Write(Shape + 0x30, corners) };
        NativeBattleWaterMeshPrepared prepared = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, Bank, 0, 1, 216);
        NativeBattleWaterMeshProjected projected = NativeBattleWaterMeshPreparation.MorphProject(prepared, Bank, 0, Resources, Map, 0x218, Projection(), 0, 0, Depths());
        NativeBattleWaterMeshProjected cold = JsonSerializer.Deserialize<NativeBattleWaterMeshProjected>(JsonSerializer.Serialize(projected))!;
        NativeBattleWaterMeshSubmission first = NativeBattleWaterMeshPreparation.Submit(projected, 0x218, Resolve);
        NativeBattleWaterMeshSubmission restored = NativeBattleWaterMeshPreparation.Submit(cold, 0x218, Resolve);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(restored));
        Assert.Equal(44, first.Packets.AllocationBytes);
        Assert.Equal(0x30002Cu, first.Frame.ReadTemporaryDword(0x60));
        Assert.Equal(0x30002Cu, BinaryPrimitives.ReadUInt32LittleEndian(first.Frame.Core.Read(Manager + 0x7C, 4)));
        Assert.Equal(0x300000u, projected.Frame.ReadTemporaryDword(0x60));
        Assert.Equal(ArenaBase + Shape + 0x11C, first.Frame.ReadTemporaryDword(0x6C));
        Assert.Equal(ArenaBase + Shape + 0x11C, first.Frame.ReadTemporaryDword(0x58));
        Assert.Equal(1u, first.Frame.ReadTemporaryDword(0x70));
        NativeBattleWaterMeshProjected alternate = projected with { Frame = projected.Frame with { Arena = projected.Frame.Arena.Write(Bank.SlotBaseOffset + 0x4C, [4]) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshPreparation.Submit(alternate, 0x218, Resolve));
    }

    [Fact]
    public void Unproved_wrappers_projection_branches_shape_extents_and_depth_owners_fail_explicitly()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshPreparation.PrepareWrapper(frame, Bank, 0, 3, 216));
        NativeBattleWaterMeshPrepared prepared = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, Bank, 0, 1, 216);
        NativeBattleWaterMeshPrepared alternate = prepared with { Frame = frame with { Arena = frame.Arena.Write(Bank.SlotBaseOffset + 0x4C, [2]) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshPreparation.MorphProject(alternate, Bank, 0, Resources, Map, 0x218, Projection(), 0, 0, Depths()));
        NativeBattleWaterMeshPrepared normals = prepared with { Frame = prepared.Frame with { Arena = prepared.Frame.Arena.Write(Bank.SlotBaseOffset + 0xE1, [1]) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshPreparation.MorphProject(normals, Bank, 0, Resources, Map, 0x218, Projection(), 0, 0, Depths()));
        NativeBattleWaterMeshPrepared extraScale = prepared with { Frame = prepared.Frame.WriteTemporaryDword(0x40, 1) };
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshPreparation.MorphProject(extraScale, Bank, 0, Resources, Map, 0x218, Projection(), 0, 0, Depths()));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshPreparation.MorphProject(prepared, Bank, 0, Resources, Map, 100, Projection(), 0, 0, Depths()));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterMeshPreparation.MorphProject(prepared, Bank, 0, Resources, Map, 0x218, Projection(), 0, 0, Depths().Write(0x2A8, [1])));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(1));
        frame = frame with { Core = frame.Core.Write(0x2797454, Enumerable.Repeat((byte)0xFF, 128).ToArray()).Write(0x2797454, [0, 1]) };
        frame = CoreDword(frame, 0x27973EC, Manager);
        frame = CoreDword(frame, 0x27973E8, ArenaBase + (uint)Bank.SlotBaseOffset);
        frame = CoreDword(frame, 0x2798A68, ArenaBase + File0);
        frame = CoreDword(frame, 0x2798A6C, ArenaBase + File1);
        frame = CoreDword(frame, Manager + 0x74, ArenaBase + Work);
        frame = CoreDword(frame, Manager + 0x7C, 0x300000);
        frame = CoreDword(frame, Manager + 0xAC, 0x186C8CC);
        frame = CoreWord(frame, 0x2797204 + 4, 9);
        frame = Matrix(frame, 2, [4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 0);
        frame = SlotWord(frame, 0, 0x12, 7);
        frame = SlotWord(frame, 1, 0x12, 9);
        frame = SlotWord(frame, 0, 0x9C, 9);
        frame = SlotDword(frame, 0, 0xD8, ArenaBase + Shape);
        frame = SlotDword(frame, 0, 0xCC, 0x00808080);
        byte[] shape = new byte[0x218];
        WriteDword(shape, 0, 2);
        WriteDword(shape, 8, 0x100);
        WriteDword(shape, 0x14, 0x30);
        WriteDword(shape, 0x18, 24);
        for (int vertex = 0; vertex < 24; vertex++)
        {
            WriteWord(shape, 0x30 + vertex * 8, (ushort)(1 + vertex));
            WriteWord(shape, 0x32 + vertex * 8, (ushort)(2 + vertex));
            WriteWord(shape, 0x34 + vertex * 8, 3000);
        }
        WriteWord(shape, 0x100, 17);
        WriteWord(shape, 0x102, 1);
        for (int corner = 0; corner < 4; corner++)
        {
            WriteDword(shape, 0x104 + corner * 4, 0x00808080);
            WriteWord(shape, 0x114 + corner * 2, (ushort)(corner * 8));
        }
        WriteWord(shape, 0x11E, 0xFFFF);
        frame = frame with { Arena = frame.Arena.Write(Shape, shape) };
        return frame.WriteTemporaryDword(0x14C, 0x1D8E058 + 17 * 4);
    }

    private static NativeBattleWaterExecutionFrame MorphFixture()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        byte[] directory = new byte[4 + 14 * 4];
        WriteDword(directory, 0, 14);
        int[] offsets = [0x5048, 0x55C8, 0x5B48, 0x60C8];
        for (int id = 5; id <= 8; id++)
        {
            WriteDword(directory, 4 + id * 4, (uint)(offsets[id - 5] - 0x100));
            byte[] shape = new byte[0x580];
            WriteDword(shape, 0, 2);
            WriteDword(shape, 0x14, 0x30);
            WriteDword(shape, 0x18, 170);
            for (int vertex = 0; vertex < 170; vertex++)
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    WriteWord(shape, 0x30 + vertex * 8 + lane * 2, (ushort)((id - 5) * 100 + vertex * 2 + lane));
                }
            }
            frame = frame with { Arena = frame.Arena.Write(File0 + offsets[id - 5], shape) };
        }
        frame = frame with { Arena = frame.Arena.Write(File0 + 0x100, directory).Write(Work, Enumerable.Repeat((byte)0xA5, 170 * 8).ToArray()).Write(Work + 170 * 8, [0xEF, 0xBE, 0xAD, 0xDE]) };
        frame = frame with { Arena = frame.Arena.Write(File0 + 12, new byte[] { 0, 1, 0, 0 }) };
        frame = frame.WriteTemporaryDword(0x7C, ArenaBase + Work).WriteTemporaryDword(0x74, ArenaBase + Shape + 0x30);
        return Tuple(frame, 5, 6);
    }

    private static NativeBattleWaterExecutionFrame Tuple(NativeBattleWaterExecutionFrame frame, int start, int end)
    {
        byte[] bytes = new byte[6];
        WriteWord(bytes, 0, 9);
        WriteWord(bytes, 2, (ushort)start);
        WriteWord(bytes, 4, (ushort)end);
        return (frame with { Arena = frame.Arena.Write(0x90000, bytes) }).WriteTemporaryDword(0x4C, ArenaBase + 0x90000);
    }

    private static NativeBattleWaterExecutionFrame Matrix(NativeBattleWaterExecutionFrame frame, int row, short[] rotation, int x, int y, int z, short focal)
    {
        byte[] bytes = new byte[32];
        for (int lane = 0; lane < 9; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(lane * 2), rotation[lane]);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), focal);
        WriteDword(bytes, 20, unchecked((uint)x));
        WriteDword(bytes, 24, unchecked((uint)y));
        WriteDword(bytes, 28, unchecked((uint)z));
        return frame with { Core = frame.Core.Write(0x27979E8 + (uint)row * 32, bytes) };
    }

    private static NativeBattleWater222Resources ResourcesFixture() => new([new("current0", new byte[0x1799C].ToImmutableArray()), new("current1", new byte[0x26258].ToImmutableArray())], new(new("tables", new byte[0x70].ToImmutableArray()), new("directory", new byte[0x1C].ToImmutableArray()), new("owners", new byte[0x14].ToImmutableArray())));
    private static NativeBattleEffectArena Depths() => NativeBattleEffectArena.Clear().MarkUnknown(0, 0x2A8);
    private static NativeBattleCameraProjection Projection() => new(null, 216, 160, 108);
    private static int Resolve(uint pointer) => checked((int)((pointer - 0x1D8E058) / 4));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short Signed(NativeBattleEffectArena arena, int offset) => unchecked((short)Word(arena, offset));
    private static void WriteDword(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void WriteWord(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    private static NativeBattleWaterExecutionFrame SlotWord(NativeBattleWaterExecutionFrame frame, int index, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        WriteWord(bytes, 0, value);
        return frame with { Arena = frame.Arena.Write(Bank.SlotBaseOffset + index * 0x100 + offset, bytes) };
    }
    private static NativeBattleWaterExecutionFrame SlotDword(NativeBattleWaterExecutionFrame frame, int index, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        WriteDword(bytes, 0, value);
        return frame with { Arena = frame.Arena.Write(Bank.SlotBaseOffset + index * 0x100 + offset, bytes) };
    }
    private static NativeBattleWaterExecutionFrame CoreWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value)
    {
        byte[] bytes = new byte[2];
        WriteWord(bytes, 0, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }
    private static NativeBattleWaterExecutionFrame CoreDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        WriteDword(bytes, 0, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }
}
