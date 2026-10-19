using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRetainedModelMatricesTests
{
    [Fact]
    public void AuthenticatedColdLoaderPublishesOnlyTheActualSevenModelMatrixFields()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(assets.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory original = new(executable);
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemoryReader.ReadColdProcess(original);
        Assert.NotNull(memory.ModelWorldMatrices64);
        Assert.Equal(7, memory.ModelWorldMatrices64.Value.Length);
        for (int model = 0; model < 7; model++)
        {
            byte[] bytes = original.ReadLoader(checked(0x1D97300u + 156u * (uint)model), 32);
            NativeBattleActionMatrix matrix = Assert.IsType<NativeBattleActionMatrix>(memory.ModelWorldMatrices64.Value[model]);
            Assert.Equal(Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(lane * 2))), matrix.Rotation);
            Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20)), matrix.X);
            Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)), matrix.Y);
            Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28)), matrix.Z);
        }
    }
}
