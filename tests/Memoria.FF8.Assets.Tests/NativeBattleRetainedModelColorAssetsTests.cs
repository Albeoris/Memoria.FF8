using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRetainedModelColorAssetsTests
{
    [Fact]
    public void Authenticated_cold_model_colors_match_all_seven_actual_loader_DWORDs()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeExecutableMemory image = new(File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe")));
        NativeBattleRetainedMemory memory = game.ReadColdProcessNativeBattleMemory();
        ImmutableArray<uint?> colors = memory.ModelColors40 ?? throw new InvalidDataException("The cold model-color owner is required.");
        Assert.Equal(7, colors.Length);
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearBank(memory.ModelColors40);
        for (int index = 0; index < 7; index++)
        {
            uint expected = BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(checked(0x1D972E8u + (uint)(index * 156)), 4));
            Assert.Equal(expected, colors[index]);
            Assert.Equal(expected, models[index].Color40);
            Assert.Equal((ushort)0, models[index].Flags0);
        }
    }
}
