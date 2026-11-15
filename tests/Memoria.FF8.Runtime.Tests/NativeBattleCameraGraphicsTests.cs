using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraGraphicsTests
{
    [Fact]
    public void F2KeepsUnitScaleWhileBonePairReferencesKeepTheirOwnDistanceScale()
    {
        NativeBattleActionMatrix primary = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 2048);
        NativeBattleActionGeometry context = new(OriginalOwner(0), BattleModelTargets.From([OriginalOwner(3)]), false) { Matrices = [primary, primary with { Scale = 1024 }, primary, primary, primary] };
        Assert.Equal(new NativeBattleVector(200, 220, 340), NativeBattlePoseMatrices.TransformCameraPoint(NativeBattleCameraGraphics.ReadMatrix(context, 0xF2), new(100, 20, 40), false));
        Assert.Equal(new NativeBattleVector(125, 205, 310), NativeBattlePoseMatrices.TransformCameraPoint(NativeBattleCameraGraphics.ReadMatrix(context, 0xF5), new(100, 20, 40), false));
        Assert.Equal((short)2048, context.Matrices.Value[0].Scale);
    }

    [Fact]
    public void Camera_21_reads_the_current_physical_targets_unsigned_byte145()
    {
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, OriginalOwner(0), new(0)), (5, OriginalOwner(5), new(0))]) with
        {
            ActionContext = Geometry(0, 5, 5)
        };
        Assert.Throws<NotSupportedException>(() => NativeBattleCameraGraphics.ReadVariable(graphics, 21).RequireScalar());
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(OriginalOwner(0), graphics.ActorModels.Get(OriginalOwner(0)) with { SequenceByte145 = 12 }).Set(OriginalOwner(5), graphics.ActorModels.Get(OriginalOwner(5)) with { SequenceByte145 = 255 }) };
        Assert.Equal(255, NativeBattleCameraGraphics.ReadVariable(graphics, 21).RequireScalar());
        graphics = graphics with { ActionContext = Geometry(0, 0, 0) };
        Assert.Equal(12, NativeBattleCameraGraphics.ReadVariable(graphics, 21).RequireScalar());
    }

    [Fact]
    public void CameraPartyCountsExcludeHealthyEnemiesAndUseDistinctStatusPredicates()
    {
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(2) { ModelId4 = (byte)(index < 3 ? index : 40), Flags8 = index == 1 ? 4u : index == 2 ? 2u : 0u }).ToArray();
        NativeBattleGraphicsState graphics = OriginalGraphics(models.Select((model, slot) => (slot, OriginalOwner(slot), model)));
        Assert.Equal(2, NativeBattleCameraGraphics.ReadVariable(graphics, 19).RequireScalar());
        Assert.Equal(1, NativeBattleCameraGraphics.ReadVariable(graphics, 27).RequireScalar());
        graphics = graphics with { ActionContext = Geometry(2, 6, 0, 4, 6) };
        Assert.Equal(2, NativeBattleCameraGraphics.ReadVariable(graphics, 24).RequireScalar());
        Assert.Equal(6, NativeBattleCameraGraphics.ReadVariable(graphics, 25).RequireScalar());
        Assert.Equal(3, NativeBattleCameraGraphics.ReadVariable(graphics, 26).RequireScalar());
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(OriginalOwner(6), models[6] with { Flags8 = 0x41021 }) };
        Assert.Equal(0x41020, NativeBattleCameraGraphics.ReadVariable(graphics, 22).RequireScalar());
        graphics = graphics with { ActionContext = Geometry(6, 1, 1) };
        Assert.Equal(0x21, NativeBattleCameraGraphics.ReadVariable(graphics with { ActorModels = graphics.ActorModels.Set(OriginalOwner(1), models[1] with { Flags8 = 0x20 }) }, 22).RequireScalar());
    }
    private static NativeBattleActionGeometry Geometry(int source, int target, params int[] recipients)
    {
        BattleModelTargets targets = BattleModelTargets.From(recipients.Select(OriginalOwner));
        return new(OriginalOwner(source), targets, false) { Target = OriginalOwner(target), TargetActors = targets };
    }

}
