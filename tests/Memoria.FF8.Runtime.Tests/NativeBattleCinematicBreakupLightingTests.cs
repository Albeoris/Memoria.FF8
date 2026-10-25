using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBreakupLightingTests
{
    [Fact]
    public void Face_normal_uses_first_three_vertices_and_preserves_winding()
    {
        NativeBattleVector first = new(100, 200, 300);
        NativeBattleVector second = new(4196, 200, 300);
        NativeBattleVector third = new(100, 4296, 300);
        Assert.Equal(new NativeBattleVector(0, 0, 4096), NativeBattleCinematicBreakupLighting.FaceNormal(first, second, third));
        Assert.Equal(new NativeBattleVector(0, 0, -4096), NativeBattleCinematicBreakupLighting.FaceNormal(first, third, second));
        Assert.Equal(new NativeBattleVector(0, 0, 0), NativeBattleCinematicBreakupLighting.FaceNormal(first, first, third));
    }

    [Fact]
    public void Face_cross_retains_signed_DWORD_overflow_before_normalization()
    {
        Assert.Equal(new NativeBattleVector(0, 0, -4096), NativeBattleCinematicBreakupLighting.FaceNormal(new(-32768, -32768, 0), new(32767, -32768, 0), new(-32768, 32767, 0)));
    }

    [Fact]
    public void Packed_normal_shading_retains_padding_FIFO_material_and_IR0()
    {
        byte[] matrix = new byte[20];
        foreach (int offset in new[] { 0, 8, 16 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(offset), 4096);
        }
        NativeBattleScriptedLightingMatrix identity = new(matrix.ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray());
        NativeBattleScriptedLightingMemory lighting = new(identity, identity, new(0, 0, 0));
        byte[] packed = [0, 0, 0, 0, 0, 16, 0xAA, 0xBB];
        NativeBattleScriptedColorRegisters initial = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [1, 2, 3], Ir0 = 123, NormalInputKnown = [true, true, true, true, true, true, false, false] };
        NativeBattleScriptedColorRegisters result = NativeBattleCinematicBreakupLighting.Shade(lighting, packed, 0x24808080, initial);
        Assert.Equal(new uint?[] { 2, 3, 0x24800000 }, result.Rgb);
        Assert.Equal(packed, result.NormalInput!.Value);
        Assert.Equal(initial.NormalInputKnown, result.NormalInputKnown);
        Assert.Equal(0x24808080u, result.Material);
        Assert.Equal(123u, result.Ir0);
        Assert.Equal(0u, result.Flags);
    }
}
