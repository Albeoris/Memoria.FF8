using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

/// <summary>Original MIDI tempo, termination and named loop ownership, independent of output volume and presentation decoding.</summary>
public sealed record Ff7NativeMusicTimeline(int Division, long DurationNumerator, bool Loops)
{
    public double DurationSeconds => DurationNumerator / (double)Division / 1_000_000;

    public bool IsPlaying(long callbacks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(callbacks);
        return Loops || (Int128)callbacks * 1_000_000 * Division < (Int128)DurationNumerator * 30;
    }

    public static Ff7NativeMusicTimeline Load(Ff7Installation installation, int track, Ff7PcExecutableData? native = null)
    {
        native ??= Ff7PcExecutableData.Load(installation);
        if (track is <= 0 || track >= native.MusicNames.Count)
        {
            throw new InvalidDataException("FFVII music timeline requires a nonzero original track identity.");
        }
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MidiArchive).Path);
        IReadOnlyList<LgpEntry> members = archive.Find(native.MusicNames[track].ToLowerInvariant() + ".mid");
        if (members.Count == 0)
        {
            return new(1, 0, false);
        }
        if (members.Count != 1)
        {
            throw new InvalidDataException("FFVII music timeline requires exactly one original MIDI member.");
        }
        return Read(archive.ReadEntry(members[0]));
    }

    public static Ff7NativeMusicTimeline Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < 14 || !source[..4].SequenceEqual("MThd"u8) || BinaryPrimitives.ReadUInt32BigEndian(source[4..]) != 6)
        {
            throw new InvalidDataException("FFVII music timeline requires an original bounded SMF header.");
        }
        int format = BinaryPrimitives.ReadUInt16BigEndian(source[8..]);
        int tracks = BinaryPrimitives.ReadUInt16BigEndian(source[10..]);
        int division = BinaryPrimitives.ReadUInt16BigEndian(source[12..]);
        if (format > 1 || tracks == 0 || format == 0 && tracks != 1 || division == 0 || (division & 0x8000) != 0)
        {
            throw new NotSupportedException("FFVII music timing requires synchronous MIDI tracks with a positive quarter-note division.");
        }
        List<(long Tick, int Tempo, int Order)> tempos = [];
        bool loops = false;
        long endTick = 0;
        int cursor = 14;
        for (int track = 0; track < tracks; track++)
        {
            if (cursor > source.Length - 8 || !source.Slice(cursor, 4).SequenceEqual("MTrk"u8))
            {
                throw new InvalidDataException("FFVII MIDI track directory is truncated.");
            }
            uint length = BinaryPrimitives.ReadUInt32BigEndian(source[(cursor + 4)..]);
            cursor += 8;
            if (length > source.Length - cursor)
            {
                throw new InvalidDataException("FFVII MIDI track extends beyond its original member.");
            }
            int end = checked(cursor + (int)length);
            long tick = 0;
            int running = 0;
            bool ended = false;
            while (cursor < end)
            {
                tick = checked(tick + ReadVariable(source, ref cursor, end));
                int status = Take(source, ref cursor, end);
                if (status < 0x80)
                {
                    if (running == 0)
                    {
                        throw new InvalidDataException("FFVII MIDI running status has no channel owner.");
                    }
                    cursor--;
                    status = running;
                }
                if (status == 0xFF)
                {
                    int kind = Take(source, ref cursor, end);
                    int count = ReadVariable(source, ref cursor, end);
                    if (count > end - cursor)
                    {
                        throw new InvalidDataException("FFVII MIDI metadata extends beyond its track.");
                    }
                    ReadOnlySpan<byte> payload = source.Slice(cursor, count);
                    cursor += count;
                    if (kind == 0x51)
                    {
                        if (count != 3)
                        {
                            throw new InvalidDataException("FFVII MIDI tempo requires three original bytes.");
                        }
                        int tempo = payload[0] << 16 | payload[1] << 8 | payload[2];
                        if (tempo == 0)
                        {
                            throw new InvalidDataException("FFVII MIDI tempo must be positive.");
                        }
                        tempos.Add((tick, tempo, tempos.Count));
                    }
                    else if (kind == 6)
                    {
                        if (payload.StartsWith("loopEnd"u8))
                        {
                            loops = true;
                        }
                    }
                    else if (kind == 0x2F)
                    {
                        if (count != 0 || cursor != end)
                        {
                            throw new InvalidDataException("FFVII MIDI end marker must terminate its original track.");
                        }
                        endTick = Math.Max(endTick, tick);
                        ended = true;
                        break;
                    }
                }
                else if (status is 0xF0 or 0xF7)
                {
                    running = 0;
                    int count = ReadVariable(source, ref cursor, end);
                    if (count > end - cursor)
                    {
                        throw new InvalidDataException("FFVII MIDI system event extends beyond its track.");
                    }
                    cursor += count;
                }
                else if (status is >= 0x80 and <= 0xEF)
                {
                    running = status;
                    int count = (status & 0xF0) is 0xC0 or 0xD0 ? 1 : 2;
                    for (int index = 0; index < count; index++)
                    {
                        if (Take(source, ref cursor, end) >= 0x80)
                        {
                            throw new InvalidDataException("FFVII MIDI channel data contains a status byte.");
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException("FFVII music timeline encountered an unsupported MIDI system event.");
                }
            }
            if (!ended)
            {
                throw new InvalidDataException("FFVII MIDI track has no original end marker.");
            }
        }
        if (cursor != source.Length)
        {
            throw new InvalidDataException("FFVII MIDI timeline has trailing data.");
        }
        int currentTempo = 500_000;
        long previous = 0;
        long numerator = 0;
        foreach (var tempo in tempos.OrderBy(item => item.Tick).ThenBy(item => item.Order))
        {
            numerator = checked(numerator + (tempo.Tick - previous) * currentTempo);
            previous = tempo.Tick;
            currentTempo = tempo.Tempo;
        }
        numerator = checked(numerator + (endTick - previous) * currentTempo);
        return new(division, numerator, loops);
    }

    private static int Take(ReadOnlySpan<byte> source, ref int cursor, int end)
    {
        if (cursor >= end)
        {
            throw new InvalidDataException("FFVII MIDI event is truncated.");
        }
        return source[cursor++];
    }

    private static int ReadVariable(ReadOnlySpan<byte> source, ref int cursor, int end)
    {
        int result = 0;
        for (int index = 0; index < 4; index++)
        {
            int value = Take(source, ref cursor, end);
            result = result << 7 | value & 0x7F;
            if (value < 0x80)
            {
                return result;
            }
        }
        throw new InvalidDataException("FFVII MIDI variable length exceeds four bytes.");
    }
}
