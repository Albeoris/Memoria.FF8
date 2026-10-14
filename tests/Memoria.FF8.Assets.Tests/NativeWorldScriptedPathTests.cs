using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldScriptedPathTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void OriginalReturnCameraEvaluatesEachModeSevenEightNineAnchorBeforeBlending()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(12);
        Assert.Equal("189C03D03F48DA9467F629A0CC4B895FBA3CE6E6D5C2B80E76CC75812EB5A501", definition.ChunkHash);
        Assert.Equal("50820772483EBAC6FF5B7675E529085BE10DEEEAFE2161B5858AC9BFACC997C0", definition.CameraHash);
        Assert.Equal(new short[] { 30, 120, 210, 300, 330, 360, 390, 420, 435, -1 }, definition.CameraRecords.Select(record => record.Deadline));
        Assert.Equal(new byte[] { 7, 7, 8, 8, 8, 8, 8, 8, 8, 9 }, definition.CameraRecords.Select(record => record.EyeMode));
        Assert.Equal(definition.CameraRecords.Select(record => record.EyeMode), definition.CameraRecords.Select(record => record.TargetMode));
        NativeWorldScriptedPathState state = NativeWorldScriptedPathState.Create(definition, new WorldTransition(12, 66, 60), null);
        Assert.Equal(new NativeWorldPathCamera(new(-4798, -760, 33521), new(-8349, -760, 35330)), state.Camera);
        Assert.Equal(new NativeWorldPathCamera(new(-5691, -760, 32856), new(-8978, -678, 35073)), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 60));
        Assert.Equal(new NativeWorldPathCamera(new(-7030, -760, 32350), new(-9990, -619, 35016)), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 120));
        Assert.Equal(new NativeWorldPathCamera(new(-18872, -760, 30455), new(-16771, -480, 33844)), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 434));
        var terminal = new NativeWorldPathCamera(new(-18959, -760, 30476), new(-16846, -482, 33858));
        Assert.Equal(terminal, NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 435));
        Assert.Equal(terminal, NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 436));
        Assert.Throws<InvalidDataException>(() => NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 0, 120));
        Assert.Throws<InvalidDataException>(() => NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, -1));
        Assert.Throws<InvalidDataException>(() => NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 32768));
        Assert.Throws<InvalidDataException>(() => NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes.SetItem(1, state.Nodes[1] with { Position = new(1, 2, 3) }), 1, 120));
        Assert.Throws<NotSupportedException>(() => (definition with { CameraRecords = definition.CameraRecords.SetItem(2, definition.CameraRecords[2] with { EyeMode = 10 }) }).Validate());
        Assert.Throws<NotSupportedException>(() => (definition with { CameraRecords = definition.CameraRecords.SetItem(2, definition.CameraRecords[2] with { Deadline = 120 }) }).Validate());
    }

    [Fact]
    public void OriginalReturnCameraRetainsActualOldNodesAndFreezesItsModeEightNineBlendDuringFade()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(12);
        NativeWorldScriptedPathState state = NativeWorldScriptedPathState.Create(definition, new WorldTransition(12, 66, 60), null);
        while (state.Callback < 100)
        {
            state = state.Step(definition, (_, _, _) => null);
        }
        var forgedReceipt = state.CameraReceipt with { Nodes = state.Nodes };
        var forgedCamera = NativeWorldScriptedPathState.EvaluateCamera(definition, forgedReceipt.Nodes, 1, forgedReceipt.Counter);
        Assert.Throws<InvalidDataException>(() => (state with { CameraReceipt = forgedReceipt, Camera = forgedCamera }).Validate(definition));
        NativeWorldScriptedPathState restored = JsonSerializer.Deserialize<NativeWorldScriptedPathState>(JsonSerializer.Serialize(state))!;
        restored.Validate(definition);
        while (!state.EndpointRequested)
        {
            state = state.Step(definition, (_, _, _) => null);
            restored = restored.Step(definition, (_, _, _) => null);
            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        }
        Assert.Equal(434, state.Callback);
        Assert.Equal(new NativeWorldPathPosition(12500, 0, 24734), state.EntityPosition);
        Assert.Equal(state.EntityPosition, state.CameraReceipt.Nodes[1].Position);
        var endpointCamera = new NativeWorldPathCamera(new(7135, -760, 16088), new(9236, -480, 19477));
        Assert.Equal(endpointCamera, state.Camera);
        Assert.Equal(new NativeWorldPathCamera(new(7713, -760, 17380), new(9826, -482, 20762)), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, -1, 435));
        // Reverse mode7 selects the last reset node; mode8 selects the middle and mode9 the first.
        Assert.Equal(new NativeWorldPathCamera(new(20101, -760, 20018), new(16550, -760, 21827)), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, -1, 0));
        Assert.Equal(new NativeWorldPathCamera(new(18755, -760, 19008), new(15795, -619, 21674)), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, -1, 120));
        Assert.NotEqual(endpointCamera, NativeWorldScriptedPathState.EvaluateCamera(definition, state.CameraReceipt.Nodes, 1, 435));
        for (int fade = 0; fade < 16; fade++)
        {
            state = state.Step(definition, (_, _, _) => throw new InvalidOperationException("Departure fade must freeze camera/node evaluation."));
            restored = restored.Step(definition, (_, _, _) => throw new InvalidOperationException("Departure fade must freeze camera/node evaluation."));
            Assert.Equal(endpointCamera, state.Camera);
            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        }
        Assert.Equal(450, state.Callback);
        Assert.True(state.FieldTransitionReady);
        Assert.Equal((byte)60, state.CompletedFieldRow);
        Assert.Throws<InvalidDataException>(() => (state with { EntityPosition = state.Nodes[1].Position }).Validate(definition));
    }

    [Fact]
    public void OriginalTrackElevenRetainsFiniteRailCameraAndStaticBoatResources()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(11);
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
        Assert.Throws<NotSupportedException>(() => assets.ReadScriptedPath(0));
        Assert.Throws<InvalidDataException>(() => assets.ReadScriptedPath(14));
    }

    [Fact]
    public void OriginalModeOnePhaseRestoresAndEndsAfter230Plus16CallbacksWithoutActivation()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(11);
        var packet = new WorldTransition(11, 66, 61);
        NativeWorldScriptedPathState state = NativeWorldScriptedPathState.Create(definition, packet, null);
        Assert.Equal(new[] { 21, 101, 0 }, state.Nodes.Select(node => node.Distance));
        Assert.Equal(new short[] { 2, 1, 0 }, state.Nodes.Select(node => node.Segment));
        Assert.Equal(new NativeWorldPathPosition(12464, 0, 24673), state.EntityPosition);
        Assert.Equal(new NativeWorldPathCamera(new(20580, -1563, 26201), new(16816, -868, 25262)), state.Camera);
        Assert.Null(state.Presentation[1].Angles);
        Assert.Null(state.Presentation[1].Orientation);
        Assert.Null(state.Presentation[1].Projection);
        Assert.Equal(2, state.NativeMovementMultiplier);
        Assert.True(state.ControlsDisabled);
        Assert.Null(state.CompletedFieldRow);
        for (int callback = 1; callback <= 29; callback++)
        {
            state = state.Step(definition, (_, _, _) => null);
        }
        Assert.Equal(0, state.Speed);
        Assert.Equal((short)1, state.Pause);
        Assert.Equal(new NativeWorldPathPosition(12464, 0, 24673), state.Nodes[1].Position);
        var beforeFirstMovement = state.Nodes;
        state = state.Step(definition, (_, _, _) => null);
        Assert.Equal(20, state.Speed);
        Assert.Equal((short)0, state.Pause);
        Assert.Equal(new NativeWorldPathPosition(12464, 0, 24673), state.EntityPosition);
        Assert.NotEqual(state.EntityPosition, state.Nodes[1].Position);
        Assert.Equal(beforeFirstMovement, state.CameraReceipt.Nodes);
        Assert.Equal((ushort)30, state.CameraReceipt.Counter);
        Assert.Equal(NativeWorldScriptedPathState.EvaluateCamera(definition, beforeFirstMovement, 1, 30), state.Camera);
        Assert.NotEqual(NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 30), state.Camera);
        while (state.Callback < 100)
        {
            state = state.Step(definition, (_, _, _) => null);
        }
        string saved = JsonSerializer.Serialize(state);
        NativeWorldScriptedPathState restored = JsonSerializer.Deserialize<NativeWorldScriptedPathState>(saved)!;
        restored.Validate(definition);
        Assert.Throws<InvalidDataException>(() => (restored with { Pause = 30 }).Validate(definition));
        Assert.Throws<InvalidDataException>(() => (restored with { EntityPosition = new(1, 2, 3) }).Validate(definition));
        Assert.Throws<InvalidDataException>(() => (restored with { Nodes = restored.Nodes.SetItem(2, restored.Nodes[2] with { Distance = restored.Nodes[2].Distance + 1 }) }).Validate(definition));
        while (!state.EndpointRequested)
        {
            state = state.Step(definition, (_, _, _) => null);
            restored = restored.Step(definition, (_, _, _) => null);
            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        }
        Assert.Equal(230, state.Callback);
        Assert.Equal(230, state.EndpointCallback);
        Assert.Equal((sbyte)-1, state.Direction);
        Assert.Equal((sbyte)1, state.CameraReceipt.Direction);
        Assert.Null(state.Pause);
        Assert.Null(state.Presentation[1].Angles);
        Assert.Equal(new NativeWorldPathPosition(570, 0, 27006), state.EntityPosition);
        Assert.False(state.FieldTransitionReady);
        Assert.Null(state.CompletedFieldRow);
        NativeWorldPathCamera endpointCamera = state.Camera;
        for (int fade = 1; fade <= 16; fade++)
        {
            state = state.Step(definition, (_, _, _) => throw new InvalidOperationException("Fade must freeze the path/visibility driver."));
            restored = restored.Step(definition, (_, _, _) => throw new InvalidOperationException("Fade must freeze the path/visibility driver."));
            Assert.Equal(endpointCamera, state.Camera);
            Assert.Equal(fade == 16, state.FieldTransitionReady);
            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        }
        Assert.Equal(246, state.Callback);
        Assert.Equal((byte)61, state.CompletedFieldRow);
        Assert.Equal(state, state.Step(definition, (_, _, _) => null));
        Assert.Throws<InvalidDataException>(() => (state with { CameraCounter = (ushort)(state.CameraCounter - 1) }).Validate(definition));
        Assert.Throws<InvalidDataException>(() => (state with { Camera = endpointCamera with { Eye = new(1, 2, 3) } }).Validate(definition));
        JsonObject missing = JsonNode.Parse(saved)!.AsObject();
        missing.Remove(nameof(NativeWorldScriptedPathState.Presentation));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldScriptedPathState>(missing.ToJsonString()));
    }

    [Fact]
    public void ExplicitModeSevenCameraAndVisibleLinkedOrientationUseOriginalMath()
    {
        using WorldAssets assets = new(GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(11);
        NativeWorldScriptedPathState state = NativeWorldScriptedPathState.Create(definition, new WorldTransition(11, 66, 61), false);
        Assert.Equal(new NativeWorldPathCamera(new(20204, -1476, 25551), new(16393, -782, 24879)), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 15));
        Assert.Equal(new NativeWorldPathPosition(19827, -1389, 24900), NativeWorldScriptedPathState.EvaluateCamera(definition, state.Nodes, 1, 30).Eye);
        NativeWorldScriptedPathState unknown = state.Step(definition, (_, _, _) => null);
        Assert.Null(unknown.Presentation[1].Angles);
        NativeWorldPathNodeProjection Project(NativeWorldPathPosition source, NativeWorldPathCamera camera, NativeWorldPathPosition origin, bool visible)
        {
            // Synthetic5470E0 output has different heights, exercising the curved
            // transformed WORDs rather than the actual rail's flat raw Y values.
            NativeWorldPathWords words = new((short)source.X, source.X < 12500 ? (short)4 : (short)80, (short)source.Z);
            return new NativeWorldPathNodeProjection(1, NativeWorldPathNodeProjection.NativeRule, source, camera, origin, words, visible);
        }
        NativeWorldScriptedPathState visible = state.Step(definition, (source, camera, origin) => Project(source, camera, origin, true));
        Assert.NotNull(visible.Presentation[1].Orientation);
        Assert.Equal(visible.Presentation[1].Orientation!.Middle, visible.Presentation[1].Projection);
        Assert.Equal(NativeWorldScriptedPathMath.LinkedOrientation(visible.Presentation[1].Orientation!.Middle.Words, visible.Presentation[1].Orientation!.Linked.Words), visible.Presentation[1].Angles);
        Assert.NotEqual((short)0, visible.Presentation[1].Angles!.Pitch);
        Assert.NotEqual(new NativeWorldPathAngles(0, 0, 0), visible.Presentation[1].Angles);
        Assert.Equal(visible.Presentation[1].Angles, visible.Step(definition, (source, camera, origin) => Project(source, camera, origin, false)).Presentation[1].Angles);
        Assert.Null(visible.Step(definition, (_, _, _) => null).Presentation[1].Angles);
        NativeWorldScriptedPathState restoredVisible = JsonSerializer.Deserialize<NativeWorldScriptedPathState>(JsonSerializer.Serialize(visible))!;
        restoredVisible.Validate(definition);
        Assert.Equal(JsonSerializer.Serialize(visible), JsonSerializer.Serialize(restoredVisible));
        Assert.Throws<InvalidDataException>(() => (visible with { Presentation = visible.Presentation.SetItem(1, visible.Presentation[1] with { Orientation = null }) }).Validate(definition));
        Assert.Throws<InvalidDataException>(() => (visible with { Presentation = visible.Presentation.SetItem(1, visible.Presentation[1] with { Angles = new NativeWorldPathAngles(0, 0, 0) }) }).Validate(definition));
        string before = JsonSerializer.Serialize(state);
        Assert.Throws<InvalidDataException>(() => state.Step(definition, (source, camera, origin) => Project(source, camera, origin, true) with { Source = new(1, 2, 3) }));
        Assert.Equal(before, JsonSerializer.Serialize(state));
        Assert.Throws<NotSupportedException>(() => NativeWorldScriptedPathState.Create(definition, new WorldTransition(12, 66, 61), false));
        Assert.Throws<NotSupportedException>(() => NativeWorldScriptedPathState.Create(definition, new WorldTransition(11, 0, 61), false));
    }
}
