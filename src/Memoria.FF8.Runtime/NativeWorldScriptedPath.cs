using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeWorldPathPosition(int X, int Y, int Z);
public sealed record NativeWorldRailPoint(int X, int Y, int Z, int Opaque);
public sealed record NativeWorldPathCameraRecord(short Deadline, byte EyeMode, byte TargetMode, byte Interpolation, NativeWorldPathPosition EyeOffset, NativeWorldPathPosition TargetOffset);
public sealed record NativeWorldPathCamera(NativeWorldPathPosition? Eye, NativeWorldPathPosition Target)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldFootCameraState? FootState { get; init; }
}
public sealed record NativeWorldPathAngles(short Pitch, short Yaw, short Roll);
public readonly record struct NativeWorldPathWords(short X, short Y, short Z);

/// <summary>Actual5470E0 output words, including sector translation and depth curvature, tied to their source node/camera.</summary>
public sealed record NativeWorldPathNodeProjection(int Version, string Rule, NativeWorldPathPosition Source, NativeWorldPathCamera Camera, NativeWorldPathPosition Origin, NativeWorldPathWords Words, bool Visible)
{
    public const string NativeRule = "ff8-en-node-5470e0-sector-552a90-depth-curve-v1";

    public void Validate(NativeWorldPathPosition source, NativeWorldPathCamera camera, NativeWorldPathPosition origin)
    {
        if (Version != 1 || Rule != NativeRule || Source != source || Camera != camera || Origin != origin)
        {
            throw new InvalidDataException("Native linked-node projection lost its actual source or camera transform provenance.");
        }
    }
}

public sealed record NativeWorldPathOrientationReceipt(int Version, long Callback, NativeWorldPathNodeProjection Middle, NativeWorldPathNodeProjection Linked);
public sealed record NativeWorldPathCameraReceipt(int Version, long Callback, ushort Counter, sbyte Direction, ImmutableArray<NativeWorldScriptedPathNode> Nodes);
public sealed record NativeWorldPathNodePresentation([property: JsonRequired] NativeWorldPathAngles? Angles, [property: JsonRequired] NativeWorldPathOrientationReceipt? Orientation, [property: JsonRequired] NativeWorldPathNodeProjection? Projection);

/// <summary>Original rail member, selected chunk and finite camera program; no field destination is substituted here.</summary>
public sealed record NativeWorldScriptedPathDefinition(int Version, string ExecutableHash, string RailHash, string ChunkHash, string CameraHash, int TrackIndex, byte FrontShape, byte RearShape, short FrontLength, short RearLength, ImmutableArray<NativeWorldRailPoint> Points, ImmutableArray<NativeWorldPathCameraRecord> CameraRecords)
{
    public int CameraListIndex { get; init; } = TrackIndex;
    public ImmutableArray<byte> ForwardStops { get; init; } = [];
    public ImmutableArray<byte> ReverseStops { get; init; } = [];
    public void Validate()
    {
        if (Version != 1 || ExecutableHash is not (NativeFieldRuntimeProfile.FF8EnExecutableHash or NativeFieldRuntimeProfile.RemasteredEnImageHash) || !IsHash(RailHash) || !IsHash(ChunkHash) || !IsHash(CameraHash) || TrackIndex is < 0 or >= 14 || FrontShape < 64 || RearShape < 64 || FrontLength < 128 || RearLength < 128 || Points.IsDefault || Points.Length is < 2 or > 127 || Points.Any(point => point is null) || CameraRecords.IsDefaultOrEmpty || CameraRecords.Any(record => record is null))
        {
            throw new InvalidDataException("Native scripted path lost its original resource identity or supported finite layout.");
        }
        for (int index = 0; index < Points.Length - 1; index++)
        {
            if (NativeWorldScriptedPathMath.SegmentLength(Points[index], Points[index + 1]) < 8)
            {
                throw new NotSupportedException("Native scripted path requires nonzero shift-three segment denominators.");
            }
        }
        if (CameraListIndex is < 0 or >= 28 || ForwardStops.IsDefault || ReverseStops.IsDefault || ForwardStops.Length != ReverseStops.Length || ForwardStops.Length > 4 || ForwardStops.Concat(ReverseStops).Any(stop => stop >= Points.Length))
        {
            throw new InvalidDataException("Native rail stop/camera selectors leave their original resource tables.");
        }
        short previous = 0;
        for (int index = 0; index < CameraRecords.Length; index++)
        {
            NativeWorldPathCameraRecord record = CameraRecords[index];
            bool final = index == CameraRecords.Length - 1;
            if (!IsNodeCameraMode(record.EyeMode) || !IsNodeCameraMode(record.TargetMode) || record.Interpolation is not (1 or 2) || final && (record.Deadline != -1 || record.Interpolation != 2) || !final && record.Deadline != -2 && record.Deadline <= previous || !NativeWorldScriptedPathMath.IsWordPosition(record.EyeOffset) || !NativeWorldScriptedPathMath.IsWordPosition(record.TargetOffset))
            {
                throw new NotSupportedException("Native scripted camera requires ordered native node-selector linear records and a fixed terminal record.");
            }
            if (record.Deadline != -2)
            {
                previous = record.Deadline;
            }
        }
    }

