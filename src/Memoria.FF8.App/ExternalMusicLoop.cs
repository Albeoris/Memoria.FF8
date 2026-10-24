using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Memoria.FF8;

internal sealed record ExternalMusicLoop(int StartByte, int EndByte)
{
    internal static ExternalMusicLoop Read(byte[] ogg, PcmAudio pcm)
    {
        List<byte[]> packets = [];
        using MemoryStream packet = new();
        for (int page = 0; page < ogg.Length && packets.Count < 2;)
        {
            if (ogg.Length - page < 27 || !ogg.AsSpan(page, 4).SequenceEqual("OggS"u8) || ogg[page + 4] != 0)
            {
                throw new InvalidDataException("External music requires an Ogg Vorbis stream.");
            }
            int segments = ogg[page + 26];
            int data = checked(page + 27 + segments);
            if (data > ogg.Length)
            {
                throw new InvalidDataException("Truncated Ogg page header.");
            }
            for (int segment = 0; segment < segments; segment++)
            {
                int length = ogg[page + 27 + segment];
                if (length > ogg.Length - data)
                {
                    throw new InvalidDataException("Truncated Ogg packet.");
                }
                packet.Write(ogg, data, length);
                data += length;
                if (length < 255)
                {
                    packets.Add(packet.ToArray());
                    packet.SetLength(0);
                    if (packets.Count == 2)
                    {
                        break;
                    }
                }
            }
            page = data;
        }
        if (packets.Count < 2 || packets[0].Length < 30 || !packets[0].AsSpan(0, 7).SequenceEqual("\x01vorbis"u8) || packets[1].Length < 11 || !packets[1].AsSpan(0, 7).SequenceEqual("\x03vorbis"u8))
        {
            throw new InvalidDataException("External music requires Vorbis identification and comment packets.");
        }
        int sourceRate = BinaryPrimitives.ReadInt32LittleEndian(packets[0].AsSpan(12, 4));
        if (sourceRate <= 0)
        {
            throw new InvalidDataException("Invalid Vorbis sample rate.");
        }
        byte[] comments = packets[1];
        int cursor = 7;
        int ReadLength()
        {
            if (cursor > comments.Length - 4)
            {
                throw new InvalidDataException("Truncated Vorbis comments.");
            }
            int value = BinaryPrimitives.ReadInt32LittleEndian(comments.AsSpan(cursor, 4));
            cursor += 4;
            if (value < 0)
            {
                throw new InvalidDataException("Invalid Vorbis comment length.");
            }
            return value;
        }
        int vendorLength = ReadLength();
        if (vendorLength > comments.Length - cursor)
        {
            throw new InvalidDataException("Truncated Vorbis vendor.");
        }
        cursor += vendorLength;
        int count = ReadLength();
        Dictionary<string, long> tags = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < count; index++)
        {
            int length = ReadLength();
            if (length > comments.Length - cursor)
            {
                throw new InvalidDataException("Truncated Vorbis comment.");
            }
            string text = Encoding.UTF8.GetString(comments, cursor, length);
            cursor += length;
            int equals = text.IndexOf('=');
            if (equals < 1)
            {
                continue;
            }
            string key = text[..equals];
            if (key.Equals("LOOPSTART", StringComparison.OrdinalIgnoreCase) || key.Equals("LOOPEND", StringComparison.OrdinalIgnoreCase) || key.Equals("LOOPLENGTH", StringComparison.OrdinalIgnoreCase))
            {
                if (!long.TryParse(text[(equals + 1)..], NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out long value) || value < 0 || !tags.TryAdd(key, value))
                {
                    throw new InvalidDataException("Invalid or duplicate Vorbis loop tag.");
                }
            }
        }
        int alignment = checked(pcm.Channels * sizeof(short));
        int ToByte(long samples)
        {
            double frame = Math.Round(samples * (double)pcm.SampleRate / sourceRate);
            if (frame > pcm.Bytes.Length / alignment)
            {
                throw new InvalidDataException("Vorbis loop tag exceeds the decoded track.");
            }
            return checked((int)frame * alignment);
        }
        long start = tags.GetValueOrDefault("LOOPSTART");
        int startByte = ToByte(start);
        int endByte = tags.TryGetValue("LOOPEND", out long end) ? ToByte(end) : tags.TryGetValue("LOOPLENGTH", out long duration) ? ToByte(checked(start + duration)) : pcm.Bytes.Length;
        if (startByte >= endByte || endByte > pcm.Bytes.Length || endByte % alignment != 0)
        {
            throw new InvalidDataException("Vorbis loop range is empty or invalid.");
        }
        return new ExternalMusicLoop(startByte, endByte);
    }
}
