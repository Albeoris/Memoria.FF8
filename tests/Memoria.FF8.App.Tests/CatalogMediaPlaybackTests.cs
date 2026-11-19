using System.Diagnostics;
using System.Text;
using Memoria.FF8.Audio;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;

namespace Memoria.FF8.App.Tests;

public sealed class CatalogMediaPlaybackTests
{
    [Fact]
    public async Task Audio_runs_past_twelve_seconds_and_switches_sides_at_the_same_position()
    {
        string tools = GetMediaTools();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        CancellationToken token = timeout.Token;
        int copies = 0;
        ModPreviewMediaInput before = new("Before.wav", null)
        {
            CopyTo = destination => WriteWave(destination, 13, 12000)
        };
        ModPreviewMediaInput after = new("After.wav", null)
        {
            CopyTo = destination =>
            {
                copies++;
                WriteWave(destination, 14, -12000);
            }
        };
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        await using CatalogMediaPlayback playback = await CatalogMediaPlayback.OpenAsync(tools, before, after, false, token, output: output);
        Assert.Null(playback.Before.Error);
        Assert.Null(playback.After.Error);
        Assert.Equal(14d, playback.DurationSeconds);
        Assert.True(playback.Before.HasAudio);
        Assert.True(playback.After.HasAudio);
        Assert.False(playback.IsPlaying);

        playback.ListenAfter = false;
        playback.SetPlaying(true);
        short[] block = new short[882];
        mixer.Render(block);
        playback.Update();
        Assert.All(block, sample => Assert.True(sample > 0));
        Assert.Equal(0.01, playback.PositionSeconds, precision: 5);

        playback.ListenAfter = true;
        mixer.Render(block);
        playback.Update();
        Assert.All(block, sample => Assert.True(sample < 0));
        Assert.Equal(0.02, playback.PositionSeconds, precision: 5);

        while (playback.PositionSeconds < 12.5)
        {
            token.ThrowIfCancellationRequested();
            double start = playback.PositionSeconds;
            mixer.Render(block);
            playback.Update();
            if (start == playback.PositionSeconds)
            {
                await Task.Delay(5, token);
            }
        }
        Assert.Contains(block, sample => sample < 0);
        playback.SetPlaying(false);

        await using CatalogMediaPlayback seek = await playback.SeekAsync(13.5, token);
        playback.Dispose();
        Assert.Equal(1, copies);
        Assert.True(seek.ListenAfter);
        Assert.Equal(13.5, seek.PositionSeconds, precision: 5);
        seek.SetPlaying(true);
        mixer.Render(block);
        seek.Update();
        Assert.All(block, sample => Assert.True(sample < 0));
        while (seek.IsPlaying)
        {
            token.ThrowIfCancellationRequested();
            double start = seek.PositionSeconds;
            mixer.Render(block);
            seek.Update();
            if (start == seek.PositionSeconds)
            {
                await Task.Delay(5, token);
            }
        }
        Assert.Equal(14d, seek.PositionSeconds);
    }

    [Fact]
    public async Task Video_seek_advances_both_sides_and_keeps_the_shorter_sides_last_frame()
    {
        string tools = GetMediaTools();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        CancellationToken token = timeout.Token;
        string beforePath = Path.GetTempFileName();
        string afterPath = Path.GetTempFileName();
        try
        {
            await GenerateVideoAsync(tools, beforePath, "green", "yellow", "1.5", "2", "0.25", token);
            await GenerateVideoAsync(tools, afterPath, "red", "blue", "12.2", "14", "-0.25", token);
            byte[] clip = await File.ReadAllBytesAsync(beforePath, token);
            await using (FileStream archive = File.Create(beforePath))
            {
                await archive.WriteAsync(new byte[123], token);
                await archive.WriteAsync(clip, token);
                await archive.WriteAsync(new byte[64], token);
            }
            ModPreviewMediaInput before = new("Before", null) { Segment = new(beforePath, 123, clip.Length) };
            PcmMixer mixer = new();
            using PcmAudioOutput output = new(mixer);
            await using CatalogMediaPlayback playback = await CatalogMediaPlayback.OpenAsync(tools, before, new("After", afterPath), true, token, output: output);
            Assert.Null(playback.Before.Error);
            Assert.Null(playback.After.Error);
            Assert.Equal(14d, playback.DurationSeconds);
            Assert.True(playback.Before.Image![320, 180].R < 20);
            Assert.True(playback.After.Image![320, 180].R > 200);

            Assert.True(playback.Before.HasAudio);
            Assert.True(playback.After.HasAudio);
            short[] samples = new short[882];
            playback.ListenAfter = false;
            playback.SetPlaying(true);
            mixer.Render(samples);
            playback.Update();
            Assert.Contains(samples, sample => sample > 0);
            double beforeSwitch = playback.PositionSeconds;
            playback.ListenAfter = true;
            mixer.Render(samples);
            playback.Update();
            Assert.Contains(samples, sample => sample < 0);
            Assert.True(playback.PositionSeconds > beforeSwitch);
            playback.SetPlaying(false);

            await using CatalogMediaPlayback seek = await playback.SeekAsync(12, token);
            playback.Dispose();
            Assert.Equal(12d, seek.PositionSeconds);
            Assert.True(seek.Before.Image![320, 180].R > 200);
            Assert.True(seek.Before.Image[320, 180].G > 200);
            Assert.True(seek.After.Image![320, 180].R > 200);
            var videoBuffer = seek.After.Image;
            long initialRevision = seek.After.ImageRevision;

            seek.SetPlaying(true);
            while (seek.IsPlaying)
            {
                token.ThrowIfCancellationRequested();
                double start = seek.PositionSeconds;
                mixer.Render(samples);
                seek.Update();
                if (start == seek.PositionSeconds)
                {
                    await Task.Delay(5, token);
                }
            }
            Assert.True(seek.Before.Image[320, 180].R > 200);
            Assert.True(seek.Before.Image[320, 180].G > 200);
            Assert.True(seek.After.Image![320, 180].B > 200);
            Assert.Same(videoBuffer, seek.After.Image);
            Assert.True(seek.After.ImageRevision > initialRevision);
            Assert.Equal(14d, seek.PositionSeconds);
        }
        finally
        {
            File.Delete(beforePath);
            File.Delete(afterPath);
        }
    }