    private static bool IsNodeCameraMode(byte mode) => mode is >= 7 and <= 9 or >= 12 and <= 16;

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

public sealed record NativeWorldScriptedPathNode(short Segment, int Distance, NativeWorldPathPosition Position);

/// <summary>Native mode1/2 linked-node driver. World entry must supply an actual packet and original visibility authority.</summary>
public sealed record NativeWorldScriptedPathState
{
    public const string LoadRule = "ff8-en-scripted-mode1-2-545330-545480-552ad0-v1";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required WorldTransition Packet { get; init; }
    [JsonRequired]
    public required string ChunkHash { get; init; }
    [JsonRequired]
    public required string CameraHash { get; init; }
    [JsonRequired]
    public required byte EntityType { get; init; }
    [JsonRequired]
    public required long Callback { get; init; }
    [JsonRequired]
    public required sbyte Direction { get; init; }
    [JsonRequired]
    public required short? Pause { get; init; }
    [JsonRequired]
    public required bool? StopAtEndpoint { get; init; }
    [JsonRequired]
    public required int Speed { get; init; }
    [JsonRequired]
    public required ushort CameraCounter { get; init; }
    [JsonRequired]
    public required ImmutableArray<NativeWorldScriptedPathNode> Nodes { get; init; }
    [JsonRequired]
    public required NativeWorldPathPosition EntityPosition { get; init; }
    [JsonRequired]
    public required NativeWorldPathCamera Camera { get; init; }
    [JsonRequired]
    public required NativeWorldPathCameraReceipt CameraReceipt { get; init; }
    [JsonRequired]
    public required ImmutableArray<NativeWorldPathNodePresentation> Presentation { get; init; }
    [JsonRequired]
    public required bool EndpointRequested { get; init; }
    [JsonRequired]
    public required long? EndpointCallback { get; init; }
    [JsonRequired]
    public required NativeWorldPathPosition? EndpointPriorEntityPosition { get; init; }
    [JsonRequired]
    public required int Fade { get; init; }

    [JsonIgnore]
    public byte Mode => EntityType is >= 16 and <= 22 ? (byte)2 : (byte)1;
    [JsonIgnore]
    public int NodeCount => Mode == 1 ? 3 : 5;
    [JsonIgnore]
    public int CentralNode => Mode == 1 ? 1 : 2;
    [JsonIgnore]
    public int MaximumSpeed => Mode == 1 ? 80 : 100;

