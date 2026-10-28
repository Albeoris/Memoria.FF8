using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PhoenixSummonAudioBindingTests
{
    [Fact]
    public void Installed_Phoenix_and_Quezacotl_descriptors_select_distinct_original_PCM_samples()
    {
        string directory = InstalledGame();
        using GameAssets assets = new(directory);
        NativeBattleEffect140Resources phoenix = assets.ReadNativeEffect140Resources();
        NativeBattleQuezacotlResources quezacotl = assets.ReadNativeQuezacotlResources();
        uint[] phoenixStreams = [StreamNumber(phoenix.Files[532]), StreamNumber(phoenix.Files[533])];
        uint[] quezacotlStreams = new[] { 510, 520, 524 }.Select(id => StreamNumber(quezacotl.Files.Single(file => file.FileId == id))).ToArray();

        Assert.Equal(new uint[] { 690122, 690121 }, phoenixStreams);
        Assert.Equal(new uint[] { 690118, 690119, 690120 }, quezacotlStreams);
        Assert.Equal(new[] { 2082, 2081 }, phoenixStreams.Select(EventEngine.MapGlobalSoundSample));
        Assert.Equal(new[] { 2078, 2079, 2080 }, quezacotlStreams.Select(EventEngine.MapGlobalSoundSample));
        Assert.Equal(new uint[] { 240066, 240069, 240072 }, phoenix.Sounds);
        Assert.Equal(new uint[] { 240069, 240072, 240039 }, new[] { quezacotl.Frame65Sound!.Value, quezacotl.Frame209Sound!.Value, quezacotl.Frame277Sound!.Value });

        string[] hashes = phoenixStreams.Concat(quezacotlStreams).Select(sound =>
        {
            int sample = EventEngine.MapGlobalSoundSample(sound);
            GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(directory, sample);
            PcmAudio pcm = PcmAudioDecoder.ReadSample(directory, sample);
            Assert.NotEmpty(pcm.Bytes);
            Assert.Equal((int)descriptor.WaveFormat.SampleRate, pcm.SampleRate);
            Assert.Equal((int)descriptor.WaveFormat.Channels, pcm.Channels);
            return Convert.ToHexStringLower(SHA256.HashData(pcm.Bytes));
        }).ToArray();
        Assert.Equal(5, hashes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Interleaved_Phoenix_and_Quezacotl_playback_keeps_the_sample_cache_bound_to_each_receipt()
    {
        string directory = InstalledGame();
        PcmMixer actualMixer = new();
        using FieldAudioPresentation output = new(directory, string.Empty) { OutputFactory = () => new PcmAudioOutput(actualMixer) };
        NativeMusicCatalog catalog = new(directory);
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        foreach (uint number in new uint[] { 690118, 690121, 690119, 690122, 690120, 690121 })
        {
            int sample = EventEngine.MapGlobalSoundSample(number);
            PcmAudio pcm = PcmAudioDecoder.ReadSample(directory, sample);
            (process, NativeBattleModuleSound receipt) = new NativeBattleModuleSound(number, 1, null) { RawPan = 128, Volume = 127 }.Commit46B2A0(process);
            output.PlayBattleModuleSound(receipt);
            Assert.Null(output.Error);
            Assert.Equal(1, output.SoundVoiceCount);

            PcmMixer reference = new();
            using PcmMixer.Voice expectedVoice = reference.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
            expectedVoice.Volume(FieldAudioPresentation.OutputGain(catalog.SoundVolume(127), 100));
            expectedVoice.Pan(catalog.SoundPan(64));
            expectedVoice.Play(false);
            int frames = checked((int)(((long)pcm.Bytes.Length / (pcm.Channels * 2) * 44100 + pcm.SampleRate - 1) / pcm.SampleRate) + 2);
            short[] expected = new short[checked(frames * 2)];
            short[] actual = new short[expected.Length];
            reference.Render(expected);
            actualMixer.Render(actual);
            Assert.Equal(expected, actual);
        }
    }

    private static uint StreamNumber(NativeBattleEffectFileAsset file)
    {
        int leadingNumber = BinaryPrimitives.ReadInt32LittleEndian(file.Data.AsSpan());
        // A controlled source address exercises publication without constructing a battle or a synthetic field.
        NativeBattleSummonStreamState published = NativeBattleSummonStreamState.FromColdProcess().Publish501860(0x1000, leadingNumber);
        Assert.Equal(0x1000u, published.SourceAddress);
        return unchecked((uint)published.Number!.Value);
    }

    private static string InstalledGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources for the Phoenix/Quezacotl audio binding check.");
        return directory!;
    }
}
