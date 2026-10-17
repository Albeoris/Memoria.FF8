using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCuragaMorphAssetsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Four_original_shape_records_keep_their_eighty_four_vertices_and_morph_into_the_actual_mesh_plus_eight_destination(int index)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleCuragaExecutableState bank = NativeBattleCuragaExecutableState.FromColdProcess().Initialize87EA20(game.ReadNativeCuragaResources().Executable!);
        uint mesh = Pointer(0x84 + 12 * 4);
        uint first = Pointer(0xC4 + index * 4);
        uint second = Pointer(0xC4 + ((index + 1) % 4) * 4);
        int count = BinaryPrimitives.ReadInt32LittleEndian(bank.Read(mesh + 4, 4));
        Assert.Equal(84, count);
        Assert.Equal(count, BinaryPrimitives.ReadInt32LittleEndian(bank.Read(first + 4, 4)));
        Assert.Equal(680u, BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(first, 4)));
        int target = checked((int)(mesh - NativeBattleCuragaExecutableState.Address + 8));
        int shapeA = checked((int)(first - NativeBattleCuragaExecutableState.Address + 8));
        int shapeB = checked((int)(second - NativeBattleCuragaExecutableState.Address + 8));
        Assert.True(target + count * 8 <= shapeA);
        Assert.True(target + count * 8 <= shapeB);
        var original = bank.Data!.Value;
        var result = NativeBattleGfMorphGeometry.Morph45E9D0(original, shapeA, original, shapeB, count, 0, original, target);
        for (int vertex = 0; vertex < count; vertex++)
        {
            Assert.Equal(original.AsSpan().Slice(shapeA + vertex * 8, 6).ToArray(), result.AsSpan().Slice(target + vertex * 8, 6).ToArray());
            Assert.Equal(original.AsSpan().Slice(target + vertex * 8 + 6, 2).ToArray(), result.AsSpan().Slice(target + vertex * 8 + 6, 2).ToArray());
        }
        Assert.Equal(original.Take(target), result.Take(target));
        Assert.Equal(original.Skip(target + count * 8), result.Skip(target + count * 8));
        NativeBattleCuragaExecutableState changed = bank with { Data = result };
        changed.Validate();
        NativeBattleCuragaExecutableState clone = JsonSerializer.Deserialize<NativeBattleCuragaExecutableState>(JsonSerializer.Serialize(changed))!;
        Assert.Equal(result.ToArray(), clone.Data!.Value.ToArray());

        uint Pointer(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(NativeBattleCuragaExecutableState.Address + (uint)offset, 4));
    }
}
