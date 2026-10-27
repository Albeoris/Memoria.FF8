using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using System.Runtime.InteropServices;

namespace Memoria.FF8.App.Tests;

public sealed class SpuWaveAudioOutputTests
{
    [Fact]
    public void MissingDeviceReportsAnAccurateNonPlaybackReceiptAfterManagedDecode()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-spu-cue-" + Guid.NewGuid().ToString("N"));
        string music = Path.Combine(directory, "Data", "music");
        Directory.CreateDirectory(music);
        string path = Path.Combine(music, "cue.wav");
        byte[] wave;
        using (var bytes = new MemoryStream())
        {
            using var writer = new BinaryWriter(bytes);
            writer.Write("RIFF"u8);
            writer.Write(40);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write((ushort)2);
            writer.Write(44100);
            writer.Write(176400);
            writer.Write((ushort)4);
            writer.Write((ushort)16);
            writer.Write("data"u8);
            writer.Write(4);
            writer.Write((short)100);
            writer.Write((short)-100);
            wave = bytes.ToArray();
        }
        File.WriteAllBytes(path, wave);
        try
        {
            MusicCueResource resource = new(MusicCue.EyesOnMe, "Data/music/cue.wav");
            using SpuWaveAudioOutput output = new(directory, resource, true)
            {
                OutputFactory = () => throw new IOException("Reconnect an audio device and restart audio.")
            };
            NativeSpuAudioAttempt receipt = output.Prepare();
            receipt.Validate();
            Assert.Equal(4, receipt.PcmBytes);
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(wave)), receipt.SourceSha256);
            Assert.False(receipt.PlaybackStarted);
            Assert.False(output.HasLiveAudio);
            Assert.Contains("Reconnect", output.Error);
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(music);
            Directory.Delete(Path.Combine(directory, "Data"));
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void ClassicCueUsesARelativeResourceAndRejectsEscapingPaths()
    {
        MusicCueResource resource = new ClassicMusicCueResolver().Resolve(MusicCue.EyesOnMe);
        Assert.Equal("Data/Music/eyes_on_me.wav", resource.RelativePath);
        resource.Validate();
        Assert.Throws<InvalidDataException>(() => new MusicCueResource(MusicCue.EyesOnMe, "../eyes_on_me.wav").Validate());
        Assert.Throws<InvalidDataException>(() => new MusicCueResource(MusicCue.EyesOnMe, Path.GetFullPath("eyes_on_me.wav")).Validate());
    }

    [Fact]
    public void InstalledCueDecodesWithoutClaimingPlaybackForMutedOutput()
    {
        string game = TestResources.GameDirectory;
        MusicCueResource resource = new ClassicMusicCueResolver().Resolve(MusicCue.EyesOnMe);
        Assert.SkipUnless(File.Exists(Path.Combine(game, resource.RelativePath)), "The installed EyesOnMe resource is required.");
        using SpuWaveAudioOutput output = new(game, resource, false);
        NativeSpuAudioAttempt receipt = output.Prepare();
        Assert.Equal("EyesOnMe", receipt.ResourceId);
        Assert.Equal(64, receipt.SourceSha256.Length);
        Assert.Equal(44100, receipt.SampleRate);
        Assert.Equal(2, receipt.Channels);
        Assert.Equal(59914848, receipt.PcmBytes);
        Assert.False(receipt.PlaybackStarted);
        Assert.False(output.HasLiveAudio);
        output.Stop();
        Assert.False(output.HasLiveAudio);
    }

    [Fact]
    public void InstalledCueStartsAndStopsExistingWindowsAudioOutput()
    {
        string game = TestResources.GameDirectory;
        MusicCueResource resource = new ClassicMusicCueResolver().Resolve(MusicCue.EyesOnMe);
        Assert.SkipUnless(File.Exists(Path.Combine(game, resource.RelativePath)), "The installed EyesOnMe resource is required.");
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using SpuWaveAudioOutput output = new(game, resource, true);
                NativeSpuAudioAttempt receipt = output.Prepare();
                Assert.True(receipt.PlaybackStarted);
                Assert.True(output.HasLiveAudio);
                output.Stop();
                Assert.False(output.HasLiveAudio);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "EyesOnMe start/stop did not finish.");
        Assert.Null(failure);
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
