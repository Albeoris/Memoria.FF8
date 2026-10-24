using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect65LowerFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect65WindowedMeshTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(0xCu)]
    [InlineData(0x800Cu)]
    [InlineData(0x8030u)]
    public void Raw_flags_publish_source_plus_eight_without_reading_unselected_vertex_owner(uint flags)
    {
        NativeBattleWindowedMeshContext108Step result = Draw(flags);
        Assert.Equal(0x100008u, BinaryPrimitives.ReadUInt32LittleEndian(result.Context108.Read(4, 4)));
        Assert.Empty(result.Batch.Attempts);
        Assert.Throws<NotSupportedException>(() => result.Context108.Read(8, 3));
    }

    [Theory]
    [InlineData(0x2000u)]
    [InlineData(0x200Cu)]
    [InlineData(0x203Cu)]
    [InlineData(0x20CCu)]
    [InlineData(0x20FCu)]
    public void Selected_flags_preserve_current_vertex_pointer_and_unknown_far_RGB(uint flags)
    {
        NativeBattleWindowedMeshContext108Step result = Draw(flags);
        Assert.Equal(0xCAFE1234u, BinaryPrimitives.ReadUInt32LittleEndian(result.Context108.Read(4, 4)));
        Assert.Null(result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Null(result.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Null(result.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
    }

    private static NativeBattleWindowedMeshContext108Step Draw(uint flags)
    {
        byte[] bytes = new byte[48];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        NativeBattleStaticMeshResource mesh = new(0, 0x100000, bytes.ToImmutableArray());
        NativeBattleEffectMemory memory = Frame().Memory with { PrimitiveProcess = new(null, null, null, 0, null) { ModelRendererRegisters = new(1, 2, 3, null, null) } };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(mesh.Address)).Write(4, BitConverter.GetBytes(0xCAFE1234u)).Write(0x1C, BitConverter.GetBytes(flags)).MarkUnknown(8, 3);
        return NativeBattleWindowedMeshContext108.DrawLoaded(memory, mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, 306, _ => throw new InvalidOperationException("No face reaches selected vertices."));
    }
}
