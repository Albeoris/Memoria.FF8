using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTrainRidePathTests
{
    [Fact]
    public void OriginalRailZeroHasRealStopsAndCameraMarkersWithoutChangingModeTwoAdmission()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(0);
        Assert.Equal(63, definition.Points.Length);
        Assert.Equal(new byte[] { 36 }, definition.ForwardStops);
        Assert.Equal(new byte[] { 33 }, definition.ReverseStops);
        Assert.Equal((short)811, definition.FrontLength);
        Assert.Equal((short)811, definition.RearLength);
        Assert.Contains(definition.CameraRecords, row => row.Deadline == -2);
        Assert.Contains(definition.CameraRecords, row => row.Deadline > 0 && row.Interpolation == 2);
        NativeTrainRailBank bank = NativeTrainRailBank.Create(definition, 1);
        Assert.Equal(new[] { 2780, 2081, 1382, 699, 0 }, NativeWorldScriptedPathState.InitialDistances(definition, 5, 1));
        Assert.Equal((short)30, bank.Pause);
        Assert.Null(bank.Status);
        Assert.Throws<NotSupportedException>(() => NativeWorldScriptedPathState.Create(definition, new(0, 16, 53), null));
        NativeWorldPathPosition origin = bank.Nodes[2].Position;
        NativeWorldPathCamera camera = NativeWorldScriptedPathState.EvaluateCamera(definition, bank.Nodes, 1, 0);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.Create(camera, origin, 16);
        for (int callback = 1; callback < 30; callback++)
        {
            bank = bank.Step(definition, callback, camera, origin, (node, _, _) => projection.ProjectNode(node));
        }
        Assert.Equal((short)1, bank.Pause);
        NativeTrainRailBank restored = JsonSerializer.Deserialize<NativeTrainRailBank>(JsonSerializer.Serialize(bank))!;
        NativeTrainRailBank next = bank.Step(definition, 30, camera, origin, (node, _, _) => projection.ProjectNode(node));
        NativeTrainRailBank restoredNext = restored.Step(definition, 30, camera, origin, (node, _, _) => projection.ProjectNode(node));
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restoredNext));
        Assert.Equal(20, next.Speed);
        Assert.Equal((short)0, next.Pause);
        Assert.Equal((byte)0, next.Status);
        Assert.Equal(20, TotalDistance(definition, next.Nodes[0]) - TotalDistance(definition, bank.Nodes[0]));
        NativeWorldRailAdvance phase = new(next.Pause, next.Speed, next.Nodes, false);
        for (int callback = 31; callback <= 60; callback++)
        {
            phase = NativeWorldScriptedPathState.AdvanceRail(definition, phase.Nodes, 1, phase.Pause, phase.Speed, 100, 2);
        }
        Assert.Equal(100, phase.Speed);
    }

    private static int TotalDistance(NativeWorldScriptedPathDefinition definition, NativeWorldScriptedPathNode node)
    {
        int result = node.Distance;
        for (int index = 0; index < node.Segment; index++)
        {
            result += NativeWorldScriptedPathMath.SegmentLength(definition.Points[index], definition.Points[index + 1]);
        }
        return result;
    }
}
