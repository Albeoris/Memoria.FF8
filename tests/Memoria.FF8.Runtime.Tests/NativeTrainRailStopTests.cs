using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeTrainRailStopTests
{
    [Fact]
    public void LeadingCrossingStopsOnceAfterEveryNodeReceivesTheOriginalMovement()
    {
        NativeWorldScriptedPathDefinition definition = Definition();
        ImmutableArray<NativeWorldScriptedPathNode> source = Nodes(definition, [3990, 3291, 2592, 1909, 1210]);
        NativeWorldRailAdvance stopped = NativeWorldScriptedPathState.AdvanceRail(definition, source, 1, 0, 20, 100, 2, 0, 0);
        Assert.Equal((short)30, stopped.Pause);
        Assert.Equal(0, stopped.Speed);
        Assert.Equal((byte)1, stopped.Status);
        Assert.Equal((byte)1, stopped.StopCounter);
        Assert.False(stopped.Endpoint);
        for (int index = 0; index < source.Length; index++)
        {
            Assert.Equal(source[index].Position.X + 24, stopped.Nodes[index].Position.X);
        }
        NativeWorldRailAdvance held = NativeWorldScriptedPathState.AdvanceRail(definition, stopped.Nodes, 1, -1, 0, 100, 2, stopped.Status, stopped.StopCounter);
        Assert.Equal((short)-1, held.Pause);
        Assert.Equal(stopped.Nodes, held.Nodes);
        Assert.Equal(stopped.Status, held.Status);
        Assert.Equal(stopped.StopCounter, held.StopCounter);
        NativeWorldRailAdvance released = NativeWorldScriptedPathState.AdvanceRail(definition, held.Nodes, 1, 0, 0, 100, 2, held.Status, held.StopCounter);
        Assert.Equal(20, released.Speed);
        Assert.Equal((byte)0, released.Status);
        Assert.Equal((byte)1, released.StopCounter);
    }

    [Fact]
    public void ReverseStopUsesOriginalReverseIndexAndTheLastAllocatedLeadingNode()
    {
        NativeWorldScriptedPathDefinition definition = Definition();
        ImmutableArray<NativeWorldScriptedPathNode> source = Nodes(definition, [1210, 1909, 2592, 3291, 3990], -1);
        NativeWorldRailAdvance stopped = NativeWorldScriptedPathState.AdvanceRail(definition, source, -1, 0, 20, 100, 2, 0, 0);
        Assert.Equal((short)30, stopped.Pause);
        Assert.Equal((byte)1, stopped.StopCounter);
        Assert.Equal((byte)1, stopped.Status);
        Assert.Equal((short)4, stopped.Nodes[^1].Segment);
    }

    [Fact]
    public void StationMarkerPinsTheCounterToTheFollowingDeadlineUntilTheStopIsReleased()
    {
        NativeWorldScriptedPathDefinition definition = Definition();
        ImmutableArray<NativeWorldScriptedPathNode> nodes = Nodes(definition, [3990, 3291, 2592, 1909, 1210]);
        NativeWorldPathCamera station = NativeWorldScriptedPathState.EvaluateCamera(definition, nodes, 1, 305, 0, out ushort nextCounter);
        Assert.Equal((ushort)360, nextCounter);
        Assert.Equal(new NativeWorldPathPosition(nodes[2].Position.X + 100, -3000, 200), station.Eye);
        Assert.Equal(station, NativeWorldScriptedPathState.EvaluateCamera(definition, nodes, 1, nextCounter, 0, out ushort heldCounter));
        Assert.Equal(nextCounter, heldCounter);
        _ = NativeWorldScriptedPathState.EvaluateCamera(definition, nodes, 1, heldCounter, -1, out ushort resumedCounter);
        Assert.Equal((ushort)361, resumedCounter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrainContactRollsBackAndPausesWithTheActualConditionalReversal(bool stopAtEndpoint)
    {
        NativeWorldScriptedPathDefinition definition = Definition();
        NativeTrainRailBank before = NativeTrainRailBank.Create(definition, 1) with { Pause = 0, Speed = 40, Status = 0, StopCounter = 0, Nodes = Nodes(definition, [3491, 2792, 2093, 1410, 711]) };
        NativeWorldPathPosition origin = before.Nodes[2].Position;
        NativeWorldPathCamera camera = NativeWorldScriptedPathState.EvaluateCamera(definition, before.Nodes, 1, 100);
        NativeWorldPathNodeProjection? Project(NativeWorldPathPosition position, NativeWorldPathCamera selected, NativeWorldPathPosition body) => new(1, NativeWorldPathNodeProjection.NativeRule, position, selected, body, new(unchecked((short)position.X), unchecked((short)position.Y), unchecked((short)position.Z)), false);
        NativeTrainRailBank advanced = before.Step(definition, 100, camera, origin, Project);
        NativeTrainRailBank resolved = advanced.ResolveContact(before, definition, 100, camera, origin, Project, stopAtEndpoint);
        Assert.Equal((short)15, resolved.Pause);
        Assert.Equal(0, resolved.Speed);
        Assert.Equal(before.Nodes.Select(node => node.Position), resolved.Nodes.Select(node => node.Position));
        Assert.Equal(stopAtEndpoint ? (sbyte)1 : (sbyte)-1, resolved.Direction);
        if (stopAtEndpoint)
        {
            Assert.Null(resolved.ContactReversal);
            Assert.Equal(before.Nodes, resolved.Nodes);
        }
        else
        {
            Assert.NotNull(resolved.ContactReversal);
            Assert.Equal((byte)1, resolved.StopCounter);
            Assert.Equal(1000 - before.Nodes[0].Distance, resolved.Nodes[0].Distance);
            Assert.Equal((short)(definition.Points.Length - before.Nodes[0].Segment - 2), resolved.Nodes[0].Segment);
        }
        NativeTrainRailBank next = resolved.Step(definition, 101, camera, origin, Project);
        Assert.Equal((short)14, next.Pause);
        Assert.Null(next.ContactReversal);
        next.Validate(definition);
    }

    [Fact]
    public void ExactStationBoundarySuppressesContactReversalWithoutSuppressingTheRollbackPause()
    {
        NativeWorldScriptedPathDefinition definition = Definition();
        NativeTrainRailBank before = NativeTrainRailBank.Create(definition, 1) with { Pause = 0, Speed = 40, Status = 0, StopCounter = 1, Nodes = Nodes(definition, [6780, 6081, 5382, 4699, 4000]) };
        NativeWorldPathPosition origin = before.Nodes[2].Position;
        NativeWorldPathCamera camera = NativeWorldScriptedPathState.EvaluateCamera(definition, before.Nodes, 1, 100);
        NativeWorldPathNodeProjection? Project(NativeWorldPathPosition position, NativeWorldPathCamera selected, NativeWorldPathPosition body) => new(1, NativeWorldPathNodeProjection.NativeRule, position, selected, body, default, false);
        NativeTrainRailBank advanced = before.Step(definition, 100, camera, origin, Project);
        NativeTrainRailBank resolved = advanced.ResolveContact(before, definition, 100, camera, origin, Project, false);
        Assert.Equal((short)15, resolved.Pause);
        Assert.Equal((sbyte)1, resolved.Direction);
        Assert.Null(resolved.ContactReversal);
        Assert.Equal(before.Nodes, resolved.Nodes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContactOnTheRealEndpointPhaseCancelsItsResetAndPreservesTheNativeByteCounter(bool stopAtEndpoint)
    {
        NativeWorldScriptedPathDefinition definition = Definition();
        NativeTrainRailBank before = NativeTrainRailBank.Create(definition, 1) with { Pause = 0, Speed = 40, Status = 0, StopCounter = 1, Nodes = Nodes(definition, [8990, 8291, 7592, 6909, 6210]) };
        NativeWorldPathPosition origin = before.Nodes[2].Position;
        NativeWorldPathCamera camera = NativeWorldScriptedPathState.EvaluateCamera(definition, before.Nodes, 1, 100);
        NativeWorldPathNodeProjection? Project(NativeWorldPathPosition position, NativeWorldPathCamera selected, NativeWorldPathPosition body) => new(1, NativeWorldPathNodeProjection.NativeRule, position, selected, body, default, false);
        int calls = 0;
        NativeTrainRailBank blocked = before.Step(definition, 100, camera, origin, Project, stopAtEndpoint, out bool endpoint, candidate =>
        {
            calls++;
            candidate.Validate(definition);
            Assert.Equal((short)0, candidate.Pause);
            Assert.Equal((byte)2, candidate.Status);
            Assert.Equal((byte)2, candidate.StopCounter);
            Assert.Equal((short)(definition.Points.Length - 1), candidate.Nodes[0].Segment);
            Assert.Equal(before.Nodes[0].Position, candidate.Nodes[0].Position);
            return candidate.ResolveContact(before, definition, 100, camera, origin, Project, stopAtEndpoint);
        });
        Assert.Equal(1, calls);
        Assert.False(endpoint);
        Assert.Equal((short)15, blocked.Pause);
        Assert.Equal(before.Nodes.Select(node => node.Position), blocked.Nodes.Select(node => node.Position));
        Assert.Equal(stopAtEndpoint ? (byte)2 : byte.MaxValue, blocked.StopCounter);
        blocked.Validate(definition);
        NativeTrainRailBank next = blocked.Step(definition, 101, camera, origin, Project);
        Assert.Equal((short)14, next.Pause);
        next.Validate(definition);
        NativeTrainRailBank unblocked = before.Step(definition, 100, camera, origin, Project, stopAtEndpoint, out bool actualEndpoint);
        Assert.True(actualEndpoint);
        Assert.Equal(stopAtEndpoint ? (short)-1 : (short)30, unblocked.Pause);
        Assert.Equal((byte)0, unblocked.StopCounter);
    }

    private static ImmutableArray<NativeWorldScriptedPathNode> Nodes(NativeWorldScriptedPathDefinition definition, int[] distances, int direction = 1)
    {
        return distances.Select(distance => NativeWorldScriptedPathState.ResolveNode(definition, new NativeWorldScriptedPathNode(0, distance, default), direction, out _)).ToImmutableArray();
    }

    private static NativeWorldScriptedPathDefinition Definition()
    {
        string hash = new('A', 64);
        ImmutableArray<NativeWorldRailPoint> points = Enumerable.Range(0, 10).Select(index => new NativeWorldRailPoint(index * 1000, 0, 0, 0)).ToImmutableArray();
        ImmutableArray<NativeWorldPathCameraRecord> camera = [new(330, 14, 14, 2, new(0, -3000, 0), default), new(-2, 14, 14, 2, new(100, -3000, 200), default), new(360, 14, 14, 2, new(100, -3000, 200), default), new(-1, 14, 14, 2, new(200, -3000, 300), default)];
        return new NativeWorldScriptedPathDefinition(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, hash, hash, hash, 0, 70, 71, 811, 811, points, camera) { ForwardStops = [4], ReverseStops = [5] };
    }
}
