using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeTrainWorldLoadReceipt([property: JsonRequired] long SourceWorldFrame, [property: JsonRequired] ushort RetainedBodyYaw, [property: JsonRequired] byte MapDisplayMode, [property: JsonRequired] NativeSavedWorldVehicleSlot Slot4, [property: JsonRequired] NativeSavedWorldVehicleSlot Slot5, [property: JsonRequired] NativeTrainWorldAllocation Allocation, [property: JsonRequired] int RetainedCameraVelocity)
{
    public static NativeTrainWorldLoadReceipt Create(NativeSavedWorldState saved, WorldState previous, NativeTrainWorldAllocation allocation)
    {
        allocation.Validate();
        (long frame, ushort yaw, byte mapDisplay) = NativeScriptedWorldLoadReceipt.ResolveRetained(saved, previous);
        return new NativeTrainWorldLoadReceipt(frame, yaw, mapDisplay, saved.Slot4, saved.Slot5, allocation, previous.CameraVelocity);
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

public sealed record NativeTrainRailContactReversal([property: JsonRequired] int Version, [property: JsonRequired] long Callback, [property: JsonRequired] sbyte SourceDirection, [property: JsonRequired] byte SourceStopCounter, [property: JsonRequired] ImmutableArray<NativeWorldScriptedPathNode> SourceNodes);

public sealed record NativeTrainRailBank([property: JsonRequired] int TrackIndex, [property: JsonRequired] string ChunkHash, [property: JsonRequired] sbyte Direction, [property: JsonRequired] short Pause, [property: JsonRequired] int Speed, [property: JsonRequired] byte? Status, [property: JsonRequired] byte StopCounter, [property: JsonRequired] ImmutableArray<NativeWorldScriptedPathNode> Nodes, [property: JsonRequired] ImmutableArray<NativeWorldPathNodePresentation> Presentation)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTrainRailContactReversal? ContactReversal { get; init; }
    //An endpoint545B20 returns2 before its XYZ stores. This owner exists only inside Step's contact phase;
    //neither the completed callback nor a serialized bank carries an unresolved endpoint candidate.
    [JsonIgnore]
    internal NativeTrainRailBank? CallbackSource { get; init; }

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
        if (TrackIndex != definition.TrackIndex || ChunkHash != definition.ChunkHash || Direction is not (1 or -1) || Pause is < -1 or > 30 || Speed is < 0 or > 100 || Status is > 2 || Nodes.IsDefault || Nodes.Length != 5 || Presentation.IsDefault || Presentation.Length != 5 || Presentation.Any(value => value is null))
        {
            throw new InvalidDataException("Native train rail bank has inconsistent physical words or resource identity.");
        }
        if (CallbackSource is { } sourceBank)
        {
            sourceBank.Validate(definition);
            NativeWorldRailAdvance expected = NativeWorldScriptedPathState.AdvanceRail(definition, sourceBank.Nodes, sourceBank.Direction, sourceBank.Pause, sourceBank.Speed, 100, 2, sourceBank.Status, sourceBank.StopCounter);
            if (!expected.Endpoint || ContactReversal is not null || Direction != sourceBank.Direction || Pause != expected.Pause || Speed != expected.Speed || Status != expected.Status || StopCounter != expected.StopCounter || !Nodes.SequenceEqual(expected.Nodes))
            {
                throw new InvalidDataException("Native in-progress train endpoint lost its executed pre-contact rail phase.");
            }
            return;
        }
        if (ContactReversal is { } reversal)
        {
            if (reversal.Version != 1 || reversal.Callback < 1 || reversal.SourceDirection is not (1 or -1) || Direction != -reversal.SourceDirection || StopCounter != unchecked((byte)(definition.ForwardStops.Length - reversal.SourceStopCounter)) || Pause != 15 || Speed != 0 || reversal.SourceNodes.IsDefault || reversal.SourceNodes.Length != 5)
            {
                throw new InvalidDataException("Native contact reversal lost its executed source phase.");
            }
            for (int index = 0; index < Nodes.Length; index++)
            {
                NativeWorldScriptedPathNode source = reversal.SourceNodes[index];
                if (source is null || source.Segment < 0 || source.Segment >= definition.Points.Length - 1 || source.Distance < 0 || NativeWorldScriptedPathState.ResolveNode(definition, source, reversal.SourceDirection, out bool complete) != source || complete || Nodes[index] != ReverseContactNode(definition, source, reversal.SourceDirection))
                {
                    throw new InvalidDataException("Native contact reversal differs from its retained previous-node XYZ and reversed rail words.");
                }
            }
            return;
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

    public ImmutableArray<NativeWorldScriptedPathNode> ValidateCallbackPhase(NativeWorldScriptedPathDefinition definition, long callback, sbyte initialDirection, Func<long, NativeTrainRailBank, NativeTrainRailBank>? callbackTail = null)
    {
        Validate(definition);
        NativeTrainRailBank phase = Create(definition, initialDirection);
        ImmutableArray<NativeWorldScriptedPathNode> previous = phase.Nodes;
        for (long index = 0; index < callback; index++)
        {
            previous = phase.Nodes;
            NativeWorldRailAdvance advance = NativeWorldScriptedPathState.AdvanceRail(definition, phase.Nodes, initialDirection, phase.Pause, phase.Speed, 100, 2, phase.Status, phase.StopCounter);
            if (advance.Endpoint)
            {
                throw new NotSupportedException("Native riding-train restored phase reaches an unsupported endpoint.");
            }
            bool align = advance.Status == 1 && advance.StopCounter != phase.StopCounter && advance.Pause > 0 && TrackIndex == 1;
            phase = phase with { Pause = advance.Pause, Speed = advance.Speed, Nodes = advance.Nodes, Status = advance.Status, StopCounter = advance.StopCounter, ContactReversal = null };
            if (align)
            {
                phase = phase.AlignAtStop(definition);
            }
            if (callbackTail is not null)
            {
                phase = callbackTail(index + 1, phase);
            }
        }
        if (Direction != initialDirection || Pause != phase.Pause || Speed != phase.Speed || StopCounter != phase.StopCounter || !Nodes.SequenceEqual(phase.Nodes) || Status != phase.Status)
        {
            throw new InvalidDataException("Native train saved bank differs from its executed loader and actual callback phase.");
        }
        return previous;
    }

    public NativeTrainRailBank Step(NativeWorldScriptedPathDefinition definition, long callback, NativeWorldPathCamera camera, NativeWorldPathPosition origin, Func<NativeWorldPathPosition, NativeWorldPathCamera, NativeWorldPathPosition, NativeWorldPathNodeProjection?> project)
    {
        return Step(definition, callback, camera, origin, project, null, out _);
    }

    public NativeTrainRailBank Step(NativeWorldScriptedPathDefinition definition, long callback, NativeWorldPathCamera camera, NativeWorldPathPosition origin, Func<NativeWorldPathPosition, NativeWorldPathCamera, NativeWorldPathPosition, NativeWorldPathNodeProjection?> project, bool? stopAtEndpoint, out bool endpoint, Func<NativeTrainRailBank, NativeTrainRailBank>? contact = null)
    {
        Validate(definition);
        NativeWorldRailAdvance phase = NativeWorldScriptedPathState.AdvanceRail(definition, Nodes, Direction, Pause, Speed, 100, 2, Status, StopCounter);
        NativeTrainRailBank next = this with { Pause = phase.Pause, Speed = phase.Speed, Nodes = phase.Nodes, Status = phase.Status, StopCounter = phase.StopCounter, ContactReversal = null, CallbackSource = phase.Endpoint ? this : null };
        next = next.Project(callback, camera, origin, project);
        if (phase.Status == 1 && phase.StopCounter != StopCounter && phase.Pause > 0 && TrackIndex == 1)
        {
            //545480 aligns track1 after a newly returned stop marker, then executes5499D0 again.
            next = next.AlignAtStop(definition).Project(callback, camera, origin, project);
        }
        if (contact is not null)
        {
            next = contact(next);
        }
        endpoint = next.Status == 2 && next.Pause == 0;
        if (endpoint)
        {
            if (stopAtEndpoint is null)
            {
                throw new NotSupportedException("Native train endpoint requires the actual BYTE103 stop flag.");
            }
            sbyte reversed = checked((sbyte)-next.Direction);
            ImmutableArray<NativeWorldScriptedPathNode> reset = NativeWorldScriptedPathState.InitialDistances(definition, 5, reversed).Select(distance => NativeWorldScriptedPathState.ResolveNode(definition, new NativeWorldScriptedPathNode(0, distance, default), reversed, out _)).ToImmutableArray();
            //545480 resets nodes after its second projection and retains the actual prior draw words.
            next = next with { Direction = reversed, Pause = stopAtEndpoint.Value ? (short)-1 : (short)30, Speed = 0, StopCounter = 0, Nodes = reset, CallbackSource = null };
        }
        if (next.CallbackSource is not null)
        {
            throw new InvalidDataException("Native train contact did not consume its in-progress endpoint phase.");
        }
        next.Validate(definition);
        return next;
    }

    /// <summary>545480 after a positive549B40 result: restore prior physical rail words, pause15, and execute the native conditional reversal.</summary>
    public NativeTrainRailBank ResolveContact(NativeTrainRailBank before, NativeWorldScriptedPathDefinition definition, long callback, NativeWorldPathCamera camera, NativeWorldPathPosition origin, Func<NativeWorldPathPosition, NativeWorldPathCamera, NativeWorldPathPosition, NativeWorldPathNodeProjection?> project, bool stopAtEndpoint)
    {
        Validate(definition);
        before.Validate(definition);
        if (Pause != 0 || before.TrackIndex != TrackIndex || before.ChunkHash != ChunkHash || before.Direction != Direction || callback < 1)
        {
            throw new InvalidDataException("Native train contact requires this callback's unpaused bank and its previous rail words.");
        }
        ImmutableArray<NativeWorldScriptedPathNode> rolledBack = before.Nodes.Select(node => NativeWorldScriptedPathState.ResolveNode(definition, node, Direction, out _)).ToImmutableArray();
        NativeTrainRailBank next = this with { Nodes = rolledBack, Pause = 15, Speed = 0, ContactReversal = null, CallbackSource = null };
        bool atBoundary = rolledBack.Any(node => node.Distance == 0 && (node.Segment == 0 || (Direction == 1 ? definition.ForwardStops : definition.ReverseStops).Contains(checked((byte)(Direction == 1 ? node.Segment : definition.Points.Length - node.Segment - 1)))));
        if (!stopAtEndpoint && !atBoundary)
        {
            var receipt = new NativeTrainRailContactReversal(1, callback, Direction, StopCounter, rolledBack);
            next = next with { Direction = checked((sbyte)-Direction), StopCounter = unchecked((byte)(definition.ForwardStops.Length - StopCounter)), Nodes = rolledBack.Select(node => ReverseContactNode(definition, node, Direction)).ToImmutableArray(), ContactReversal = receipt };
        }
        //XYZ was computed by the rollback545B20 call. Reversal changes distance/segment/direction only;
        //the second5499D0 projects those retained XYZ before the next callback resolves the reversed phase.
        next = next.Project(callback, camera, origin, project);
        next.Validate(definition);
        return next;
    }

    private static NativeWorldScriptedPathNode ReverseContactNode(NativeWorldScriptedPathDefinition definition, NativeWorldScriptedPathNode source, sbyte direction)
    {
        int point = direction == 1 ? source.Segment : definition.Points.Length - source.Segment - 1;
        int length = NativeWorldScriptedPathMath.SegmentLength(definition.Points[point], definition.Points[point + direction]);
        return source with { Segment = checked((short)(definition.Points.Length - source.Segment - 2)), Distance = length - source.Distance };
    }

    private NativeTrainRailBank AlignAtStop(NativeWorldScriptedPathDefinition definition)
    {
        NativeWorldScriptedPathNode leading = Nodes.OrderByDescending(node => node.Segment).ThenByDescending(node => node.Distance).First();
        int[] spacing = NativeWorldScriptedPathState.InitialDistances(definition, 5, Direction);
        var aligned = ImmutableArray.CreateBuilder<NativeWorldScriptedPathNode>(5);
        foreach (int distance in spacing)
        {
            NativeWorldScriptedPathNode node = NativeWorldScriptedPathState.ResolveNode(definition, new NativeWorldScriptedPathNode(leading.Segment, distance, default), Direction, out bool complete);
            if (complete)
            {
                throw new NotSupportedException("Native train stop alignment reaches an invalid endpoint.");
            }
            aligned.Add(node);
        }
        return this with { Nodes = aligned.MoveToImmutable() };
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
    public required ImmutableArray<long> StationDeclines { get; init; }
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
    [JsonRequired]
    public required NativeWorldGroundCacheState GroundCache { get; init; }
    [JsonRequired]
    public required int GroundYaw { get; init; }
    [JsonRequired]
    public required NativeTrainCameraGeometry CameraGeometry { get; init; }
    public byte EntityType => unchecked((byte)Packet.Mode);
    public byte ControlledObjectEntityType => checked((byte)Load.Allocation.Objects[Load.Allocation.ControlledObjectIndex].EntityType);

    public void Validate()
    {
        Load?.Validate();
        Forest?.Validate();
        GroundCache?.Validate();
        if (GroundCache is null || GroundYaw is < -1120 or > 1120 || CameraGeometry != NativeTrainCameraGeometry.Evaluate(Camera))
        {
            throw new InvalidDataException("Native train lost its executed ground or retained camera words.");
        }
        if (Packet is null)
        {
            throw new InvalidDataException("Native train ride has no original packet.");
        }
        byte entry = unchecked((byte)Packet.EntryIndex);
        if (Version != 1 || Rule != RuntimeRule || Packet is null || unchecked((byte)Packet.Mode) is not (>= 16 and <= 22) || entry is < 64 or > 67 || unchecked((sbyte)Packet.Extra) != -1 || Load is null || Callback < 0 || Primary is null || Primary.TrackIndex != (entry <= 65 ? 0 : 1) || Alternate is not null && Alternate.TrackIndex != 1 - Primary.TrackIndex || ControlledObjectEntityType is not (0 or 1) || CameraListIndex != (entry <= 65 ? 0 : 1) + (entry % 2 == 0 ? 0 : 14) || CameraCounter > short.MaxValue || Camera is null || CameraReceipt is null || CameraReceipt.Callback != Callback || CameraReceipt.Counter > short.MaxValue || CameraReceipt.Nodes.IsDefault || CameraReceipt.Nodes.Length != 5 || BodyPosition != CameraReceipt.Nodes[2].Position || Forest is null || StationDeclines.IsDefault || StationDeclines.Any(value => value < 1 || value > Callback) || !StationDeclines.SequenceEqual(StationDeclines.Distinct().Order()))
        {
            throw new InvalidDataException("Native mode3 world snapshot lost its executed packet, physical bank or process authority.");
        }
    }

    public WorldState ToWorldState(WorldState? previous = null)
    {
        Validate();
        return (previous ?? new WorldState()) with { Frame = Callback, X = BodyPosition.X, Z = unchecked(-BodyPosition.Z), NativeHeight = BodyPosition.Y, BodyYaw = Load.RetainedBodyYaw, CameraYaw = CameraGeometry.Yaw & 4095, CameraVelocity = Load.RetainedCameraVelocity, CameraTargetX = Camera.Target.X, CameraTargetZ = -Camera.Target.Z, CameraTargetNativeHeight = Camera.Target.Y, AnimationId = 0, AnimationCounter = 0, NativeFootRuntime = null, NativeCarRuntime = null, NativeScriptedRuntime = null, NativeTrainRuntime = this };
    }
}
