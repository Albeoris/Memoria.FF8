using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Memoria.FF8.Media;

public sealed partial class FfmpegMovieDecoder
{
    private static int CountVp8DisplayFrames(string toolsDirectory, string moviePath, int width, int height)
    {
        // Stream-copy preserves VP8 frame tags. Count show_frame (RFC6386 section9.1),
        // not packets or duration*FPS: invisible reference frames must not advance the movie clock.
        // IVF framing: https://ffmpeg.org/doxygen/trunk/ivfenc_8c_source.html
        using Process packets = Start(Memoria.FF8.Platform.MediaToolPaths.ResolveExecutable(toolsDirectory, "ffmpeg"), "-v", "error", "-i", moviePath, "-map", "0:v:0", "-c:v", "copy", "-f", "ivf", "pipe:1");
        Task<string> errors = packets.StandardError.ReadToEndAsync();
        try
        {
            using Stream stream = packets.StandardOutput.BaseStream;
            Span<byte> header = stackalloc byte[32];
            stream.ReadExactly(header);
            if (!header[..4].SequenceEqual("DKIF"u8) || BinaryPrimitives.ReadUInt16LittleEndian(header[4..]) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(header[6..]) != 32 || !header.Slice(8, 4).SequenceEqual("VP80"u8) || BinaryPrimitives.ReadUInt16LittleEndian(header[12..]) != width || BinaryPrimitives.ReadUInt16LittleEndian(header[14..]) != height)
            {
                throw new InvalidDataException("The compressed movie probe requires matching VP8 IVF framing.");
            }
            Span<byte> frameHeader = stackalloc byte[12];
            Span<byte> frameTag = stackalloc byte[3];
            byte[] discard = new byte[65536];
            int frames = 0;
            int first;
            while ((first = stream.ReadByte()) != -1)
            {
                frameHeader[0] = (byte)first;
                stream.ReadExactly(frameHeader[1..]);
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(frameHeader);
                if (size < 3)
                {
                    throw new InvalidDataException("The compressed movie probe encountered a truncated VP8 frame tag.");
                }
                stream.ReadExactly(frameTag);
                if ((frameTag[0] & 0x10) != 0)
                {
                    frames = checked(frames + 1);
                }
                uint remaining = size - 3;
                while (remaining != 0)
                {
                    int count = (int)Math.Min(remaining, (uint)discard.Length);
                    stream.ReadExactly(discard.AsSpan(0, count));
                    remaining -= (uint)count;
                }
            }
            packets.WaitForExit();
            if (packets.ExitCode != 0)
            {
                throw new InvalidDataException(errors.GetAwaiter().GetResult());
            }
            return frames;
        }
        finally
        {
            if (!packets.HasExited)
            {
                packets.Kill(entireProcessTree: true);
                packets.WaitForExit();
            }
        }
    }

    private static int CountDecodedFrames(string toolsDirectory, string moviePath)
    {
        // Other codecs do not share VP8's one-frame tag; retain actual decoded counting for those formats.
        using Process probe = Start(Memoria.FF8.Platform.MediaToolPaths.ResolveExecutable(toolsDirectory, "ffprobe"), "-v", "error", "-select_streams", "v:0", "-count_frames", "-show_entries", "stream=nb_read_frames", "-of", "json", moviePath);
        Task<string> errors = probe.StandardError.ReadToEndAsync();
        string json = probe.StandardOutput.ReadToEnd();
        probe.WaitForExit();
        if (probe.ExitCode != 0)
        {
            throw new InvalidDataException(errors.GetAwaiter().GetResult());
        }
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement stream = document.RootElement.GetProperty("streams")[0];
        if (!stream.TryGetProperty("nb_read_frames", out JsonElement count) || !int.TryParse(count.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int frames) || frames <= 0)
        {
            throw new InvalidDataException("FFprobe could not determine the movie's decoded frame count.");
        }
        return frames;
    }
}
