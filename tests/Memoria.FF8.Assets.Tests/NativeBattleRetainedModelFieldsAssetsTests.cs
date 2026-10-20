using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRetainedModelFieldsAssetsTests
{
    [Fact]
    public void Authenticated_all_seven_model_rows_supply_cold_rotation_padding_transform_and_normal_color()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeExecutableMemory image = new(game.ReadNativeExecutable());
        NativeBattleRetainedMemory memory = game.ReadColdProcessNativeBattleMemory();
        ImmutableArray<NativeBattleModelRetainedFields> fields = memory.ModelTransformRows ?? throw new InvalidDataException("The retained physical model fields are required.");
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearBank(retainedTransformRows: fields);
        Assert.Equal(7, fields.Length);
        for (int index = 0; index < 7; index++)
        {
            byte[] original = image.ReadLoader(checked(0x1D972C0u + (uint)index * 156), 156);
            NativeBattleModelRetainedFields expected = NativeBattleModelRetainedFields.FromLoaderBytes(original);
            Assert.Equal(expected, fields[index]);
            Assert.Equal(expected, NativeBattleModelRetainedFields.Capture(models[index]));
            Assert.Equal(expected, NativeBattleRetainedMemory.FromColdProcess().ModelTransformRows!.Value[index]);
        }
    }
}
