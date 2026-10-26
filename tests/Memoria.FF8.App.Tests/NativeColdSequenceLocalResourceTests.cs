using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeColdSequenceLocalResourceTests
{
    [Fact]
    public void Actual_cold_asset_reader_supplies_the_fresh_graphics_sequence_local_bank()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleRetainedMemory imported = assets.ReadColdProcessNativeBattleMemory();
        imported.Validate();
        Assert.NotNull(imported.ModelSequenceLocals);
        Assert.Equal(7, imported.ModelSequenceLocals.Value.Length);
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearBank(imported.ModelColors40, imported.ModelStatusTimers, imported.ModelWorldMatrices64, imported.ModelTransformRows, imported.ModelSequenceLocals);
        foreach (NativeBattleGraphicsModel model in models)
        {
            Assert.NotNull(model.SequenceState);
            Assert.Equal(8, model.SequenceState.Locals.Length);
            Assert.All(model.SequenceState.Locals, value => Assert.Equal((short)0, value));
        }
        NativeBattleSequenceState loaded = models[4].SequenceState!.InitializeControl5027D0(1, 0);
        Assert.Equal((short)0, loaded.Locals[7]);
        Assert.All(new NativeBattleSequenceState().Locals, Assert.Null);
    }
}
