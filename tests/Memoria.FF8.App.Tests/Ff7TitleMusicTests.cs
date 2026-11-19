using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF8.Audio;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7TitleMusicTests
{
    [Fact]
    public void AnxiousHeartUsesItsCatalogNameRatherThanATrackNumber()
    {
        Assert.Equal(3, Ff7Scene.ResolveTitleMusicTrack(["", "pre", "oa", "KURAI", "tifa"]));
        Assert.Equal(1, Ff7Scene.ResolveTitleMusicTrack(["", "kurai", "pre"]));
        Assert.Throws<NotSupportedException>(() => Ff7Scene.ResolveTitleMusicTrack(["", "pre", "anchu"]));
    }

    [Fact]
    public void TitleUpdatesKeepTheSamePlaybackAndDoNotCreateGameplayMusicState()
    {
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using AudioDecodeQueue decoder = new(exception => throw exception);
        PcmAudio music = new(new byte[44100 * sizeof(short)], 44100, 1);
        PreparedPcmMusic prepared = new(music, new(0, music.Bytes.Length), PcmMusicRepeat.SeparateLoop);
        object owner = typeof(Ff7Scene).GetField("MusicDecodeOwner", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        MethodInfo start = typeof(Ff7Scene).GetMethod("StartTitleMusic", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Set("audioEnabled", true);
        Set("audioOutput", output);
        Set("musicDecoder", decoder);
        decoder.Queue(owner, _ => Task.FromResult(prepared), track => start.Invoke(scene, [track]));
        Assert.True(SpinWait.SpinUntil(() => decoder.ReadyCount == 1, TimeSpan.FromSeconds(5)));
        Update();
        using PcmLoopPlayback playback = Assert.IsType<PcmLoopPlayback>(Get("musicPlayback"));
        Assert.False(decoder.IsPending(owner));
        Assert.Null(Get("musicSource"));
        mixer.Render(new short[20]);
        var position = playback.CapturePosition(0, 0);
        Update();
        Assert.Same(playback, Get("musicPlayback"));
        Assert.Equal(position, playback.CapturePosition(0, 0));
        Assert.False(decoder.IsPending(owner));
        Assert.Null(Get("musicSource"));

        object? Get(string name) => typeof(Ff7Scene).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene);
        void Set(string name, object value) => typeof(Ff7Scene).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene, value);
        void Update() => typeof(Ff7Scene).GetMethod("UpdateTitleAudio", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, null);
    }

    [Fact]
    public void MutedTitleStartsSilentlyAndVolumeChangeKeepsItsPosition()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using PcmLoopPlayback playback = Ff7Scene.CreateTitleMusicPlayback(output, new(Music(), new(2, 6), PcmMusicRepeat.SeparateLoop), 0, false);
        AssertFrame(mixer, 0);
        Assert.Equal((2, false), playback.CapturePosition(0, 2));
        playback.SetGain(0);
        AssertFrame(mixer, 2000);
        Assert.Equal((4, false), playback.CapturePosition(0, 2));
    }

    [Fact]
    public void TitleUsesTaggedLoopAndPreservesPositionAcrossPauseThenReleasesItsVoices()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using PcmLoopPlayback playback = Ff7Scene.CreateTitleMusicPlayback(output, new(Music(), new(2, 6), PcmMusicRepeat.SeparateLoop), 100, true);
        AssertFrame(mixer, 0);
        Assert.Equal((0, false), playback.CapturePosition(0, 2));
        playback.Pause(false);
        AssertFrame(mixer, 1000);
        AssertFrame(mixer, 2000);
        AssertFrame(mixer, 3000);
        playback.UpdateAt(3 / 44100d);
        Assert.True(playback.LoopStarted);
        AssertFrame(mixer, 2000);
        playback.Pause(true);
        var position = playback.CapturePosition(0, 2);
        AssertFrame(mixer, 0);
        Assert.Equal(position, playback.CapturePosition(0, 2));
        playback.Pause(false);
        AssertFrame(mixer, 3000);
        AssertFrame(mixer, 2000);
        playback.Dispose();
        AssertFrame(mixer, 0);
    }

    private static PcmAudio Music()
    {
        byte[] bytes = new byte[8];
        for (int frame = 0; frame < 4; frame++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(frame * sizeof(short)), checked((short)((frame + 1) * 1000)));
        }
        return new(bytes, 44100, 1);
    }

    private static void AssertFrame(PcmMixer mixer, short expected)
    {
        short[] frame = new short[2];
        mixer.Render(frame);
        Assert.Equal(new[] { expected, expected }, frame);
    }
}
