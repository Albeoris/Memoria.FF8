using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private static bool IsRidingTrainPacket(WorldTransition packet) => unchecked((byte)packet.Mode) is >= 16 and <= 22 && unchecked((byte)packet.EntryIndex) is >= 64 and <= 67 && unchecked((sbyte)packet.Extra) == -1;

    private WorldState CreateTrainRide(WorldTransition packet, WorldEntryVariables inputs)
    {
        NativeSavedWorldState saved = session.State.SavedWorld ?? throw new NotSupportedException("Native mode3 requires actual saved vehicle records.");
        WorldState previous = session.State.DepartingWorldState ?? throw new NotSupportedException("Native mode3 requires the preceding native world body.");
        NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Native mode3 camera selection needs the current main-loop counter.");
        NativeWorldControlState controls = session.State.WorldControls ?? throw new NotSupportedException("Native mode3 track selection needs actual event BYTE103.");
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(inputs.StoryWord, controls.EventFlags103);
        NativeTrainWorldLoadReceipt load = NativeTrainWorldLoadReceipt.Create(saved, previous, allocation);
        if (saved.Slot4.ShapeId is not (0 or 255))
        {
            throw new NotSupportedException("Native mode3 still needs its saved-vehicle append allocation for this active slot4 shape.");
        }
        byte entry = unchecked((byte)packet.EntryIndex);
        int track = entry <= 65 ? 0 : 1;
        int list = track + (entry % 2 == 0 ? 0 : 14);
        sbyte direction = entry % 2 == 0 ? (sbyte)1 : (sbyte)-1;
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(track, list);
        NativeTrainRailBank primary = NativeTrainRailBank.Create(definition, direction);
        NativeTrainRailBank? alternate = (controls.EventFlags103 & 1) == 0 ? NativeTrainRailBank.Create(assets.ReadScriptedPath(1 - track), direction) : null;
        NativeWorldForestState forest = NativeWorldForestState.Create(process.MainLoopCounter, process.CameraCooldown).DrawRandom(assets.ForestTables.Random, out _);
        NativeWorldPathPosition body = primary.Nodes[2].Position;
        NativeWorldPathCamera camera = NativeWorldScriptedPathState.EvaluateCamera(definition, primary.Nodes, direction, 0);
        ushort mask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte);
        IReadOnlyDictionary<int, int> selectedReplacements = WorldTerrainSelection.Replacements(mask);
        _ = assets.LookupNativeGround(body.X, -body.Z, selectedReplacements, []) ?? throw new NotSupportedException("Native mode3 loader has no actual53EB80 ground polygon.");
        var train = new NativeTrainWorldRuntimeState
        {
            Version = 1, Rule = NativeTrainWorldRuntimeState.RuntimeRule, Packet = packet, Load = load, Callback = 0, Primary = primary, Alternate = alternate, CameraListIndex = list, CameraCounter = 1,
            Camera = camera, CameraReceipt = new NativeWorldPathCameraReceipt(1, 0, 0, direction, primary.Nodes), BodyPosition = body, Forest = forest
        };
        ValidateTrainRide(train);
        return train.ToWorldState() with { ReplacementMask = mask };
    }

    private void ValidateTrainRide(NativeTrainWorldRuntimeState train)
    {
        train.Validate();
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(train.Primary.TrackIndex, train.CameraListIndex);
        sbyte initialDirection = unchecked((byte)train.Packet.EntryIndex) % 2 == 0 ? (sbyte)1 : (sbyte)-1;
        ImmutableArray<NativeWorldScriptedPathNode> cameraNodes = train.Primary.ValidateCallbackPhase(definition, train.Callback, initialDirection);
        train.Alternate?.ValidateCallbackPhase(assets.ReadScriptedPath(train.Alternate.TrackIndex), train.Callback, initialDirection);
        if (!train.CameraReceipt.Nodes.SequenceEqual(cameraNodes) || train.CameraReceipt.Direction != initialDirection)
        {
            throw new InvalidDataException("Native mode3 saved camera lost this callback's actual pre-movement nodes.");
        }
        NativeTrainWorldAllocation actual = assets.ReadTrainWorldAllocation(train.Load.Allocation.Story, train.Load.Allocation.EventFlags103);
        if (train.Load.Allocation.ProgramHash != actual.ProgramHash || train.Load.Allocation.CoordinateHash != actual.CoordinateHash || !train.Load.Allocation.ClauseWords.SequenceEqual(actual.ClauseWords) || train.Camera != NativeWorldScriptedPathState.EvaluateCamera(definition, train.CameraReceipt.Nodes, train.CameraReceipt.Direction, train.CameraReceipt.Counter))
        {
            throw new InvalidDataException("Native mode3 restored resource selection or camera differs from its original callback source.");
        }
        if (actual.Objects.Length != train.Load.Allocation.Objects.Length || actual.Objects.Where((item, index) => item.RawOperand != train.Load.Allocation.Objects[index].RawOperand || !item.CoordinateBytes.SequenceEqual(train.Load.Allocation.Objects[index].CoordinateBytes)).Any())
        {
            throw new InvalidDataException("Native train saved object coordinates differ from the actual selected original records.");
        }
        foreach (NativeTrainRailBank bank in new[] { train.Primary, train.Alternate }.OfType<NativeTrainRailBank>())
        {
            for (int index = 0; index < bank.Presentation.Length; index++)
            {
                NativeWorldPathNodePresentation pose = bank.Presentation[index];
                if (pose.Projection is not null && pose.Projection.Source != bank.Nodes[index].Position || pose.Angles is null != (pose.Orientation is null) || pose.Orientation is not null && (pose.Angles != NativeWorldScriptedPathMath.LinkedOrientation(pose.Orientation.Middle.Words, pose.Orientation.Linked.Words) || pose.Orientation.Callback > train.Callback))
                {
                    throw new InvalidDataException("Native train saved orientation lost its actual node/direction source.");
                }
                if (pose.Projection is not null)
                {
                    NativeWorldPathProjectionCertificate.Create(train.Camera, train.BodyPosition, train.EntityType).ValidateProjection(pose.Projection);
                }
            }
        }
    }

    private WorldState StepTrainRide(WorldState before, float horizontal, float vertical, int priorGraphicsSpeed, out WorldFieldSelection? fieldEntry, out ReplayWorldNavigationStimulus proposal, out uint movementUnits, out int graphicsSpeed)
    {
        NativeTrainWorldRuntimeState train = before.NativeTrainRuntime!;
        ValidateTrainRide(train);
        //548080 retains original packet entity16..22.5573C0/559140 clear its movement axes even though544860 selects the independent foot object for C76640.
        long callback = checked(train.Callback + 1);
        NativeWorldPathPosition origin = train.Primary.Nodes[2].Position;
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(train.Primary.TrackIndex, train.CameraListIndex);
        if (train.Primary.Status is 1 or 2 && train.Primary.StopCounter == 1)
        {
            throw new NotSupportedException("Native train reached its actual station-camera marker.");
        }
        NativeWorldPathCamera camera = NativeWorldScriptedPathState.EvaluateCamera(definition, train.Primary.Nodes, train.Primary.Direction, train.CameraCounter);
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.Create(camera, origin, train.EntityType);
        NativeTrainRailBank primary = train.Primary.Step(definition, callback, camera, origin, (node, cam, body) => context.ProjectNode(node));
        NativeTrainRailBank? alternate = train.Alternate;
        if (alternate is not null)
        {
            NativeWorldScriptedPathDefinition other = assets.ReadScriptedPath(alternate.TrackIndex);
            alternate = alternate.Step(other, callback, camera, origin, (node, cam, body) => context.ProjectNode(node));
        }
        //549B40 excludes global entities16..22;5472C0 hides the selected normal-foot model, and550070 emits no normal-foot effect for that global entity.
        NativeTrainWorldRuntimeState next = train with { Callback = callback, Primary = primary, Alternate = alternate, CameraCounter = checked((ushort)(train.CameraCounter + 1)), Camera = camera, CameraReceipt = new NativeWorldPathCameraReceipt(1, callback, train.CameraCounter, train.Primary.Direction, train.Primary.Nodes), BodyPosition = origin };
        next.Validate();
        fieldEntry = null;
        movementUnits = 0;
        graphicsSpeed = 0;
        proposal = new ReplayWorldNavigationStimulus(origin.X, -origin.Z);
        return next.ToWorldState(before);
    }

    private byte[] RenderTrainRide(WorldState state)
    {
        NativeTrainWorldRuntimeState train = state.NativeTrainRuntime!;
        ValidateTrainRide(train);
        if (train.Callback == 0)
        {
            throw new NativeWorldPresentationDeferredException("Native train allocation awaits its first actual draw callback.");
        }
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.Create(train.Camera, train.BodyPosition, train.EntityType);
        List<NativeWorldStaticModelDraw> models = [];
        foreach (NativeTrainRailBank bank in new[] { train.Primary, train.Alternate }.OfType<NativeTrainRailBank>())
        {
            NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(bank.TrackIndex);
            for (int index = 0; index < 5; index++)
            {
                NativeWorldPathNodePresentation pose = bank.Presentation[index];
                if (NativeTrainRailBank.Link(index) != 0 && pose.Projection is { Visible: true } projection)
                {
                    NativeWorldPathAngles angles = pose.Angles ?? throw new NativeWorldPresentationDeferredException("Visible riding-train node has unknown orientation words.");
                    models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(bank.Shape(definition, index)), new NativeWorldStaticModelPose(context.CanonicalModelAnchor(projection), angles)));
                }
            }
        }
        foreach (NativeTrainWorldObject item in train.Load.Allocation.Objects)
        {
            if (item.EntityType is 0 or 1)
            {
                continue;
            }
            NativeWorldPathNodeProjection projection = context.ProjectNode(item.Position) ?? throw new NativeWorldPresentationDeferredException(context.UnsupportedReason ?? "Native train allocation projection is unproved.");
            if (projection.Visible)
            {
                if (item.Roll != 0)
                {
                    throw new NotSupportedException("Native visible static allocation requires its original nonzero roll transform.");
                }
                NativeWorldPathAngles angles = new(0, unchecked((short)item.PackedOrientation), item.Roll);
                models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(item.ShapeId), new NativeWorldStaticModelPose(context.CanonicalModelAnchor(projection), angles)));
            }
        }
        WorldSky sky = WorldTerrainSelection.SelectSky(assets.Skies, state.X, state.Z, storyByte);
        return renderer!.Render(new Vector3(state.X, -state.NativeHeight, state.Z), state.BodyYaw * MathF.Tau / 4096, state.AnimationId, state.AnimationCounter >> 4, WorldCamera.ScriptedPath(train.Camera, 2048), replacements, sky, avatarVisible: false, staticModels: models);
    }
}
