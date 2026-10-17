using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraGraphicsTests
{
    [Fact]
    public void F2KeepsUnitScaleWhileBonePairReferencesKeepTheirOwnDistanceScale()
    {
        NativeBattleActionMatrix primary = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 2048);
        NativeBattleActionGeometry context = new(0, 8) { Matrices = [primary, primary with { Scale = 1024 }, primary, primary, primary] };
        Assert.Equal(new NativeBattleVector(200, 220, 340), NativeBattlePoseMatrices.TransformCameraPoint(NativeBattleCameraGraphics.ReadMatrix(context, 0xF2), new(100, 20, 40), false));
        Assert.Equal(new NativeBattleVector(125, 205, 310), NativeBattlePoseMatrices.TransformCameraPoint(NativeBattleCameraGraphics.ReadMatrix(context, 0xF5), new(100, 20, 40), false));
        Assert.Equal((short)2048, context.Matrices.Value[0].Scale);
    }

    [Fact]
    public void Camera_21_reads_the_current_physical_targets_unsigned_byte145()
    {
        NativeBattleGraphicsState graphics = new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0)
        {
            ActionContext = new(0, 1 << 5) { TargetIndex = 5 }
        };
        Assert.Throws<NotSupportedException>(() => NativeBattleCameraGraphics.ReadVariable(graphics, 21));
        graphics = graphics with { Models = graphics.Models.SetItem(0, graphics.Models[0] with { SequenceByte145 = 12 }).SetItem(5, graphics.Models[5] with { SequenceByte145 = 255 }) };
        Assert.Equal(255, NativeBattleCameraGraphics.ReadVariable(graphics, 21));
        graphics = graphics with { ActionContext = graphics.ActionContext with { TargetIndex = 0 } };
        Assert.Equal(12, NativeBattleCameraGraphics.ReadVariable(graphics, 21));
    }

    [Fact]
    public void CameraPartyCountsExcludeHealthyEnemiesAndUseDistinctStatusPredicates()
    {
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 2) { ModelId4 = (byte)(index < 3 ? index : 40), Flags8 = index == 1 ? 4u : index == 2 ? 2u : 0u }).ToArray();
        NativeBattleGraphicsState graphics = new(models.ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0);
        Assert.Equal(2, NativeBattleCameraGraphics.ReadVariable(graphics, 19));
        Assert.Equal(1, NativeBattleCameraGraphics.ReadVariable(graphics, 27));
        graphics = graphics with { ActionContext = new(2, 0x51) { PhysicalTargetMask = 0x51, TargetIndex = 6 } };
        Assert.Equal(2, NativeBattleCameraGraphics.ReadVariable(graphics, 24));
        Assert.Equal(6, NativeBattleCameraGraphics.ReadVariable(graphics, 25));
        Assert.Equal(3, NativeBattleCameraGraphics.ReadVariable(graphics, 26));
        graphics = graphics with { Models = graphics.Models.SetItem(6, models[6] with { Flags8 = 0x41021 }) };
        Assert.Equal(0x41020, NativeBattleCameraGraphics.ReadVariable(graphics, 22));
        graphics = graphics with { ActionContext = new(6, 2) { PhysicalTargetMask = 2, TargetIndex = 1 } };
        Assert.Equal(0x21, NativeBattleCameraGraphics.ReadVariable(graphics with { Models = graphics.Models.SetItem(1, models[1] with { Flags8 = 0x20 }) }, 22));
    }
}
