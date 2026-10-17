using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShortVectorTests
{
    [Fact]
    public void Low_word_stores_wrap_each_rounded_component_instead_of_saturating_or_applying_translation()
    {
        NativeBattleActionMatrix matrix = new([8192, 0, 0, 0, -8192, 0, 0, 0, 16384], 1234, -5678, int.MaxValue, 4096);
        NativeBattleVector point = new(20000, -20000, 20000);
        Assert.Equal((40000, 40000, 80000), NativeBattleGfMatrices.Rotate56C3D0(matrix, point));
        Assert.Equal(new NativeBattleVector(-25536, -25536, 14464), NativeBattleGfMatrices.RotateShort56C4F0(matrix, point));
    }

    [Fact]
    public void The_same_dot_product_core_retains_row_operand_order_and_biased_rounding_of_both_half_signs()
    {
        NativeBattleActionMatrix rows = new([4096, 2048, -1024, -2048, 4096, 1024, 1024, -1024, 4096], 0, 0, 0, 4096);
        Assert.Equal(new NativeBattleVector(9, 11, 12), NativeBattleGfMatrices.RotateShort56C4F0(rows, new(7, 11, 13)));
        NativeBattleActionMatrix half = rows with { Rotation = [2048, 0, 0, 0, 2048, 0, 0, 0, 2048] };
        Assert.Equal(new NativeBattleVector(0, 2, -2), NativeBattleGfMatrices.RotateShort56C4F0(half, new(1, 3, -3)));
        Assert.Equal(new NativeBattleVector(0, -2, 2), NativeBattleGfMatrices.RotateShort56C4F0(half, new(-1, -3, 3)));
    }

    [Fact]
    public void Serialized_matrix_keeps_the_same_result_and_missing_rotation_words_remain_invalid()
    {
        NativeBattleActionMatrix matrix = new([8192, 1024, -4096, -1024, 8192, 2048, 4096, -2048, 8192], 33, 44, 55, 4096);
        NativeBattleActionMatrix independent = JsonSerializer.Deserialize<NativeBattleActionMatrix>(JsonSerializer.Serialize(matrix))!;
        NativeBattleVector point = new(short.MinValue, short.MaxValue, -12345);
        Assert.Equal(NativeBattleGfMatrices.RotateShort56C4F0(matrix, point), NativeBattleGfMatrices.RotateShort56C4F0(independent, point));
        Assert.Throws<InvalidDataException>(() => NativeBattleGfMatrices.RotateShort56C4F0(matrix with { Rotation = [] }, point));
    }
}
