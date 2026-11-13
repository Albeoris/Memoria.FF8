using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7HighwayRoadSegment(int Z, ushort Width, byte Surface, byte Stripe, bool BeyondBrokenRoad)
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Yaw { get; init; }
    public int Pitch { get; init; }
    public int Roll { get; init; }
}

internal sealed class Ff7HighwayRoad
{
    private readonly Ff7HighwayRoadSegment[] segments;
    private readonly Ff7BattleCameraMath math;

    internal Ff7HighwayRoad(PcHighwayCourse course, Ff7BattleCameraMath math)
    {
        this.math = math;
        List<Ff7HighwayRoadSegment> road = [];
        int x = 0;
        int y = 0;
        int z = 0;
        int yaw = 0;
        int pitchFrom = 0;
        int rollFrom = 0;
        bool beyondBrokenRoad = false;
        foreach (PcHighwayTrackCommand command in course.Track)
        {
            int pitchStep = ((command.Pitch - pitchFrom) << 16) / command.Length;
            int rollStep = ((command.Roll - rollFrom) << 16) / command.Length;
            int turnStep = 0;
            if (command.Turn != 0)
            {
                int radius = (int)(Math.Sqrt(command.Radius * command.Radius - 0.25) * 4096);
                turnStep = math.Angle(0x800, radius) * (command.Turn == 1 ? 2 : -2);
            }
            ReadOnlySpan<byte> pattern = course.RoadPatterns[command.Pattern].Span;
            for (int step = 1; step <= command.Length; step++)
            {
                yaw = (yaw + turnStep) & 0xFFF;
                int pitch = pitchFrom + ((pitchStep * step) >> 16);
                int roll = rollFrom + ((rollStep * step) >> 16);
                int pitchCos = math.Cosine(pitch);
                x += (math.Sine(yaw) * pitchCos) >> 16;
                y += math.Sine(-pitch) >> 4;
                z += (math.Cosine(yaw) * pitchCos) >> 16;
                byte surface = pattern[step % pattern.Length];
                road.Add(new(z, command.Width, surface, (byte)(road.Count & 7), beyondBrokenRoad) { X = x, Y = y, Yaw = yaw, Pitch = pitch, Roll = roll });
                beyondBrokenRoad |= surface == 4;
            }
            pitchFrom = command.Pitch;
            rollFrom = command.Roll;
        }
        segments = road.ToArray();
    }

    internal IReadOnlyList<Ff7HighwayRoadSegment> Window(int distance)
    {
        return Array.AsReadOnly(segments.AsSpan(distance >> 8, 80).ToArray());
    }

    internal Ff7HighwayRoadSegment Segment(int position)
    {
        return segments[Math.Clamp(position >> 8, 0, segments.Length - 1)];
    }

    internal Vector3 Sample(int position, int lateralOffset)
    {
        Ff7HighwayRoadSegment previous = Segment(position);
        Ff7HighwayRoadSegment next = Segment(position + 256);
        Vector3 left = At(previous, lateralOffset);
        Vector3 right = At(next, lateralOffset);
        int fraction = position & 255;
        return new(left.X + (((int)(right.X - left.X) * fraction) >> 8), left.Y + (((int)(right.Y - left.Y) * fraction) >> 8), left.Z + (((int)(right.Z - left.Z) * fraction) >> 8));
    }

    private Vector3 At(Ff7HighwayRoadSegment segment, int offset)
    {
        int horizontal = (math.Cosine(segment.Roll) * offset) >> 12;
        return new(segment.X + ((math.Cosine(segment.Yaw) * horizontal) >> 12), segment.Y + ((math.Sine(segment.Roll) * offset) >> 12), segment.Z - ((math.Sine(segment.Yaw) * horizontal) >> 12));
    }
}
