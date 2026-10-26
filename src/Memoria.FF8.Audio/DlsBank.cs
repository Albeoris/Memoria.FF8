using System.Buffers.Binary;

namespace Memoria.FF8.Audio;

public sealed record DlsConnection(ushort Source, ushort Control, ushort Destination, ushort Transform, int Scale);
public sealed record DlsSampleSettings(int UnityNote, int FineTune, int Attenuation, int LoopStart, int LoopLength);
public sealed record DlsWave(short[] Samples, int Rate, DlsSampleSettings Settings);
public sealed record DlsRegion(int KeyLow, int KeyHigh, int VelocityLow, int VelocityHigh, int KeyGroup, bool SelfNonExclusive, DlsWave Wave, DlsSampleSettings Settings, IReadOnlyList<DlsConnection> Connections);
public sealed record DlsInstrument(uint Bank, uint Program, IReadOnlyList<DlsRegion> Regions);

/// <summary>Reads DLS level 1 regions, pool cues, sample loops and articulation connections.</summary>
public sealed class DlsBank
{
    public IReadOnlyList<DlsInstrument> Instruments { get; }

    public DlsBank(byte[] bytes)
    {
        RiffChunk root = RiffChunk.Read(bytes, "DLS ");
        RiffChunk pool = root.Children.Single(c => c.Type == "wvpl");
        Dictionary<int, DlsWave> waves = [];
        foreach (RiffChunk wave in pool.Children.Where(c => c.Type == "wave"))
        {
            ReadOnlySpan<byte> format = wave.Required("fmt ").Data.Span;
            if (format.Length < 16)
            {
                throw new InvalidDataException("Truncated DLS wave format.");
            }
            int channels = U16(format, 2);
            byte[] pcm = WaveDecoder.Decode((ushort)U16(format, 0), channels, U16(format, 14), U16(format, 12), format.Length > 18 ? format[18..] : [], wave.Required("data").Data.Span);
            if (channels != 1)
            {
                throw new NotSupportedException("DLS stereo wave-link routing requires an explicit linked-region contract.");
            }
            short[] samples = new short[pcm.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2));
            }
            DlsSampleSettings settings = Settings(wave.Children.SingleOrDefault(c => c.Id == "wsmp"), samples.Length);
            waves.Add(wave.Offset - pool.Offset - 12, new DlsWave(samples, checked((int)U32(format, 4)), settings));
        }
        ReadOnlySpan<byte> table = root.Required("ptbl").Data.Span;
        int tableSize = checked((int)U32(table, 0));
        int cueCount = checked((int)U32(table, 4));
        DlsWave[] cues = new DlsWave[cueCount];
        for (int i = 0; i < cueCount; i++)
        {
            if (!waves.TryGetValue(checked((int)U32(table, tableSize + i * 4)), out DlsWave? wave))
            {
                throw new InvalidDataException("DLS pool cue does not name a wave boundary.");
            }
            cues[i] = wave;
        }
        List<DlsInstrument> instruments = [];
        foreach (RiffChunk instrument in root.Descendants().Where(c => c.Type == "ins "))
        {
            ReadOnlySpan<byte> header = instrument.Required("insh").Data.Span;
            List<DlsConnection> articulation = Connections(instrument);
            List<DlsRegion> regions = [];
            foreach (RiffChunk region in instrument.Descendants().Where(c => c.Type is "rgn " or "rgn2"))
            {
                ReadOnlySpan<byte> range = region.Required("rgnh").Data.Span;
                int waveIndex = checked((int)U32(region.Required("wlnk").Data.Span, 8));
                if ((uint)waveIndex >= cues.Length)
                {
                    throw new InvalidDataException("DLS region references an unavailable pool cue.");
                }
                DlsWave wave = cues[waveIndex];
                RiffChunk? settings = region.Children.SingleOrDefault(c => c.Id == "wsmp");
                Dictionary<(ushort, ushort, ushort, ushort), DlsConnection> merged = articulation.ToDictionary(c => (c.Source, c.Control, c.Destination, c.Transform));
                foreach (DlsConnection connection in Connections(region))
                {
                    merged[(connection.Source, connection.Control, connection.Destination, connection.Transform)] = connection;
                }
                int velocityLow = U16(range, 4);
                int velocityHigh = U16(range, 6);
                if (region.Type == "rgn " && velocityLow == 0 && velocityHigh == 0)
                {
                    velocityHigh = 127;
                }
                regions.Add(new DlsRegion(U16(range, 0), U16(range, 2), velocityLow, velocityHigh, U16(range, 10), (U16(range, 8) & 1) != 0, wave, settings is null ? wave.Settings : Settings(settings, wave.Samples.Length), merged.Values.ToArray()));
            }
            if (U32(header, 0) != regions.Count)
            {
                throw new InvalidDataException("DLS instrument region count does not match its header.");
            }
            instruments.Add(new DlsInstrument(U32(header, 4), U32(header, 8), regions));
        }
        Instruments = instruments;
    }

    private static List<DlsConnection> Connections(RiffChunk owner)
    {
        List<DlsConnection> result = [];
        foreach (RiffChunk list in owner.Children.Where(c => c.Type is "lart" or "lar2"))
        {
            foreach (RiffChunk chunk in list.Children.Where(c => c.Id is "art1" or "art2"))
            {
                ReadOnlySpan<byte> data = chunk.Data.Span;
                int size = checked((int)U32(data, 0));
                int count = checked((int)U32(data, 4));
                for (int i = 0; i < count; i++)
                {
                    int offset = checked(size + i * 12);
                    var connection = new DlsConnection((ushort)U16(data, offset), (ushort)U16(data, offset + 2), (ushort)U16(data, offset + 4), (ushort)U16(data, offset + 6), I32(data, offset + 8));
                    if (connection.Destination is 0x0208 or 0x030c && connection.Scale == 0)
                    {
                        continue;
                    }
                    if (connection.Transform != 0 || connection.Source > 5 || connection.Control != 0 && connection.Control is < 128 or > 255 || connection.Destination is not (1 or 3 or 4 or 0x0104 or 0x0105 or 0x0206 or 0x0207 or 0x0209 or 0x020a or 0x030a or 0x030b or 0x030d or 0x030e))
                    {
                        throw new NotSupportedException($"Unsupported DLS articulation {connection.Source}:{connection.Control}:{connection.Destination:X4}:{connection.Transform}.");
                    }
                    result.Add(connection);
                }
            }
        }
        return result;
    }

    private static DlsSampleSettings Settings(RiffChunk? chunk, int frames)
    {
        if (chunk is null)
        {
            return new DlsSampleSettings(60, 0, 0, 0, 0);
        }
        ReadOnlySpan<byte> data = chunk.Data.Span;
        int size = checked((int)U32(data, 0));
        uint count = U32(data, 16);
        if (count > 1)
        {
            throw new NotSupportedException("DLS waves with multiple sample loops are unsupported.");
        }
        int start = 0;
        int length = 0;
        if (count == 1)
        {
            if (U32(data, size + 4) != 0)
            {
                throw new NotSupportedException("Only DLS forward loops are supported.");
            }
            start = checked((int)U32(data, size + 8));
            length = checked((int)U32(data, size + 12));
            if (start < 0 || length <= 0 || (long)start + length > frames)
            {
                throw new InvalidDataException("DLS sample loop exceeds its wave.");
            }
        }
        return new DlsSampleSettings(U16(data, 4), BinaryPrimitives.ReadInt16LittleEndian(data[6..]), I32(data, 8), start, length);
    }

    internal static int U16(ReadOnlySpan<byte> data, int offset)
    {
        Require(data, offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
    }
    internal static uint U32(ReadOnlySpan<byte> data, int offset)
    {
        Require(data, offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    }
    internal static int I32(ReadOnlySpan<byte> data, int offset)
    {
        return unchecked((int)U32(data, offset));
    }
    private static void Require(ReadOnlySpan<byte> data, int offset, int count)
    {
        if (offset < 0 || offset > data.Length - count)
        {
            throw new InvalidDataException("Truncated DLS structure.");
        }
    }
}
