using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionMatrixTests
{
    [Fact]
    public void Geometry_bone_matrices_use_the_published_model_bank_after_inline_model_changes()
    {
        BattlePartyActorModel source = new(new("actor"));
        BattleEnemyActorModel target = new(new("target"));
        NativeBattleGraphicsModel actor = new(3) { Transform = new(new(100, 0, 0), new(100, 0, 0), 0, 0, 4096), Rotation12 = 0, Rotation14 = 0, Rotation16 = 0 };
        NativeBattleGraphicsModel recipient = new(3) { Transform = new(new(6000, 0, 0), new(6000, 0, 0), 0, 0, 4096) };
        BattleActorModels models = new([new(source, actor), new(target, recipient)]);
        BattleRetainedModelReference targetReference = new BattleRetainedModelReference.CanonicalOwner(target);
        BattleRetainedModelSelection targets = BattleRetainedModelSelection.From([targetReference]);
        NativeBattleGraphicsState stale = new(models, NativeBattleModelBoundary.FromRetainedMemory(null), 0, 0, OriginalEffects()) { ActionContext = new(new BattleRetainedModelReference.CanonicalOwner(source), targets, false) { TargetActors = targets, Target = targetReference } };
        NativeBattleGraphicsState published = stale with { ActorModels = stale.ActorModels.Set(source, actor with { Transform = actor.Transform! with { Position28 = new(1000, 0, 0) } }) };
        List<BattleModelOwner> reads = [];
        NativeBattleGraphicsState actual = NativeBattleActionGeometryRules.PrepareMatrices50D300(published, (bank, owner, bone, fraction) =>
        {
            Assert.Same(published, bank);
            Assert.Equal((short)2048, fraction);
            reads.Add(owner);
            NativeBattleVector position = bank.ActorModels.Get(owner).Transform!.Position28;
            return position with { Y = bone == 240 ? (short)100 : (short)200 };
        });
        Assert.Equal<BattleModelOwner>([source, source, target, target], reads);
        Assert.Equal(3500, actual.ActionContext!.Matrices!.Value[1].X);
    }

    [Theory]
    [InlineData(0, 0, 5000, 110, 220, 2830)]
    [InlineData(5000, 0, 0, 2630, 220, 290)]
    [InlineData(0, 0, -5000, 90, 220, -2230)]
    public void CapturedDirectionTransformsTheLocalAxesAroundTheNativeMidpoint(short dx, short dy, short dz, short x, short y, short z)
    {
        NativeBattleActionMatrix matrix = NativeBattleActionGeometryRules.CaptureMatrix(new(100, 200, 300), new((short)(100 + dx), (short)(200 + dy), (short)(300 + dz)), new(0, 0, 0));
        Assert.Equal((short)4096, matrix.Scale);
        Assert.Equal(new NativeBattleVector(x, y, z), NativeBattlePoseMatrices.TransformCameraPoint(matrix, new(10, 20, 30), false));
        NativeBattleActionMatrix restored = JsonSerializer.Deserialize<NativeBattleActionMatrix>(JsonSerializer.Serialize(matrix))!;
        Assert.Equal(NativeBattlePoseMatrices.TransformCameraPoint(matrix, new(10, 20, 30), true), NativeBattlePoseMatrices.TransformCameraPoint(restored, new(10, 20, 30), true));
    }

    [Fact]
    public void GroupTargetUsesBoundsRatherThanTheMeanAndClearsHeight()
    {
        Assert.Equal(new NativeBattleVector(-1, 0, 3), NativeBattleActionGeometryRules.TargetCenter([new(-9, 90, -3), new(-8, 80, 2), new(6, -50, 10)]));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGeometryRules.TargetCenter([]));
    }

    [Fact]
    public void CoincidentTargetsUseTheCapturedActorRotationAndZeroScaleKeepsNativeMirrorFallback()
    {
        NativeBattleActionMatrix matrix = NativeBattleActionGeometryRules.CaptureMatrix(new(100, 200, 300), new(100, 200, 300), new(0, 2048, 0));
        Assert.Equal(new NativeBattleVector(90, 220, 270), NativeBattlePoseMatrices.TransformCameraPoint(matrix, new(10, 20, 30), false));
        Assert.Equal(new NativeBattleVector(10, 20, -30), NativeBattlePoseMatrices.TransformCameraPoint(matrix with { Scale = 0 }, new(10, 20, 30), true));
        Assert.Throws<InvalidDataException>(() => (matrix with { Rotation = ImmutableArray<short>.Empty }).Validate());
    }
}