    [Fact]
    public async Task GamePreviewExposesOneTimelineAndAnIndependentSoundSwitchAt720Lines()
    {
        string tools = GetMediaTools();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        CancellationToken token = timeout.Token;
        string root = Path.Combine(Path.GetTempPath(), "memoria-media-preview-" + Guid.NewGuid().ToString("N"));
        string game = Path.Combine(root, "game");
        string original = Path.Combine(game, "Data", "Music");
        string modsRoot = Path.Combine(root, "Mods");
        string package = Path.Combine(modsRoot, "Memoria", "Media");
        string replacement = Path.Combine(package, "music");
        Directory.CreateDirectory(original);
        Directory.CreateDirectory(replacement);
        try
        {
            using (Stream before = File.Create(Path.Combine(original, "fixture.wav")))
            using (Stream after = File.Create(Path.Combine(replacement, "fixture.wav")))
            {
                WriteWave(before, 13, 8000);
                WriteWave(after, 14, -8000);
            }
            File.WriteAllText(Path.Combine(package, "mod.json"), """{"id":"preview-media-test","name":"Media test","package":"Memoria"}""");
            using ModRuntime mods = new(root, watch: false);
            Assert.True(mods.PreparePending(force: true));
            Assert.True(mods.CommitPending());
            ModPackageInstaller installer = new(Path.Combine(root, "state"), modsRoot, "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            PcmMixer mixer = new();
            using PcmAudioOutput output = new(mixer);
            MenuController menus = new();
            menus.ConnectMods(mods);
            menus.ConnectModLibrary(library);
            menus.ConnectModPreview(new(game), tools, audioOutput: output);
            menus.Open("mods");
            try
            {
                Find("mod-preview").Key(UiKey.Confirm);
                Assert.Equal("mods-preview", menus.ScreenId);
                while (!Find("preview-timeline").Visible)
                {
                    await Task.Delay(15, token);
                    menus.RefreshModView();
                }
                UiElement timeline = Find("preview-timeline");
                Assert.True(timeline.Bounds.Width > 400);
                Assert.Equal("Play", Assert.IsType<UiButton>(Find("preview-action-0")).Text);
                Assert.Equal("Sound · Before", Assert.IsType<UiButton>(Find("preview-action-1")).Text);

                Find("preview-action-0").Key(UiKey.Confirm);
                short[] samples = new short[882];
                mixer.Render(samples);
                Assert.Contains(samples, sample => sample > 0);
                Find("preview-action-1").Key(UiKey.Confirm);
                Assert.Equal("Sound · After", Assert.IsType<UiButton>(Find("preview-action-1")).Text);
                mixer.Render(samples);
                Assert.Contains(samples, sample => sample < 0);
                Assert.Equal("Pause", Assert.IsType<UiButton>(Find("preview-action-0")).Text);

                menus.PauseModMediaForFocus();
                mixer.Render(samples);
                Assert.All(samples, sample => Assert.Equal((short)0, sample));
                menus.Navigate(MenuInput.Cancel);
                Assert.Equal("mods", menus.ScreenId);
            }
            finally
            {
                menus.Close();
            }

            UiElement Find(string id)
            {
                UiView view = menus.BuildView();
                view.Resize(1280, 720);
                return Assert.Single(Descendants(view.Root), element => element.Id == id);
            }
        }
        finally
        {
            for (int retry = 0; ; retry++)
            {
                try
                {
                    Directory.Delete(root, true);
                    break;
                }
                catch (IOException) when (retry < 40)
                {
                    await Task.Delay(25, CancellationToken.None);
                }
            }
        }
    }

    [Fact]
    public async Task InstalledMovieCanBeComparedWithAnUncompressedMemberOfALargeJunctionArchive()
    {
        string archive = TestResources.FixturePath("junction-movie-archive");
        string game = TestResources.GameDirectory;
        string tools = GetMediaTools();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        CancellationToken token = timeout.Token;
        string root = Path.Combine(TestResources.ArtifactDirectory, "junction-movie");
        string modsRoot = Path.Combine(root, "Mods");
        using ModRuntime mods = new(root, watch: false);
        ModPackageInstaller installer = new(Path.Combine(root, "state"), modsRoot, "Classic/en/test");
        using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
        await library.StopAsync();
        ModPackage package = new("Junction/fixture", "Video fixture", ModFormat.Junction, null, false, ModEffectiveStatus.Disabled, archive);
        ModPreviewDocument preview = await library.OpenPreviewAsync(null, package, token);
        foreach (ModPreviewEntry entry in preview.Entries.Where(change => change.IsMedia && change.Category == "Video"))
        {
            using ModPreviewMediaSources pair = await preview.OpenMediaAsync(entry, new(game), token);
            if (pair.Before?.Path is null || pair.After.Segment is not { Length: > 1_000_000 })
            {
                continue;
            }
            await using CatalogMediaPlayback playback = await CatalogMediaPlayback.OpenAsync(tools, pair.Before, pair.After, true, token, audioEnabled: false);
            Assert.Null(playback.Before.Error);
            Assert.Null(playback.After.Error);
            if (playback.DurationSeconds <= 13)
            {
                continue;
            }
            await using CatalogMediaPlayback seek = await playback.SeekAsync(playback.DurationSeconds - 0.8, token);
            Assert.Null(seek.Before.Error);
            Assert.Null(seek.After.Error);
            Assert.NotNull(seek.Before.Image);
            Assert.NotNull(seek.After.Image);
            Assert.True(seek.PositionSeconds > 12);
            return;
        }
        Assert.Fail("The fixture must include a movie longer than thirteen seconds with a stock counterpart and a seekable archive member.");
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static string GetMediaTools()
    {
        string? tools = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(tools) && File.Exists(MediaToolPaths.ResolveExecutable(tools, "ffmpeg")) &&
            File.Exists(MediaToolPaths.ResolveExecutable(tools, "ffprobe")), "Set MEMORIA_FF8_MEDIA_TOOLS to run the generated preview playback regressions.");
        return tools!;
    }

    private static void WriteWave(Stream destination, int seconds, short value)
    {
        using BinaryWriter writer = new(destination, Encoding.UTF8, leaveOpen: true);
        int byteCount = CatalogMediaPlayback.SampleRate * seconds * 4;
        writer.Write("RIFF"u8);
        writer.Write(36 + byteCount);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)2);
        writer.Write(CatalogMediaPlayback.SampleRate);
        writer.Write(CatalogMediaPlayback.SampleRate * 4);
        writer.Write((short)4);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(byteCount);
        byte[] block = new byte[CatalogMediaPlayback.SampleRate * 4];
        for (int offset = 0; offset < block.Length; offset += sizeof(short))
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(block.AsSpan(offset), value);
        }
        for (int index = 0; index < seconds; index++)
        {
            writer.Write(block);
        }
    }

    private static async Task GenerateVideoAsync(string tools, string path, string firstColor, string secondColor, string changeSeconds, string duration, string sample, CancellationToken token)
    {
        ProcessStartInfo start = new(MediaToolPaths.ResolveExecutable(tools, "ffmpeg"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        CatalogMediaAsset.Add(start, "-v", "error", "-y", "-f", "lavfi", "-i", $"color=c={firstColor}:s=160x90:r=30:d={duration}",
            "-f", "lavfi", "-i", $"aevalsrc={sample}:s={CatalogMediaPlayback.SampleRate}:d={duration}",
            "-vf", $"drawbox=color={secondColor}:t=fill:enable='gte(t,{changeSeconds})'", "-c:v", "mpeg4", "-c:a", "pcm_s16le", "-f", "matroska", path);
        using Process generate = Process.Start(start)!;
        Task<string> errors = generate.StandardError.ReadToEndAsync(token);
        try
        {
            await generate.WaitForExitAsync(token);
            Assert.True(generate.ExitCode == 0, await errors);
        }
        finally
        {
            if (!generate.HasExited)
            {
                generate.Kill(entireProcessTree: true);
                await generate.WaitForExitAsync();
            }
        }
    }
}
