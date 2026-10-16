using System.Diagnostics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Device output is transient. Executed VM audio receipts keep their deterministic headless contract.</summary>
internal sealed partial class FieldAudioPresentation(nint window, string gameDirectory, string toolsDirectory) : IDisposable
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
    private int battleVoice;

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
    internal int PrimaryMusicId => musicIds[0];
    internal int SoundVoiceCount => voices.Count;
    internal float PrimaryMusicVolume => musicKinds[0] == MusicKind.Midi ? midiVolume : streamVolume;
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

    public void ResumeMusic(SceneEventState scene, int fieldId, bool worldMap = false)
    {
        // Saved native channel authority proves the track identity, not its device phase.
        // Resume from the original segment's start; never synthesize effect-buffer activity.
        NativeMusicChannelState? secondary = scene.MusicChannel112;
        NativeMusicChannelState? primary = scene.MusicChannel108;
        if (primary is { ChannelId: >= 0, MusicId: byte first } && first != 255)
        {
            TryOutput(() =>
            {
                RequireOrdinaryMusicOutput(primary.ChoiceMask);
                PlayMusic(first, 0, worldMap ? scene.WorldMusicLoad?.TargetVolume ?? throw new NotSupportedException("World music resume requires its executed native loader receipt.") : scene.SoundMusicVolume197, fieldId, worldMap ? scene.WorldMusicLoad!.StartPoint : 0);
            });
        }
        if (secondary is { ChannelId: >= 0, MusicId: byte second } && second != 255)
        {
            TryOutput(() => PlayMusic(second, 1, scene.SoundMusicVolume198, fieldId));
        }
    }

    internal void Execute(object command)
    {
        TryOutput(() =>
        {
            switch (command)
            {
                case NativeWorldMusicLoad world:
                    if (world.Operation == NativeWorldMusicOperation.Stop)
                    {
                        StopMusicSlot(0);
                    }
                    else if (world.Music is byte worldMusic)
                    {
                        PlayMusic(worldMusic, 0, world.InitialVolume ?? (int)PrimaryMusicVolume, world.SourceFieldId, world.StartPoint, forceRestart: world.Operation == NativeWorldMusicOperation.Play);
                        SetMusicGain(0, false, null, world.TargetVolume, world.DurationMilliseconds);
                    }
                    break;
                case GlobalSoundRequest request when request.Resource.BackendStatus != GlobalSoundBackendStatus.Failed:
                    PlaySound(request);
                    break;
                case NativeFootstepSound footstep:
                    PlayFootstepSound(footstep);
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
                    RequireOrdinaryMusicOutput(play.ChoiceMask);
                    PlayMusic(track, slot, play.Volume, play.FieldId, play.SkipPosition ?? 0, play.Opcode == "CROSSMUSIC");
                    break;
                case NativeMusicReplayRequest replay:
                    PlayMusic(replay.Music, 0, replay.Volume, replay.FieldId);
                    SetMusicGain(0, false, null, replay.Volume, 0);
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

    internal static void RequireOrdinaryMusicOutput(uint? choiceMask)
    {
        if (choiceMask is not null)
        {
            // TODO(TD-003): concert segments need their actual DirectMusic output lifecycle.
            throw new NotSupportedException("Concert segment output requires the original instrument-selection backend; ordinary full-track rendering is unsupported.");
        }
    }

    private void PlaySound(GlobalSoundRequest request)
    {
        PlaySoundSample(request.Resource.SampleIndex, request.Voice, request.Volume, request.EffectivePan, request.Resource.LoopPolicy);
    }

    internal static (int SampleIndex, int Voice, uint Volume, uint EffectivePan) ResolveFootstepPlayback(NativeFootstepSound footstep)
    {
        _ = footstep.RequireSpatialization();
        if (footstep.Flags is not (0x400000 or 0x800000) || footstep.Pan > 255 || footstep.Volume > 127)
        {
            throw new NotSupportedException("Footstep output requires the original520260 channel mask, unsigned pan and volume.");
        }
        // Both520260 masks select explicit voices, so46B0F0 requires no automatic allocation state.
        NativeGlobalSoundProcess.FromBufferlessColdProcess().SelectVoice(footstep.Flags, out NativeGlobalSoundAllocation allocation);
        return (EventEngine.MapGlobalSoundSample(footstep.EffectId), allocation.Voice, footstep.Volume, footstep.Pan >> 1);
    }

    private void PlayFootstepSound(NativeFootstepSound footstep)
    {
        (int sample, int voice, uint volume, uint pan) = ResolveFootstepPlayback(footstep);
        GlobalSoundResource resource = GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(gameDirectory, sample)) ?? throw new NotSupportedException($"Footstep sound {footstep.EffectId} requires an unsupported audio format.");
        // Native footsteps replace device voices23/24, sharing script stop/volume controls.
        // The transient callback creates no persisted VM request or allocator mutation.
        PlaySoundSample(sample, voice, volume, pan, resource.LoopPolicy);
    }

    private void PlaySoundSample(int index, int voiceId, uint volume, uint effectivePan, GlobalSoundLoopPolicy policy)
    {
        if (!samples.TryGetValue(index, out PcmAudio? pcm))
        {
            pcm = PcmAudioDecoder.ReadSample(gameDirectory, index);
            samples.Add(index, pcm);
        }
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
            int gain = OutputGain(Catalog.SoundVolume((int)volume), soundPreference);
            int pan = Catalog.SoundPan((int)effectivePan);
            primary.Volume(gain);
            primary.Pan(pan);
            loop?.Volume(gain);
            loop?.Pan(pan);
            StopVoice(voiceId);
            var voice = new Voice(primary, loop, volume, effectivePan, policy.LoopTransitionDelayMilliseconds);
            voices.Add(voiceId, voice);
            primary.Play(loop is null && (policy.PlaybackFlags & 1) != 0);
        }
        catch
        {
            primary.Dispose();
            loop?.Dispose();
            voices.Remove(voiceId);
            throw;
        }
    }

    public void PlayBattleMusic(int track)
    {
        TryOutput(() => PlayMusic(track, 0, 127, -1));
    }

    public void PlayStartupMusic()
    {
        TryOutput(() => PlayMusic(NativeStartupCredits.MusicId, 0, 127, -1));
    }

    public void PlayBattleImpact(int actorId)
    {
        TryOutput(() =>
        {
            // Native504290 selects slot1 for actor0 and slot0 for other actors;501640 reads B8A418.
            uint effect = Catalog.BattleActorSound(actorId, actorId == 0 ? 1 : 0);
            if (effect == 0)
            {
                return;
            }
            // TODO(TD-009): Centered maximum-gain contact at scaffold damage timing; native sequence/camera spatialization remains deferred.
            // The negative voice is device-only and cannot collide with native field voices0..31 or create a VM receipt.
            PlayPresentationSound(effect, -1);
        });
    }

    public void PlayUiSound(GameUi.MenuSound cue)
    {
        TryOutput(() =>
        {
            if (!Enum.IsDefined(cue))
            {
                throw new ArgumentOutOfRangeException(nameof(cue));
            }
            // Native4C0A30/4C0A80 use menu1 for cursor motion;4AD8D0 uses2/3 for confirm/cancel.
            PlayPresentationSound(Catalog.MenuSound((int)cue), -2);
        });
    }

    public void PlayBattleSequenceSound(BattleCombatant actor, BattleSequenceSoundCue cue)
    {
        TryOutput(() =>
        {
            if (actor.ModelId < 0)
            {
                return;
            }
            int actorId = actor.IsPlayer ? actor.ModelId : checked(actor.ModelId + 16);
            uint effect = cue.ActorSound ? Catalog.BattleActorSound(actorId, cue.SoundId) : checked((uint)cue.SoundId);
            if (effect == 0)
            {
                return;
            }
            // TODO(TD-009): Preserve the cue's sound identity; scheduling/spatialization still use presentation tuning.
            // A bounded device-only voice pool lets swing and impact tails overlap without owning field VM voices.
            int voice = -10 - battleVoice++ % 16;
            PlayPresentationSound(effect, voice);
        });
    }

    public void PlayBattleModuleSound(NativeBattleModuleSound sound)
    {
        TryOutput(() =>
        {
            uint pan = ResolveNativeModuleSoundPan(sound);
            // TODO(TD-009): Retain the original sound ID/flags/anchor; device voice allocation and spatialization use the current presentation output.
            int transientVoice = (sound.Flags & 0xFFFFFF) == 0 ? -10 - battleVoice++ % 16 : 0;
            PlayPresentationSound(sound.Sound, ResolveNativeModuleSoundVoice(sound, transientVoice), pan);
        });
    }

    internal static uint ResolveNativeModuleSoundPan(NativeBattleModuleSound sound)
    {
        byte nativePan = sound.Pan ?? throw new NotSupportedException("Native501740/5017A0 sound requires its actual5011B0 projected pan BYTE.");
        //46B2A0 passes its unsigned pan argument shifted right once to4699A0.
        return (uint)nativePan >> 1;
    }

    internal static int ResolveNativeModuleSoundVoice(NativeBattleModuleSound sound, int transientVoice)
    {
        //46B0F0 numbers the lowest selected attribute bit from1;46B2A0 replaces that voice.
        if ((sound.Flags & 0xFFFFFF) == 0)
        {
            return transientVoice;
        }
        NativeGlobalSoundProcess.FromBufferlessColdProcess().SelectVoice(sound.Flags, out NativeGlobalSoundAllocation allocation);
        return allocation.Voice;
    }

    private void PlayPresentationSound(uint effect, int voice, uint pan = 64)
    {
        int sample = EventEngine.MapGlobalSoundSample(effect);
        GlobalSoundResource resource = GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(gameDirectory, sample)) ?? throw new NotSupportedException($"Presentation sound {effect} requires an unsupported audio format.");
        PlaySoundSample(sample, voice, 127, pan, resource.LoopPolicy);
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
        SetMusicGain(request.Slot, request.BothChannels == true, request.FadeInitialVolumeOperand, request.RequestedVolume, request.EffectiveDuration);
    }

    private void SetMusicGain(int slot, bool bothChannels, int? initialVolume, int requestedVolume, int duration)
    {
        bool midi = bothChannels || musicKinds[slot] == MusicKind.Midi;
        bool stream = bothChannels || musicKinds[slot] == MusicKind.Stream;
        if (initialVolume is int initial && initial is >= 0 and <= 127)
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
        if (requestedVolume is >= 0 and <= 127)
        {
            if (midi)
            {
                midiRamp = new Ramp(midiVolume, requestedVolume, duration);
            }
            if (stream)
            {
                streamRamp = new Ramp(streamVolume, requestedVolume, duration);
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

    public void StopSounds()
    {
        foreach (int voice in voices.Keys.ToArray())
        {
            TryOutput(() => StopVoice(voice));
        }
    }

    public void StopAll()
    {
        StopSounds();
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