    public sbyte NodeLink(int index)
    {
        if (index < 0 || index >= NodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return Mode == 1 ? index == 1 ? Direction : (sbyte)0 : index == 2 ? (sbyte)0 : index < 2 ? (sbyte)1 : (sbyte)-1;
    }

    public byte NodeShape(NativeWorldScriptedPathDefinition definition, int index)
    {
        _ = NodeLink(index);
        return Mode == 1 ? EntityType == 65 ? (byte)77 : (byte)78 : index is 0 or 4 ? definition.FrontShape : definition.RearShape;
    }

    public bool ControlsDisabled => true;
    // 559240 always selects2 when native entity!=50; it runs before545480.
    public int NativeMovementMultiplier => 2;
    public bool FieldTransitionReady => EndpointRequested && Fade == 256;
    public byte? CompletedFieldRow => FieldTransitionReady ? unchecked((byte)Packet.Extra) : null;

    public static NativeWorldScriptedPathState Create(NativeWorldScriptedPathDefinition definition, WorldTransition packet, bool? stopAtEndpoint)
    {
        definition.Validate();
        byte entity = unchecked((byte)packet.Mode);
        byte entry = unchecked((byte)packet.EntryIndex);
        if (entity is not (65 or 66) && entity is not (>= 16 and <= 22) || entry >= 14 || entry != definition.TrackIndex || unchecked((sbyte)packet.Extra) < 0)
        {
            throw new NotSupportedException("Native scripted path requires an actual forward mode1/2 packet and its matching original track.");
        }
        if (!definition.ForwardStops.IsEmpty || definition.CameraListIndex != definition.TrackIndex)
        {
            throw new NotSupportedException("Native positive mode1/2 entry requires its original nonstop rail/camera selection.");
        }
        int nodeCount = entity is >= 16 and <= 22 ? 5 : 3;
        ImmutableArray<NativeWorldScriptedPathNode> nodes = InitialDistances(definition, nodeCount, 1).Select(distance => ResolveNode(definition, new NativeWorldScriptedPathNode(0, distance, default), 1, out _)).ToImmutableArray();
        var state = new NativeWorldScriptedPathState
        {
            Version = 1, Rule = LoadRule, Packet = packet, ChunkHash = definition.ChunkHash, CameraHash = definition.CameraHash, EntityType = entity,
            Callback = 0, Direction = 1, Pause = 30, StopAtEndpoint = stopAtEndpoint, Speed = 0, CameraCounter = 1, Nodes = nodes, EntityPosition = nodes[nodeCount / 2].Position,
            Camera = EvaluateCamera(definition, nodes, 1, 0), CameraReceipt = new NativeWorldPathCameraReceipt(1, 0, 0, 1, nodes), Presentation = Enumerable.Range(0, nodeCount).Select(_ => new NativeWorldPathNodePresentation(null, null, null)).ToImmutableArray(), EndpointRequested = false, EndpointCallback = null, EndpointPriorEntityPosition = null, Fade = 0
        };
        state.Validate(definition);
        return state;
    }

    public void Validate(NativeWorldScriptedPathDefinition definition)
    {
        definition.Validate();
        if (Version != 1 || Rule != LoadRule || Packet is null || ChunkHash != definition.ChunkHash || CameraHash != definition.CameraHash || unchecked((byte)Packet.EntryIndex) != definition.TrackIndex || unchecked((byte)Packet.Mode) != EntityType || EntityType is not (65 or 66) && EntityType is not (>= 16 and <= 22) || Callback < 0 || Direction != (EndpointRequested ? -1 : 1) || Presentation.IsDefault || Presentation.Length != NodeCount || Presentation.Any(pose => pose is null) || Nodes.IsDefault || Nodes.Length != NodeCount || Nodes.Any(node => node is null || node.Segment < 0 || node.Segment >= definition.Points.Length - 1 || node.Distance < 0) || Speed < 0 || Speed > MaximumSpeed || Speed > 16 + 4 * Callback || Fade is < 0 or > 256 || Fade % 16 != 0 || !EndpointRequested && (Pause is null or < 0 or > 30 || Fade != 0) || EndpointRequested && Pause != (StopAtEndpoint is null ? null : StopAtEndpoint.Value ? -1 : 30))
        {
            throw new InvalidDataException("Native scripted path snapshot has inconsistent packet, resource or driver phase.");
        }
        if (EndpointRequested != (EndpointPriorEntityPosition is not null) || EndpointRequested && (EndpointCallback is null or < 30 || Callback != EndpointCallback + Fade / 16) || !EndpointRequested && EndpointCallback is not null)
        {
            throw new InvalidDataException("Native scripted endpoint lost its prior committed body witness or save/fade callback phase.");
        }
        if (EndpointRequested)
        {
            ValidateEndpointBodyWitness(definition);
        }
        foreach (NativeWorldScriptedPathNode node in Nodes)
        {
            NativeWorldScriptedPathNode resolved = ResolveNode(definition, node, Direction, out bool completed);
            if (completed || resolved != node)
            {
                throw new InvalidDataException("Native scripted path node differs from its retained signed interpolation phase.");
            }
        }
        if (!EndpointRequested)
        {
            int[] initial = InitialDistances(definition, NodeCount, 1);
            long? advanced = null;
            for (int index = 0; index < Nodes.Length; index++)
            {
                long distance = Nodes[index].Distance;
                for (int segment = 0; segment < Nodes[index].Segment; segment++)
                {
                    distance += NativeWorldScriptedPathMath.SegmentLength(definition.Points[segment], definition.Points[segment + 1]);
                }
                long delta = distance - initial[index];
                if (delta < 0 || advanced is not null && delta != advanced)
                {
                    throw new InvalidDataException("Native scripted path lost its original linked-node spacing.");
                }
                advanced = delta;
            }
            if (Pause != Math.Max(0, 30 - Callback) || Callback < 30 && (Speed != 0 || advanced != 0))
            {
                throw new InvalidDataException("Native scripted path pause differs from its initial thirty-callback countdown.");
            }
            int movement = Callback < 30 ? 0 : NativeMovementMultiplier * Math.Min(Speed, MaximumSpeed) >> 1;
            var oldMiddle = ResolveNode(definition, new NativeWorldScriptedPathNode(0, checked((int)advanced!.Value + initial[CentralNode] - movement), default), 1, out bool oldComplete);
            if (oldComplete || EntityPosition != oldMiddle.Position)
            {
                throw new InvalidDataException("Native scripted path entity differs from its original pre-update middle node.");
            }
        }
        if (CameraCounter != (EndpointRequested ? 0 : Callback + 1) || CameraCounter > short.MaxValue || EndpointRequested && (EndpointCallback is null or < 30 || Callback != EndpointCallback + Fade / 16) || !EndpointRequested && EndpointCallback is not null || Camera is null || CameraReceipt is null || CameraReceipt.Version != 1 || CameraReceipt.Callback != (EndpointCallback ?? Callback) || CameraReceipt.Counter != (EndpointCallback ?? CameraCounter - 1) || CameraReceipt.Direction != 1 || CameraReceipt.Nodes.IsDefault || CameraReceipt.Nodes.Length != NodeCount || CameraReceipt.Nodes.Any(node => node is null || node.Segment < 0 || node.Segment >= definition.Points.Length - 1 || node.Distance < 0) || Camera != EvaluateCamera(definition, CameraReceipt.Nodes, CameraReceipt.Direction, CameraReceipt.Counter) || Callback == 0 && (Pause != 30 || Speed != 0 || Presentation.Any(pose => pose.Angles is not null || pose.Orientation is not null || pose.Projection is not null) || EndpointRequested))
        {
            throw new InvalidDataException("Native scripted path camera or initial allocation phase is inconsistent.");
        }
        foreach (NativeWorldScriptedPathNode node in CameraReceipt.Nodes)
        {
            if (node is null || node.Segment < 0 || node.Segment >= definition.Points.Length - 1 || node.Distance < 0 || ResolveNode(definition, node, CameraReceipt.Direction, out bool complete) != node || complete)
            {
                throw new InvalidDataException("Native scripted camera receipt lost its original pre-movement node phase.");
            }
        }
        int[] initialDistances = InitialDistances(definition, NodeCount, 1);
        int[] reverseDistances = InitialDistances(definition, NodeCount, -1);
        long? cameraAdvance = null;
        for (int index = 0; index < CameraReceipt.Nodes.Length; index++)
        {
            long distance = CameraReceipt.Nodes[index].Distance;
            for (int segment = 0; segment < CameraReceipt.Nodes[index].Segment; segment++)
            {
                distance += NativeWorldScriptedPathMath.SegmentLength(definition.Points[segment], definition.Points[segment + 1]);
            }
            long advance = distance - initialDistances[index];
            if (advance < 0 || cameraAdvance is not null && advance != cameraAdvance)
            {
                throw new InvalidDataException("Native scripted camera source nodes lost their original linked-node spacing.");
            }
            cameraAdvance = advance;
            if (!EndpointRequested)
            {
                int movement = Callback < 30 ? 0 : NativeMovementMultiplier * Math.Min(Speed, MaximumSpeed) >> 1;
                var expected = ResolveNode(definition, new NativeWorldScriptedPathNode(0, checked((int)distance + movement), default), 1, out bool complete);
                if (complete || expected != Nodes[index])
                {
                    throw new InvalidDataException("Native scripted camera source differs from this callback's actual pre-movement nodes.");
                }
            }
            else
            {
                int reverseDistance = reverseDistances[index];
                var reset = ResolveNode(definition, new NativeWorldScriptedPathNode(0, reverseDistance, default), -1, out bool complete);
                if (complete || reset != Nodes[index])
                {
                    throw new InvalidDataException("Native scripted camera departure lost the original endpoint node reset.");
                }
            }
        }
        if (EntityPosition != CameraReceipt.Nodes[CentralNode].Position)
        {
            throw new InvalidDataException("Native scripted camera source differs from the callback's published old middle node.");
        }
        for (int index = 0; index < NodeCount; index++)
        {
            NativeWorldPathNodePresentation pose = Presentation[index];
            if (pose.Angles is null != (pose.Orientation is null) || pose.Angles is not null && (pose.Angles.Pitch is < -1024 or > 1024 || pose.Angles.Yaw is < 0 or > 4096 || pose.Angles.Roll != 0) || NodeLink(index) == 0 && (pose.Angles is not null || pose.Orientation is not null))
            {
                throw new InvalidDataException("Native linked orientation requires its transformed-word authority, without inferred angles.");
            }
            if (pose.Projection is NativeWorldPathNodeProjection projection)
            {
                projection.Validate(projection.Source, Camera, EntityPosition);
                if (!EndpointRequested && projection.Source != Nodes[index].Position || projection.Visible && pose.Angles is not null && pose.Orientation?.Middle != projection)
                {
                    throw new InvalidDataException("Native linked model projection differs from its current drawn node authority.");
                }
            }
            else if (pose.Angles is not null)
            {
                throw new InvalidDataException("Known linked model angles require actual current visibility/projection authority.");
            }
            if (pose.Orientation is NativeWorldPathOrientationReceipt receipt)
            {
                if (receipt.Version != 1 || receipt.Callback < 1 || receipt.Callback > (EndpointCallback ?? Callback) || receipt.Middle is null || receipt.Linked is null || !receipt.Middle.Visible || receipt.Middle.Camera != receipt.Linked.Camera || pose.Angles != NativeWorldScriptedPathMath.LinkedOrientation(receipt.Middle.Words, receipt.Linked.Words))
                {
                    throw new InvalidDataException("Native linked orientation differs from its retained5470E0 transformed-word receipt.");
                }
                receipt.Middle.Validate(receipt.Middle.Source, receipt.Middle.Camera, receipt.Middle.Origin);
                receipt.Linked.Validate(receipt.Linked.Source, receipt.Middle.Camera, receipt.Middle.Origin);
            }
        }
    }

    private void ValidateEndpointBodyWitness(NativeWorldScriptedPathDefinition definition)
    {
        NativeWorldScriptedPathState initial = Create(definition, Packet, StopAtEndpoint);
        ImmutableArray<NativeWorldScriptedPathNode> nodes = initial.Nodes;
        NativeWorldPathPosition committedBody = initial.EntityPosition;
        short pause = initial.Pause!.Value;
        int speed = initial.Speed;
        for (long callback = 1; callback <= EndpointCallback!.Value; callback++)
        {
            NativeWorldPathPosition currentBody = nodes[CentralNode].Position;
            NativeWorldRailAdvance advance = AdvanceRail(definition, nodes, 1, pause, speed, MaximumSpeed, NativeMovementMultiplier);
            if (advance.Endpoint)
            {
                if (callback != EndpointCallback || committedBody != EndpointPriorEntityPosition || currentBody != EntityPosition || !nodes.SequenceEqual(CameraReceipt.Nodes))
                {
                    throw new InvalidDataException("Native scripted saved body witness differs from the actual forward rail callbacks before549E80.");
                }
                return;
            }
            committedBody = currentBody;
            nodes = advance.Nodes;
            pause = advance.Pause;
            speed = advance.Speed;
        }
        throw new InvalidDataException("Native scripted endpoint callback precedes the actual forward rail completion.");
    }

    /// <param name="projectNode">Actual native5470E0 transformed WORDs and visibility. Null never establishes raw-position angle authority.</param>
    public NativeWorldScriptedPathState Step(NativeWorldScriptedPathDefinition definition, Func<NativeWorldPathPosition, NativeWorldPathCamera, NativeWorldPathPosition, NativeWorldPathNodeProjection?> projectNode)
    {
        Validate(definition);
        ArgumentNullException.ThrowIfNull(projectNode);
        if (EndpointRequested)
        {
            return FieldTransitionReady ? this : this with { Callback = checked(Callback + 1), Fade = Fade + 16 };
        }
        // 53FAC0 evaluates552AD0 against old nodes/current counter before545480.
        NativeWorldPathCamera callbackCamera = EvaluateCamera(definition, Nodes, Direction, CameraCounter);
        var cameraReceipt = new NativeWorldPathCameraReceipt(1, Callback + 1, CameraCounter, Direction, Nodes);
        NativeWorldRailAdvance advance = AdvanceRail(definition, Nodes, Direction, Pause!.Value, Speed, MaximumSpeed, NativeMovementMultiplier);
        short pause = advance.Pause;
        int speed = advance.Speed;
        NativeWorldScriptedPathNode[] nodes = advance.Nodes.ToArray();
        bool endpoint = advance.Endpoint;
        //5499D0 visits every node in order, projecting its linked neighbor only when visible.
        NativeWorldPathPosition callbackOrigin = Nodes[CentralNode].Position;
        NativeWorldPathNodeProjection?[] projections = new NativeWorldPathNodeProjection?[NodeCount];
        NativeWorldPathNodeProjection? Project(int index)
        {
            if (projections[index] is null)
            {
                projections[index] = projectNode(nodes[index].Position, callbackCamera, callbackOrigin);
                projections[index]?.Validate(nodes[index].Position, callbackCamera, callbackOrigin);
            }
            return projections[index];
        }
        var presentation = Presentation.ToArray();
        for (int index = 0; index < NodeCount; index++)
        {
            NativeWorldPathNodeProjection? current = Project(index);
            NativeWorldPathNodePresentation retained = presentation[index];
            NativeWorldPathAngles? angles = retained.Angles;
            NativeWorldPathOrientationReceipt? orientation = retained.Orientation;
            if (current is null)
            {
                angles = null;
                orientation = null;
            }
            else if (current.Visible && NodeLink(index) != 0)
            {
                NativeWorldPathNodeProjection? linked = Project(index + NodeLink(index));
                angles = linked is null ? null : NativeWorldScriptedPathMath.LinkedOrientation(current.Words, linked.Words);
                orientation = linked is null ? null : new NativeWorldPathOrientationReceipt(1, Callback + 1, current, linked);
            }
            presentation[index] = new NativeWorldPathNodePresentation(angles, orientation, current);
        }
        sbyte direction = 1;
        short? retainedPause = pause;
        if (endpoint)
        {
            direction = -1;
            retainedPause = StopAtEndpoint is null ? null : StopAtEndpoint.Value ? (short)-1 : (short)30;
            speed = 0;
            nodes = InitialDistances(definition, NodeCount, -1).Select(distance => ResolveNode(definition, new NativeWorldScriptedPathNode(0, distance, default), -1, out _)).ToArray();
        }
        var next = this with
        {
            Callback = checked(Callback + 1), Direction = direction, Pause = retainedPause, Speed = speed, CameraCounter = endpoint ? (ushort)0 : checked((ushort)(CameraCounter + 1)), Nodes = nodes.ToImmutableArray(),
            EntityPosition = callbackOrigin, Camera = callbackCamera, CameraReceipt = cameraReceipt, Presentation = presentation.ToImmutableArray(), EndpointRequested = endpoint, EndpointCallback = endpoint ? Callback + 1 : null, EndpointPriorEntityPosition = endpoint ? EntityPosition : null
        };
        next.Validate(definition);
        return next;
    }

    /// <summary>The shared545480/545B20 phase, before node projection and contact handling.</summary>
    public static NativeWorldRailAdvance AdvanceRail(NativeWorldScriptedPathDefinition definition, ImmutableArray<NativeWorldScriptedPathNode> source, sbyte direction, short pause, int speed, int maximumSpeed, int multiplier, byte? status = null, byte stopCounter = 0, int? selectedSpeed = null)
    {
        definition.Validate();
        if (direction is not (1 or -1) || pause < -1 || speed < 0 || source.IsDefaultOrEmpty || multiplier != 2 || selectedSpeed is < 0 || selectedSpeed > maximumSpeed)
        {
            throw new InvalidDataException("Native rail advance requires its current physical bank and callback multiplier.");
        }
        if (pause > 0)
        {
            pause--;
        }
        var nodes = source.ToArray();
        if (pause == 0)
        {
            speed = selectedSpeed ?? unchecked((speed == 0 ? 16 : speed) + 4);
            int leading = direction == 1 ? source[0].Segment : definition.Points.Length - source[^1].Segment - 1;
            int remaining = direction == 1 ? definition.Points.Length - leading : leading;
            ImmutableArray<byte> stops = direction == 1 ? definition.ForwardStops : definition.ReverseStops;
            foreach (byte stop in stops)
            {
                int distance = direction == 1 ? stop - leading : leading - stop;
                if (distance > 0)
                {
                    remaining = Math.Min(remaining, distance);
                }
            }
            if (selectedSpeed is null && remaining <= 3)
            {
                speed = Math.Min(speed, Math.Max(10, maximumSpeed + 4 * (5 * remaining - 15)));
            }
            //545480 writes the capped speed back into physical bank DWORD20 in the node loop.
            speed = Math.Min(speed, maximumSpeed);
            int movement = multiplier * speed >> 1;
            for (int index = 0; index < nodes.Length; index++)
            {
                nodes[index] = nodes[index] with { Distance = unchecked(nodes[index].Distance + movement) };
            }
        }
        bool endpoint = false;
        if (pause == 0)
        {
            status = 0;
        }
        for (int index = 0; index < nodes.Length; index++)
        {
            bool leading = index == (direction == 1 ? 0 : nodes.Length - 1);
            bool stopped = false;
            void EnterSegment(short segment)
            {
                if (pause == 0 && leading)
                {
                    foreach (byte stop in direction == 1 ? definition.ForwardStops : definition.ReverseStops)
                    {
                        int originalSegment = direction == 1 ? segment : definition.Points.Length - segment - 1;
                        if (originalSegment == stop)
                        {
                            pause = 30;
                            speed = 0;
                            stopCounter = unchecked((byte)(stopCounter + 1));
                            stopped = true;
                        }
                    }
                }
            }
            nodes[index] = ResolveNode(definition, nodes[index], direction, out bool complete, EnterSegment);
            if (complete)
            {
                stopCounter = unchecked((byte)(stopCounter + 1));
                status = 2;
                endpoint = true;
            }
            else if (stopped)
            {
                status = 1;
            }
        }
        return new NativeWorldRailAdvance(pause, speed, nodes.ToImmutableArray(), endpoint) { Status = status, StopCounter = stopCounter };
    }

    public static NativeWorldPathCamera EvaluateCamera(NativeWorldScriptedPathDefinition definition, ImmutableArray<NativeWorldScriptedPathNode> nodes, sbyte direction, int counter)
    {
        return EvaluateCamera(definition, nodes, direction, counter, -1, out _);
    }

    public static NativeWorldPathCamera EvaluateCamera(NativeWorldScriptedPathDefinition definition, ImmutableArray<NativeWorldScriptedPathNode> nodes, sbyte direction, int counter, int station, out ushort nextCounter)
    {
        definition.Validate();
        if (direction is not (1 or -1) || counter is < 0 or > short.MaxValue || nodes.IsDefault || nodes.Length is not (3 or 5) || nodes.Any(node => node is null || node.Segment < 0 || node.Segment >= definition.Points.Length - 1 || node.Distance < 0))
        {
            throw new InvalidDataException("Native scripted camera requires its actual resolved source nodes, signed direction and finite WORD counter.");
        }
        foreach (NativeWorldScriptedPathNode node in nodes)
        {
            if (ResolveNode(definition, node, direction, out bool complete) != node || complete)
            {
                throw new InvalidDataException("Native scripted camera source differs from its actual signed rail interpolation.");
            }
        }
        if (station is < -1 or > 2)
        {
            throw new InvalidDataException("Native camera station selector is outside548BF0's signed result.");
        }
        int index = 0;
        int weight = 4096;
        nextCounter = checked((ushort)(counter + 1));
        if (station is 0 or 2)
        {
            //54BCB0 selects the first -2 record while the train is at a station;552AD0 increments its rewritten WORD afterwards.
            while (definition.CameraRecords[index].Deadline is not (-1 or -2))
            {
                index++;
            }
            if (definition.CameraRecords[index].Deadline == -2)
            {
                nextCounter = checked((ushort)definition.CameraRecords[index + 1].Deadline);
            }
        }
        else
        {
            int previous = 0;
            while (definition.CameraRecords[index].Deadline != -1 && (definition.CameraRecords[index].Deadline == -2 || counter > definition.CameraRecords[index].Deadline))
            {
                previous = definition.CameraRecords[index].Deadline;
                index++;
            }
            if (definition.CameraRecords[index].Deadline != -1)
            {
                weight = Math.Clamp(checked((counter - previous) << 12) / (definition.CameraRecords[index].Deadline - previous), 0, 4096);
            }
        }
        NativeWorldPathCameraRecord record = definition.CameraRecords[index];
        NativeWorldPathPosition eye = CameraPosition(nodes, direction, record.EyeMode, record.EyeOffset);
        NativeWorldPathPosition targetPosition = CameraPosition(nodes, direction, record.TargetMode, record.TargetOffset);
        if (record.Interpolation == 2)
        {
            return new NativeWorldPathCamera(eye, targetPosition);
        }
        NativeWorldPathCameraRecord target = definition.CameraRecords[index + 1];
        return new NativeWorldPathCamera(Blend(eye, CameraPosition(nodes, direction, target.EyeMode, target.EyeOffset), weight), Blend(targetPosition, CameraPosition(nodes, direction, target.TargetMode, target.TargetOffset), weight));
    }

    //54C160/54C3E0 modes7..9 add XYZ to the selected boat node;12..16 add X/Z but use absolute signed Y.
    private static NativeWorldPathPosition CameraPosition(ImmutableArray<NativeWorldScriptedPathNode> nodes, sbyte direction, byte mode, NativeWorldPathPosition offset)
    {
        int firstMode = nodes.Length == 3 ? 7 : 12;
        int index = mode - firstMode;
        if (index < 0 || index >= nodes.Length)
        {
            throw new NotSupportedException("Native scripted camera selector does not match this actual mode1/2 node allocation.");
        }
        NativeWorldPathPosition source = nodes[direction == 1 ? index : nodes.Length - index - 1].Position;
        return new NativeWorldPathPosition(unchecked(source.X + offset.X), nodes.Length == 5 ? offset.Y : unchecked(source.Y + offset.Y), unchecked(source.Z + offset.Z));
    }

    public static int[] InitialDistances(NativeWorldScriptedPathDefinition definition, int nodeCount, int direction)
    {
        int front = definition.FrontLength - 128 + 16;
        int rear = definition.RearLength - 128;
        int[] distances = nodeCount == 3 ? [0, front, front + rear] : [0, front, front + rear, front + rear + rear + 16, front + rear + rear + 16 + front];
        if (direction == 1)
        {
            Array.Reverse(distances);
        }
        return distances;
    }

    private static NativeWorldPathPosition Blend(NativeWorldPathPosition first, NativeWorldPathPosition second, int weight) => new(unchecked(first.X + weight * (second.X - first.X) / 4096), unchecked(first.Y + weight * (second.Y - first.Y) / 4096), unchecked(first.Z + weight * (second.Z - first.Z) / 4096));

    public static NativeWorldScriptedPathNode ResolveNode(NativeWorldScriptedPathDefinition definition, NativeWorldScriptedPathNode original, int direction, out bool completed)
    {
        return ResolveNode(definition, original, direction, out completed, null);
    }

    private static NativeWorldScriptedPathNode ResolveNode(NativeWorldScriptedPathDefinition definition, NativeWorldScriptedPathNode original, int direction, out bool completed, Action<short>? enterSegment)
    {
        int segment = original.Segment;
        int distance = original.Distance;
        completed = false;
        while (segment < definition.Points.Length - 1)
        {
            int source = direction == 1 ? segment : definition.Points.Length - segment - 1;
            NativeWorldRailPoint first = definition.Points[source];
            NativeWorldRailPoint second = definition.Points[source + direction];
            int length = NativeWorldScriptedPathMath.SegmentLength(first, second);
            if (distance < length)
            {
                int ratio = distance >> 3;
                int denominator = length >> 3;
                NativeWorldPathPosition position = new(unchecked(first.X + ratio * (second.X - first.X) / denominator), unchecked(first.Y + ratio * (second.Y - first.Y) / denominator), unchecked(first.Z + ratio * (second.Z - first.Z) / denominator));
                return new NativeWorldScriptedPathNode((short)segment, distance, position);
            }
            distance -= length;
            segment++;
            enterSegment?.Invoke((short)segment);
        }
        completed = true;
        // 545B20 returns2 before the coordinate stores on this branch.
        return original with { Segment = (short)segment, Distance = distance };
    }
}

public sealed record NativeWorldRailAdvance(short Pause, int Speed, ImmutableArray<NativeWorldScriptedPathNode> Nodes, bool Endpoint)
{
    public byte? Status { get; init; }
    public byte StopCounter { get; init; }
}

public static class NativeWorldScriptedPathMath
{
    private const double NativeAtanScale = 651.8986206054688;

