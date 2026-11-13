using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record PcHighwayTrackCommand(byte Turn, byte Radius, ushort Length, short Roll, short Pitch, ushort Width, short Pattern);
public sealed record PcHighwayEvent(int Segment, int Code);

public sealed class PcHighwayPath
{
    private readonly (short X, short Y, short Z)[] points;

    internal PcHighwayPath((short X, short Y, short Z)[] points)
    {
        this.points = points;
    }

    public int PointCount => points.Length;

    public Vector3 Sample(uint position, bool positiveYAndZ)
    {
        int index = (int)(position >> 16);
        if (index >= points.Length - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
        int fraction = (ushort)position;
        var (x, y, z) = points[index];
        var (nextX, nextY, nextZ) = points[index + 1];
        int sampleX = Interpolate(x, nextX, fraction);
        int sampleY = Interpolate(y, nextY, fraction);
        int sampleZ = Interpolate(z, nextZ, fraction);
        return positiveYAndZ ? new(-sampleX, sampleY, sampleZ) : new(-sampleX, -sampleY, -sampleZ);
    }

    private static int Interpolate(short current, short next, int fraction) => current + (unchecked((next - current) * fraction) >> 16);
}

public sealed class PcHighwayCourse
{
    private const int StoryTrackMember = 15;
    private const int ArcadeTrackMember = 29;
    private const int TrackRecordSize = 12;
    private const int EventRecordSize = 8;
    private const int PathCountsMember = 32;
    private const int PathOffsetsMember = 33;
    private const int PathDataMember = 34;
    private const int PathRecordSize = 8;
    private const int EndOfEvents = ushort.MaxValue;

    private PcHighwayCourse(PcHighwayTrackCommand[] track, ReadOnlyMemory<byte>[] roadPatterns, PcHighwayEvent[] events, PcHighwayPath[] paths)
    {
        Track = Array.AsReadOnly(track);
        RoadPatterns = Array.AsReadOnly(roadPatterns);
        Events = Array.AsReadOnly(events);
        Paths = Array.AsReadOnly(paths);
    }

    public IReadOnlyList<PcHighwayTrackCommand> Track { get; }
    public IReadOnlyList<ReadOnlyMemory<byte>> RoadPatterns { get; }
    public IReadOnlyList<PcHighwayEvent> Events { get; }
    public IReadOnlyList<PcHighwayPath> Paths { get; }

