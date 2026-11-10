using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldAudioResumeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(48)]
    [InlineData(50)]
    public void Host_resumes_music_once_after_semantic_world_allocation_without_a_field(byte entityType)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, true);
        ushort story = entityType == 48 ? (ushort)750 : entityType == 50 ? (ushort)3600 : (ushort)0;
        var entry = document.Progression!.Story.WorldEntry!.Value.SetItem(0, unchecked((byte)story)).SetItem(1, (byte)(story >> 8));
        if (entityType == 48)
        {
            entry = entry.SetItem(8, 9).SetItem(9, 1);
        }
        document = document with
        {
            Resume = document.Resume with { World = document.Resume.World! with { EntityType = entityType } },
            Progression = document.Progression! with { Story = document.Progression.Story with { WorldEntry = entry }, MusicPlayback = new(127, 0, 41, 0, NativeMusicPlaybackMode.Midi, NativeMusicPlaybackMode.Stopped, null) }
        };
        GameSession session = new(GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets));
        Assert.Null(session.State.EventState);
        Assert.Null(session.State.DepartingFieldState);
        Assert.NotNull(session.State.WorldEventState);
        NativeMusicCatalog native = new(directory);
        string name = ExternalMusicResolver.TrackName(native.MusicFileResolved(41));
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, "example")
        {
            JunctionMetadata = new() { UseExternalMusic = true, ExternalMusicExtension = "ogg" }
        };
        ModCatalogSnapshot catalog = new(1, [package], [new(package.Id, ModFormat.Junction, $"music/{name}.ogg", 1, () => ExternalMusicPcmTests.Ogg(44100))]);
        PcmMixer mixer = new();
        bool outputAttached = false;
        using FieldAudioPresentation audio = new(directory, "", () => catalog)
        {
            DecodeOverride = (_, _, _) => Task.FromResult(new PcmAudio(new byte[44100 * 4], 44100, 2)),
            OutputFactory = () =>
            {
                outputAttached = true;
                return new(mixer);
            }
        };
        string settings = Path.Combine(Path.GetTempPath(), "Memoria.FF8-world-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settings);
        try
        {
            using RecreateGame host = new(new LaunchOptions { GameDirectory = directory, SettingsDirectory = settings, SavePath = Path.Combine(settings, "unused.json"), ControlExclusive = true }, session);
            typeof(RecreateGame).GetField("fieldAudio", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, audio);
            GameWorld world = (GameWorld)typeof(RecreateGame).GetMethod("CreateWorld", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null)!;
            typeof(RecreateGame).GetField("world", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, world);
            host.ResumeGameplayAudio();
            Assert.False(outputAttached);
            world.EnsureField();
            Assert.Null(world.Field);
            Assert.NotNull(world.WorldScene);
            host.ResumeGameplayAudio();
            for (int attempt = 0; !outputAttached && attempt < 100; attempt++)
            {
                audio.Update();
                Thread.Sleep(10);
            }
            Assert.True(outputAttached);
            Assert.Null(audio.Error);
            Assert.Equal(41, audio.PrimaryMusicId);
            mixer.Render(new short[34]);
            long position = Assert.Single(Assert.Single(audio.CapturePlaybackPositions()!.Music).Positions).Position;
            Assert.Equal(17, position);
            host.ResumeGameplayAudio();
            Assert.Equal(position, Assert.Single(Assert.Single(audio.CapturePlaybackPositions()!.Music).Positions).Position);
            Assert.Null(session.State.AudioPresentation);
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.Null(audio.Error);
            if (entityType == 0)
            {
                Assert.Equal(0, audio.SoundVoiceCount);
            }
            else
            {
                Assert.InRange(audio.SoundVoiceCount, 1, 2);
            }
        }
        finally
        {
            Directory.Delete(settings, recursive: true);
        }
    }
}
