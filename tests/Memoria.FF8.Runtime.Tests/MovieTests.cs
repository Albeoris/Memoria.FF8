using System.Diagnostics;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MovieTests
{
    [Fact]
    public async Task Installed_short_movie_next_frame_preserves_every_decoded_callback_when_late()
    {
        string tools = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS") ?? string.Empty;
        string game = TestResources.GameDirectory;
        string path = Path.Combine(game, "Data", "movies", "disc00_01h.avi");
        Assert.SkipUnless(File.Exists(Path.Combine(tools, "ffmpeg.exe")) && File.Exists(Path.Combine(tools, "ffprobe.exe")) && File.Exists(path), "Installed short AVI corpus and cached FFmpeg are required.");
        using var decoder = new FfmpegMovieDecoder(tools, path);
        Assert.Equal(15, decoder.Information.FramesPerSecond);
        Assert.Equal(205, decoder.Information.FrameCount);
        Assert.False(decoder.TryGetNextFrame(-1, out MovieFrame? early));
        Assert.Null(early);
        Stopwatch timeout = Stopwatch.StartNew();
        int callbacks = 0;
        long audioBytes = 0;
        bool firstFrameAdmitted = false;
        while ((!decoder.VideoFinished || !decoder.AudioFinished) && timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            while (decoder.TryGetAudio(out byte[]? pcm))
            {
                Assert.Equal(0, pcm!.Length % 4);
                audioBytes += pcm.Length;
            }
            // Admit frame0 at its exact deadline, then simulate a late UI update.
            // Every call must still return exactly the next frame, including the tail.
            double deadline = firstFrameAdmitted ? 1000 : 0;
            while (decoder.TryGetNextFrame(deadline, out MovieFrame? frame))
            {
                Assert.NotNull(frame);
                Assert.Equal(callbacks, frame.Index);
                Assert.Equal(decoder.Information.Width * decoder.Information.Height * 4, frame.Rgba.Length);
                callbacks++;
                if (!firstFrameAdmitted)
                {
                    firstFrameAdmitted = true;
                    Assert.False(decoder.TryGetNextFrame(0, out MovieFrame? notYetDue));
                    Assert.Null(notYetDue);
                    break;
                }
            }
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }
        Assert.True(decoder.VideoFinished, $"Video did not drain in30s: callbacks={callbacks}; error={decoder.Error}");
        Assert.True(decoder.AudioFinished, "Audio did not drain in30s.");
        Assert.Null(decoder.Error);
        Assert.True(audioBytes > 0);
        Assert.Equal(decoder.Information.FrameCount, callbacks);
        Assert.False(decoder.TryGetNextFrame(1000, out MovieFrame? afterEnd));
        Assert.Null(afterEnd);
    }

    [Fact]
    public async Task Installed_opening_and_next_movie_decode_frames_audio_and_dispose_processes()
    {
        string tools = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS") ?? string.Empty;
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(tools, "ffmpeg.exe")) && Directory.Exists(game), "Installed AVI corpus and cached FFmpeg are required.");
        HashSet<int> before = Process.GetProcessesByName("ffmpeg").Select(process => process.Id).ToHashSet();
        foreach (int index in new[] { 30, 1 })
        {
            using (var decoder = new FfmpegMovieDecoder(tools, Path.Combine(game, "Data", "movies", $"disc00_{index:00}h.avi")))
            {
                Assert.Equal(1280, decoder.Information.Width);
                Assert.Equal(896, decoder.Information.Height);
                Assert.Equal(15, decoder.Information.FramesPerSecond);
                Assert.True(decoder.Information.DurationSeconds > 10);
                Assert.Equal(index == 30 ? 2925 : 205, decoder.Information.FrameCount);
                MovieFrame? frame = null;
                byte[]? audio = null;
                Stopwatch timeout = Stopwatch.StartNew();
                while ((frame is null || audio is null || !audio.Any(value => value != 0)) && timeout.Elapsed < TimeSpan.FromSeconds(10))
                {
                    if (frame is null)
                    {
                        decoder.TryGetFrame(2, out frame);
                    }
                    if (audio is null || !audio.Any(value => value != 0))
                    {
                        decoder.TryGetAudio(out audio);
                    }
                    await Task.Delay(20, TestContext.Current.CancellationToken);
                }
                Assert.NotNull(frame);
                Assert.Equal(1280 * 896 * 4, frame.Rgba.Length);
                Assert.NotNull(audio);
                Assert.Equal(0, audio.Length % 4);
                Assert.Contains(audio, value => value != 0);
            }
        }
        using (var tail = new FfmpegMovieDecoder(tools, Path.Combine(game, "Data", "movies", "disc00_01h.avi"), 12))
        {
            Stopwatch timeout = Stopwatch.StartNew();
            long lastFrame = -1;
            while ((!tail.VideoFinished || !tail.AudioFinished) && timeout.Elapsed < TimeSpan.FromSeconds(10))
            {
                if (tail.TryGetFrame(1000, out MovieFrame? frame))
                {
                    lastFrame = frame!.Index;
                }
                while (tail.TryGetAudio(out _))
                {
                }
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
            Assert.True(tail.VideoFinished, tail.Error);
            Assert.True(tail.AudioFinished);
            Assert.InRange(lastFrame, 23, 25);
        }
        Assert.All(Process.GetProcessesByName("ffmpeg"), process => Assert.Contains(process.Id, before));
    }
}
