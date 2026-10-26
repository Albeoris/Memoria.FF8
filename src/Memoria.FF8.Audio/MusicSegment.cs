using System.Buffers.Binary;
using static Memoria.FF8.Audio.DlsBank;

namespace Memoria.FF8.Audio;

public sealed record SegmentEvent(int Tick, int Channel, int Status, int Data1, int Data2, int Duration = 0, uint Patch = 0, uint Flags = 0, int Transpose = 0);
public sealed record SegmentTempo(int Tick, double BeatsPerMinute);

/// <summary>DirectMusic fixed sequence tracks use 768 music ticks per quarter note.</summary>
public sealed class MusicSegment
{
    public const int TicksPerQuarter = 768;
    public int Length { get; }
    public int PlayStart { get; }
    public int LoopStart { get; }
    public int LoopEnd { get; }
    public uint Repeats { get; }
    public IReadOnlyList<SegmentEvent> Events { get; }
    public IReadOnlyList<SegmentTempo> Tempos { get; }

    public MusicSegment(byte[] bytes)
    {
        RiffChunk root = RiffChunk.Read(bytes, "DMSG");
        ValidateTracks(root);
        ReadOnlySpan<byte> header = root.Required("segh").Data.Span;
        Repeats = U32(header, 0);
        Length = I32(header, 4);
        PlayStart = I32(header, 8);
        LoopStart = I32(header, 12);
        LoopEnd = I32(header, 16);
        if (LoopEnd == 0)
        {
            LoopEnd = Length;
        }
        if (Length <= 0 || PlayStart < 0 || PlayStart >= Length || LoopStart < 0 || LoopEnd > Length || LoopStart >= LoopEnd)
        {
            throw new InvalidDataException("Invalid DirectMusic segment length, initial position or loop range.");
        }
        List<SegmentEvent> events = [];
        List<SegmentTempo> tempos = [];
        foreach (RiffChunk chunk in root.Descendants())
        {
            ReadOnlySpan<byte> data = chunk.Data.Span;
            if (chunk.Id == "tetr")
            {
                int size = I32(data, 0);
                if (size != 16 || (data.Length - 4) % size != 0)
                {
                    throw new NotSupportedException("Unsupported DirectMusic tempo record size.");
                }
                for (int offset = 4; offset < data.Length; offset += size)
                {
                    double tempo = BinaryPrimitives.ReadDoubleLittleEndian(data[(offset + 8)..]);
                    if (!double.IsFinite(tempo) || tempo <= 0)
                    {
                        throw new InvalidDataException("Invalid DirectMusic tempo.");
                    }
                    tempos.Add(new SegmentTempo(I32(data, offset), tempo));
                }
            }
            else if (chunk.Id == "seqt")
            {
                foreach (RiffChunk sequence in RiffChunk.ReadChildren(chunk.Data, 0, chunk.Data.Length))
                {
                    ReadSequence(sequence, events);
                }
            }
            else if (chunk.Type == "lbnd")
            {
                int tick = I32(chunk.Required("bdih").Data.Span, 0);
                foreach (RiffChunk instrument in chunk.Descendants().Where(c => c.Id == "bins"))
                {
                    ReadOnlySpan<byte> band = instrument.Data.Span;
                    uint flags = U32(band, 28);
                    events.Add(new SegmentEvent(tick, I32(band, 24), 0xc0, band[32], band[33], Patch: U32(band, 0), Flags: flags, Transpose: BinaryPrimitives.ReadInt16LittleEndian(band[34..])));
                }
            }
            else if (chunk.Id == "syex")
            {
                for (int offset = 0; offset < data.Length;)
                {
                    int tick = I32(data, offset);
                    int channel = I32(data, offset + 4);
                    int size = checked((int)U32(data, offset + 8));
                    if (size < 2 || size > data.Length - offset - 12)
                    {
                        throw new InvalidDataException("Truncated DirectMusic system-exclusive message.");
                    }
                    ReadOnlySpan<byte> message = data.Slice(offset + 12, size);
                    if (message.SequenceEqual(new byte[] { 0xf0, 0x43, 0x10, 0x4c, 0, 0, 0x7e, 0, 0xf7 }))
                    {
                        events.Add(new SegmentEvent(tick, channel, 0xf0, 1, 0));
                    }
                    else if (message.Length == 8 && message[0] == 0xf0 && message[1] == 0x7f && message[3] == 4 && message[4] == 1 && message[5] < 128 && message[6] < 128 && message[7] == 0xf7)
                    {
                        events.Add(new SegmentEvent(tick, channel, 0xf0, 2, message[5] | message[6] << 7));
                    }
                    else
                    {
                        throw new NotSupportedException("This segment requires unsupported system-exclusive effects or instrument routing.");
                    }
                    offset += 12 + size;
                }
            }
        }
        if (tempos.Count == 0 || tempos.All(t => t.Tick > 0))
        {
            tempos.Add(new SegmentTempo(0, 120));
        }
        Events = events.OrderBy(e => e.Tick).ThenBy(e => e.Status == 0xc0 ? 0 : e.Status == 0x90 ? 2 : 1).ToArray();
        Tempos = tempos.OrderBy(t => t.Tick).ToArray();
    }