    public static PcHighwayCourse Read(ReadOnlySpan<byte> directory, ReadOnlySpan<byte> data, bool arcade)
    {
        int firstMember = arcade ? ArcadeTrackMember : StoryTrackMember;
        int eventMember = firstMember + 2;
        if (directory.Length % sizeof(uint) != 0 || directory.Length / sizeof(uint) <= PathDataMember + 1)
        {
            throw new InvalidDataException("The FFVII highway address table is incomplete.");
        }

        BinaryCursor address = new(directory);
        uint origin = address.U32();
        int[] offsets = new int[directory.Length / sizeof(uint)];
        for (int i = 1; i < offsets.Length; i++)
        {
            uint offset = unchecked(address.U32() - origin);
            if (offset > data.Length || offset <= offsets[i - 1])
            {
                throw new InvalidDataException($"FFVII highway member {i} is outside its payload or is unordered.");
            }
            offsets[i] = (int)offset;
        }

        ReadOnlySpan<byte> trackData = data[offsets[firstMember]..offsets[firstMember + 1]];
        if (trackData.Length % TrackRecordSize != 0)
        {
            throw new InvalidDataException("The FFVII highway track command section has a partial record.");
        }
        BinaryCursor trackCursor = new(trackData);
        PcHighwayTrackCommand[] track = new PcHighwayTrackCommand[trackData.Length / TrackRecordSize];
        for (int i = 0; i < track.Length; i++)
        {
            PcHighwayTrackCommand command = new(trackCursor.U8(), trackCursor.U8(), trackCursor.U16(), trackCursor.I16(), trackCursor.I16(), trackCursor.U16(), trackCursor.I16());
            if (command.Turn > 2 || command.Turn != 0 && command.Radius == 0 || command.Length == 0 || command.Width == 0 || command.Pattern < 0)
            {
                throw new InvalidDataException($"FFVII highway track command {i} has invalid geometry.");
            }
            track[i] = command;
        }

        ReadOnlyMemory<byte>[] roadPatterns = ReadRoadPatterns(data[offsets[firstMember + 1]..offsets[eventMember]], track.Max(command => command.Pattern) + 1);

        ReadOnlySpan<byte> eventData = data[offsets[eventMember]..offsets[eventMember + 1]];
        if (eventData.Length % EventRecordSize != 0)
        {
            throw new InvalidDataException("The FFVII highway event section has a partial record.");
        }
        BinaryCursor eventCursor = new(eventData);
        PcHighwayEvent[] events = new PcHighwayEvent[eventData.Length / EventRecordSize - 1];
        int lastSegment = 0;
        for (int i = 0; i < events.Length; i++)
        {
            PcHighwayEvent entry = new(eventCursor.I32(), eventCursor.I32());
            if (entry.Segment < lastSegment || entry.Segment >= EndOfEvents)
            {
                throw new InvalidDataException($"FFVII highway event {i} is outside the ordered course.");
            }
            lastSegment = entry.Segment;
            events[i] = entry;
        }
        if (eventCursor.I32() != EndOfEvents)
        {
            throw new InvalidDataException("The FFVII highway event section has no terminal segment.");
        }

        PcHighwayPath[] paths = ReadPaths(data[offsets[PathCountsMember]..offsets[PathOffsetsMember]], data[offsets[PathOffsetsMember]..offsets[PathDataMember]], data[offsets[PathDataMember]..offsets[PathDataMember + 1]]);
        return new(track, roadPatterns, events, paths);
    }

    private static ReadOnlyMemory<byte>[] ReadRoadPatterns(ReadOnlySpan<byte> data, int count)
    {
        int indexStart = data[0];
        if (count <= 0 || count > indexStart || indexStart + count * 2 > data.Length)
        {
            throw new InvalidDataException("The FFVII highway road pattern directory is incomplete.");
        }
        ReadOnlyMemory<byte>[] patterns = new ReadOnlyMemory<byte>[count];
        for (int index = 0; index < count; index++)
        {
            int length = data[index + 1];
            int start = data[indexStart + index * 2 + 1];
            if (length == 0 || start < indexStart + count * 2 || start + length > data.Length)
            {
                throw new InvalidDataException($"FFVII highway road pattern {index} is outside its payload.");
            }
            patterns[index] = data.Slice(start, length).ToArray();
        }
        return patterns;
    }

    private static PcHighwayPath[] ReadPaths(ReadOnlySpan<byte> counts, ReadOnlySpan<byte> offsets, ReadOnlySpan<byte> data)
    {
        if (counts.Length != offsets.Length || counts.Length % sizeof(int) != 0 || counts.Length == 0)
        {
            throw new InvalidDataException("The FFVII highway path count and address tables disagree.");
        }
        PcHighwayPath[] paths = new PcHighwayPath[counts.Length / sizeof(int)];
        BinaryCursor countCursor = new(counts);
        BinaryCursor offsetCursor = new(offsets);
        int previousEnd = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            int count = countCursor.I32();
            int offset = offsetCursor.I32();
            if (offset < previousEnd || offset > data.Length || count is < 2 or > ushort.MaxValue || count > (data.Length - offset) / PathRecordSize)
            {
                throw new InvalidDataException($"FFVII highway path {i} is outside its payload or overlaps its predecessor.");
            }
            int length = count * PathRecordSize;
            BinaryCursor cursor = new(data.Slice(offset, length));
            (short X, short Y, short Z)[] points = new (short, short, short)[count];
            for (int point = 0; point < points.Length; point++)
            {
                points[point] = (cursor.I16(), cursor.I16(), cursor.I16());
                cursor.Skip(sizeof(short));
            }
            paths[i] = new(points);
            previousEnd = offset + length;
        }
        return paths;
    }
}
