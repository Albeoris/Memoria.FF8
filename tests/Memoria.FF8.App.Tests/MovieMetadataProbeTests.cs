using System.Diagnostics;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MovieMetadataProbeTests
{
    [Fact]
    public void Changed_local_source_is_rejected_before_a_decoder_process_starts()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "first version");
            MovieInformation information = FfmpegMovieDecoder.ParsePresentationInformation("""
                {"streams":[{"width":16,"height":16,"avg_frame_rate":"10/1","duration":"1"}]}
                """) with { SourceVersion = MovieFileVersion.Capture(path) };
            Assert.NotNull(information.SourceVersion);
            File.AppendAllText(path, " changed");

            Assert.Throws<IOException>(() => new FfmpegMovieDecoder("unavailable-tools", path, information: information));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Archive_source_versions_track_the_container_and_ignore_synthetic_sources()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "archive payload");
            MovieFileVersion? version = MovieFileVersion.Capture($"subfile,,start,1,end,5,,:file:{path}");
            Assert.NotNull(version);
            Assert.Equal(Path.GetFullPath(path), version.Path);
            version.Validate();
            File.AppendAllText(path, " changed");
            Assert.Throws<IOException>(version.Validate);
            Assert.Null(MovieFileVersion.Capture("resolved:movies/opening.avi"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Presentation_recovers_video_eof_and_refreshes_changed_archive_metadata()
    {
        string? tools = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS");
        Assert.SkipUnless(!string.IsNullOrEmpty(tools) && File.Exists(Path.Combine(tools, "ffmpeg.exe")) && File.Exists(Path.Combine(tools, "ffprobe.exe")), "Set MEMORIA_FF8_MEDIA_TOOLS to run the generated completion-seek regression.");
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8.MovieCompletion-" + Guid.NewGuid().ToString("N") + ".avi");
        try
        {
            ProcessStartInfo start = new(Path.Combine(tools!, "ffmpeg.exe")) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (string argument in new[] { "-v", "error", "-f", "lavfi", "-i", "color=c=red:s=16x16:r=10:d=1", "-f", "lavfi", "-i", "anullsrc=r=44100:cl=stereo", "-t", "2", "-c:v", "mpeg4", "-fps_mode", "passthrough", "-c:a", "pcm_s16le", path })
            {
                start.ArgumentList.Add(argument);
            }
            using (Process generate = Process.Start(start)!)
            {
                Task<string> errors = generate.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                Task<string> output = generate.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                if (!generate.WaitForExit(20000))
                {
                    generate.Kill(entireProcessTree: true);
                    generate.WaitForExit();
                    throw new TimeoutException("Generated movie did not complete within its fixture budget.");
                }
                Assert.True(generate.ExitCode == 0, await errors);
                await output;
            }
            MovieInformation information = FfmpegMovieDecoder.ProbePresentation(tools!, path);

            MovieCompletionFrame completion = MovieCompletionFrame.Read(tools!, path, information, 1.8);

            Assert.Equal(9, completion.FrameIndex);
            Assert.Equal(16 * 16 * 4, completion.Pixels.Length);

            string game = TestResources.GameDirectory;
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetMovieSource(new NativeMovieSource(0, 0));
            session.RequestMovie(0);
            long originalLength = new FileInfo(path).Length;
            string archiveSource = $"subfile,,start,0,end,{originalLength},,:file:{path}";
            using MovieClock clock = new(session, game, tools, relative => relative.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ? archiveSource : path);
            clock.Prepare();
            MovieInformation first = clock.PreparePresentation();
            File.AppendAllText(path, "changed container");
            Assert.Throws<IOException>(() => clock.SourcePath);
            session.RequestMovie(0);
            clock.Prepare();
            MovieInformation refreshed = clock.PreparePresentation();
            Assert.NotSame(first, refreshed);
            Assert.NotNull(refreshed.SourceVersion);
            Assert.True(refreshed.SourceVersion.Length > originalLength);
            Assert.Equal(0, MovieCompletionFrame.Read(tools!, archiveSource, refreshed, 0).FrameIndex);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Presentation_metadata_keeps_the_decoded_frame_count_unknown()
    {
        MovieInformation information = FfmpegMovieDecoder.ParsePresentationInformation("""
            {"streams":[{"width":1920,"height":1080,"avg_frame_rate":"24000/1001","duration":"2.002","nb_frames":"48"}],"format":{"duration":"3.0"}}
            """);

        Assert.Equal(1920, information.Width);
        Assert.Equal(1080, information.Height);
        Assert.Equal(24000d / 1001, information.FramesPerSecond);
        Assert.Equal(2.002, information.DurationSeconds);
        Assert.Equal(0, information.FrameCount);
    }

    [Fact]
    public void Unknown_presentation_count_does_not_clamp_decoded_indices_or_establish_native_callbacks()
    {
        MovieInformation information = new(640, 480, 30, 2, 0);

        Assert.Equal(450, MovieFrameTimeline.FrameAt(15, information));
        Assert.Equal(450, MovieClock.AbsoluteFrame(15, information));
        Assert.Throws<InvalidDataException>(() => new MovieFrameTimeline(information, 0));
        MovieInformation native = information with { FrameCount = 60 };
        Assert.Equal(59, MovieFrameTimeline.FrameAt(15, native));
        Assert.Equal(59, MovieClock.AbsoluteFrame(15, native));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("N/A")]
    [InlineData("0")]
    [InlineData("NaN")]
    public void Presentation_metadata_uses_container_duration_when_stream_duration_is_unavailable(string? streamDuration)
    {
        string durationProperty = streamDuration is null ? "" : ",\"duration\":\"" + streamDuration + "\"";
        string json = "{\"streams\":[{\"width\":640,\"height\":480,\"avg_frame_rate\":\"30/1\"" + durationProperty + "}],\"format\":{\"duration\":\"1.5\"}}";

        MovieInformation information = FfmpegMovieDecoder.ParsePresentationInformation(json);

        Assert.Equal(1.5, information.DurationSeconds);
        Assert.Equal(0, information.FrameCount);
    }

    [Theory]
    [InlineData("0/0")]
    [InlineData("30/0")]
    [InlineData("-30/1")]
    [InlineData("NaN/1")]
    [InlineData("Infinity/1")]
    [InlineData("1/Infinity")]
    [InlineData("1e308/1e-308")]
    [InlineData("N/A")]
    public void Presentation_metadata_rejects_invalid_frame_rates(string rate)
    {
        string json = "{\"streams\":[{\"width\":640,\"height\":480,\"avg_frame_rate\":\"" + rate + "\"}],\"format\":{\"duration\":\"1.5\"}}";

        Assert.Throws<InvalidDataException>(() => FfmpegMovieDecoder.ParsePresentationInformation(json));
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("0")]
    [InlineData("-1")]
    public void Presentation_metadata_rejects_unavailable_or_invalid_duration(string duration)
    {
        string json = "{\"streams\":[{\"width\":640,\"height\":480,\"avg_frame_rate\":\"30/1\"}],\"format\":{\"duration\":\"" + duration + "\"}}";

        Assert.Throws<InvalidDataException>(() => FfmpegMovieDecoder.ParsePresentationInformation(json));
    }

    [Theory]
    [InlineData("{\"streams\":[]}")]
    [InlineData("{}")]
    [InlineData("{\"streams\":[{\"width\":0,\"height\":480,\"avg_frame_rate\":\"30/1\",\"duration\":\"1\"}]}")]
    [InlineData("{\"streams\":[{\"width\":640,\"height\":-1,\"avg_frame_rate\":\"30/1\",\"duration\":\"1\"}]}")]
    public void Presentation_metadata_requires_a_video_stream_with_positive_dimensions(string json)
    {
        Assert.Throws<InvalidDataException>(() => FfmpegMovieDecoder.ParsePresentationInformation(json));
    }
}
