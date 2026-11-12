using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestPcmTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(0.5)]
    [InlineData(1.75)]
    public void GuestAndHostVoicesSharePcmWhileRetainingTheirSourceAndColdPosition(double speed)
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7) && !string.IsNullOrWhiteSpace(ff8), "Select both original game installations.");
        using GameAssets resources = new(ff8!);
        NativeBattleOriginalEffectState effect = NativeBattleOriginalEffects.Select(2, resources.ReadNativeEffectCatalogForEffects([2]));
        uint soundId = effect.Binding.ProgramResources!.Sound;
        (_, NativeBattleModuleSound sound) = new NativeBattleModuleSound(soundId, 0x100, null) { Pan = 128, Volume = 100 }.Commit46B2A0(NativeGlobalSoundProcess.FromBufferlessColdProcess());
        PcmMixer sourceMixer = new();
        PcmMixer coldMixer = new();
        using PcmAudioOutput sourceOutput = new(sourceMixer);
        using PcmAudioOutput coldOutput = new(coldMixer);
        using FieldAudioPresentation guest = new(ff8!, "") { OutputFactory = () => sourceOutput, OwnsSoundOutput = false };
        using FieldAudioPresentation coldGuest = new(ff8!, "") { OutputFactory = () => coldOutput, OwnsSoundOutput = false };
        guest.SetSoundSpeed(speed);
        coldGuest.SetSoundSpeed(speed);
        guest.PlayBattleModuleSoundStrict(sound);
        Ff7SoundSample sample = Ff7SoundResources.Read(Ff7Installation.Discover(ff7!), 25);
        PcmAudio hostPcm = PcmAudioDecoder.ReadWave(sample.WaveBytes);
        string fingerprint = Convert.ToHexString(SHA256.HashData(sample.WaveBytes));
        using Ff7SoundChannels host = new();
        using Ff7SoundChannels coldHost = new();
        host.SetTempo(speed);
        coldHost.SetTempo(speed);
        host.Play(sourceOutput, hostPcm.Bytes, hostPcm.SampleRate, hostPcm.Channels, false, 0, -1200, 26, fingerprint: fingerprint);
        sourceMixer.Render(new short[256]);
        PresentationSoundVoice voice = Assert.Single(guest.CapturePresentationSounds(0));
        Assert.Equal(EventEngine.MapGlobalSoundSample(soundId), voice.SampleIndex);
        Assert.Equal(sound.ReadCommittedVoice(), voice.Voice);
        Assert.True(voice.Position.Position > 0);
        Ff7SoundVoicePosition hostVoice = Assert.Single(host.Capture(0));
        CrossoverGuestPcmSnapshot saved = new(1, SessionGame.Ff8, [voice]);
        Ff7AudioPresentationState envelope = new() { Sounds = [hostVoice], GuestSoundPayload = JsonSerializer.SerializeToUtf8Bytes(saved) };
        envelope = JsonSerializer.Deserialize<Ff7AudioPresentationState>(JsonSerializer.Serialize(envelope))!;
        envelope.Validate();
        saved = JsonSerializer.Deserialize<CrossoverGuestPcmSnapshot>(envelope.GuestSoundPayload!)!;
        saved.Validate();
        coldGuest.RestorePresentationSounds(saved.Voices);
        Assert.True(coldHost.RestoreVoice(coldOutput, hostPcm.Bytes, hostPcm.SampleRate, hostPcm.Channels, 26, false, false, 0, 0, fingerprint, hostVoice));
        Assert.Equal(voice, Assert.Single(coldGuest.CapturePresentationSounds(0)));
        short[] expected = new short[512];
        short[] actual = new short[512];
        sourceMixer.Render(expected);
        coldMixer.Render(actual);
        Assert.Contains(expected, value => value != 0);
        Assert.Equal(expected, actual);
        guest.SetFocusPaused(true);
        host.Pause(true);
        PresentationSoundVoice paused = Assert.Single(guest.CapturePresentationSounds(0));
        sourceMixer.Render(actual);
        Assert.All(actual, value => Assert.Equal(0, value));
        Assert.Equal(paused, Assert.Single(guest.CapturePresentationSounds(0)));
        host.Pause(false);
        guest.Dispose();
        sourceMixer.Render(actual);
        Assert.Contains(actual, value => value != 0);
        Assert.NotEmpty(host.Capture(0));
    }

    [Fact]
    public void GuestPresentationRejectsForeignOrDuplicatedVoicesAndOldAbsenceRemainsOptional()
    {
        PresentationSoundVoice voice = new(9, 1, 100, 64, new("pcm16-v1", new('A', 64), "source-frames", 1, 22050, 1, 0, 100));
        CrossoverGuestPcmSnapshot saved = new(1, SessionGame.Ff8, [voice]);
        saved.Validate();
        Assert.Throws<InvalidDataException>(() => (saved with { Origin = SessionGame.Ff7 }).Validate());
        Assert.Throws<InvalidDataException>(() => (saved with { Voices = [voice, voice] }).Validate());
        Assert.Throws<InvalidDataException>(() => (saved with { Voices = [voice with { Position = voice.Position with { Position = 100 } }] }).Validate());
        Ff7AudioPresentationState old = JsonSerializer.Deserialize<Ff7AudioPresentationState>("{\"Version\":1,\"Sounds\":[],\"Music\":null}")!;
        old.Validate();
        Assert.Null(old.GuestSoundPayload);
    }
}
