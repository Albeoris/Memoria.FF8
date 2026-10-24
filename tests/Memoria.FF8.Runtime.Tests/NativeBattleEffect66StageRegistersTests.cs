using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect66StageRegistersTests
{
    [Fact]
    public void Software_projection_retains_vector_fifos_and_loads_even_the_last_empty_bone_group()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectMemory result = NativeBattleEffect66StageRegisters.Publish5099D0(memory, Mesh(), Matrices(), Scratch(), 0, Context(0), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(new int?[] { 10, 20, 30 }, result.GeometryRegisters!.Translation);
        Assert.Null(result.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.GeometryRegisters.Rotation20[19]);
        Assert.Equal(memory.GeometryRegisters!.Sxy, result.GeometryRegisters.Sxy);
        Assert.Equal(memory.GeometryRegisters.Sz, result.GeometryRegisters.Sz);
        Assert.Equal(memory.ScriptedColorRegisters, result.ScriptedColorRegisters);
    }

    [Fact]
    public void Accepted_colored_quads_publish_ordered_dpcs_and_full_ir0_while_rejected_faces_do_not()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleRawMesh mesh = Mesh();
        NativeBattleEffectMemory result = NativeBattleEffect66StageRegisters.Publish5099D0(memory, mesh, Matrices(), Scratch(), 0, Context(2048), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0x08000800u, result.ScriptedColorRegisters!.Ir0);
        Assert.Equal(0x2C204060u, result.ScriptedColorRegisters.Material);
        Assert.Equal(memory.ScriptedColorRegisters!.Rgb[2], result.ScriptedColorRegisters.Rgb[0]);
        Assert.Equal(result.ScriptedColorRegisters.Rgb[1], result.ScriptedColorRegisters.Rgb[2]);
        NativeBattleEffectMemory rejected = NativeBattleEffect66StageRegisters.Publish5099D0(memory, mesh, Matrices(), Scratch(true), 0, Context(2048), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(memory.ScriptedColorRegisters, rejected.ScriptedColorRegisters);
        NativeBattleEffectMemory partial = NativeBattleEffect66StageRegisters.Publish5099D0(memory, mesh, Matrices(), Scratch(), 0, Context(2048).MarkUnknown(0x26, 2), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Null(partial.ScriptedColorRegisters!.Ir0);
        Assert.Equal(result.ScriptedColorRegisters.Rgb, partial.ScriptedColorRegisters.Rgb);
        NativeBattleGeometryRegisters imported = JsonSerializer.Deserialize<NativeBattleGeometryRegisters>(JsonSerializer.Serialize(result.GeometryRegisters))!;
        Assert.Equal(result.GeometryRegisters!.Translation, imported.Translation);
    }

    private static NativeBattleEffectArena Context(short cue) => NativeBattleEffectArena.Clear().Write(0x24, BitConverter.GetBytes(cue));

    private static NativeBattleEffectMemory Memory()
    {
        return NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Sxy = [1u, 2u, 3u], Sz = [4u, 5u, 6u, 7u] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [11u, 22u, 33u] }, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().PublishModelColor(0) };
    }

    private static ImmutableArray<NativeBattleActionMatrix> Matrices() => [new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096), new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096)];

    private static NativeBattleRawMesh Mesh()
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 0x2C204060);
        NativeBattleRawPolygon polygon = new(true, bytes.ToImmutableArray());
        return new(Enumerable.Repeat(new NativeBattleRawVertex(new(0, 0, 0), 0), 4).ToImmutableArray(), [polygon, polygon]) { PackedVertexGroups = [new(0, 4, new byte[24].ToImmutableArray()), new(1, 0, [])] };
    }

    private static ImmutableArray<byte> Scratch(bool reject = false)
    {
        byte[] bytes = new byte[32];
        uint[] coordinates = [0, 10, 10u << 16, (10u << 16) | 10];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 8), coordinates[index]);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 8 + 4), reject ? 0x20080u : 128u);
        }
        return bytes.ToImmutableArray();
    }
}
