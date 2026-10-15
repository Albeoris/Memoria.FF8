using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeScriptedWorldModeTwoTests
{
    [Theory]
    [InlineData(4, 20, 348, 1, 55, 720)]
    [InlineData(11, 66, 40, 0, 61, 260)]
    public void OriginalFiniteModesDistinguishThePriorSavedFootFromTheLateBodyUpdate(int track, byte entity, ushort story, byte field265, short terminal, int destination)
    {
        // Installed-resource component fixture; no player session or journal is fabricated.
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeScriptedWorldFootRecord foot = assets.ReadScriptedFootRecord(story, field265);
        Assert.Equal(new NativeSavedWorldPose(0, 0, 0, 0), foot.Pose);
        if (track == 4)
        {
            NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(story, field265);
            Assert.Equal((5, (ushort)348, (ushort)392, 0), (allocation.ClauseIndex, foot.MinimumStory, foot.MaximumStory, allocation.ControlledObjectIndex));
            Assert.Equal((byte)0, Assert.Single(allocation.Objects).ShapeId);
        }
        (NativeSavedWorldState saved, WorldState previous) = PreviousScriptedModeComponent(entity, story);
        NativeScriptedWorldLoadReceipt load = NativeScriptedWorldLoadReceipt.Create(saved, previous, foot);
        Assert.Equal((ushort)2256, load.RetainedBodyYaw);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(track);
        WorldTransition packet = new(checked((short)track), entity, terminal);
        NativeWorldScriptedPathState path = NativeWorldScriptedPathState.Create(definition, packet, null);
        NativeWorldScriptedPathState? independentlyRestored = null;
        NativeScriptedWorldRuntimeState runtime = new(1, NativeScriptedWorldRuntimeState.RuntimeRule, path, load, null);
        NativeWorldPathPosition previousBody = path.EntityPosition;
        bool lagObserved = false;
        bool bodyMoved = false;
        for (int callback = 0; callback < 5000 && !path.EndpointRequested; callback++)
        {
            NativeWorldScriptedPathState before = path;
            previousBody = before.EntityPosition;
            path = before.Step(definition, (_, _, _) => null);
            Assert.Equal(before.Nodes[before.CentralNode].Position, path.EntityPosition);
            lagObserved |= path.EntityPosition != path.Nodes[path.CentralNode].Position;
            bodyMoved |= path.EntityPosition != before.EntityPosition;
            if (independentlyRestored is not null)
            {
                independentlyRestored = independentlyRestored.Step(definition, (_, _, _) => null);
                if (path.Callback == 31 || path.EndpointRequested)
                {
                    Assert.Equal(JsonSerializer.Serialize(path), JsonSerializer.Serialize(independentlyRestored));
                }
            }
            if (path.Callback == 30)
            {
                independentlyRestored = JsonSerializer.Deserialize<NativeWorldScriptedPathState>(JsonSerializer.Serialize(path))!;
                independentlyRestored.Validate(definition);
            }
            runtime = runtime with { Path = path };
            NativeSavedWorldPose currentFoot = path.Mode == 1 ? foot.Pose : FootPose(path.EntityPosition, load.RetainedBodyYaw);
            Assert.Equal(currentFoot, runtime.CurrentFootPose0);
            if (!path.EndpointRequested)
            {
                Assert.Null(path.EndpointPriorEntityPosition);
                runtime.Validate();
            }
        }
        Assert.True(path.EndpointRequested && lagObserved && bodyMoved);
        Assert.Equal(previousBody, path.EndpointPriorEntityPosition);
        WorldFieldEntry row = assets.FieldEntries.Entries[terminal];
        FieldTransition transition = new(row.FieldId, row.X, row.Y, row.Triangle, row.Direction);
        Assert.Equal(destination, transition.FieldId);
        NativeSavedWorldPose expectedSave = path.Mode == 1 ? foot.Pose : FootPose(previousBody, load.RetainedBodyYaw);
        NativeSavedWorldScriptedReturn receipt = NativeSavedWorldScriptedReturn.Create(runtime, 267, story, assets.GetNativeMapLocationId(path.EntityPosition.X, -path.EntityPosition.Z), transition);
        runtime = runtime with { Return = receipt };
        runtime.Validate();
        Assert.Equal(expectedSave, receipt.Pose0);
        if (path.Mode == 2)
        {
            Assert.NotEqual(runtime.CurrentFootPose0, receipt.Pose0);
            Assert.Equal((ushort)2256, receipt.Pose0.Yaw);
            WorldState current = runtime.ToWorldState();
            Assert.Equal(runtime.CurrentFootPose0, new NativeSavedWorldPose(current.X, current.Z, checked((short)current.NativeHeight), checked((ushort)current.BodyYaw)));
            Assert.Throws<InvalidDataException>(() => (runtime with { Return = receipt with { Pose0 = runtime.CurrentFootPose0 } }).Validate());
            NativeWorldPathPosition wrongPrior = previousBody with { X = previousBody.X + 1 };
            NativeScriptedWorldRuntimeState coherentTamper = runtime with
            {
                Path = path with { EndpointPriorEntityPosition = wrongPrior },
                Return = receipt with { Pose0 = FootPose(wrongPrior, load.RetainedBodyYaw) }
            };
            // Matching saved fields cannot replace the independently replayed installed route phase.
            NativeScriptedWorldRuntimeState coherentRestored = JsonSerializer.Deserialize<NativeScriptedWorldRuntimeState>(JsonSerializer.Serialize(coherentTamper))!;
            coherentRestored.Validate();
            Assert.Throws<InvalidDataException>(() => coherentRestored.Path.Validate(definition));
        }
        else
        {
            Assert.Equal(foot.Pose, receipt.Pose0);
            Assert.NotEqual(FootPose(path.EntityPosition, load.RetainedBodyYaw), receipt.Pose0);
        }
        Assert.Throws<InvalidDataException>(() => (path with { EndpointPriorEntityPosition = null }).Validate(definition));
        JsonObject malformed = JsonNode.Parse(JsonSerializer.Serialize(receipt))!.AsObject();
        malformed.Remove(nameof(NativeSavedWorldScriptedReturn.Pose0));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSavedWorldScriptedReturn>(malformed.ToJsonString()));
        NativeScriptedWorldRuntimeState restored = JsonSerializer.Deserialize<NativeScriptedWorldRuntimeState>(JsonSerializer.Serialize(runtime))!;
        restored.Validate();
        for (int callback = 0; callback < 16; callback++)
        {
            runtime = runtime with { Path = runtime.Path.Step(definition, (_, _, _) => throw new InvalidOperationException("Departure fade must not reevaluate nodes.")) };
            restored = restored with { Path = restored.Path.Step(definition, (_, _, _) => throw new InvalidOperationException("Restored departure fade must not reevaluate nodes.")) };
            runtime.Validate();
            restored.Validate();
            Assert.Equal(expectedSave, runtime.Return!.Pose0);
            Assert.Equal(previousBody, runtime.Path.EndpointPriorEntityPosition);
            Assert.Equal(JsonSerializer.Serialize(runtime), JsonSerializer.Serialize(restored));
        }
        Assert.True(runtime.Path.FieldTransitionReady);
    }

    private static NativeSavedWorldPose FootPose(NativeWorldPathPosition position, ushort retainedYaw)
    {
        return new(position.X, -position.Z, checked((short)position.Y), retainedYaw);
    }

    private static (NativeSavedWorldState Saved, WorldState Previous) PreviousScriptedModeComponent(byte entity, ushort story)
    {
        NativeSavedWorldState initial = NativeSavedWorldState.CreateNewGame();
        if (entity == 66)
        {
            WorldState previous = new() { Frame = 500, X = 12390, Z = -26709, NativeHeight = -301, BodyYaw = 2256, CameraYaw = 2256, NativeCarRuntime = NativeCarWorldRuntimeState.Create(initial, NativeWorldGroundCacheState.CreateFresh(new(0, 0, 0, 0))) };
            return (initial.WithOrdinaryCarEntry().WithCarReturn(NativeSavedWorldCarReturn.Create(previous, 262, story, 1)), previous);
        }
        WorldState foot = new() { Frame = 500, X = 13895, Z = -27012, NativeHeight = -383, BodyYaw = 2256, CameraYaw = 2256, NativeFootRuntime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(initial) };
        return (initial.WithFootReturn(NativeSavedWorldFootReturn.RecordFootDeparture(foot, 104, story, 1)), foot);
    }
}
