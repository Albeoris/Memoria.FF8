using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record GameplayLoopSoundPlayback(GameplaySaveFieldLoopSound Sound, GlobalSoundResource Resource);

public sealed partial class EventEngine
{
    private static bool IsLoopingSound(GlobalSoundResource resource)
    {
        return resource.LoopPolicy.LoopStartPcmByte != 0 || (resource.LoopPolicy.PlaybackFlags & 1) != 0;
    }

    private void TrackGameplayLoopSounds(object command)
    {
        ImmutableDictionary<int, GameplaySaveFieldLoopSound> loops = Scene.GameplayLoopSounds ?? ImmutableDictionary<int, GameplaySaveFieldLoopSound>.Empty;
        void Volume(int voice, byte value)
        {
            if (loops.TryGetValue(voice, out GameplaySaveFieldLoopSound? sound))
            {
                loops = loops.SetItem(voice, sound with { Volume = value });
            }
        }
        void Pan(int voice, byte value)
        {
            if (loops.TryGetValue(voice, out GameplaySaveFieldLoopSound? sound))
            {
                loops = loops.SetItem(voice, sound with { Pan = value });
            }
        }
        switch (command)
        {
            case GlobalSoundRequest request when request.Resource.BackendStatus != GlobalSoundBackendStatus.Failed:
                loops = IsLoopingSound(request.Resource) ? loops.SetItem(request.Voice, new(request.Voice, request.OriginalEffectId, checked((byte)request.Volume), checked((byte)request.EffectivePan))) : loops.Remove(request.Voice);
                break;
            case NativeFootstepSound footstep:
                NativeGlobalSoundProcess.FromBufferlessColdProcess().SelectVoice(footstep.Flags, out NativeGlobalSoundAllocation allocation);
                loops = loops.Remove(allocation.Voice);
                break;
            case NativeSoundStop stop:
                loops = loops.RemoveRange(stop.Voices);
                break;
            case NativeFieldSoundDeparture departure:
                loops = loops.RemoveRange(departure.Voices);
                break;
            case NativeSoundInitialization:
                loops = loops.Clear();
                break;
            case NativeSoundVolume volume:
                Volume(volume.Voice, volume.Volume);
                break;
            case NativeSoundVolumeTransition volume:
                Volume(volume.Voice, volume.Volume);
                break;
            case NativeAllSoundVolume volume:
                for (int voice = volume.FirstVoice; voice < volume.FirstVoice + volume.VoiceCount; voice++)
                {
                    Volume(voice, volume.Volume);
                }
                break;
            case NativeAllSoundVolumeTransition volume:
                for (int voice = volume.FirstVoice; voice < volume.FirstVoice + volume.VoiceCount; voice++)
                {
                    Volume(voice, volume.Volume);
                }
                break;
            case NativeSoundPosition position:
                Pan(position.Voice, position.Pan);
                break;
            case NativeAllSoundPosition position:
                foreach (int voice in loops.Keys.Where(voice => voice is >= 1 and < 30).ToArray())
                {
                    Pan(voice, position.Pan);
                }
                break;
        }
        Scene.GameplayLoopSounds = loops;
    }

    private GameplaySaveFieldLoopSound[] CaptureGameplayLoopSounds()
    {
        GameplaySaveFieldLoopSound[] result = (Scene.GameplayLoopSounds ?? ImmutableDictionary<int, GameplaySaveFieldLoopSound>.Empty).OrderBy(pair => pair.Key).Select(pair =>
        {
            pair.Value.Validate();
            if (pair.Key != pair.Value.Voice)
            {
                throw new InvalidDataException("Gameplay ambient sound key contradicts its voice owner.");
            }
            return pair.Value;
        }).ToArray();
        return result;
    }

    private void RestoreGameplayLoopSounds(GameplaySaveFieldLoopSound[]? sounds)
    {
        Scene.GameplayLoopSounds = (sounds ?? []).ToImmutableDictionary(sound => sound.Voice);
        // Current descriptors are checked while preparing the candidate, without starting device output.
        foreach (GameplaySaveFieldLoopSound sound in Scene.GameplayLoopSounds.Values)
        {
            _ = ResolveGameplayLoopSound(sound);
        }
    }

    private GlobalSoundResource ResolveGameplayLoopSound(GameplaySaveFieldLoopSound sound)
    {
        sound.Validate();
        int sample = MapGlobalSoundSample(sound.EffectId);
        GlobalSoundResource? resource = ResolveGlobalSoundResource?.Invoke(sample);
        if (resource is null || !ValidGlobalSoundResource(resource, sample))
        {
            throw new NotSupportedException($"Gameplay ambient sound {sound.EffectId} requires its current installed sample descriptor.");
        }
        return resource;
    }

    /// <summary>Rebuilds ambient output from current resources at terminal controls, without advancing the VM or its allocator.</summary>
    public void ResumeGameplayLoopSounds()
    {
        foreach (GameplaySaveFieldLoopSound sound in CaptureGameplayLoopSounds())
        {
            GlobalSoundResource resource = ResolveGameplayLoopSound(sound);
            if (resource.BackendStatus != GlobalSoundBackendStatus.Failed && IsLoopingSound(resource))
            {
                AudioCommandExecuted?.Invoke(new GameplayLoopSoundPlayback(sound, resource));
            }
        }
    }
}
