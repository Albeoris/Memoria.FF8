using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeWorldPathPosition(int X, int Y, int Z);
public sealed record NativeWorldRailPoint(int X, int Y, int Z, int Opaque);
public sealed record NativeWorldPathCameraRecord(short Deadline, byte EyeMode, byte TargetMode, byte Interpolation, NativeWorldPathPosition EyeOffset, NativeWorldPathPosition TargetOffset);
public sealed record NativeWorldPathCamera(NativeWorldPathPosition Eye, NativeWorldPathPosition Target);
public sealed record NativeWorldPathAngles(short Pitch, short Yaw, short Roll);

/// <summary>Original rail member, selected chunk and finite camera program; no field destination is substituted here.</summary>
public sealed record NativeWorldScriptedPathDefinition(int Version, string ExecutableHash, string RailHash, string ChunkHash, string CameraHash, int TrackIndex, short FrontLength, short RearLength, ImmutableArray<NativeWorldRailPoint> Points, ImmutableArray<NativeWorldPathCameraRecord> CameraRecords)
{
    public void Validate()
    {
        if (Version != 1 || ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || !IsHash(RailHash) || !IsHash(ChunkHash) || !IsHash(CameraHash) || TrackIndex is < 0 or >= 14 || FrontLength < 128 || RearLength < 128 || Points.IsDefault || Points.Length is < 2 or > 127 || Points.Any(point => point is null) || CameraRecords.IsDefaultOrEmpty || CameraRecords.Any(record => record is null))
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
        short previous = 0;
        for (int index = 0; index < CameraRecords.Length; index++)
        {
            NativeWorldPathCameraRecord record = CameraRecords[index];
            bool final = index == CameraRecords.Length - 1;
            if (record.EyeMode != 7 || record.TargetMode != 7 || record.Interpolation != (final ? 2 : 1) || final && record.Deadline != -1 || !final && record.Deadline <= previous || !NativeWorldScriptedPathMath.IsWordPosition(record.EyeOffset) || !NativeWorldScriptedPathMath.IsWordPosition(record.TargetOffset))
            {
                throw new NotSupportedException("Native scripted camera requires ordered mode7 relative linear records and a fixed terminal record.");
            }
            previous = record.Deadline;
        }
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

public sealed record NativeWorldScriptedPathNode(short Segment, int Distance, NativeWorldPathPosition Position);

/// <summary>Dormant mode1 three-node driver. World entry must supply an actual packet and original visibility authority.</summary>
public sealed record NativeWorldScriptedPathState
{
    public const string LoadRule = "ff8-en-scripted-mode1-545330-545480-552ad0-v1";
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
    public required byte ShapeId { get; init; }
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
    public required NativeWorldPathAngles? ModelAngles { get; init; }
    [JsonRequired]
    public required bool EndpointRequested { get; init; }
    [JsonRequired]
    public required long? EndpointCallback { get; init; }
    [JsonRequired]
    public required int Fade { get; init; }

    public bool ControlsDisabled => true;
    public bool FieldTransitionReady => EndpointRequested && Fade == 256;
    public byte? CompletedFieldRow => FieldTransitionReady ? unchecked((byte)Packet.Extra) : null;

    public static NativeWorldScriptedPathState Create(NativeWorldScriptedPathDefinition definition, WorldTransition packet, bool? stopAtEndpoint)
    {
        definition.Validate();
        byte entity = unchecked((byte)packet.Mode);
        byte entry = unchecked((byte)packet.EntryIndex);
        if (entity is not (65 or 66) || entry >= 14 || entry != definition.TrackIndex || unchecked((sbyte)packet.Extra) < 0)
        {
            throw new NotSupportedException("Native scripted boat requires an actual forward mode1 packet and its matching original track.");
        }
        int front = definition.FrontLength - 128 + 16;
        int rear = definition.RearLength - 128;
        ImmutableArray<NativeWorldScriptedPathNode> nodes = new[] { front + rear, front, 0 }.Select(distance => ResolveNode(definition, new NativeWorldScriptedPathNode(0, distance, default), 1, out _)).ToImmutableArray();
        var state = new NativeWorldScriptedPathState
        {
            Version = 1, Rule = LoadRule, Packet = packet, ChunkHash = definition.ChunkHash, CameraHash = definition.CameraHash, EntityType = entity, ShapeId = entity == 65 ? (byte)77 : (byte)78,
            Callback = 0, Direction = 1, Pause = 30, StopAtEndpoint = stopAtEndpoint, Speed = 0, CameraCounter = 1, Nodes = nodes, EntityPosition = nodes[1].Position,
            Camera = EvaluateCamera(definition, nodes, 1, 0), ModelAngles = null, EndpointRequested = false, EndpointCallback = null, Fade = 0
        };
        state.Validate(definition);
        return state;
    }

    public void Validate(NativeWorldScriptedPathDefinition definition)
    {
        definition.Validate();
        if (Version != 1 || Rule != LoadRule || Packet is null || ChunkHash != definition.ChunkHash || CameraHash != definition.CameraHash || unchecked((byte)Packet.EntryIndex) != definition.TrackIndex || unchecked((byte)Packet.Mode) != EntityType || EntityType is not (65 or 66) || ShapeId != (EntityType == 65 ? 77 : 78) || Callback < 0 || Direction != (EndpointRequested ? -1 : 1) || Nodes.IsDefault || Nodes.Length != 3 || Nodes.Any(node => node is null || node.Segment < 0 || node.Segment >= definition.Points.Length - 1 || node.Distance < 0) || Speed < 0 || Speed > 16 + 4 * Callback || Fade is < 0 or > 256 || Fade % 16 != 0 || !EndpointRequested && (Pause is null or < 0 or > 30 || Fade != 0) || EndpointRequested && Pause != (StopAtEndpoint is null ? null : StopAtEndpoint.Value ? -1 : 30))
        {
            throw new InvalidDataException("Native scripted path snapshot has inconsistent packet, resource or driver phase.");
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
            int front = definition.FrontLength - 128 + 16;
            int rear = definition.RearLength - 128;
            int[] initial = [front + rear, front, 0];
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
                    throw new InvalidDataException("Native scripted path lost its original three-node spacing.");
                }
                advanced = delta;
            }
            if (Pause != Math.Max(0, 30 - Callback) || Callback < 30 && (Speed != 0 || advanced != 0))
            {
                throw new InvalidDataException("Native scripted path pause differs from its initial thirty-callback countdown.");
            }
            int movement = Callback < 30 ? 0 : Math.Min(Speed, 80);
            var oldMiddle = ResolveNode(definition, new NativeWorldScriptedPathNode(0, checked((int)advanced!.Value + front - movement), default), 1, out bool oldComplete);
            if (oldComplete || EntityPosition != oldMiddle.Position)
            {
                throw new InvalidDataException("Native scripted path entity differs from its original pre-update middle node.");
            }
        }
        if (CameraCounter != (EndpointRequested ? EndpointCallback + 1 : Callback + 1) || CameraCounter > short.MaxValue || EndpointRequested && (EndpointCallback is null or < 30 || Callback != EndpointCallback + Fade / 16) || !EndpointRequested && EndpointCallback is not null || Camera is null || Camera != EvaluateCamera(definition, Nodes, Direction, CameraCounter - 1) || ModelAngles is not null && (ModelAngles.Pitch is < -1024 or > 1024 || ModelAngles.Yaw is < 0 or > 4096 || ModelAngles.Roll != 0) || Callback == 0 && (Pause != 30 || Speed != 0 || ModelAngles is not null || EndpointRequested))
        {
            throw new InvalidDataException("Native scripted path camera or initial allocation phase is inconsistent.");
        }
    }

