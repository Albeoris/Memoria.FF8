using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldPathProjectionTests
{
    [Theory]
    [InlineData(-16, 8190)]
    [InlineData(-32, 8184)]
    public void ProximityRoundsHalfIntegerOffsetsBeforeSelectingTheReferenceSector(short pitch, int boundary)
    {
        for (int delta = -1; delta <= 1; delta++)
        {
            NativeWorldFootCameraState camera = new(0, boundary + delta, 0, 0, boundary + delta, 0, 0, pitch, 0, -7168);
            NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(camera);
            Assert.Equal(delta >= 0, context.IsNear(new(0, 0, -12288)));
            Assert.NotNull(context.ViewRotation);
            Assert.NotNull(context.ViewTranslation);
            Assert.NotNull(context.CameraRelativeOffset);
            Assert.NotNull(context.ProjectNode(new(0, 0, -12288)));
        }
    }

    [Fact]
    public void ProximityPreservesSectorWrapAtTheRoundedBoundary()
    {
        NativeWorldFootCameraState camera = new(0, 98302, 0, 0, 98302, 0, 0, -16, 0, -7168);
        NativeWorldPathPosition node = new(0, 0, 94208);
        Assert.True(NativeWorldPathProjectionCertificate.CreateFoot(camera).IsNear(node));
        Assert.False(NativeWorldPathProjectionCertificate.CreateFoot(camera with { RawZ = 98301 }).IsNear(node));
    }

    [Fact]
    public void StationCameraUsesOneRoundedReferenceForDistantTrainNodes()
    {
        NativeWorldFootCameraState camera = new(-28127, -24720, -664, -28137, -24692, -932, 3817, -512, -341, -7168) { ProjectionDistance = 640 };
        NativeWorldPathPosition node = new(-20728, -487, 40736);
        Assert.False(NativeWorldPathProjectionCertificate.CreateFoot(camera).IsNear(node));
        Assert.True(NativeWorldPathProjectionCertificate.CreateFoot(camera with { RawZ = -24721 }).IsNear(node));
        Assert.False(NativeWorldPathProjectionCertificate.CreateRagnarok(camera).IsNear(node));
    }

    [Fact]
    public void FootProjectionUsesTheRetainedFocalDistanceForScreenVisibility()
    {
        NativeWorldFootCameraState camera = new(0, 0, 0, 0, 0, 0, 0, 0, 0, -7168);
        NativeWorldPathPosition node = new(3000, 0, 0);
        NativeWorldPathNodeProjection normal = NativeWorldPathProjectionCertificate.CreateFoot(camera).ProjectNode(node)!;
        NativeWorldPathNodeProjection wide = NativeWorldPathProjectionCertificate.CreateFoot(camera with { ProjectionDistance = 640 }).ProjectNode(node)!;
        Assert.NotNull(normal);
        Assert.NotNull(wide);
        Assert.False(normal.Visible);
        Assert.True(wide.Visible);
        Assert.Equal(normal.Words, wide.Words);
        Assert.Throws<InvalidDataException>(() => (camera with { ProjectionDistance = 639 }).Validate());
        Assert.Throws<InvalidDataException>(() => (camera with { ProjectionDistance = 1025 }).Validate());
    }

    [Fact]
    public void OriginalTrackElevenProjectsActualNodesAndRestoresTheirNativeReceipts()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(11);
        NativeWorldScriptedPathState live = NativeWorldScriptedPathState.Create(definition, new WorldTransition(11, 66, 61), null);
        NativeWorldPathNodeProjection? Project(NativeWorldPathPosition node, NativeWorldPathCamera camera, NativeWorldPathPosition origin)
        {
            NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.Create(camera, origin, live.EntityType);
            NativeWorldPathNodeProjection? projection = context.ProjectNode(node);
            if (projection is not null)
            {
                projection.Validate(node, camera, origin);
                Vector3 anchor = context.CanonicalModelAnchor(projection);
                Assert.Equal(-projection.Words.Y, anchor.Y);
                Assert.Equal(node.X, anchor.X);
                Assert.Equal(-node.Z, anchor.Z);
            }
            return projection;
        }
        live = live.Step(definition, Project);
        Assert.NotNull(live.Presentation[1].Projection);
        Assert.True(live.Presentation[1].Projection!.Visible);
        Assert.NotNull(live.Presentation[1].Angles);
        Assert.Equal((short)0, live.Presentation[1].Angles!.Pitch);
        int known = 1;
        int unknown = 0;
        int visible = 1;
        NativeWorldScriptedPathState restored = JsonSerializer.Deserialize<NativeWorldScriptedPathState>(JsonSerializer.Serialize(live))!;
        while (!live.EndpointRequested)
        {
            live = live.Step(definition, Project);
            restored = restored.Step(definition, Project);
            Assert.Equal(JsonSerializer.Serialize(live), JsonSerializer.Serialize(restored));
            if (live.Presentation[1].Projection is null)
            {
                unknown++;
            }
            else
            {
                known++;
                visible += live.Presentation[1].Projection!.Visible ? 1 : 0;
            }
        }
        Assert.Equal(230, live.Callback);
        Assert.Equal(230, known + unknown);
        Assert.True(visible > 100);
        Console.WriteLine($"Original boat projection: {known} unique/{unknown} unsupported callback receipts, {visible} visible; first actual callback projected linked orientation and independent restore matched.");
    }

    [Fact]
    public void SectorRelativeOutputReconstructsTheNativeWrappedAnchor()
    {
        NativeWorldPathPosition origin = new(12464, 0, 24673);
        NativeWorldPathCamera camera = new(new(20580, -1563, 26201), new(16816, -868, 25262));
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.Create(camera, origin, 66);
        NativeWorldPathNodeProjection projected = context.ProjectNode(origin)!;
        Assert.NotNull(projected);
        Assert.Equal(new NativeWorldPathWords(176, 0, -1951), projected.Words);
        Assert.Equal(new Vector3(12464, 0, -24673), context.CanonicalModelAnchor(projected));
        Assert.Throws<InvalidDataException>(() => context.CanonicalModelAnchor(projected with { Origin = new(0, 0, 0) }));
        Assert.Throws<InvalidDataException>(() => context.ValidateProjection(projected with { Words = projected.Words with { Y = 1 } }));
        Assert.Throws<InvalidDataException>(() => context.ValidateProjection(projected with { Visible = !projected.Visible }));
        Assert.Throws<NotSupportedException>(() => NativeWorldPathProjectionCertificate.Create(camera, origin, 0));
    }

    [Fact]
    public void NativeCurvatureAndWorldWrapChangeThePublishedModelAnchor()
    {
        NativeWorldPathProjectionCertificate deep = NativeWorldPathProjectionCertificate.Create(new NativeWorldPathCamera(new(0, 0, 0), new(0, 0, 1000)), new(0, 0, 0), 66);
        NativeWorldPathNodeProjection curved = deep.ProjectNode(new(0, 0, 30000))!;
        Assert.NotNull(curved);
        Assert.Equal(new NativeWorldPathWords(0, 1225, 30000), curved.Words);
        Assert.False(curved.Visible);
        Assert.Equal(new Vector3(0, -1225, -30000), deep.CanonicalModelAnchor(curved));
        NativeWorldPathPosition origin = new(131000, 0, 1000);
        NativeWorldPathProjectionCertificate wrap = NativeWorldPathProjectionCertificate.Create(new NativeWorldPathCamera(origin, new(131000, 0, 2000)), origin, 66);
        NativeWorldPathNodeProjection crossed = wrap.ProjectNode(new(-131000, 0, 1000))!;
        Assert.NotNull(crossed);
        Assert.Equal((short)2120, crossed.Words.X);
        Assert.Equal(new Vector3(131144, 0, -1000), wrap.CanonicalModelAnchor(crossed));
    }
}
