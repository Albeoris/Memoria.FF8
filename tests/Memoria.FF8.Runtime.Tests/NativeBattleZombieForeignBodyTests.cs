using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleZombieForeignBodyTests
{
    private sealed record Material : INativeBattleForeignMaterial;

    [Fact]
    public void Original_wave_projection_preserves_posed_host_triangles_and_material_with_bounded_scratch()
    {
        NativeBattleForeignSurface surface = new(new Material(), [new(0, 0), new(1, 0), new(0, 1)], [Vector4.One, Vector4.One, Vector4.One]);
        NativeBattleForeignModelFace face = new(new(-256, -256, 0), new(256, -256, 0), new(-256, 256, 0), surface);
        NativeBattleActionMatrix world = NativeBattleForeignModelGeometry.Identity;
        NativeBattleForeignModelGeometry geometry = new(world, new(default, default, 0, new(default, default)), [face]);
        NativeBattleCameraProjection projection = new(world with { Z = 8192 }, 128, 160, 108);
        ImmutableArray<short> wave = Enumerable.Repeat((short)4096, 4096).ToImmutableArray();
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.FromColdProcess()), 24);
        NativeBattlePilotVisualIdentity identity = new(114, 1, 0, 1, 0);
        NativeBattleZombieOverlayDraw actual = NativeBattleZombieOverlay.DrawForeign(geometry, projection, 0x808080, temporary.Staging, temporary.Address, wave, 1280, 0, 256, 0, 0, identity);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(projection.View!, geometry.World);
        NativeBattleZombieOverlayDraw native = NativeBattleZombieOverlay.Draw([face.Triangle()], [matrix], projection, 0x808080, 1, null, temporary.Address, 24, temporary.Staging, wave, 1280, 0, 256, 0, 0, identity);
        NativeBattleGeometryPrimitive primitive = Assert.Single(actual.Packets.Attempts.Where(item => item.Primitive is not null)).Primitive!;
        NativeBattleGeometryPrimitive expected = Assert.Single(native.Packets.Attempts.Where(item => item.Primitive is not null)).Primitive!;
        Assert.Equal(expected.Words.ToArray(), primitive.Words.ToArray());
        Assert.Equal(native.Staging.Read(temporary.Address, 24), actual.Staging.Read(temporary.Address, 24));
        Assert.Same(surface, primitive.PilotGeometry!.ForeignSurface);
        Assert.True(primitive.PilotGeometry.ScreenSpace);
        Assert.Equal(expected.PilotGeometry!.A, primitive.PilotGeometry.A);
        _ = NativeBattleTemporaryMemory.Release5082D0(actual.Staging, 24);
    }
}