    /// <param name="linkedNodeVisible">Actual native5470E0 result for the linked middle node. Unknown retains unknown/previous angles.</param>
    public NativeWorldScriptedPathState Step(NativeWorldScriptedPathDefinition definition, Func<NativeWorldPathPosition, NativeWorldPathCamera, bool?> linkedNodeVisible)
    {
        Validate(definition);
        ArgumentNullException.ThrowIfNull(linkedNodeVisible);
        if (EndpointRequested)
        {
            return FieldTransitionReady ? this : this with { Callback = checked(Callback + 1), Fade = Fade + 16 };
        }
        short pause = Pause!.Value;
        if (pause > 0)
        {
            pause--;
        }
        int speed = Speed;
        var nodes = Nodes.ToArray();
        if (pause == 0)
        {
            speed = (speed == 0 ? 16 : speed) + 4;
            int remaining = definition.Points.Length - nodes[0].Segment;
            if (remaining <= 3)
            {
                speed = Math.Min(speed, Math.Max(10, 80 + 4 * (5 * remaining - 15)));
            }
            int advance = Math.Min(speed, 80);
            for (int index = 0; index < nodes.Length; index++)
            {
                nodes[index] = nodes[index] with { Distance = checked(nodes[index].Distance + advance) };
            }
        }
        bool endpoint = false;
        for (int index = 0; index < nodes.Length; index++)
        {
            nodes[index] = ResolveNode(definition, nodes[index], 1, out bool completed);
            endpoint |= completed;
        }
        // 5499D0 observes the old camera matrix and the newly stepped nodes.
        bool? visible = linkedNodeVisible(nodes[1].Position, Camera);
        NativeWorldPathAngles? angles = visible == true ? NativeWorldScriptedPathMath.LinkedOrientation(nodes[1].Position, nodes[2].Position) : ModelAngles;
        if (visible is null)
        {
            angles = angles == NativeWorldScriptedPathMath.LinkedOrientation(nodes[1].Position, nodes[2].Position) ? angles : null;
        }
        sbyte direction = 1;
        short? retainedPause = pause;
        if (endpoint)
        {
            direction = -1;
            retainedPause = StopAtEndpoint is null ? null : StopAtEndpoint.Value ? (short)-1 : (short)30;
            speed = 0;
            int front = definition.FrontLength - 128 + 16;
            int rear = definition.RearLength - 128;
            nodes = new[] { 0, front, front + rear }.Select(distance => ResolveNode(definition, new NativeWorldScriptedPathNode(0, distance, default), -1, out _)).ToArray();
        }
        var next = this with
        {
            Callback = checked(Callback + 1), Direction = direction, Pause = retainedPause, Speed = speed, CameraCounter = checked((ushort)(CameraCounter + 1)), Nodes = nodes.ToImmutableArray(),
            EntityPosition = Nodes[1].Position, Camera = EvaluateCamera(definition, nodes.ToImmutableArray(), direction, CameraCounter), ModelAngles = angles, EndpointRequested = endpoint, EndpointCallback = endpoint ? Callback + 1 : null
        };
        next.Validate(definition);
        return next;
    }

