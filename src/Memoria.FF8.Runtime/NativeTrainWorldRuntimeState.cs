using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeTrainWorldLoadReceipt([property: JsonRequired] long SourceWorldFrame, [property: JsonRequired] ushort RetainedBodyYaw, [property: JsonRequired] byte MapDisplayMode, [property: JsonRequired] NativeSavedWorldVehicleSlot Slot4, [property: JsonRequired] NativeSavedWorldVehicleSlot Slot5, [property: JsonRequired] NativeTrainWorldAllocation Allocation)
{
    public static NativeTrainWorldLoadReceipt Create(NativeSavedWorldState saved, WorldState previous, NativeTrainWorldAllocation allocation)
    {
        allocation.Validate();
        (long frame, ushort yaw, byte mapDisplay) = NativeScriptedWorldLoadReceipt.ResolveRetained(saved, previous);
        return new NativeTrainWorldLoadReceipt(frame, yaw, mapDisplay, saved.Slot4, saved.Slot5, allocation);
    }

    public void Validate()
    {
        Allocation?.Validate();
        if (SourceWorldFrame < 0 || RetainedBodyYaw >= 4096 || Slot4?.Pose is null || Slot5?.Pose is null || Allocation is null)
        {
            throw new InvalidDataException("Native riding-train allocation lost its preceding physical body or saved vehicle records.");
        }
    }
}

public sealed record NativeTrainRailBank([property: JsonRequired] int TrackIndex, [property: JsonRequired] string ChunkHash, [property: JsonRequired] sbyte Direction, [property: JsonRequired] short Pause, [property: JsonRequired] int Speed, [property: JsonRequired] byte? Status, [property: JsonRequired] byte StopCounter, [property: JsonRequired] ImmutableArray<NativeWorldScriptedPathNode> Nodes, [property: JsonRequired] ImmutableArray<NativeWorldPathNodePresentation> Presentation)
{
    public static NativeTrainRailBank Create(NativeWorldScriptedPathDefinition definition, sbyte direction)
    {
        definition.Validate();
        if (direction is not (1 or -1))
        {
            throw new InvalidDataException("Native train bank direction must be the actual signed loader byte.");
        }
        ImmutableArray<NativeWorldScriptedPathNode> nodes = NativeWorldScriptedPathState.InitialDistances(definition, 5, direction).Select(distance => NativeWorldScriptedPathState.ResolveNode(definition, new(0, distance, default), direction, out _)).ToImmutableArray();
        //545330 initializes pause/speed/stop count, but does not write retained BYTE3.
        return new NativeTrainRailBank(definition.TrackIndex, definition.ChunkHash, direction, 30, 0, null, 0, nodes, Enumerable.Range(0, 5).Select(_ => new NativeWorldPathNodePresentation(null, null, null)).ToImmutableArray());
    }

    public void Validate(NativeWorldScriptedPathDefinition definition)
    {
        definition.Validate();
        if (TrackIndex != definition.TrackIndex || ChunkHash != definition.ChunkHash || Direction is not (1 or -1) || Pause is < 0 or > 30 || Speed is < 0 or > 100 || Status is > 2 || StopCounter > definition.ForwardStops.Length || Nodes.IsDefault || Nodes.Length != 5 || Presentation.IsDefault || Presentation.Length != 5 || Presentation.Any(value => value is null))
        {
            throw new InvalidDataException("Native train rail bank has inconsistent physical words or resource identity.");
        }
        foreach (NativeWorldScriptedPathNode node in Nodes)
        {
            if (node is null || node.Segment is < 0 || node.Segment >= definition.Points.Length - 1 || node.Distance < 0 || NativeWorldScriptedPathState.ResolveNode(definition, node, Direction, out bool complete) != node || complete)
            {
                throw new InvalidDataException("Native train node lost its actual signed rail interpolation.");
            }
        }
    }

    public static sbyte Link(int index) => index == 2 ? (sbyte)0 : index < 2 ? (sbyte)1 : (sbyte)-1;
    public byte Shape(NativeWorldScriptedPathDefinition definition, int index) => index is 0 or 4 ? definition.FrontShape : definition.RearShape;

    public ImmutableArray<NativeWorldScriptedPathNode> ValidateCallbackPhase(NativeWorldScriptedPathDefinition definition, long callback, sbyte initialDirection)
    {
        Validate(definition);
        NativeTrainRailBank initial = Create(definition, initialDirection);
        NativeWorldRailAdvance phase = new(initial.Pause, initial.Speed, initial.Nodes, false);
        ImmutableArray<NativeWorldScriptedPathNode> previous = initial.Nodes;
        for (long index = 0; index < callback; index++)
        {
            previous = phase.Nodes;
            phase = NativeWorldScriptedPathState.AdvanceRail(definition, phase.Nodes, initialDirection, phase.Pause, phase.Speed, 100, 2);
            if (phase.Endpoint)
            {
                throw new NotSupportedException("Native riding-train restored phase reaches an unsupported endpoint.");
            }
        }
        if (Direction != initialDirection || Pause != phase.Pause || Speed != phase.Speed || StopCounter != 0 || !Nodes.SequenceEqual(phase.Nodes) || callback >= 30 && Status != 0)
        {
            throw new InvalidDataException("Native train saved bank differs from its executed loader and actual callback phase.");
        }
        return previous;
    }

