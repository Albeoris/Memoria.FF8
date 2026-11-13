using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7HighwayGauge(ushort Value, ushort Maximum)
{
    public byte PortraitState { get; init; } = 1;
    public byte HitFlash { get; init; }
}

internal sealed class Ff7HighwayStart
{
    private const int RoadRingSize = 80;
    private const int RoadStep = 256;
    private const int EventLookAhead = 45;

    private Ff7HighwayStart(FieldHostRequest request, bool arcade, ushort highScore, PcHighwayCourse course, Ff7HighwayRoadSegment[] road)
    {
        Request = request;
        Arcade = arcade;
        HighScore = highScore;
        Course = course;
        Road = Array.AsReadOnly(road);
        Gauges = Array.AsReadOnly(Enumerable.Repeat(new Ff7HighwayGauge(ushort.MaxValue, ushort.MaxValue), 5).ToArray());
    }

    internal FieldHostRequest Request { get; }
    internal bool Arcade { get; }
    internal ushort HighScore { get; }
    internal PcHighwayCourse Course { get; }
    internal IReadOnlyList<Ff7HighwayRoadSegment> Road { get; }
    internal IReadOnlyList<Ff7HighwayGauge> Gauges { get; }
    internal int InitialSpeed => 0x7A;
    internal int InitialTrackSegment => Road.Count;
    internal PcHighwayEvent? FirstDueEvent => Course.Events.FirstOrDefault() is { } entry && entry.Segment < InitialTrackSegment - EventLookAhead ? entry : null;

    internal static Ff7HighwayStart Prepare(FieldHostRequest request, bool arcade, ushort highScore, PcHighwayCourse course)
    {
        Ff7HighwayRoadSegment[] road = new Ff7HighwayRoadSegment[RoadRingSize];
        int commandIndex = 0;
        int commandPosition = 0;
        bool beyondBrokenRoad = false;
        for (int i = 0; i < road.Length; i++)
        {
            if (commandIndex >= course.Track.Count)
            {
                throw new InvalidDataException("The FFVII highway commands do not fill the initial road ring.");
            }
            PcHighwayTrackCommand command = course.Track[commandIndex];
            if (command.Turn != 0 || command.Pitch != 0 || command.Roll != 0)
            {
                throw new NotSupportedException("The FFVII highway requires curved road generation during initialization.");
            }
            ReadOnlySpan<byte> pattern = course.RoadPatterns[command.Pattern].Span;
            byte surface = pattern[(commandPosition + 1) % pattern.Length];
            road[i] = new((i + 1) * RoadStep, command.Width, surface, (byte)(i & 7), beyondBrokenRoad);
            beyondBrokenRoad |= surface == 4;
            commandPosition++;
            if (commandPosition == command.Length)
            {
                commandIndex++;
                commandPosition = 0;
            }
        }
        return new(request, arcade, highScore, course, road);
    }
}
