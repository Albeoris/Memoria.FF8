using System.Numerics;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverForeignSkeletonTests
{
    [Theory]
    [InlineData(0, 10, -20, 30)]
    [InlineData(1024, 35, -45, 80)]
    [InlineData(2048, 60, -70, 130)]
    [InlineData(4096, 110, -120, 230)]
    public void OrdinarySelectorsUseTheActualHostBoneSegmentAndNativeFraction(short fraction, short x, short y, short z)
    {
        NativeBattleForeignActor actor = Foreign().ToActor(3);
        Assert.Equal(1, actor.BoneCount);
        Assert.Equal(new NativeBattleVector(x, y, z), actor.ReadBone(0, fraction));
        Assert.Throws<NotSupportedException>(() => actor.ReadBone(1, 0));
        Assert.Throws<NotSupportedException>(() => actor.ReadBone(0, -1));
    }

    private static CrossoverNativeForeignTarget Foreign()
    {
        NativeBattleForeignSurface surface = new(new NativeBattleForeignRenderMaterial(new(null), false), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        Ff7BattleActorGeometry geometry = new([], [Vector3.Zero])
        {
            GuestFaces = [(Vector3.Zero, Vector3.UnitX, Vector3.UnitY)],
            GuestSurfaces = [surface],
            Bones = [(new(10, -20, 30), new(110, -120, 230))]
        };
        return new(1, new(default, default, 0, 0, 4096), NativeBattleForeignModelGeometry.Identity, new(default, default, 0, new(default, default)), geometry, Matrix4x4.Identity);
    }
}
