using System.Diagnostics;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MovieTests
{
    [Fact]
    public async Task Installed_opening_and_next_movie_decode_frames_audio_and_dispose_processes()
    {
        string tools = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../../tools/ffmpeg/ffmpeg-9.0.2-essentials_build/bin"));
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
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