    public static bool IsWordPosition(NativeWorldPathPosition position) => position.X is >= short.MinValue and <= short.MaxValue && position.Y is >= short.MinValue and <= short.MaxValue && position.Z is >= short.MinValue and <= short.MaxValue;

    public static int SegmentLength(NativeWorldRailPoint first, NativeWorldRailPoint second)
    {
        int dx = unchecked((short)(second.X - first.X));
        int dy = unchecked((short)(second.Y - first.Y));
        int dz = unchecked((short)(second.Z - first.Z));
        int squared = unchecked(dx * dx + dy * dy + dz * dz);
        if (squared < 0)
        {
            throw new NotSupportedException("Native scripted rail signed square-root overflow is outside the finite contract.");
        }
        return (int)Math.Sqrt(squared);
    }

    public static NativeWorldPathAngles LinkedOrientation(NativeWorldPathWords middle, NativeWorldPathWords linked)
    {
        int dx = unchecked((short)(linked.X - middle.X));
        int dy = unchecked((short)(linked.Y - middle.Y));
        int dz = unchecked((short)(linked.Z - middle.Z));
        int length = (int)Math.Sqrt(dx * dx + dz * dz);
        short pitch = unchecked((short)NativeAngle(-dy, length));
        short yaw = unchecked((short)(NativeAngle(-dz, dx) + 2048));
        return new NativeWorldPathAngles(pitch, yaw, 0);
    }

    // 56D160's magic-double conversion uses the existing nearest-even PC FPU contract.
    public static int NativeAngle(int y, int x) => checked((int)Math.Round(Math.Atan2(y, x) * NativeAtanScale, MidpointRounding.ToEven));
}
