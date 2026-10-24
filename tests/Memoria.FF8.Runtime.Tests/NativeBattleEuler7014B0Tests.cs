using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEuler7014B0Tests
{
    [Fact]
    public void Rounded_YZ_then_X_witness_0()
    {
        NativeBattleVector angles = new(0, 0, 0);
        short[] expected = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096];
        Assert.Equal(expected, NativeBattleEuler7014B0.Rotation(angles).Rotation);
    }

    [Fact]
    public void Rounded_YZ_then_X_witness_1()
    {
        NativeBattleVector angles = new(-285, 0, 455);
        short[] expected = [3138, 2385, -1114, -2632, 2843, -1328, 0, 1734, 3711];
        Assert.Equal(expected, NativeBattleEuler7014B0.Rotation(angles).Rotation);
    }

    [Fact]
    public void Rounded_YZ_then_X_witness_2()
    {
        NativeBattleVector angles = new(284, 0, -456);
        short[] expected = [3134, -2390, -1113, 2637, 2841, 1323, 0, -1729, 3713];
        Assert.Equal(expected, NativeBattleEuler7014B0.Rotation(angles).Rotation);
    }

    [Fact]
    public void Rounded_YZ_then_X_witness_3()
    {
        NativeBattleVector angles = new(1024, 1024, 1024);
        short[] expected = [0, -4096, 0, -4096, 0, 0, 0, 0, -4096];
        Assert.Equal(expected, NativeBattleEuler7014B0.Rotation(angles).Rotation);
    }

    [Fact]
    public void Rounded_YZ_then_X_witness_4()
    {
        NativeBattleVector angles = new(-32768, 32767, -32767);
        short[] expected = [4096, 6, -6, -6, 4096, 0, 6, 0, 4096];
        Assert.Equal(expected, NativeBattleEuler7014B0.Rotation(angles).Rotation);
    }

    [Fact]
    public void Packed_writer_ignores_input_W_copies_unknown_padding_and_preserves_translation_masks()
    {
        NativeBattlePackedMatrix32 retained = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 123, Y = -456, Z = 789 }, 0x1234);
        bool[] known = retained.Known.ToArray();
        known[29] = false;
        retained = retained with { Known = known.ToImmutableArray() };
        NativeBattleEffectArena angles = NativeBattleEffectArena.Clear().MarkUnknown(6, 2).Write(0, BitConverter.GetBytes((short)-285)).Write(4, BitConverter.GetBytes((short)455));
        NativeBattlePackedMatrix32 result = NativeBattleEuler7014B0.Write(angles, retained);
        Assert.All(result.Known.Take(18), known => Assert.True(known));
        Assert.False(result.Known[18]);
        Assert.False(result.Known[19]);
        Assert.Equal(retained.Bytes.AsSpan().Slice(20).ToArray(), result.Bytes.AsSpan().Slice(20).ToArray());
        Assert.Equal(retained.Known.AsSpan().Slice(20).ToArray(), result.Known.AsSpan().Slice(20).ToArray());
        Assert.NotEqual(NativeBattleGfMatrices.Euler7015B0(new(-285, 0, 455)).Rotation.ToArray(), NativeBattleEuler7014B0.Rotation(new(-285, 0, 455)).Rotation.ToArray());
    }
}
