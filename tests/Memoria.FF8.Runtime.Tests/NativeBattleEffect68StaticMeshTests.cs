using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect68StaticMeshTests
{
    [Theory]
    [InlineData(1, 24, 0x28485058u)]
    [InlineData(4, 28, 0x30485058u)]
    public void F0_flat_quad_and_gouraud_triangle_publish_current_cue_and_packets(int group, int length, uint expectedColor)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.DrawLoaded(Memory(1000), mesh, new(null, 100, 160, 108), Context(mesh), NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Equal(length, result.Batch.AllocationBytes);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(expectedColor, attempt.Primitive!.Words[1]);
        Assert.Equal(expectedColor, result.Memory.ScriptedColorRegisters!.Rgb[2]);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(result.Memory.GeometryRegisters!.Otz, U32(result.Context, 0x2C));
        Assert.NotEqual(0xDEADBEEFu, U32(result.Context, 0x24));
    }

    [Theory]
    [InlineData(1, 0x05000000u)]
    [InlineData(4, 0x06000000u)]
    public void F0_near_rejection_preserves_native_prefix_and_retained_clip_and_depth(int group, uint tag)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.DrawLoaded(Memory(1), mesh, new(null, 100, 160, 108), Context(mesh), NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(tag, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan()));
        Assert.Equal(0xDEADBEEFu, U32(result.Context, 0x24));
        Assert.Equal(0xA55AA55Au, U32(result.Context, 0x2C));
        Assert.NotEqual(0u, U32(result.Context, 0x30) & 0x60000);
    }

    private static NativeBattleEffectArena Context(NativeBattleStaticMeshResource mesh) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, BitConverter.GetBytes(0x00102030u)).Write(12, BitConverter.GetBytes(2048u)).Write(0x1C, BitConverter.GetBytes(0xF0u)).Write(0x24, BitConverter.GetBytes(0xDEADBEEFu)).Write(0x2C, BitConverter.GetBytes(0xA55AA55Au));
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static NativeBattleEffectMemory Memory(int z)
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096);
        return NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    }

    private static NativeBattleStaticMeshResource Mesh(int selectedGroup)
    {
        int faceLength = selectedGroup == 1 ? 12 : 20;
        byte[] bytes = new byte[40 + 32 + faceLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] coordinates = [-100, -100, 0, 0, 100, -100, 0, 0, -100, 100, 0, 0, 100, 100, 0, 0];
        for (int index = 0; index < coordinates.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), coordinates[index]);
        }
        int cursor = 40;
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), group == selectedGroup ? 1u : 0);
            cursor += 4;
            if (group != selectedGroup)
            {
                continue;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), group == 1 ? 0x28808080u : 0x30808080u);
            for (int corner = 0; corner < (group == 1 ? 4 : 3); corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4 + corner * 2), checked((ushort)(corner * 2)));
            }
            if (group == 4)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 12), 0x00604020);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 16), 0x00402010);
            }
            cursor += faceLength;
        }
        return new(0, 0x5000, bytes.ToImmutableArray());
    }
}
