using System.Diagnostics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Device output is transient. Executed VM audio receipts keep their deterministic headless contract.</summary>
internal sealed partial class FieldAudioPresentation(nint window, string gameDirectory, string toolsDirectory, Func<ModCatalogSnapshot>? modCatalog = null) : IDisposable
{
    private readonly GameFileResources gameFiles = new(gameDirectory);
    private readonly Dictionary<int, PcmAudio> samples = [];
    private readonly Dictionary<int, Voice> voices = [];
    private NativeMusicCatalog? catalog;
    private DirectSoundOutput? sound;
    private DirectMusicOutput? music;
    private DirectSoundOutput.Buffer? streamedMusic;
    private bool streamedMusicPaused;
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

    public void SetOutputPreferences(int musicVolume, int soundVolume, int voiceVolume = 100)
    {
        OutputGain(0, musicVolume);
        OutputGain(0, soundVolume);
        OutputGain(0, voiceVolume);
        voicePreference = voiceVolume;
        musicPreference = musicVolume;
        soundPreference = soundVolume;
        TryOutput(() =>
        {
            foreach (DirectSoundOutput.Buffer buffer in dialogueVoices.Values)
            {
                buffer.Volume(OutputGain(0, voicePreference));
            }
            foreach (Voice voice in voices.Values)
            {
                int gain = OutputGain(Catalog.SoundVolume(Math.Clamp((int)voice.Volume, 0, 127)), soundPreference);
                voice.Primary.Volume(gain);
                voice.Loop?.Volume(gain);
            }
            UpdateExternalGain(MusicKind.Midi, Math.Clamp((int)midiVolume, 0, 127));
            UpdateExternalGain(MusicKind.Stream, Math.Clamp((int)streamVolume, 0, 127));
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
    private NativeMusicCatalog Catalog => catalog ??= new NativeMusicCatalog(gameFiles.Resources);
    private DirectSoundOutput Sound => sound ??= new DirectSoundOutput(window);
    private DirectMusicOutput Music => music ??= new DirectMusicOutput(window, Path.GetDirectoryName(gameFiles.ResolveDataFile("music/dmusic/FF8.dls"))!);

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

    public void ResumeMusic(NativeMusicPlaybackState? playback)
    {
        TryOutput(() => DispatchNativeMusicResume(playback, (slot, id, gain) =>
        {
            // TODO(TD-009): Device phase is not retained; restart the proved selection at its segment start.
            PlayMusic(id, slot, gain, -1, forceRestart: true, identityResolved: true);
        }));
    }

    internal void Execute(object command)
    {
        TryOutput(() =>
        {
            switch (command)
            {
                case NativeBattleMusicEntryReceipt entry:
                    ExecuteBattleMusicEntry(entry);
                    break;
                case NativeBattleMusicReturnReceipt returned:
                    ExecuteBattleMusicReturn(returned);
                    break;
                case NativeBattleMusicCommandReceipt battleMusic:
                    PlayBattleMusicCommand(battleMusic);
                    break;
                case NativeWorldMusicLoad world:
                    ExecuteMusicPlayback(world.Playback, world.SourceFieldId, world.StartPoint, world.DurationMilliseconds, world.Operation == NativeWorldMusicOperation.Play ? world.InitialVolume : null);
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
                case NativeFieldSoundDeparture departure:
                    foreach (int voice in departure.Voices)
                    {
                        StopVoice(voice);
                    }
                    break;
                case NativeSoundInitialization initialization:
                    foreach (int voice in voices.Keys.ToArray())
                    {
                        StopVoice(voice);
                    }
                    DispatchNativeMusicInitialization(initialization, (slot, gain) => SetMusicGain(slot, null, gain, 0));
                    break;
                case NativeSoundVolume volume:
                    SetVoiceVolume(volume.Voice, volume.Volume, volume.DurationMilliseconds);
                    break;
                case NativeSoundVolumeTransition volume:
                    SetVoiceVolume(volume.Voice, volume.Volume, volume.DurationMilliseconds);
                    break;
                case NativeAllSoundPosition position:
                    for (int voice = 1; voice < 30; voice++)
                    {
                        SetVoicePan(voice, position.Pan, position.DurationMilliseconds);
                    }
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
                case NativeMusicPlayRequest { ConsumedPending: true } play:
                    ExecuteMusicPlayback(play.Playback, play.FieldId, play.SkipPosition ?? 0, choiceMask: play.ChoiceMask, noIntro: play.SkipPosition is not null);
                    break;
                case NativeMusicReplayRequest replay:
                    ExecuteMusicPlayback(replay.Playback, replay.FieldId);
                    break;
                case NativeMusicStop { StopRequested: true } stop:
                    DispatchNativeMusicStop(stop, StopMusicSlot);
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
        GlobalSoundResource resource = GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(gameFiles.Resources, sample)) ?? throw new NotSupportedException($"Footstep sound {footstep.EffectId} requires an unsupported audio format.");
        // Native footsteps replace device voices23/24, sharing script stop/volume controls.
        // The transient callback creates no persisted VM request or allocator mutation.
        PlaySoundSample(sample, voice, volume, pan, resource.LoopPolicy);
    }

    private void PlaySoundSample(int index, int voiceId, uint volume, uint effectivePan, GlobalSoundLoopPolicy policy)
    {
        if (!samples.TryGetValue(index, out PcmAudio? pcm))
        {
            pcm = PcmAudioDecoder.ReadSample(gameFiles.Resources, index);
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

    public void PlayStartupMusic()
    {
        TryOutput(() => PlayMusic(NativeStartupCredits.MusicId, 0, 127, -1));
    }

    public void PlayTitleMusic()
    {
        // The Successor uses the installed game's music catalog and ordinary looping output.
        TryOutput(() => PlayMusic(96, 0, 127, -1));
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
            PlayPresentationSound(sound.Sound, ResolveNativeModuleSoundVoice(sound), pan, sound.Volume);
        });
    }

    internal static uint ResolveNativeModuleSoundPan(NativeBattleModuleSound sound)
    {
        uint nativePan = sound.RawPan ?? (uint?)sound.Pan ?? throw new NotSupportedException("Native501740/5017A0 sound requires its actual pan operand or5011B0 projected pan BYTE.");
        //46B2A0 passes its unsigned pan argument shifted right once to4699A0.
        return nativePan >> 1;
    }

    internal static int ResolveNativeModuleSoundVoice(NativeBattleModuleSound sound)
    {
        return sound.ReadCommittedVoice();
    }

    private void PlayPresentationSound(uint effect, int voice, uint pan = 64, uint volume = 127)
    {
        //4699A0 rejects these unsigned operands; the original request remains recorded even when output is rejected.
        if (volume > 127 || pan > 127)
        {
            return;
        }
        int sample = EventEngine.MapGlobalSoundSample(effect);
        GlobalSoundResource resource = GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(gameFiles.Resources, sample)) ?? throw new NotSupportedException($"Presentation sound {effect} requires an unsupported audio format.");
        PlaySoundSample(sample, voice, volume, pan, resource.LoopPolicy);
    }

    private void PlayMusic(int track, int slot, int? volume, int fieldId, int startPoint = 0, bool forceRestart = false, bool identityResolved = false, bool noIntro = false)
    {
        if (volume is not (>= 0 and <= 127))
        {
            throw new NotSupportedException("Music output requires the executed native volume.");
        }
        if (!identityResolved)
        {
            track = NativeMusicCatalog.ResolveMusicId(track, fieldId);
        }
        if (!forceRestart && musicIds[slot] == track)
        {
            return;
        }
        StopMusicSlot(slot);
        string selectedFilename = Catalog.MusicFileResolved(track);
        if (PlayExternalMusic(selectedFilename, slot, track, volume.Value, noIntro || startPoint != 0))
        {
            return;
        }
        string path = gameFiles.ResolveInstallationFile(selectedFilename);
        if (Path.GetExtension(path).Equals(".sgt", StringComparison.OrdinalIgnoreCase))
        {
            Music.Play(path, OutputGain(Catalog.MusicVolume(volume.Value), musicPreference), startPoint);
            nativeMidiPaused = false;
            musicKinds[slot] = MusicKind.Midi;
            midiVolume = volume.Value;
            midiRamp = null;
        }
        else
        {
            PcmAudio pcm = PcmAudioDecoder.ReadFile(toolsDirectory, path);
            streamedMusic?.Dispose();
            streamedMusic = Sound.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
            streamedMusicPaused = false;
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
        DispatchNativeMusicVolume(request, (slot, gain) => SetMusicGain(slot, request.FadeInitialVolumeOperand, gain, request.EffectiveDuration));
    }

    private void SetMusicGain(int slot, int? initialVolume, int requestedVolume, int duration)
    {
        if (requestedVolume is < 0 or > 127 || initialVolume is < 0 or > 127 || duration < 0)
        {
            throw new NotSupportedException("Native music device output requires reached gain-table indices and a nonnegative effective duration.");
        }
        bool midi = musicKinds[slot] == MusicKind.Midi;
        bool stream = musicKinds[slot] == MusicKind.Stream;
        if (initialVolume is int initial)
        {
            SetImmediate(initial);
        }
        if (duration == 0)
        {
            SetImmediate(requestedVolume);
            return;
        }
        if (midi)
        {
            midiRamp = new Ramp(midiVolume, requestedVolume, duration);
        }
        if (stream)
        {
            streamRamp = new Ramp(streamVolume, requestedVolume, duration);
        }

        void SetImmediate(int value)
        {
            if (midi)
            {
                UpdateExternalGain(MusicKind.Midi, value);
                midiVolume = value;
                midiRamp = null;
                music?.Volume(OutputGain(Catalog.MusicVolume(value), musicPreference));
            }
            if (stream)
            {
                UpdateExternalGain(MusicKind.Stream, value);
                streamVolume = value;
                streamRamp = null;
                streamedMusic?.Volume(OutputGain(Catalog.SoundVolume(value), musicPreference));
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
        if (focusPaused)
        {
            return;
        }
        if (music?.Error is Exception failure)
        {
            midiRamp = null;
            ReportOutputFailure(failure);
        }
        TryOutput(() =>
        {
            foreach (ExternalMusicOutput? output in externalMusic)
            {
                output?.Update();
            }
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
                UpdateExternalGain(MusicKind.Midi, Math.Clamp((int)midiVolume, 0, 127));
                music?.Volume(OutputGain(Catalog.MusicVolume(Math.Clamp((int)midiVolume, 0, 127)), musicPreference));
                if (transition.Finished)
                {
                    midiRamp = null;
                }
            }
            if (streamRamp is Ramp streamTransition)
            {
                streamVolume = streamTransition.Value;
                UpdateExternalGain(MusicKind.Stream, Math.Clamp((int)streamVolume, 0, 127));
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
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException or TimeoutException or System.Text.Json.JsonException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        {
            ReportOutputFailure(exception);
        }
    }

    private void ReportOutputFailure(Exception exception)
    {
        if (Error == exception.Message)
        {
            return;
        }
        Error = exception.Message;
        Console.Error.WriteLine($"field-audio=Failed {Error}");
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
        MusicKind kind = musicKinds[slot];
        try
        {
            StopExternalMusic(kind);
            // Native46C060 chooses the process-wide MIDI or WAV backend by this slot's type.
            if (kind == MusicKind.Midi)
            {
                music?.Stop();
            }
            else if (kind == MusicKind.Stream)
            {
                streamedMusic?.Dispose();
            }
        }
        finally
        {
            musicKinds[slot] = MusicKind.None;
            musicIds[slot] = -1;
            if (kind == MusicKind.Midi)
            {
                nativeMidiPaused = false;
                midiRamp = null;
            }
            else if (kind == MusicKind.Stream)
            {
                streamedMusic = null;
                streamedMusicPaused = false;
                streamRamp = null;
            }
            if (music is { Error: not null } failed)
            {
                music = null;
                nativeMidiPaused = false;
                midiRamp = null;
                failed.Dispose();
            }
        }
    }

    public void StopBattleSoundVoice(byte voice)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(voice, 32);
        TryOutput(() => StopVoice(voice));
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
        TryOutput(StopDialogueVoices);
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
        gameFiles.Dispose();
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

    internal sealed class Ramp(float initial, float target, int milliseconds, TimeProvider? timeProvider = null)
    {
        private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
        private long started = (timeProvider ?? TimeProvider.System).GetTimestamp();
        private TimeSpan elapsed;
        private bool paused;
        private double ElapsedMilliseconds => (elapsed + (paused ? TimeSpan.Zero : time.GetElapsedTime(started))).TotalMilliseconds;
        public bool Finished => milliseconds <= 0 || ElapsedMilliseconds >= milliseconds;
        public float Value => Finished ? target : initial + (target - initial) * (float)(ElapsedMilliseconds / milliseconds);

        public void Pause()
        {
            if (!paused)
            {
                elapsed += time.GetElapsedTime(started);
                paused = true;
            }
        }

        public void Resume()
        {
            if (paused)
            {
                started = time.GetTimestamp();
                paused = false;
            }
        }
    }

    private enum MusicKind
    {
        None,
        Midi,
        Stream
    }
}
