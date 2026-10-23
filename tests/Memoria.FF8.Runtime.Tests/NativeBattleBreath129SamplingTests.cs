using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath129SamplingTests
{
    private const uint Triangles = NativeBattleEffectNodeMemory.ArenaAddress + 0x1000;
    private static readonly NativeBattleBreath129Resources Resources = new(null!, null!, null!, null!, null!, null!, null!, [], [], [], [], [], Enumerable.Range(0, 256).Select(value => (ushort)Math.Floor(Math.Sqrt(value) * 512)).ToImmutableArray(), [], 0);

    [Fact]
    public void Triangle_uses_current_bone_transforms_wide_centroid_and_preserves_output_W()
    {
        NativeBattleBreath129Frame frame = Fixture();
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(6, new byte[] { 0xAB, 0xCD });
        NativeBattleBreath129SampleStep result = NativeBattleBreath129Geometry.SampleTriangle6B7170(frame, Body(300, 600, 900), Triangles, retained, retained.MarkUnknown(7, 1), Resources);
        Assert.Equal(new short[] { 321, 621, 1923 }, Lanes(result.Point8));
        Assert.Equal(new short[] { 0, 0, -4096 }, Lanes(result.Normal8));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, result.Point8.Read(6, 2));
        Assert.Equal((byte)0xAB, result.Normal8.Read(6, 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Normal8.Read(7, 1));
        Assert.Equal(new int?[] { 300, 600, 900 }, result.Frame.Memory.GeometryRegisters!.Translation);
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(frame.State.ReadDword(0x252117C), result.Frame.State.ReadDword(0x252117C));
    }

    [Fact]
    public void Triangle_retains_last_corner_and_publishes_raw_control_DWORDs_square_and_leading_state()
    {
        NativeBattleBreath129SampleStep result = NativeBattleBreath129Geometry.SampleTriangle6B7170(Fixture(), Body(), Triangles, Resources);
        NativeBattleGeometryRegisters geometry = result.Frame.Memory.GeometryRegisters!;
        Assert.Equal(BitConverter.GetBytes(-64), geometry.Rotation20.Take(4).Select(value => value!.Value));
        Assert.Equal(new byte?[] { 0, 0, 0, 0 }, geometry.Rotation20.Skip(8).Take(4));
        Assert.Equal(new byte?[] { 0, 0, 0, 0 }, geometry.Rotation20.Skip(16).Take(4));
        Assert.Equal(new int?[] { 0, 0, 16777216 }, result.Frame.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { 0, 0, 32767 }, result.Frame.Memory.ScriptedColorRegisters.Ir);
        Assert.Equal(0x00400000u, result.Frame.Memory.ScriptedColorRegisters.Flags);
        Assert.Equal(0x01000000u, geometry.Lzcs);
        Assert.Equal(7u, geometry.Lzcr);
        Assert.Equal(new uint?[] { 1, 2, 3 }, result.Frame.Memory.ScriptedColorRegisters.Rgb);
        Assert.Throws<NotSupportedException>(() => result.Point8.Read(6, 2));
    }

    [Fact]
    public void Triangle_control_difference_keeps_full_DWORD_before_OP_consumes_signed_low_WORD()
    {
        NativeBattleBreath129Frame frame = NativeBattleBreath129LowerTests.Frame();
        frame = WriteBytes(frame, Triangles, Triangle((-20000, 0, 1024), (20000, 0, 1024), (-20000, 1, 1024)), 24);
        NativeBattleBreath129SampleStep result = NativeBattleBreath129Geometry.SampleTriangle6B7170(frame, Body(), Triangles, Resources);
        Assert.Equal(BitConverter.GetBytes(-40000), result.Frame.Memory.GeometryRegisters!.Rotation20.Take(4).Select(value => value!.Value));
        Assert.True(Lanes(result.Normal8)[2] > 4000);
        Assert.Equal(12768 * 12768, result.Frame.Memory.ScriptedColorRegisters!.Mac[2]);
    }

    [Fact]
    public void Whole_phase_selects_next_triangle_without_blending_or_changing_IR0()
    {
        NativeBattleBreath129Frame frame = Fixture();
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = frame.Memory.ScriptedColorRegisters! with { Ir0 = 123 } } };
        NativeBattleBreath129SampleStep result = NativeBattleBreath129Geometry.Sample6B6FE0(frame, Body(), Triangles, 2, 4096, Resources, NativeBattleBreath129LowerTests.Inputs());
        Assert.Equal(new short[] { 149, 21, 1023 }, Lanes(result.Point8));
        Assert.Equal(123u, result.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new uint?[] { 1, 2, 3 }, result.Frame.Memory.ScriptedColorRegisters.Rgb);
    }

    [Fact]
    public void Intermediate_phase_blends_point_then_normal_and_retains_four_arithmetic_FIFO_updates()
    {
        NativeBattleBreath129SampleStep result = NativeBattleBreath129Geometry.Sample6B6FE0(Fixture(), Body(), Triangles, 2, 2048, Resources, NativeBattleBreath129LowerTests.Inputs());
        Assert.Equal(new short[] { 84, 20, 1024 }, Lanes(result.Point8));
        Assert.Equal(new short[] { 0, 0, -4096 }, Lanes(result.Normal8));
        Assert.Equal(2048u, result.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new int?[] { 0, 0, 16777216 }, result.Frame.Memory.ScriptedColorRegisters.Mac);
        Assert.Equal(new uint?[] { 0x00FF1454, 0, 0 }, result.Frame.Memory.ScriptedColorRegisters.Rgb);
    }

    [Fact]
    public void Serialized_frame_continues_sampling_identically_and_degenerate_triangle_fails_at_division()
    {
        NativeBattleBreath129SampleStep first = NativeBattleBreath129Geometry.Sample6B6FE0(Fixture(), Body(), Triangles, 2, 1024, Resources, NativeBattleBreath129LowerTests.Inputs());
        NativeBattleBreath129Frame restored = JsonSerializer.Deserialize<NativeBattleBreath129Frame>(JsonSerializer.Serialize(first.Frame))!;
        NativeBattleBreath129SampleStep next = NativeBattleBreath129Geometry.Sample6B6FE0(first.Frame, Body(), Triangles, 2, 3072, Resources, NativeBattleBreath129LowerTests.Inputs());
        NativeBattleBreath129SampleStep resumed = NativeBattleBreath129Geometry.Sample6B6FE0(restored, Body(), Triangles, 2, 3072, Resources, NativeBattleBreath129LowerTests.Inputs());
        Assert.Equal(JsonSerializer.Serialize(next.Frame.Memory.GeometryRegisters), JsonSerializer.Serialize(resumed.Frame.Memory.GeometryRegisters));
        Assert.Equal(JsonSerializer.Serialize(next.Frame.Memory.ScriptedColorRegisters), JsonSerializer.Serialize(resumed.Frame.Memory.ScriptedColorRegisters));
        Assert.Equal(next.Point8.Read(0, 6), resumed.Point8.Read(0, 6));
        NativeBattleBreath129Frame degenerate = WriteBytes(Fixture(), Triangles, Triangle((0, 0, 1), (0, 0, 1), (0, 0, 1)), 24);
        Assert.Throws<DivideByZeroException>(() => NativeBattleBreath129Geometry.SampleTriangle6B7170(degenerate, Body(), Triangles, Resources));
    }

    private static NativeBattleBreath129Frame Fixture()
    {
        NativeBattleBreath129Frame frame = NativeBattleBreath129LowerTests.Frame();
        frame = WriteBytes(frame, Triangles, Triangle((0, 0, 1024), (64, 0, 1024), (0, 64, 1024)), 24);
        return WriteBytes(frame, Triangles + 24, Triangle((128, 0, 1024), (192, 0, 1024), (128, 64, 1024)), 24);
    }

    private static NativeBattleBreath129BodySource Body(int x = 0, int y = 0, int z = 0) => new(0x910000, 0, (_, _) => throw new InvalidOperationException(), [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = x, Y = y, Z = z }, null)]);

    private static NativeBattleEffectArena Triangle((short X, short Y, short Z) first, (short X, short Y, short Z) second, (short X, short Y, short Z) third)
    {
        NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear();
        (short X, short Y, short Z)[] vertices = [first, second, third];
        for (int index = 0; index < 3; index++)
        {
            bytes = bytes.Write(index * 8, BitConverter.GetBytes(vertices[index].X)).Write(index * 8 + 2, BitConverter.GetBytes(vertices[index].Y)).Write(index * 8 + 4, BitConverter.GetBytes(vertices[index].Z));
        }
        return bytes;
    }

    private static short[] Lanes(NativeBattleEffectArena point) => Enumerable.Range(0, 3).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(point.Read(lane * 2, 2))).ToArray();
}
