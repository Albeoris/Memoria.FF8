using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Audio;
using Memoria.FF8.Media;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleAudioTests
{
    [Fact]
    public void OpeningEnemyActionPublishesItsOriginalSoundThroughTheLiveSceneOnce()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("This installed-resource check requires MEMORIA_FF7_GAME_DIR.");
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using GpuTestDevice device = new();
        using Ff7Scene scene = new(device.Device, installation, "", new(50, 32000), MovieSource);
        scene.StartNewGame();
        Ff7Session session = Assert.IsType<Ff7Session>(scene.Session);
        session.SetFullCombatEnabled(true);
        for (int tick = 0; tick < 4500 && session.Battle is null; tick++)
        {
            if (session.Host.Process.Movie is { Playing: true } movie)
            {
                session.Host.PublishMovieFrame(Math.Min(movie.Frame + tick % 2, 1799), movie.Frame >= 1799);
            }
            if (tick % 30 == 0)
            {
                foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed))
                {
                    session.Host.CompleteDialogue(dialogue.RequestId, dialogue.FirstChoice ?? 0);
                }
            }
            if (tick >= 4110 && !session.Host.Process.PlayerLocked)
            {
                session.MovePlayer(0, -1, true);
            }
            session.AdvanceTick();
        }
        Ff7BattleSession battle = Assert.IsType<Ff7BattleSession>(session.Battle);
        for (int tick = 0; tick < 2000 && battle.Capture().SoundRequests.Length == 0; tick++)
        {
            session.AdvanceTick();
        }
        Ff7BattleSoundReceipt request = Assert.Single(battle.Capture().SoundRequests);
        PcmMixer mixer = new();
        SetOutput(scene, mixer);
        int track = battle.MusicTrack ?? session.Host.Process.NativeMusicTrack ?? session.Host.ResolveNativeMusicTrack(session.Host.Process.Music?.Track ?? 0);
        typeof(Ff7Scene).GetField("audioTrack", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene, Ff7FieldHost.NormalizeNativeMusicTrack(track));
        scene.Update(new GameTime(TimeSpan.Zero, TimeSpan.Zero), new KeyboardState());
        Assert.Null(scene.Error);
        Assert.Empty(battle.Capture().SoundRequests);
        Ff7SoundVoicePosition voice = Assert.Single(Channels(scene).Capture(0));
        Assert.Equal(request.Sound, voice.SoundId);
        Assert.Equal(127, voice.NativeVolume);
        scene.Update(new GameTime(TimeSpan.Zero, TimeSpan.Zero), new KeyboardState());
        Assert.Equal(voice, Assert.Single(Channels(scene).Capture(0)));
        Ff7SessionSave saved = scene.Capture().Ff7!;
        Assert.Empty(saved.Battle!.SoundRequests);
        Assert.Equal(voice, Assert.Single(saved.AudioPresentation!.Sounds));

        Ff7MovieSource MovieSource(int id)
        {
            Ff7MovieResources resource = Ff7MovieResources.Load(installation, id);
            Ff7MovieCameraTrack? cameras = resource.Cameras is { } records ? new(records) : null;
            return new(resource.Path, new MovieInformation(320, 240, 15, 120, 1800), 1800, 15)
            {
                CameraRecordCount = resource.Cameras?.Count ?? 0,
                SnapshotCameraFrame = cameras is null ? null : (frame, words) => cameras.SnapshotAt(frame, words)
            };
        }
    }

    [Fact]
    public void NativeBattleSoundUsesOriginalPcmAndResumesAtCapturedMixerPosition()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("This installed-resource check requires MEMORIA_FF7_GAME_DIR.");
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using GpuTestDevice device = new();
        using Ff7Scene first = new(device.Device, installation, "", new(50, 32000), _ => throw new InvalidOperationException());
        using Ff7Scene cold = new(device.Device, installation, "", new(50, 32000), _ => throw new InvalidOperationException());
        PcmMixer liveMixer = new();
        PcmMixer coldMixer = new();
        SetOutput(first, liveMixer);
        SetOutput(cold, coldMixer);
        Invoke(first, "PlayBattleSound", new Ff7BattleSoundReceipt(1, 26, 20));
        Ff7SoundChannels live = Channels(first);
        short[] prefix = new short[256];
        liveMixer.Render(prefix);
        Ff7SoundVoicePosition voice = Assert.Single(live.Capture(0));
        Assert.Equal(26, voice.SoundId);
        Assert.Equal(1, voice.Channel);
        Assert.Equal(127, voice.NativeVolume);
        Assert.True(voice.PositionFrames > 0);
        Ff7AudioPresentationState saved = new() { Sounds = [voice] };
        saved = JsonSerializer.Deserialize<Ff7AudioPresentationState>(JsonSerializer.Serialize(saved))!;
        Invoke(cold, "RestoreAudioPresentation", saved, true);
        Invoke(cold, "RestorePendingSounds");
        Assert.Equal(voice, Assert.Single(Channels(cold).Capture(0)));
        short[] expected = new short[512];
        short[] actual = new short[512];
        liveMixer.Render(expected);
        coldMixer.Render(actual);
        Assert.Contains(expected, sample => sample != 0);
        Assert.Equal(expected, actual);
        Invoke(cold, "PlayBattleSound", new Ff7BattleSoundReceipt(2, 0, 64));
        Assert.Empty(Channels(cold).Capture(0));
        Invoke(first, "SetAudioPaused", true);
        long paused = Assert.Single(live.Capture(0)).PositionFrames;
        liveMixer.Render(actual);
        Assert.All(actual, sample => Assert.Equal(0, sample));
        Assert.Equal(paused, Assert.Single(live.Capture(0)).PositionFrames);
    }

    private static Ff7SoundChannels Channels(Ff7Scene scene) => (Ff7SoundChannels)typeof(Ff7Scene).GetField("soundChannels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene)!;

    private static void SetOutput(Ff7Scene scene, PcmMixer mixer)
    {
        typeof(Ff7Scene).GetField("audioOutput", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene, new PcmAudioOutput(mixer));
    }

    private static void Invoke(Ff7Scene scene, string method, params object[] arguments)
    {
        typeof(Ff7Scene).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, arguments);
    }
}
