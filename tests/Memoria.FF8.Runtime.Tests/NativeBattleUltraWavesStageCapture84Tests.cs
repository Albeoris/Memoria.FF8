using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltraWavesStageCapture84Tests
{
    private static NativeBattleActionMatrix Matrix => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
    private static NativeBattleCameraProjection Projection => new(null, 256, 160, 108);
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));

    [Fact]
    public void Prepare_preserves_unwritten_bytes_and_copies_unknown_flags_without_graphics_substitution()
    {
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 84).ToArray());
        NativeBattleEffectArena flags = NativeBattleEffectArena.Clear().MarkUnknown(0, 4);
        NativeBattleEffectArena actual = NativeBattleUltraWavesStageCapture84.Prepare(retained, 0x196A018, flags);
        Assert.Equal(0x196A018u, Dword(actual, 0x2C));
        Assert.Equal(new byte[4], actual.Read(0x3C, 4));
        Assert.Equal((ushort)320, Word(actual, 0x40));
        Assert.Equal((ushort)216, Word(actual, 0x42));
        Assert.Equal(uint.MaxValue, Dword(actual, 0x48));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 0x28), actual.Read(0, 0x28));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, actual.Read(0x4E, 2));
        Assert.Throws<NotSupportedException>(() => actual.Read(0x38, 4));
    }

    [Fact]
    public void Plain_body_reuses_CPU_packets_and_publishes_only_reached_R_T_and_far_controls()
    {
        byte[] geometry = Geometry(false);
        NativeBattleEffectMemory memory = Memory();
        var result = Draw(memory, geometry, Temporary(), 4096);
        NativeBattleRawMesh mesh = Assert.Single(NativeBattleRawGeometryReader.Read(geometry, 1));
        NativeBattleRawModelBodyDraw expected = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [Matrix], Projection, 0x00808080, 1, new byte[32].ToImmutableArray(), Constants, 6, new(0, 0, 320, 216), 4122, 4096);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Packets), NativeBattleTestSnapshots.Json(result.Batch));
        Assert.Equal(expected.ProjectedScratch.ToArray(), result.ProjectedScratch.Read(0, 32));
        Assert.Throws<NotSupportedException>(() => result.ProjectedScratch.Read(32, 1));
        Assert.Equal(NativeBattleTestSnapshots.Json(memory.ScriptedColorRegisters), NativeBattleTestSnapshots.Json(result.Memory.ScriptedColorRegisters));
        Assert.Equal(memory.GeometryRegisters!.Sxy, result.Memory.GeometryRegisters!.Sxy);
        Assert.Equal(memory.GeometryRegisters.Sz, result.Memory.GeometryRegisters.Sz);
        Assert.Equal(memory.GeometryRegisters.Mac0, result.Memory.GeometryRegisters.Mac0);
        Assert.Equal(memory.GeometryRegisters.Otz, result.Memory.GeometryRegisters.Otz);
        Assert.Equal(new int?[] { 0, 0, 2000 }, result.Memory.GeometryRegisters.Translation);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal((uint?)2048, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal((uint?)null, result.Memory.PrimitiveProcess.ModelRendererRegisters.PacketCursor);
        Assert.Equal((uint?)(0x1939C30 + geometry.Length), result.Memory.PrimitiveProcess.ModelRendererRegisters.SourceCursor);
        Assert.Equal((ushort)0, Word(result.Temporary84, 0x30));
        Assert.Equal((ushort)1, Word(result.Temporary84, 0x32));
        Assert.Equal(0x1939C30u + 52, Dword(result.Temporary84, 0x28));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Accepted_colored_quad_publishes_DPCS_and_retains_full_IR0_knownness(bool knownHigh)
    {
        NativeBattleEffectArena temporary = Temporary();
        temporary = knownHigh ? temporary.Write(0x4E, BitConverter.GetBytes((ushort)0x1234)) : temporary.MarkUnknown(0x4E, 2);
        NativeBattleEffectMemory memory = Memory();
        var actual = Draw(memory, Geometry(true), temporary, 1024);
        NativeBattleScriptedDpcsResult expected = NativeBattleScriptedDpcs.Transform45F270(memory.ScriptedColorRegisters!, 0x2C403020, 0x1A340400, new NativeBattleModelRendererRegisters(0, 0, 0, null, null).PublishModelColor(0x00808080), Constants);
        Assert.Equal(expected.Registers.Material, actual.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(expected.Registers.Mac.ToArray(), actual.Memory.ScriptedColorRegisters.Mac.ToArray());
        Assert.Equal(expected.Registers.Ir.ToArray(), actual.Memory.ScriptedColorRegisters.Ir.ToArray());
        Assert.Equal(expected.Registers.Rgb.ToArray(), actual.Memory.ScriptedColorRegisters.Rgb.ToArray());
        Assert.Equal(expected.Registers.Flags, actual.Memory.ScriptedColorRegisters.Flags);
        Assert.Equal(knownHigh ? (uint?)0x1A340400 : null, actual.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(expected.PackedRgb, Assert.Single(actual.Batch.Attempts).Primitive!.Words[1]);
    }

    [Fact]
    public void Rejected_colored_polygon_does_not_publish_DPCS()
    {
        NativeBattleEffectMemory memory = Memory();
        var actual = Draw(memory, Geometry(true), Temporary(), 1024, Matrix with { X = 30000 });
        Assert.Empty(actual.Batch.Attempts);
        Assert.Equal(NativeBattleTestSnapshots.Json(memory.ScriptedColorRegisters), NativeBattleTestSnapshots.Json(actual.Memory.ScriptedColorRegisters));
        Assert.Equal((uint?)2048, actual.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarBlue);
    }

    [Fact]
    public void Renderer_gate_returns_before_geometry_far_color_and_matrix_reads()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { PrimitiveProcess = memory.PrimitiveProcess! with { RendererGateB8B9F8 = 0 } };
        var actual = NativeBattleUltraWavesStageCapture84.DrawGroup(memory, Temporary(), 0x00808080, 100, [NativeBattlePackedMatrix32.FromMatrix(Matrix, null)], (_, _) => throw new InvalidOperationException(), 0x1939C30, NativeBattleEffectArena.Clear().MarkUnknown(0, 64), 64, Projection, Constants, false, 4122, 6);
        Assert.Empty(actual.Batch.Attempts);
        Assert.Same(memory.GeometryRegisters, actual.Memory.GeometryRegisters);
        Assert.Equal(memory.PrimitiveProcess.ModelRendererRegisters, actual.Memory.PrimitiveProcess!.ModelRendererRegisters);
        Assert.Equal(0x00808080u, Dword(actual.Temporary84, 0x44));
        Assert.Equal((ushort)100, Word(actual.Temporary84, 0x4C));
        Assert.Equal(1u, actual.Memory.PrimitiveProcess.RendererGateB8B9F8);
    }

    [Fact]
    public void Empty_object_directory_still_publishes_far_color_without_loading_bones()
    {
        NativeBattleEffectMemory memory = Memory();
        var actual = Draw(memory, new byte[4], Temporary(), 100);
        Assert.Same(memory.GeometryRegisters, actual.Memory.GeometryRegisters);
        Assert.Equal(NativeBattleTestSnapshots.Json(memory.ScriptedColorRegisters), NativeBattleTestSnapshots.Json(actual.Memory.ScriptedColorRegisters));
        Assert.Equal((uint?)2048, actual.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Empty(actual.Batch.Attempts);
    }

    [Fact]
    public void Cold_JSON_next_draw_preserves_unknown_padding_and_matches_registers_and_packets()
    {
        var first = Draw(Memory(), Geometry(true), Temporary().MarkUnknown(0x4E, 2), 1024);
        string json = NativeBattleTestSnapshots.Json(first);
        NativeBattleUltraWavesStageCapture84Step restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleUltraWavesStageCapture84Step>(json)!;
        var expected = Draw(first.Memory, Geometry(true), first.Temporary84, 1024, previous: first.ProjectedScratch);
        var actual = Draw(restored.Memory, Geometry(true), restored.Temporary84, 1024, previous: restored.ProjectedScratch);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Null(actual.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Null(actual.Memory.GeometryRegisters!.Rotation20[18]);
    }
    private static NativeBattleUltraWavesStageCapture84Step Draw(NativeBattleEffectMemory memory, byte[] geometry, NativeBattleEffectArena temporary, short depth, NativeBattleActionMatrix? matrix = null, NativeBattleEffectArena? previous = null) => NativeBattleUltraWavesStageCapture84.DrawGroup(memory, temporary, 0x00808080, depth, [NativeBattlePackedMatrix32.FromMatrix(matrix ?? Matrix, null)], (offset, length) => geometry.AsSpan(offset, length).ToArray(), 0x1939C30, previous ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 64), 64, Projection, Constants, false, 4122, 6);
    private static NativeBattleEffectArena Temporary() => NativeBattleUltraWavesStageCapture84.Prepare(NativeBattleEffectArena.Clear().MarkUnknown(0, 84), 0x196A018, NativeBattleEffectArena.Clear().MarkUnknown(0, 4));
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with
    {
        PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(),
        GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Sxy = [1, 2, 3], Sz = [4, 5, 6, 7], Mac0 = 99, Otz = 88 },
        ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAA987654, Mac = [11, 22, 33], Ir = [44, 55, 66], Rgb = [1, 2, 3], Flags = 0xAB, Ir0 = 0xCD }
    };
    private static byte[] Geometry(bool colored)
    {
        byte[] bytes = new byte[colored ? 76 : 72];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 4);
        short[] points = [-400, -400, 0, 400, -400, 0, -400, 400, 0, 400, 400, 0];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 2), points[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(40 + (colored ? 6 : 2)), 1);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52 + corner * 2), (ushort)corner);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 0x00040603);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 0x08070504);
        if (colored)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x2C403020);
        }
        return bytes;
    }
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