    public NativeTrainRailBank Step(NativeWorldScriptedPathDefinition definition, long callback, NativeWorldPathCamera camera, NativeWorldPathPosition origin, Func<NativeWorldPathPosition, NativeWorldPathCamera, NativeWorldPathPosition, NativeWorldPathNodeProjection?> project)
    {
        Validate(definition);
        NativeWorldRailAdvance phase = NativeWorldScriptedPathState.AdvanceRail(definition, Nodes, Direction, Pause, Speed, 100, 2);
        if (phase.Endpoint)
        {
            throw new NotSupportedException("Native riding train reached its real rail endpoint; station field-return continuation is not implemented.");
        }
        NativeTrainRailBank next = this with { Pause = phase.Pause, Speed = phase.Speed, Nodes = phase.Nodes, Status = phase.Pause == 0 ? (byte)0 : Status };
        next = next.Project(callback, camera, origin, project);
        next.Validate(definition);
        return next;
    }

    private NativeTrainRailBank Project(long callback, NativeWorldPathCamera camera, NativeWorldPathPosition origin, Func<NativeWorldPathPosition, NativeWorldPathCamera, NativeWorldPathPosition, NativeWorldPathNodeProjection?> project)
    {
        NativeWorldPathNodeProjection?[] projections = new NativeWorldPathNodeProjection?[5];
        NativeWorldPathNodeProjection Require(int index)
        {
            return projections[index] ??= project(Nodes[index].Position, camera, origin) ?? throw new NotSupportedException("Native train callback needs exact5470E0 node visibility and transformed words.");
        }
        var presentation = ImmutableArray.CreateBuilder<NativeWorldPathNodePresentation>(5);
        for (int index = 0; index < 5; index++)
        {
            NativeWorldPathNodeProjection current = Require(index);
            NativeWorldPathAngles? angles = Presentation[index].Angles;
            NativeWorldPathOrientationReceipt? orientation = Presentation[index].Orientation;
            if (current.Visible && Link(index) != 0)
            {
                NativeWorldPathNodeProjection linked = Require(index + Link(index));
                angles = NativeWorldScriptedPathMath.LinkedOrientation(current.Words, linked.Words);
                orientation = new NativeWorldPathOrientationReceipt(1, callback, current, linked);
            }
            presentation.Add(new NativeWorldPathNodePresentation(angles, orientation, current));
        }
        return this with { Presentation = presentation.MoveToImmutable() };
    }
}

/// <summary>Executed544860 mode3 allocation: two real train banks and an independent controlled normal-foot record.</summary>
public sealed record NativeTrainWorldRuntimeState
{
    public const string RuntimeRule = "ff8-en-riding-train-mode3-544860-545330-53fac0-v1";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required WorldTransition Packet { get; init; }
    [JsonRequired]
    public required NativeTrainWorldLoadReceipt Load { get; init; }
    [JsonRequired]
    public required long Callback { get; init; }
    [JsonRequired]
    public required NativeTrainRailBank Primary { get; init; }
    [JsonRequired]
    public required NativeTrainRailBank? Alternate { get; init; }
    [JsonRequired]
    public required int CameraListIndex { get; init; }
    [JsonRequired]
    public required ushort CameraCounter { get; init; }
    [JsonRequired]
    public required NativeWorldPathCamera Camera { get; init; }
    [JsonRequired]
    public required NativeWorldPathCameraReceipt CameraReceipt { get; init; }
    [JsonRequired]
    public required NativeWorldPathPosition BodyPosition { get; init; }
    [JsonRequired]
    public required NativeWorldForestState Forest { get; init; }
    public byte EntityType => unchecked((byte)Packet.Mode);
    public byte ControlledObjectEntityType => checked((byte)Load.Allocation.Objects[Load.Allocation.ControlledObjectIndex].EntityType);

    public void Validate()
    {
        Load?.Validate();
        Forest?.Validate();
        if (Packet is null)
        {
            throw new InvalidDataException("Native train ride has no original packet.");
        }
        byte entry = unchecked((byte)Packet.EntryIndex);
        if (Version != 1 || Rule != RuntimeRule || Packet is null || unchecked((byte)Packet.Mode) is not (>= 16 and <= 22) || entry is < 64 or > 67 || unchecked((sbyte)Packet.Extra) != -1 || Load is null || Callback < 0 || Primary is null || Primary.TrackIndex != (entry <= 65 ? 0 : 1) || Alternate is not null && Alternate.TrackIndex != 1 - Primary.TrackIndex || ControlledObjectEntityType is not (0 or 1) || CameraListIndex != (entry <= 65 ? 0 : 1) + (entry % 2 == 0 ? 0 : 14) || CameraCounter != Callback + 1 || CameraCounter > short.MaxValue || Camera is null || CameraReceipt is null || CameraReceipt.Callback != Callback || CameraReceipt.Counter != Callback || CameraReceipt.Nodes.IsDefault || CameraReceipt.Nodes.Length != 5 || BodyPosition != CameraReceipt.Nodes[2].Position || Forest is null)
        {
            throw new InvalidDataException("Native mode3 world snapshot lost its executed packet, physical bank or process authority.");
        }
    }

    public WorldState ToWorldState(WorldState? previous = null)
    {
        Validate();
        return (previous ?? new WorldState()) with { Frame = Callback, X = BodyPosition.X, Z = unchecked(-BodyPosition.Z), NativeHeight = BodyPosition.Y, BodyYaw = Load.RetainedBodyYaw, CameraYaw = NativeSavedWorldScriptedReturn.CameraYaw(Camera), AnimationId = 0, AnimationCounter = 0, GroundType = null, NativeFootRuntime = null, NativeCarRuntime = null, NativeScriptedRuntime = null, NativeTrainRuntime = this };
    }
}
