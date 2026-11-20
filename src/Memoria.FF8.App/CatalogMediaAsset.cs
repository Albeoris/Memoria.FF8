using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Memoria.FF8.Audio;
using Memoria.FF8.Media;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

/// <summary>A local, seekable preview input and its stream metadata, shared by successive playback sessions.</summary>
internal sealed class CatalogMediaAsset
{
    private readonly bool temporary;
    private readonly MovieFileVersion? version;
    private int references = 1;

    private CatalogMediaAsset(string path, bool temporary, MovieFileVersion? version, string ffmpeg, double videoDuration, double audioDuration, double? framesPerSecond, double? videoAspectRatio, string? videoDetails, string? audioDetails)
    {
        Path = path;
        this.temporary = temporary;
        this.version = version;
        FFmpeg = ffmpeg;
        VideoDuration = videoDuration;
        AudioDuration = audioDuration;
        VideoFrameSeconds = framesPerSecond is double fps ? 1 / fps : 1;
        VideoRateText = FormatFrameRate(framesPerSecond);
        VideoAspectRatio = videoAspectRatio;
        VideoDetails = videoDetails;
        AudioDetails = audioDetails;
    }

    public string Path { get; }
    public string FFmpeg { get; }
    public double VideoDuration { get; }
    public double AudioDuration { get; }
    public double VideoFrameSeconds { get; }
    public string? VideoRateText { get; }
    public double? VideoAspectRatio { get; }
    public string? VideoDetails { get; }
    public string? AudioDetails { get; }
    public double Duration => Math.Max(VideoDuration, AudioDuration);

    public static async Task<CatalogMediaAsset> OpenAsync(string ffprobe, string ffmpeg, ModPreviewMediaInput input, bool video, CancellationToken token)
    {
        string? temporary = null;
        try
        {
            string path;
            if (input.WriteWave is { } writeWave)
            {
                path = temporary = System.IO.Path.GetTempFileName();
                await using FileStream output = new(path, FileMode.Truncate, FileAccess.ReadWrite, FileShare.None, 81920, useAsync: true);
                await writeWave(output, token).ConfigureAwait(false);
            }
            else if (input.Path is { } local)
            {
                path = System.IO.Path.GetFullPath(local);
            }
            else if (input.Segment is { } segment)
            {
                FileInfo source = new(segment.Path);
                if (segment.Offset < 0 || segment.Length <= 0 || segment.Length > source.Length || segment.Offset > source.Length - segment.Length)
                {
                    throw new InvalidDataException("The preview media file segment is invalid.");
                }
                path = FormattableString.Invariant($"subfile,,start,{segment.Offset},end,{segment.Offset + segment.Length},,:file:{source.FullName}");
            }
            else
            {
                path = temporary = System.IO.Path.GetTempFileName();
                await Task.Run(() => Materialize(input, path, token), token).ConfigureAwait(false);
            }
            MovieFileVersion? sourceVersion = MovieFileVersion.Capture(path);
            ProcessStartInfo probe = CreateStart(ffprobe, path);
            Add(probe, "-show_entries", "stream=codec_type,codec_name,start_time,duration,width,height,avg_frame_rate,sample_rate,channels,bit_rate:stream_disposition=attached_pic:stream_tags=DURATION:format=start_time,duration", "-of", "json", "-i", path);
            byte[] json = await AudioDecodeProcess.RunAsync(probe, null, token, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            using JsonDocument result = JsonDocument.Parse(json);
            JsonElement root = result.RootElement;
            double duration = root.TryGetProperty("format", out JsonElement format) ? ReadTime(format, "duration") ?? 0 : 0;
            double origin = format.ValueKind == JsonValueKind.Object ? ReadTime(format, "start_time") ?? 0 : 0;
            double videoDuration = 0;
            double audioDuration = 0;
            double? framesPerSecond = null;
            double? videoAspectRatio = null;
            string? videoDetails = null;
            string? audioDetails = null;
            bool foundVideo = false;
            bool foundAudio = false;
            foreach (JsonElement stream in root.GetProperty("streams").EnumerateArray())
            {
                string? type = stream.GetProperty("codec_type").GetString();
                if (video && !foundVideo && type == "video" && (!stream.TryGetProperty("disposition", out JsonElement disposition) || disposition.GetProperty("attached_pic").GetInt32() == 0))
                {
                    foundVideo = true;
                    videoDuration = ReadStreamEnd(stream, origin);
                    framesPerSecond = ReadFrameRate(stream);
                    int? width = ReadPositiveInt(stream, "width");
                    int? height = ReadPositiveInt(stream, "height");
                    string? resolution = null;
                    if (width is int w && height is int h)
                    {
                        videoAspectRatio = (double)w / h;
                        resolution = FormattableString.Invariant($"{w} × {h}");
                    }
                    videoDetails = DescribeStream("Video", stream, resolution, FormatFrameRate(framesPerSecond));
                }
                if (!foundAudio && type == "audio")
                {
                    foundAudio = true;
                    audioDuration = ReadStreamEnd(stream, origin);
                    int? sampleRate = ReadPositiveInt(stream, "sample_rate");
                    int? channels = ReadPositiveInt(stream, "channels");
                    audioDetails = DescribeStream(input.WriteWave is null ? "Audio" : "Audio (rendered)", stream,
                        sampleRate is int hz ? FormattableString.Invariant($"{hz} Hz") : null,
                        channels is int count ? FormattableString.Invariant($"{count} {(count == 1 ? "channel" : "channels")}") : null);
                }
            }

            duration = Math.Max(duration, Math.Max(videoDuration, audioDuration));
            if (video && !foundVideo || !video && !foundAudio)
            {
                throw new InvalidDataException(video ? "The media contains no video track." : "The media contains no audio track.");
            }
            if (!double.IsFinite(duration) || duration <= 0 || duration >= long.MaxValue / (double)CatalogMediaPlayback.SampleRate)
            {
                throw new InvalidDataException("FFprobe could not determine the media duration.");
            }
            double videoEnd = foundVideo ? (videoDuration > 0 ? videoDuration : duration) : 0;
            double audioEnd = foundAudio ? (audioDuration > 0 ? audioDuration : duration) : 0;
            sourceVersion?.Validate();
            return new(path, temporary is not null, sourceVersion, ffmpeg, videoEnd, audioEnd, framesPerSecond, videoAspectRatio, videoDetails, audioDetails);
        }
        catch
        {
            if (temporary is not null)
            {
                File.Delete(temporary);
            }
            throw;
        }
    }

    private static double ReadStreamEnd(JsonElement stream, double origin)
    {
        if (ReadTime(stream, "duration") is double duration && duration > 0)
        {
            return Math.Max(0, duration + (ReadTime(stream, "start_time") ?? origin) - origin);
        }
        if (stream.TryGetProperty("tags", out JsonElement tags) && tags.TryGetProperty("DURATION", out JsonElement tag) && tag.GetString() is { } timestamp)
        {
            string[] parts = timestamp.Split(':');
            if (parts.Length == 3 && double.TryParse(parts[0], CultureInfo.InvariantCulture, out double hours) &&
                double.TryParse(parts[1], CultureInfo.InvariantCulture, out double minutes) && double.TryParse(parts[2], CultureInfo.InvariantCulture, out double seconds))
            {
                return Math.Max(0, hours * 3600 + minutes * 60 + seconds - origin);
            }
        }
        return 0;
    }

    private static double? ReadTime(JsonElement owner, string property)
    {
        return owner.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) &&
            double.IsFinite(seconds) && Math.Abs(seconds) < long.MaxValue / (double)CatalogMediaPlayback.SampleRate ? seconds : null;
    }

