using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianFormCameraTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleCameraProjection Projection = new(Identity, 512, 160, 108);

    [Fact]
    public void SourceSizedBodyFitsWithoutMovingOrScalingItsVertices()
    {
        ImmutableArray<NativeBattleRawMesh> body = [new([new(new(-1000, -3000, 6000), 0), new(new(1000, 0, 6000), 0)], [])];
        string before = JsonSerializer.Serialize(body);
        ushort focal = NativeBattleGuardianFormCamera.FitFocalLength(512, Projection, [Identity], body);
        Assert.InRange(focal, (ushort)1, (ushort)511);
        Assert.All(body[0].Vertices, vertex => Assert.Equal(0u, Project(vertex, focal).DepthAndClip & 0x3020000));
        Assert.Contains(body[0].Vertices, vertex => (Project(vertex, checked((ushort)(focal + 1))).DepthAndClip & 0x3020000) != 0);
        Assert.Equal(before, JsonSerializer.Serialize(body));
        Assert.Equal(focal, NativeBattleGuardianFormCamera.FitFocalLength(focal, Projection, [Identity], body));
    }

    [Fact]
    public void AlreadyFittingBodyPreservesTheSourceFocalLength()
    {
        ImmutableArray<NativeBattleRawMesh> body = [new([new(new(-10, -10, 6000), 0), new(new(10, 10, 6000), 0)], [])];
        Assert.Equal(512, NativeBattleGuardianFormCamera.FitFocalLength(512, Projection, [Identity], body));
    }

    [Fact]
    public void EveryBoneAndRestoredProjectionUsesTheSameNativeFrustum()
    {
        NativeBattleActionMatrix translated = Identity with { Y = -3000 };
        ImmutableArray<NativeBattleRawMesh> body = [new([new(new(0, 0, 6000), 0), new(new(0, 0, 6000), 1)], [])];
        ushort focal = NativeBattleGuardianFormCamera.FitFocalLength(512, Projection, [Identity, translated], body);
        Assert.True(focal < 512);
        NativeBattleCameraProjection restored = JsonSerializer.Deserialize<NativeBattleCameraProjection>(JsonSerializer.Serialize(Projection))!;
        Assert.Equal(focal, NativeBattleGuardianFormCamera.FitFocalLength(512, restored, [Identity, translated], body));
    }

    [Fact]
    public void MissingBoneOrBodyBehindCameraFailsExplicitly()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleGuardianFormCamera.FitFocalLength(512, Projection, [Identity], [new([new(new(0, 0, 6000), 1)], [])]));
        Assert.Throws<NotSupportedException>(() => NativeBattleGuardianFormCamera.FitFocalLength(512, Projection, [Identity], [new([new(new(0, 0, -6000), 0)], [])]));
    }

    private static NativeBattleProjectedVertex Project(NativeBattleRawVertex vertex, ushort focal)
    {
        return NativeBattleGeometryProjection.Project50F900(Identity, vertex.Position, Projection with { FocalLength = focal }, new(0, 0, 320, 216));
    }
}
