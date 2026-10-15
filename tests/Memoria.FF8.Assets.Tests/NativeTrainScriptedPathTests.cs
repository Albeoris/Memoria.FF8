using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTrainScriptedPathTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void ActualTrackNineUsesFiveNodesFourCarriagesAndAbsoluteHeightCameraSelectors()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(9);
        Assert.Equal("512427C09A78CCBE7E2524DAB87A0062615C370DF769BC5AE57D6E00A162C526", definition.ChunkHash);
        Assert.Equal("94F36ED692C5B1F6F818628D9F1D7AA4BC5FDA0CBA1BABB7B99B8CCBD72DC3FB", definition.CameraHash);
        Assert.Equal((byte)66, definition.FrontShape);
        Assert.Equal((byte)67, definition.RearShape);
        Assert.Equal(new short[] { 30, 54, 78, 102, 144, -1 }, definition.CameraRecords.Select(record => record.Deadline));
        NativeWorldScriptedPathState state = NativeWorldScriptedPathState.Create(definition, new WorldTransition(9, 22, 59), null);
        Assert.Equal((byte)2, state.Mode);
        Assert.Equal(5, state.NodeCount);
        Assert.Equal(2, state.CentralNode);
        Assert.Equal(100, state.MaximumSpeed);
        Assert.Equal(new[] { 2966, 2174, 1475, 792, 0 }, state.Nodes.Select(node => RailDistance(definition, node)));
        Assert.Equal(new sbyte[] { 1, 1, 0, -1, -1 }, Enumerable.Range(0, 5).Select(state.NodeLink));
        Assert.Equal(new byte[] { 66, 67, 67, 66 }, Enumerable.Range(0, 5).Where(index => state.NodeLink(index) != 0).Select(index => state.NodeShape(definition, index)));
        Assert.Equal(state.Nodes[2].Position, state.EntityPosition);
        Assert.All(state.Presentation, pose => Assert.True(pose.Angles is null && pose.Orientation is null && pose.Projection is null));
        NativeWorldPathPosition center = state.Nodes[2].Position;
        Assert.Equal(new NativeWorldPathCamera(new(center.X + 8400, -2500, center.Z + 2000), new(center.X + 4609, -1597, center.Z + 1097)), state.Camera);
        Assert.Equal(new NativeWorldPathCamera(new(center.X + 7800, -2500, center.Z + 2000), new(center.X + 4040, -1536, center.Z + 1036)), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 30));
        NativeWorldPathPosition last = state.Nodes[4].Position;
        Assert.Equal(new NativeWorldPathCamera(new(last.X + 3500, -2600, last.Z + 3300), new(last.X + 1140, -1049, last.Z + 468)), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 144));
        Assert.Equal(2, assets.ReadScriptedStaticModel(66).ModelIndex);
        Assert.Equal(3, assets.ReadScriptedStaticModel(67).ModelIndex);
        Assert.Throws<NotSupportedException>(() => NativeWorldScriptedPathState.Create(definition, new WorldTransition(9, 66, 59), null));
    }

    [Fact]
    public void SharedDriverPreservesPauseCapCameraOrderingIndependentRestoreAndEndpointFade()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(9);
        NativeWorldScriptedPathState live = NativeWorldScriptedPathState.Create(definition, new WorldTransition(9, 22, 59), null);
        for (int callback = 0; callback < 29; callback++)
        {
            live = live.Step(definition, (_, _, _) => null);
        }
        Assert.Equal(0, live.Speed);
        Assert.Equal((short)1, live.Pause);
        var originalNodes = live.Nodes;
        live = live.Step(definition, (_, _, _) => null);
        Assert.Equal(20, live.Speed);
        Assert.Equal(originalNodes[2].Position, live.EntityPosition);
        Assert.Equal(originalNodes, live.CameraReceipt.Nodes);
        Assert.Equal(20, RailDistance(definition, live.Nodes[0]) - RailDistance(definition, originalNodes[0]));
        NativeWorldScriptedPathState restored = JsonSerializer.Deserialize<NativeWorldScriptedPathState>(JsonSerializer.Serialize(live))!;
        int largestAdvance = 0;
        while (!live.EndpointRequested)
        {
            var before = live;
            live = live.Step(definition, (_, _, _) => null);
            restored = restored.Step(definition, (_, _, _) => null);
            Assert.Equal(JsonSerializer.Serialize(live), JsonSerializer.Serialize(restored));
            Assert.Equal(before.Nodes[2].Position, live.EntityPosition);
            if (!live.EndpointRequested)
            {
                int advance = RailDistance(definition, live.Nodes[0]) - RailDistance(definition, before.Nodes[0]);
                largestAdvance = Math.Max(largestAdvance, advance);
                Assert.InRange(advance, 0, 100);
            }
        }
        Assert.Equal(100, largestAdvance);
        Assert.Equal(new[] { 0, 792, 1475, 2174, 2966 }, live.Nodes.Select(node => RailDistance(definition, node, -1)));
        NativeWorldPathPosition first = live.Nodes[0].Position;
        Assert.Equal(new NativeWorldPathCamera(new(first.X + 3500, -2600, first.Z + 3300), new(first.X + 1140, -1049, first.Z + 468)), NativeWorldScriptedPathState.EvaluateCamera(definition, live.Nodes, -1, 144));
        NativeWorldPathCamera endpointCamera = live.Camera;
        for (int callback = 0; callback < 16; callback++)
        {
            live = live.Step(definition, (_, _, _) => throw new InvalidOperationException("Endpoint fade must not recompute rail or presentation."));
            restored = restored.Step(definition, (_, _, _) => throw new InvalidOperationException("Endpoint fade must not recompute rail or presentation."));
            Assert.Equal(endpointCamera, live.Camera);
            Assert.Equal(JsonSerializer.Serialize(live), JsonSerializer.Serialize(restored));
        }
        Assert.True(live.FieldTransitionReady);
        Assert.Equal((byte)59, live.CompletedFieldRow);
        JsonObject missing = JsonNode.Parse(JsonSerializer.Serialize(live))!.AsObject();
        missing.Remove(nameof(NativeWorldScriptedPathState.Presentation));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldScriptedPathState>(missing.ToJsonString()));
    }

    [Fact]
    public void EveryVisibleCarriageUsesItsOwnLinkedProjectionAndUnknownAnglesNeverBecomeDefaults()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(9);
        NativeWorldScriptedPathState live = NativeWorldScriptedPathState.Create(definition, new WorldTransition(9, 22, 59), null);
        NativeWorldPathNodeProjection? Project(NativeWorldPathPosition source, NativeWorldPathCamera camera, NativeWorldPathPosition origin) => NativeWorldPathProjectionCertificate.Create(camera, origin, 22).ProjectNode(source);
        live = live.Step(definition, Project);
        for (int index = 0; index < live.NodeCount; index++)
        {
            NativeWorldPathNodePresentation pose = live.Presentation[index];
            Assert.NotNull(pose.Projection);
            Assert.Equal(live.Nodes[index].Position, pose.Projection.Source);
            if (live.NodeLink(index) == 0)
            {
                Assert.Null(pose.Angles);
                Assert.Null(pose.Orientation);
            }
            else if (pose.Projection.Visible)
            {
                Assert.NotNull(pose.Orientation);
                Assert.Equal(live.Presentation[index + live.NodeLink(index)].Projection, pose.Orientation.Linked);
                Assert.Equal(NativeWorldScriptedPathMath.LinkedOrientation(pose.Orientation.Middle.Words, pose.Orientation.Linked.Words), pose.Angles);
            }
        }
        Assert.Equal(4, live.Presentation.Count(pose => pose.Angles is not null));
        NativeWorldScriptedPathState restored = JsonSerializer.Deserialize<NativeWorldScriptedPathState>(JsonSerializer.Serialize(live))!;
        restored.Validate(definition);
        Assert.Equal(JsonSerializer.Serialize(live.Step(definition, Project)), JsonSerializer.Serialize(restored.Step(definition, Project)));
        NativeWorldScriptedPathState unknown = live.Step(definition, (_, _, _) => null);
        Assert.All(unknown.Presentation, pose => Assert.Null(pose.Angles));
        string before = JsonSerializer.Serialize(live);
        Assert.Throws<InvalidDataException>(() => live.Step(definition, (source, camera, origin) => new NativeWorldPathNodeProjection(1, NativeWorldPathNodeProjection.NativeRule, new(1, 2, 3), camera, origin, default, true)));
        Assert.Equal(before, JsonSerializer.Serialize(live));
    }

    private static int RailDistance(NativeWorldScriptedPathDefinition definition, NativeWorldScriptedPathNode node, int direction = 1)
    {
        int result = node.Distance;
        for (int segment = 0; segment < node.Segment; segment++)
        {
            int source = direction == 1 ? segment : definition.Points.Length - segment - 1;
            result += NativeWorldScriptedPathMath.SegmentLength(definition.Points[source], definition.Points[source + direction]);
        }
        return result;
    }
}
