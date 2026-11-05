using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Memoria.FF8.Events;
using System.Collections.Immutable;

namespace Memoria.FF8.App.Tests;

public sealed class GameplayAudioPositionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Live_audio_cursor_resumes_compatible_PCM_or_PSF_and_discards_changed_resources(bool psf, bool changed)
    {
        NativeMusicCatalog native = new(TestResources.GameDirectory);
        string name = ExternalMusicResolver.TrackName(native.MusicFileResolved(3));
        ModCatalogSnapshot Catalog(bool change)
        {
            ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, "example")
            {
                JunctionMetadata = new() { UseExternalMusic = true, ExternalMusicExtension = psf ? "minipsf" : "ogg" }
            };
            return new(1, [package], [new(package.Id, ModFormat.Junction, $"music/{name}.{(psf ? "minipsf" : "ogg")}", 1, () => psf ? [change ? (byte)2 : (byte)1] : ExternalMusicPcmTests.Ogg(44100))]);
        }
        byte[] Pcm(bool change)
        {
            byte[] pcm = new byte[44100 * 4];
            for (int frame = 0; frame < 44100; frame++)
            {
                short sample = checked((short)(100 + frame % 100 + (change ? 1 : 0)));
                BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(frame * 4), sample);
                BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(frame * 4 + 2), sample);
            }
            return pcm;
        }
        PcmMixer sourceMixer = new();
        bool sourceAttached = false;
        using FieldAudioPresentation source = new(TestResources.GameDirectory, "", () => Catalog(false))
        {
            DecodeOverride = (_, _, _) => Task.FromResult(new PcmAudio(Pcm(false), 44100, 2)),
            PsfStreamOverride = (_, _) => Task.FromResult<Stream>(new MemoryStream(Pcm(false))),
            OutputFactory = () =>
            {
                sourceAttached = true;
                return new(sourceMixer);
            }
        };
        source.PlayMusic(3, 0, 127, -1, identityResolved: true);
        await PumpUntil(source, () => sourceAttached);
        sourceMixer.Render(new short[34]);
        source.SetFocusPaused(true);
        sourceMixer.Render(new short[20]);
        GameplaySaveAudioPresentation saved = source.CapturePlaybackPositions()!;
        Assert.Equal(17, Assert.Single(Assert.Single(saved.Music).Positions).Position);
        PcmMixer targetMixer = new();
        bool targetAttached = false;
        using FieldAudioPresentation target = new(TestResources.GameDirectory, "", () => Catalog(changed))
        {
            DecodeOverride = (_, _, _) => Task.FromResult(new PcmAudio(Pcm(changed), 44100, 2)),
            PsfStreamOverride = (_, _) => Task.FromResult<Stream>(new MemoryStream(Pcm(changed))),
            OutputFactory = () =>
            {
                targetAttached = true;
                return new(targetMixer);
            }
        };
        target.SetPlaybackResumePositions(saved);
        target.PlayMusic(3, 0, 127, -1, identityResolved: true);
        await PumpUntil(target, () => targetAttached);
        GameplaySaveAudioPosition resumed = Assert.Single(Assert.Single(target.CapturePlaybackPositions()!.Music).Positions);
        Assert.Equal(changed ? 0 : 17, resumed.Position);
        Assert.Null(target.Error);
        short[] frameOutput = new short[2];
        targetMixer.Render(frameOutput);
        Assert.True(frameOutput[0] > 0);
        Assert.Equal(frameOutput[0], frameOutput[1]);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 0.5)]
    [InlineData(true, 1.75)]
    public void Ambient_PCM_keeps_intro_or_repeating_region_position_without_a_game_tick(bool inLoop, double speed)
    {
        string directory = TestResources.GameDirectory;
        GlobalSoundResource resource = GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(directory, 2253))!;
        Assert.True(resource.LoopPolicy.LoopStartPcmByte > 0);
        GameplaySaveFieldLoopSound selection = new(1, 10103, 100, 64);
        EventEngine Engine()
        {
            EventEngine events = new(GameSession.CreateColdProcess());
            typeof(SceneEventState).GetProperty(nameof(SceneEventState.GameplayLoopSounds))!.SetValue(events.Scene, ImmutableDictionary<int, GameplaySaveFieldLoopSound>.Empty.Add(1, selection));
            return events;
        }
        PcmMixer sourceMixer = new();
        using FieldAudioPresentation source = new(directory, "") { OutputFactory = () => new(sourceMixer) };
        source.Attach(Engine());
        source.SetSoundSpeed(speed);
        source.Execute(new GameplayLoopSoundPlayback(selection, resource));
        Assert.Null(source.Error);
        var voices = (System.Collections.IDictionary)typeof(FieldAudioPresentation).GetField("voices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(source)!;
        object voice = voices[1]!;
        if (inLoop)
        {
            PcmAudioOutput.Buffer primary = (PcmAudioOutput.Buffer)voice.GetType().GetProperty("Primary")!.GetValue(voice)!;
            PcmAudioOutput.Buffer loop = (PcmAudioOutput.Buffer)voice.GetType().GetProperty("Loop")!.GetValue(voice)!;
            primary.Stop();
            loop.Play(true);
            voice.GetType().GetProperty("LoopStarted")!.SetValue(voice, true);
        }
        sourceMixer.Render(new short[74]);
        GameplaySaveAudioPresentation saved = source.CapturePlaybackPositions()!;
        GameplaySaveAudioPosition position = Assert.Single(saved.Ambient).Position;
        Assert.Equal(inLoop ? 1 : 0, position.StartPoint);
        PcmMixer targetMixer = new();
        using FieldAudioPresentation target = new(directory, "") { OutputFactory = () => new(targetMixer) };
        target.Attach(Engine());
        target.SetSoundSpeed(speed);
        target.SetPlaybackResumePositions(saved);
        target.Execute(new GameplayLoopSoundPlayback(selection, resource));
        Assert.Null(target.Error);
        Assert.Equal(position, Assert.Single(target.CapturePlaybackPositions()!.Ambient).Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Isolated_writer_keeps_optional_live_positions_and_reads_old_absence(bool includePosition)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveAudioPresentation? audio = includePosition ? new(1, [new(0, 3, [new("pcm16-v1", "resource", "source-frames", 12345, 44100, 2, 0, 44100)])], []) : null;
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, false) with { AudioPresentation = audio };
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-audio-save-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(folder, "save.ff8");
            GameState restored = GameplaySaveSessionWriter.Write(path, document, directory, sharedAssets: assets);
            using FileStream stream = File.OpenRead(path);
            GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(stream);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(audio), System.Text.Json.JsonSerializer.Serialize(decoded.AudioPresentation));
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(audio), System.Text.Json.JsonSerializer.Serialize(restored.AudioPresentation));
            Assert.Equal(document.Progression!.Clocks.Playtime, restored.Playtime);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void Position_units_and_decoder_mismatches_are_independently_discarded()
    {
        GameplaySaveAudioPosition valid = new("pcm16-v1", "ABC", "source-frames", 1, 44100, 2, 0, 10);
        Assert.True(FieldAudioPresentation.CompatiblePosition(valid, "pcm16-v1", "ABC"));
        Assert.False(FieldAudioPresentation.CompatiblePosition(valid with { Unit = "game-frames" }, "pcm16-v1", "ABC"));
        Assert.False(FieldAudioPresentation.CompatiblePosition(valid with { Backend = "unknown" }, "pcm16-v1", "ABC"));
        Assert.False(FieldAudioPresentation.CompatiblePosition(valid with { Position = -1 }, "pcm16-v1", "ABC"));
        Assert.False(FieldAudioPresentation.CompatiblePosition(valid, "pcm16-v1", "DEF"));
    }

    private static async Task PumpUntil(FieldAudioPresentation output, Func<bool> ready)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!ready())
        {
            output.Update();
            Assert.Null(output.Error);
            await Task.Delay(5, timeout.Token);
        }
    }
}
