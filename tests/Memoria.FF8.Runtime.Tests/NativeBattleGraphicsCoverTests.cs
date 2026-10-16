using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsCoverTests
{
    [Fact]
    public void CoverHoldsThenReturnsToInitialZWithTheOriginalTwoStepHop()
    {
        NativeBattleGraphicsModel model = new(0, 3) { Flags2 = 0, Flags8 = 0, Transform = new(new(100, 200, 300), new(120, 220, 320), 0, 0, 256) };
        model = NativeBattleGraphicsModels.StartService(model, 9);
        for (int callback = 0; callback < 15; callback++)
        {
            model = NativeBattleGraphicsModels.AdvanceCover(model);
            Assert.Equal(new NativeBattleVector(120, 220, 1100), model.Transform!.Position28);
        }
        Assert.Equal((byte)15, model.Phase6);
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(model))!;
        model = NativeBattleGraphicsModels.AdvanceCover(model);
        Assert.Equal(new NativeBattleVector(120, -100, 700), model.Transform!.Position28);
        Assert.Equal(JsonSerializer.Serialize(model), JsonSerializer.Serialize(NativeBattleGraphicsModels.AdvanceCover(restored)));
        model = NativeBattleGraphicsModels.AdvanceCover(model);
        Assert.Equal(new NativeBattleVector(120, 0, 300), model.Transform!.Position28);
        Assert.Equal((byte)9, model.Kind5);
        model = NativeBattleGraphicsModels.AdvanceCover(model);
        Assert.Equal((byte)0, model.Kind5);
        Assert.Equal((byte)18, model.Phase6);
    }
}