    public static NativeWorldPathCamera EvaluateCamera(NativeWorldScriptedPathDefinition definition, ImmutableArray<NativeWorldScriptedPathNode> nodes, sbyte direction, int counter)
    {
        int index = 0;
        int previous = 0;
        while (definition.CameraRecords[index].Deadline != -1 && counter > definition.CameraRecords[index].Deadline)
        {
            previous = definition.CameraRecords[index].Deadline;
            index++;
        }
        NativeWorldPathCameraRecord record = definition.CameraRecords[index];
        NativeWorldPathPosition anchor = nodes[direction == 1 ? 0 : 2].Position;
        if (record.Interpolation == 2)
        {
            return new NativeWorldPathCamera(Add(anchor, record.EyeOffset), Add(anchor, record.TargetOffset));
        }
        int weight = Math.Clamp(checked((counter - previous) << 12) / (record.Deadline - previous), 0, 4096);
        NativeWorldPathCameraRecord target = definition.CameraRecords[index + 1];
        return new NativeWorldPathCamera(Add(anchor, Blend(record.EyeOffset, target.EyeOffset, weight)), Add(anchor, Blend(record.TargetOffset, target.TargetOffset, weight)));
    }

    private static NativeWorldPathPosition Add(NativeWorldPathPosition first, NativeWorldPathPosition second) => new(unchecked(first.X + second.X), unchecked(first.Y + second.Y), unchecked(first.Z + second.Z));
    private static NativeWorldPathPosition Blend(NativeWorldPathPosition first, NativeWorldPathPosition second, int weight) => new(unchecked(first.X + weight * (second.X - first.X) / 4096), unchecked(first.Y + weight * (second.Y - first.Y) / 4096), unchecked(first.Z + weight * (second.Z - first.Z) / 4096));

    private static NativeWorldScriptedPathNode ResolveNode(NativeWorldScriptedPathDefinition definition, NativeWorldScriptedPathNode original, int direction, out bool completed)
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
        }
        completed = true;
        // 545B20 returns2 before the coordinate stores on this branch.
        return original with { Segment = (short)segment, Distance = distance };
    }
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

    public static NativeWorldPathAngles LinkedOrientation(NativeWorldPathPosition middle, NativeWorldPathPosition linked)
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