    private static void ValidateTracks(RiffChunk root)
    {
        foreach (RiffChunk list in root.Descendants().Where(c => c.Type == "trkl"))
        {
            foreach (RiffChunk track in list.Children)
            {
                if (track.Type != "DMTK")
                {
                    throw new NotSupportedException($"Unsupported DirectMusic track form {track.Type ?? track.Id}.");
                }
                ReadOnlySpan<byte> header = track.Required("trkh").Data.Span;
                uint id = U32(header, 24);
                U32(header, 28);
                string identifier = System.Text.Encoding.ASCII.GetString(header.Slice(id != 0 ? 24 : 28, 4));
                string expectedClass = identifier switch
                {
                    "tetr" => "d2ac2885-b39b-11d1-8704-00600893b1bd",
                    "seqt" => "d2ac2886-b39b-11d1-8704-00600893b1bd",
                    "syex" => "d2ac2887-b39b-11d1-8704-00600893b1bd",
                    "tims" => "d2ac2888-b39b-11d1-8704-00600893b1bd",
                    "cord" => "d2ac288b-b39b-11d1-8704-00600893b1bd",
                    "DMBT" => "d2ac2894-b39b-11d1-8704-00600893b1bd",
                    _ => throw new NotSupportedException($"Unsupported DirectMusic track {identifier}.")
                };
                if (new Guid(header[..16]) != new Guid(expectedClass))
                {
                    throw new NotSupportedException($"Unsupported DirectMusic class for {identifier}.");
                }
                int payloads = 0;
                foreach (RiffChunk child in track.Children)
                {
                    if (id != 0 ? child.Id == identifier : child.Type == identifier)
                    {
                        payloads++;
                    }
                    else if (child.Id is not ("trkh" or "trkx" or "trke" or "guid" or "vers") && child.Type is not ("UNFO" or "INFO"))
                    {
                        throw new NotSupportedException($"Unsupported {identifier} track payload {child.Type ?? child.Id}.");
                    }
                }
                if (payloads != 1)
                {
                    throw new InvalidDataException($"DirectMusic {identifier} track must contain exactly one declared payload.");
                }
            }
        }
    }

    private static void ReadSequence(RiffChunk chunk, List<SegmentEvent> events)
    {
        ReadOnlySpan<byte> data = chunk.Data.Span;
        if (data.Length < 4)
        {
            throw new InvalidDataException("Truncated DirectMusic sequence data.");
        }
        int size = I32(data, 0);
        int minimum = chunk.Id == "evtl" ? 17 : chunk.Id == "curl" ? 28 : throw new NotSupportedException($"Unsupported DirectMusic sequence chunk {chunk.Id}.");
        if (size < minimum || (data.Length - 4) % size != 0)
        {
            throw new InvalidDataException("Invalid DirectMusic sequence record size.");
        }
        for (int offset = 4; offset < data.Length; offset += size)
        {
            ReadOnlySpan<byte> item = data.Slice(offset, size);
            int tick = checked(I32(item, 0) + BinaryPrimitives.ReadInt16LittleEndian(item[(chunk.Id == "evtl" ? 12 : 16)..]));
            if (chunk.Id == "evtl")
            {
                if (item[14] != 0x90)
                {
                    throw new NotSupportedException($"Unsupported DirectMusic sequence MIDI status {item[14]:X2}.");
                }
                events.Add(new SegmentEvent(tick, I32(item, 8), item[14], item[15], item[16], I32(item, 4)));
            }
            else
            {
                if (item[25] != 1 || BinaryPrimitives.ReadInt16LittleEndian(item[18..]) != BinaryPrimitives.ReadInt16LittleEndian(item[20..]))
                {
                    throw new NotSupportedException("Only instantaneous DirectMusic controller curves have an established contract.");
                }
                int status = item[24] switch
                {
                    3 => 0xe0,
                    4 => 0xb0,
                    _ => throw new NotSupportedException($"Unsupported DirectMusic curve type {item[24]}.")
                };
                events.Add(new SegmentEvent(tick, I32(item, 12), status, item[26], BinaryPrimitives.ReadInt16LittleEndian(item[18..])));
            }
        }
    }

    public double SecondsAt(int tick)
    {
        double seconds = 0;
        int position = 0;
        double tempo = 120;
        foreach (SegmentTempo change in Tempos)
        {
            if (change.Tick > tick)
            {
                break;
            }
            seconds += (change.Tick - position) * 60d / (tempo * TicksPerQuarter);
            position = change.Tick;
            tempo = change.BeatsPerMinute;
        }
        return seconds + (tick - position) * 60d / (tempo * TicksPerQuarter);
    }
}
