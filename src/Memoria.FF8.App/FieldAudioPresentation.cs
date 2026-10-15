using System.Diagnostics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

/// <summary>Device output is transient. Executed VM audio receipts keep their deterministic headless contract.</summary>
internal sealed class FieldAudioPresentation(nint window, string gameDirectory, string toolsDirectory) : IDisposable
{
    private readonly Dictionary<int, PcmAudio> samples = [];
    private readonly Dictionary<int, Voice> voices = [];
    private NativeMusicCatalog? catalog;
    private DirectSoundOutput? sound;
    private DirectMusicOutput? music;
    private DirectSoundOutput.Buffer? streamedMusic;
    private EventEngine? engine;
    private readonly int[] musicIds = [-1, -1];
    private readonly MusicKind[] musicKinds = [MusicKind.None, MusicKind.None];
    private Ramp? midiRamp;
    private Ramp? streamRamp;
    private float midiVolume = 127;
    private float streamVolume = 127;
    private int musicPreference = 100;
    private int soundPreference = 100;

    internal static int OutputGain(int nativeGain, int percentage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percentage);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percentage, 100);
        return percentage == 0 ? -10000 : Math.Clamp(nativeGain + (int)Math.Round(2000 * Math.Log10(percentage / 100d)), -10000, 0);
    }

    public void SetOutputPreferences(int musicVolume, int soundVolume)
    {
        OutputGain(0, musicVolume);
        OutputGain(0, soundVolume);
        musicPreference = musicVolume;
        soundPreference = soundVolume;
        TryOutput(() =>
        {
            foreach (Voice voice in voices.Values)
            {
                int gain = OutputGain(Catalog.SoundVolume(Math.Clamp((int)voice.Volume, 0, 127)), soundPreference);
                voice.Primary.Volume(gain);
                voice.Loop?.Volume(gain);
            }
            if (music is not null)
            {
                music.Volume(OutputGain(Catalog.MusicVolume(Math.Clamp((int)midiVolume, 0, 127)), musicPreference));
            }
            if (streamedMusic is not null)
            {
                streamedMusic.Volume(OutputGain(Catalog.SoundVolume(Math.Clamp((int)streamVolume, 0, 127)), musicPreference));
            }
        });
    }

    public string? Error { get; private set; }
    internal bool IsMidiPlaying => music?.IsPlaying == true;
    private NativeMusicCatalog Catalog => catalog ??= new NativeMusicCatalog(gameDirectory);
    private DirectSoundOutput Sound => sound ??= new DirectSoundOutput(window);
    private DirectMusicOutput Music => music ??= new DirectMusicOutput(window, Path.Combine(gameDirectory, "Data", "Music", "dmusic"));

    public void Attach(EventEngine next)
    {
        if (ReferenceEquals(engine, next))
        {
            return;
        }
        if (engine is not null)
        {
            engine.AudioCommandExecuted -= Execute;
        }
        StopAll();
        engine = next;
        next.AudioCommandExecuted += Execute;
    }

    public void ResumeMusic(SceneEventState scene, int fieldId)
    {
        // Saved native channel authority proves the track identity, not its device phase.
        // Resume from the original segment's start; never synthesize effect-buffer activity.
        NativeMusicChannelState? secondary = scene.MusicChannel112;
        NativeMusicChannelState? primary = scene.MusicChannel108;
        if (primary is { ChannelId: >= 0, MusicId: byte first })
        {
            TryOutput(() => PlayMusic(first, 0, scene.SoundMusicVolume197, fieldId));
        }
        if (secondary is { ChannelId: >= 0, MusicId: byte second })
        {
            TryOutput(() => PlayMusic(second, 1, scene.SoundMusicVolume198, fieldId));
        }
    }

    private void Execute(object command)
    {
        TryOutput(() =>
        {
            switch (command)
            {
                case GlobalSoundRequest request when request.Resource.BackendStatus != GlobalSoundBackendStatus.Failed:
                    PlaySound(request);
                    break;
                case NativeSoundStop stop:
                    foreach (int voice in stop.Voices)
                    {
                        StopVoice(voice);
                    }
                    break;
                case NativeSoundInitialization:
                    foreach (int voice in voices.Keys.ToArray())
                    {
                        StopVoice(voice);
                    }
                    break;
                case NativeSoundVolume volume:
                    SetVoiceVolume(volume.Voice, volume.Volume, volume.DurationMilliseconds);
                    break;
                case NativeSoundVolumeTransition volume:
                    SetVoiceVolume(volume.Voice, volume.Volume, volume.DurationMilliseconds);
                    break;
                case NativeSoundPosition position:
                    SetVoicePan(position.Voice, position.Pan, position.DurationMilliseconds);
                    break;
                case NativeAllSoundVolume volume:
                    SetAllVoiceVolumes(volume.FirstVoice, volume.VoiceCount, volume.Volume, volume.DurationMilliseconds);
                    break;
                case NativeAllSoundVolumeTransition volume:
                    SetAllVoiceVolumes(volume.FirstVoice, volume.VoiceCount, volume.Volume, volume.DurationMilliseconds);
                    break;
                case NativeMusicPlayRequest { ConsumedPending: true, Music: byte track, Channel: int slot } play:
                    PlayMusic(track, slot, play.Volume, play.FieldId, play.SkipPosition ?? 0, play.Opcode == "CROSSMUSIC");
                    break;
                case NativeMusicStop { StopRequested: true } stop:
                    StopMusicSlot(stop.Slot);
                    break;
                case NativeMusicVolume volume when volume.BothChannels is not null:
                    SetMusicVolume(volume);
                    break;
            }
        });
    }

    private void PlaySound(GlobalSoundRequest request)
    {
        int index = request.Resource.SampleIndex;
        if (!samples.TryGetValue(index, out PcmAudio? pcm))
        {
            pcm = PcmAudioDecoder.ReadSample(gameDirectory, index);
            samples.Add(index, pcm);
        }
        GlobalSoundLoopPolicy policy = request.Resource.LoopPolicy;
        DirectSoundOutput.Buffer primary = Sound.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
        DirectSoundOutput.Buffer? loop = null;
        try
        {
            if (policy.LoopStartPcmByte != 0)
            {
                int start = checked((int)policy.LoopStartPcmByte);
                int length = checked((int)(policy.LoopEndPcmByte - policy.LoopStartPcmByte));
                loop = Sound.Create(pcm.Bytes.AsSpan(start, length).ToArray(), pcm.SampleRate, pcm.Channels);
            }
            int gain = OutputGain(Catalog.SoundVolume((int)request.Volume), soundPreference);
            int pan = Catalog.SoundPan((int)request.EffectivePan);
            primary.Volume(gain);
            primary.Pan(pan);
            loop?.Volume(gain);
            loop?.Pan(pan);
            StopVoice(request.Voice);
            var voice = new Voice(primary, loop, request.Volume, request.EffectivePan, policy.LoopTransitionDelayMilliseconds);
            voices.Add(request.Voice, voice);
            primary.Play(loop is null && (policy.PlaybackFlags & 1) != 0);
        }
        catch
        {
            primary.Dispose();
            loop?.Dispose();
            voices.Remove(request.Voice);
            throw;
        }
    }

    private void PlayMusic(int track, int slot, int? volume, int fieldId, int startPoint = 0, bool forceRestart = false)
    {
        if (volume is not (>= 0 and <= 127))
        {
            throw new NotSupportedException("Music output requires the executed native volume.");
        }
        track = NativeMusicCatalog.ResolveMusicId(track, fieldId);
        if (!forceRestart && musicIds[slot] == track)
        {
            return;
        }
        StopMusicSlot(slot);
        string path = Path.Combine(gameDirectory, Catalog.MusicFile(track, fieldId));
        if (Path.GetExtension(path).Equals(".sgt", StringComparison.OrdinalIgnoreCase))
        {
            Music.Play(path, OutputGain(Catalog.MusicVolume(volume.Value), musicPreference), startPoint);
            musicKinds[slot] = MusicKind.Midi;
            midiVolume = volume.Value;
            midiRamp = null;
        }
        else
        {
            PcmAudio pcm = PcmAudioDecoder.ReadFile(toolsDirectory, path);
            streamedMusic?.Dispose();
            streamedMusic = Sound.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
            streamedMusic.Volume(OutputGain(Catalog.SoundVolume(volume.Value), musicPreference));
            streamedMusic.Play(true);
            musicKinds[slot] = MusicKind.Stream;
            streamVolume = volume.Value;
            streamRamp = null;
        }
        musicIds[slot] = track;
        Console.WriteLine($"field-music=Playing track={track} slot={slot} file={Path.GetFileName(path)}");
    }

    private void SetMusicVolume(NativeMusicVolume request)
    {
        bool midi = request.BothChannels == true || musicKinds[request.Slot] == MusicKind.Midi;
        bool stream = request.BothChannels == true || musicKinds[request.Slot] == MusicKind.Stream;
        if (request.FadeInitialVolumeOperand is int initial && initial is >= 0 and <= 127)
        {
            if (midi)
            {
                midiVolume = initial;
                music?.Volume(OutputGain(Catalog.MusicVolume(initial), musicPreference));
            }
            if (stream)
            {
                streamVolume = initial;
                streamedMusic?.Volume(OutputGain(Catalog.SoundVolume(initial), musicPreference));
            }
        }
        if (request.RequestedVolume is >= 0 and <= 127)
        {
            if (midi)
            {
                midiRamp = new Ramp(midiVolume, request.RequestedVolume, request.EffectiveDuration);
            }
            if (stream)
            {
                streamRamp = new Ramp(streamVolume, request.RequestedVolume, request.EffectiveDuration);
            }
        }
    }

    private void SetVoiceVolume(int voice, int volume, int duration)
    {
        if (voices.TryGetValue(voice, out Voice? active))
        {
            active.VolumeRamp = new Ramp(active.Volume, volume, duration);
        }
    }

    private void SetAllVoiceVolumes(int first, int count, int volume, int duration)
    {
        foreach (int voice in voices.Keys.Where(voice => voice >= first && voice < first + count))
        {
            SetVoiceVolume(voice, volume, duration);
        }
    }

    private void SetVoicePan(int voice, int pan, int duration)
    {
        if (voices.TryGetValue(voice, out Voice? active))
        {
            active.PanRamp = new Ramp(active.Pan, pan, duration);
        }
    }

    public void Update()
    {
        TryOutput(() =>
        {
            foreach (Voice voice in voices.Values)
            {
                if (!voice.LoopStarted && voice.Loop is not null && voice.Clock.Elapsed.TotalMilliseconds >= voice.LoopDelay)
                {
                    // Native46AEB0 starts the secondary loop and stops the primary buffer.
                    voice.Loop.Play(true);
                    voice.Primary.Stop();
                    voice.LoopStarted = true;
                }
                if (voice.VolumeRamp is Ramp ramp)
                {
                    voice.Volume = ramp.Value;
                    int gain = OutputGain(Catalog.SoundVolume(Math.Clamp((int)voice.Volume, 0, 127)), soundPreference);
                    voice.Primary.Volume(gain);
                    voice.Loop?.Volume(gain);
                    if (ramp.Finished)
                    {
                        voice.VolumeRamp = null;
                    }
                }
                if (voice.PanRamp is Ramp panRamp)
                {
                    voice.Pan = panRamp.Value;
                    int pan = Catalog.SoundPan(Math.Clamp((int)voice.Pan, 0, 127));
                    voice.Primary.Pan(pan);
                    voice.Loop?.Pan(pan);
                    if (panRamp.Finished)
                    {
                        voice.PanRamp = null;
                    }
                }
            }
            if (midiRamp is Ramp transition)
            {
                midiVolume = transition.Value;
                music?.Volume(OutputGain(Catalog.MusicVolume(Math.Clamp((int)midiVolume, 0, 127)), musicPreference));
                if (transition.Finished)
                {
                    midiRamp = null;
                }
            }
            if (streamRamp is Ramp streamTransition)
            {
                streamVolume = streamTransition.Value;
                streamedMusic?.Volume(OutputGain(Catalog.SoundVolume(Math.Clamp((int)streamVolume, 0, 127)), musicPreference));
                if (streamTransition.Finished)
                {
                    streamRamp = null;
                }
            }
        });
    }

    private void TryOutput(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException or TimeoutException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        {
            Error = exception.Message;
            Console.Error.WriteLine($"field-audio=Failed {Error}");
        }
    }

    private void StopVoice(int voice)
    {
        if (voices.Remove(voice, out Voice? active))
        {
            try
            {
                active.Primary.Dispose();
            }
            finally
            {
                active.Loop?.Dispose();
            }
        }
    }

    private void StopMusicSlot(int slot)
    {
        // Native46C060 chooses the process-wide MIDI or WAV backend by this slot's type.
        if (musicKinds[slot] == MusicKind.Midi)
        {
            music?.Stop();
            midiRamp = null;
        }
        else if (musicKinds[slot] == MusicKind.Stream)
        {
            streamedMusic?.Dispose();
            streamedMusic = null;
            streamRamp = null;
        }
        musicKinds[slot] = MusicKind.None;
        musicIds[slot] = -1;
    }

    public void StopAll()
    {
        foreach (int voice in voices.Keys.ToArray())
        {
            TryOutput(() => StopVoice(voice));
        }
        TryOutput(() => StopMusicSlot(0));
        TryOutput(() => StopMusicSlot(1));
    }

    public void Dispose()
    {
        if (engine is not null)
        {
            engine.AudioCommandExecuted -= Execute;
        }
        StopAll();
        music?.Dispose();
        sound?.Dispose();
    }

    private sealed class Voice(DirectSoundOutput.Buffer primary, DirectSoundOutput.Buffer? loop, float volume, float pan, int? loopDelay)
    {
        public DirectSoundOutput.Buffer Primary { get; } = primary;
        public DirectSoundOutput.Buffer? Loop { get; } = loop;
        public Stopwatch Clock { get; } = Stopwatch.StartNew();
        public int LoopDelay { get; } = loopDelay ?? 0;
        public bool LoopStarted { get; set; }
        public float Volume { get; set; } = volume;
        public Ramp? VolumeRamp { get; set; }
        public float Pan { get; set; } = pan;
        public Ramp? PanRamp { get; set; }
    }

    internal sealed class Ramp(float initial, float target, int milliseconds)
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        public bool Finished => milliseconds <= 0 || clock.Elapsed.TotalMilliseconds >= milliseconds;
        public float Value => Finished ? target : initial + (target - initial) * (float)(clock.Elapsed.TotalMilliseconds / milliseconds);
    }

    private enum MusicKind
    {
        None,
        Midi,
        Stream
    }
}
