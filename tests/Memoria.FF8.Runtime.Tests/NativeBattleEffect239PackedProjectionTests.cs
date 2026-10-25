using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect239PackedProjectionTests
{
    private const uint Source = 0x10000000;
    private static readonly uint Scratch = NativeBattleEffectNodeMemory.ArenaAddress + 0x500;
    private static readonly NativeBattleCameraProjection Projection = new(null, 100, 0, 0);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 2)]
    [InlineData(-32768, 2)]
    public void Nonpositive_count_preserves_registers_scratch_and_zero_count_source_cursor(short count, int advance)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        NativeBattleGeometryRegisterStep registers = Registers();
        List<(uint Address, int Length)> reads = [];
        NativeBattleCinematicPackedProjectionStep result = Run(frame, registers, Bytes(count), reads);
        Assert.Same(frame, result.Frame);
        Assert.Same(registers, result.Registers);
        Assert.Equal(Source + (uint)advance, result.SourceCursor);
        Assert.Equal(Scratch, result.ScratchCursor);
        Assert.Equal(new[] { (Source, 2) }, reads);
    }

    [Fact]
    public void Positive_count_advances_packed_stream_and_publishes_literal_projection_feedback()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        NativeBattleGeometryRegisterStep before = Registers();
        List<(uint Address, int Length)> reads = [];
        NativeBattleCinematicPackedProjectionStep result = Run(frame, before, Bytes(2, 10, 20, 100, 20, -10, 200, 0x1234), reads);
        Assert.Equal(Source + 14, result.SourceCursor);
        Assert.Equal(Scratch + 16, result.ScratchCursor);
        Assert.Equal(new[] { (Source, 2), (Source + 2, 8), (Source + 8, 8) }, reads);
        Assert.Equal(new uint[] { 0x00A00050, 100, 0xFFD80050, 200 }, Words(result.Frame, 16));
        Assert.Equal(new uint?[] { 3, 0x00A00050, 0xFFD80050 }, result.Registers.Geometry.Sxy);
        Assert.Equal(new uint?[] { 33, 44, 100, 200 }, result.Registers.Geometry.Sz);
        Assert.Equal(new int?[] { 20, -10, 200 }, result.Registers.Colors.Mac);
        Assert.Equal(new int?[] { 20, -10, 200 }, result.Registers.Colors.Ir);
        Assert.Equal(17u, result.Registers.Colors.Ir0);
        Assert.Equal(69632u, result.Registers.Geometry.Mac0);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        Assert.Equal(new byte[] { 20, 0, 246, 255, 200, 0, 0x34, 0x12 }, result.Registers.Colors.NormalInput!.Value);
        Assert.Null(result.Registers.Colors.NormalInputKnown);
        Retained(before, result.Registers);
        Assert.Equal(JsonSerializer.Serialize(frame.TemporaryFrame), JsonSerializer.Serialize(result.Frame.TemporaryFrame));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Scratch_depth_clamp_does_not_replace_live_SZ3()
    {
        NativeBattleCinematicPackedProjectionStep result = Run(WaterSchedulerFixture.Create(), Registers(), Bytes(1, 0, 0, 5, 77), projection: new(null, 2, 0, 0));
        Assert.Equal(new uint[] { 0, 10 }, Words(result.Frame, 8));
        Assert.Equal(5u, result.Registers.Geometry.Sz[3]);
        Assert.Equal(new int?[] { 0, 0, 5 }, result.Registers.Colors.Mac);
        Assert.Equal(0u, result.Registers.Colors.Flags);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void Eight_byte_input_retains_individual_unknown_W_bytes(int unknownByte)
    {
        NativeBattleCinematicPackedProjectionStep result = Run(WaterSchedulerFixture.Create(), Registers(), Bytes(1, 10, 20, 100, 0x1234), unknownAbsoluteOffset: 2 + unknownByte);
        Assert.False(result.Registers.Colors.NormalInputKnown!.Value[unknownByte]);
        Assert.True(result.Registers.Colors.NormalInputKnown.Value[unknownByte == 6 ? 7 : 6]);
        Assert.All(result.Registers.Colors.NormalInputKnown.Value.Take(6), value => Assert.True(value));
        Assert.Equal((byte)0x34, result.Registers.Colors.NormalInput!.Value[6]);
        Assert.Equal((byte)0x12, result.Registers.Colors.NormalInput.Value[7]);
        Assert.Equal(new int?[] { 10, 20, 100 }, result.Registers.Colors.Mac);
    }

    [Fact]
    public void Later_unknown_XYZ_cannot_modify_input_frame_or_registers()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        NativeBattleGeometryRegisterStep registers = Registers();
        string before = JsonSerializer.Serialize(new Saved(frame, registers));
        Assert.Throws<NotSupportedException>(() => Run(frame, registers, Bytes(2, 10, 20, 100, 20, -10, 200, 0), unknownAbsoluteOffset: 9));
        Assert.Equal(before, JsonSerializer.Serialize(new Saved(frame, registers)));
    }

    [Fact]
    public void JSON_next_preserves_partial_V0_and_full_feedback_before_the_next_group()
    {
        NativeBattleCinematicPackedProjectionStep first = Run(WaterSchedulerFixture.Create(), Registers(), Bytes(1, 10, 20, 100, 0x1234), unknownAbsoluteOffset: 9);
        string json = JsonSerializer.Serialize(new Saved(first.Frame, first.Registers));
        Saved restored = JsonSerializer.Deserialize<Saved>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        byte[] next = Bytes(1, 20, -10, 200, 0x2345);
        NativeBattleCinematicPackedProjectionStep direct = Run(first.Frame, first.Registers, next);
        NativeBattleCinematicPackedProjectionStep cold = Run(restored.Frame, restored.Registers, next);
        Assert.Equal(JsonSerializer.Serialize(direct), JsonSerializer.Serialize(cold));
        Assert.Equal(new uint?[] { 3, 0x00A00050, 0xFFD80050 }, cold.Registers.Geometry.Sxy);
    }

    private static NativeBattleCinematicPackedProjectionStep Run(NativeBattleWaterExecutionFrame frame, NativeBattleGeometryRegisterStep registers, byte[] bytes, List<(uint Address, int Length)>? reads = null, int? unknownAbsoluteOffset = null, NativeBattleCameraProjection? projection = null)
    {
        return NativeBattleCinematicPackedProjection50FD40.Execute(frame, Source, Scratch, registers, projection ?? Projection, 2, 4096, (address, length) =>
        {
            reads?.Add((address, length));
            int offset = checked((int)(address - Source));
            byte[] data = bytes.AsSpan(offset, length).ToArray();
            bool[] known = Enumerable.Range(offset, length).Select(index => index != unknownAbsoluteOffset).ToArray();
            return (data, known);
        });
    }

    private static byte[] Bytes(short count, params short[] words)
    {
        byte[] bytes = new byte[2 + words.Length * 2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, count);
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2 + index * 2), words[index]);
        }
        return bytes;
    }

    private static uint[] Words(NativeBattleWaterExecutionFrame frame, int length)
    {
        byte[] bytes = frame.Arena.Read(0x500, length);
        return Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToArray();
    }

    private static NativeBattleGeometryRegisterStep Registers()
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, 0x1234) with { V1 = [1, null, 3, 4, 5, 6, 7, 8], V2 = [8, 7, 6, 5, 4, 3, 2, null], Sxy = [1, 2, 3], Sz = [11, 22, 33, 44], Otz = 99, Lzcs = 7, Lzcr = 29 };
        return new(geometry, new([1, 2, 3], [4, 5, 6], [7, null, 9], 0xFFFFFFFF) { Material = 0x12345678 });
    }

    private static void Retained(NativeBattleGeometryRegisterStep before, NativeBattleGeometryRegisterStep after)
    {
        Assert.Equal(before.Geometry.Rotation20, after.Geometry.Rotation20);
        Assert.Equal(before.Geometry.Translation, after.Geometry.Translation);
        Assert.Equal(before.Geometry.V1, after.Geometry.V1);
        Assert.Equal(before.Geometry.V2, after.Geometry.V2);
        Assert.Equal(before.Geometry.Otz, after.Geometry.Otz);
        Assert.Equal(before.Geometry.Lzcs, after.Geometry.Lzcs);
        Assert.Equal(before.Geometry.Lzcr, after.Geometry.Lzcr);
        Assert.Equal(before.Colors.Rgb, after.Colors.Rgb);
        Assert.Equal(before.Colors.Material, after.Colors.Material);
    }

    public sealed record Saved(NativeBattleWaterExecutionFrame Frame, NativeBattleGeometryRegisterStep Registers);
}
