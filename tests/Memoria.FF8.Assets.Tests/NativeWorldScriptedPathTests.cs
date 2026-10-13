using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldScriptedPathTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void OriginalTrackElevenRetainsFiniteRailCameraAndStaticBoatResources()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedBoatPath(11);
        Assert.Equal("1BAEE9C2790679E26401B08B31132B995DE081DD298DA1C498D4F6FFD80D7E2A", definition.RailHash);
        Assert.Equal("40D3A382C6EFB010B01D8DA9EFA154A1021E5982CBB81BB2FAE18B0ACB3CF45A", definition.ChunkHash);
        Assert.Equal(11, definition.Points.Length);
        Assert.Equal((short)904, definition.FrontLength);
        Assert.Equal((short)811, definition.RearLength);
        Assert.Equal(new[] { 691, 763, 674, 1200, 1367, 1002, 1049, 1338, 2116, 5136 }, Enumerable.Range(0, 10).Select(index => NativeWorldScriptedPathMath.SegmentLength(definition.Points[index], definition.Points[index + 1])));
        Assert.Equal(new short[] { 30, 60, 90, 120, 150, 210, -1 }, definition.CameraRecords.Select(record => record.Deadline));
        Assert.All(definition.CameraRecords, record => Assert.True(record.EyeMode == 7 && record.TargetMode == 7));
        WorldStaticModel boat = assets.ReadScriptedStaticModel(78);
        Assert.Equal((byte)78, boat.ShapeId);
        Assert.Equal(14, boat.ModelIndex);
        Assert.NotEmpty(boat.NativeVertices);
        Assert.NotEmpty(boat.Faces);
        Assert.NotEmpty(boat.Textures);
        Assert.All(boat.Faces, face => Assert.All(face.Vertices, index => Assert.InRange(index, 0, boat.NativeVertices.Count - 1)));
        Assert.Throws<NotSupportedException>(() => assets.ReadScriptedBoatPath(0));
        Assert.Throws<InvalidDataException>(() => assets.ReadScriptedBoatPath(14));
    }

    [Fact]
    public void OriginalModeOnePhaseRestoresAndEndsAfter230Plus16CallbacksWithoutActivation()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedBoatPath(11);
        var packet = new WorldTransition(11, 66, 61);
        NativeWorldScriptedPathState state = NativeWorldScriptedPathState.Create(definition, packet, null);
        Assert.Equal(new[] { 21, 101, 0 }, state.Nodes.Select(node => node.Distance));
        Assert.Equal(new short[] { 2, 1, 0 }, state.Nodes.Select(node => node.Segment));
        Assert.Equal(new NativeWorldPathPosition(12464, 0, 24673), state.EntityPosition);
        Assert.Equal(new NativeWorldPathCamera(new(20580, -1563, 26201), new(16816, -868, 25262)), state.Camera);
        Assert.Null(state.ModelAngles);
        Assert.True(state.ControlsDisabled);
        Assert.Null(state.CompletedFieldRow);
        for (int callback = 1; callback <= 29; callback++)
        {
            state = state.Step(definition, (_, _) => null);
        }
        Assert.Equal(0, state.Speed);
        Assert.Equal((short)1, state.Pause);
        Assert.Equal(new NativeWorldPathPosition(12464, 0, 24673), state.Nodes[1].Position);
        state = state.Step(definition, (_, _) => null);
        Assert.Equal(20, state.Speed);
        Assert.Equal((short)0, state.Pause);
        Assert.Equal(new NativeWorldPathPosition(12464, 0, 24673), state.EntityPosition);
        Assert.NotEqual(state.EntityPosition, state.Nodes[1].Position);
        while (state.Callback < 100)
        {
            state = state.Step(definition, (_, _) => null);
        }
        string saved = JsonSerializer.Serialize(state);
        NativeWorldScriptedPathState restored = JsonSerializer.Deserialize<NativeWorldScriptedPathState>(saved)!;
        restored.Validate(definition);
        Assert.Throws<InvalidDataException>(() => (restored with { Pause = 30 }).Validate(definition));
        Assert.Throws<InvalidDataException>(() => (restored with { EntityPosition = new(1, 2, 3) }).Validate(definition));
        Assert.Throws<InvalidDataException>(() => (restored with { Nodes = restored.Nodes.SetItem(2, restored.Nodes[2] with { Distance = restored.Nodes[2].Distance + 1 }) }).Validate(definition));
        while (!state.EndpointRequested)
        {
            state = state.Step(definition, (_, _) => null);
            restored = restored.Step(definition, (_, _) => null);
            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        }
        Assert.Equal(230, state.Callback);
        Assert.Equal(230, state.EndpointCallback);
        Assert.Equal((sbyte)-1, state.Direction);
        Assert.Null(state.Pause);
        Assert.Null(state.ModelAngles);
        Assert.Equal(new NativeWorldPathPosition(570, 0, 27006), state.EntityPosition);
        Assert.False(state.FieldTransitionReady);
        Assert.Null(state.CompletedFieldRow);
        NativeWorldPathCamera endpointCamera = state.Camera;
        for (int fade = 1; fade <= 16; fade++)
        {
            state = state.Step(definition, (_, _) => throw new InvalidOperationException("Fade must freeze the path/visibility driver."));
            restored = restored.Step(definition, (_, _) => throw new InvalidOperationException("Fade must freeze the path/visibility driver."));
            Assert.Equal(endpointCamera, state.Camera);
            Assert.Equal(fade == 16, state.FieldTransitionReady);
            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        }
        Assert.Equal(246, state.Callback);
        Assert.Equal((byte)61, state.CompletedFieldRow);
        Assert.Equal(state, state.Step(definition, (_, _) => null));
        Assert.Throws<InvalidDataException>(() => (state with { CameraCounter = (ushort)(state.CameraCounter - 1) }).Validate(definition));
        Assert.Throws<InvalidDataException>(() => (state with { Camera = endpointCamera with { Eye = new(1, 2, 3) } }).Validate(definition));
        JsonObject missing = JsonNode.Parse(saved)!.AsObject();
        missing.Remove(nameof(NativeWorldScriptedPathState.ModelAngles));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldScriptedPathState>(missing.ToJsonString()));
    }

    [Fact]
    public void ExplicitModeSevenCameraAndVisibleLinkedOrientationUseOriginalMath()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedBoatPath(11);
        NativeWorldScriptedPathState state = NativeWorldScriptedPathState.Create(definition, new WorldTransition(11, 66, 61), false);
        Assert.Equal(new NativeWorldPathCamera(new(20204, -1476, 25551), new(16393, -782, 24879)), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 15));
        Assert.Equal(new NativeWorldPathPosition(19827, -1389, 24900), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 30).Eye);
        NativeWorldScriptedPathState unknown = state.Step(definition, (_, _) => null);
        Assert.Null(unknown.ModelAngles);
        NativeWorldScriptedPathState visible = state.Step(definition, (_, _) => true);
        Assert.Equal(NativeWorldScriptedPathMath.LinkedOrientation(visible.Nodes[1].Position, visible.Nodes[2].Position), visible.ModelAngles);
        Assert.NotEqual(new NativeWorldPathAngles(0, 0, 0), visible.ModelAngles);
        Assert.Equal(visible.ModelAngles, visible.Step(definition, (_, _) => false).ModelAngles);
        Assert.Throws<NotSupportedException>(() => NativeWorldScriptedPathState.Create(definition, new WorldTransition(12, 66, 61), false));
        Assert.Throws<NotSupportedException>(() => NativeWorldScriptedPathState.Create(definition, new WorldTransition(11, 0, 61), false));
    }
}