    private static int? ReadPositiveInt(JsonElement stream, string property)
    {
        if (!stream.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }
        int number = 0;
        bool parsed = value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out number),
            JsonValueKind.String => int.TryParse(value.GetString(), CultureInfo.InvariantCulture, out number),
            _ => false
        };
        return parsed && number > 0 ? number : null;
    }

    private static string DescribeStream(string label, JsonElement stream, params string?[] characteristics)
    {
        List<string> parts = [label];
        parts.AddRange(characteristics.OfType<string>());
        if (stream.TryGetProperty("codec_name", out JsonElement codec) && codec.ValueKind == JsonValueKind.String && codec.GetString() is { Length: > 0 } name && name != "unknown")
        {
            parts.Add(name);
        }
        if (ReadPositiveInt(stream, "bit_rate") is int bitrate)
        {
            parts.Add(FormattableString.Invariant($"{bitrate / 1000d:0.###} kb/s"));
        }
        return string.Join(" · ", parts);
    }

    private static string? FormatFrameRate(double? framesPerSecond)
    {
        return framesPerSecond is double rate ? FormattableString.Invariant($"{rate:0.##} FPS") : null;
    }

    private static double? ReadFrameRate(JsonElement stream)
    {
        if (stream.TryGetProperty("avg_frame_rate", out JsonElement value) && value.GetString() is { } text)
        {
            string[] parts = text.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[0], CultureInfo.InvariantCulture, out int numerator) && numerator > 0 &&
                int.TryParse(parts[1], CultureInfo.InvariantCulture, out int denominator) && denominator > 0)
            {
                return (double)numerator / denominator;
            }
        }
        return null;
    }

    private static void Materialize(ModPreviewMediaInput input, string path, CancellationToken token)
    {
        using FileStream file = File.OpenWrite(path);
        using CancellationDestination destination = new(file, token);
        if (input.CopyTo is { } copy)
        {
            copy(destination);
        }
        else if (input.Read is { } read)
        {
            destination.Write(read());
        }
        else
        {
            throw new InvalidDataException("The selected media is unavailable.");
        }
        token.ThrowIfCancellationRequested();
    }

    public CatalogMediaAsset Retain()
    {
        Interlocked.Increment(ref references);
        return this;
    }

    public void Release()
    {
        if (Interlocked.Decrement(ref references) == 0 && temporary)
        {
            File.Delete(Path);
        }
    }

    public ProcessStartInfo Decode(double startSeconds)
    {
        version?.Validate();
        ProcessStartInfo start = CreateStart(FFmpeg, Path);
        Add(start, "-nostdin", "-filter_threads", "1", "-threads", "2", "-ss", startSeconds.ToString("0.######", CultureInfo.InvariantCulture), "-i", Path);
        return start;
    }

    private static ProcessStartInfo CreateStart(string executable, string path)
    {
        ProcessStartInfo start = new(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Environment["AV_LOG_FORCE_NOCOLOR"] = "1";
        string protocols = path.StartsWith("subfile,", StringComparison.Ordinal) ? "file,pipe,subfile" : "file,pipe";
        Add(start, "-v", "error", "-protocol_whitelist", protocols, "-format_whitelist", "avi,mov,matroska,webm,ogg,wav,mp3,flac,bink,mpeg,mpegts,m4v,mpegvideo,asf,flv,aac,aiff,ape,w64,tta,wv,ac3");
        return start;
    }

    internal static void Add(ProcessStartInfo start, params string[] arguments)
    {
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
    }

    private sealed class CancellationDestination(Stream output, CancellationToken token) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => output.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            token.ThrowIfCancellationRequested();
            output.Write(buffer);
        }
    }
}
