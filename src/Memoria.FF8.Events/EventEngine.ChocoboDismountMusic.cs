using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record PreparedChocoboDismountMusic(NativeWorldMusicLoad Load, NativeGlobalSoundProcess Sound);

public sealed partial class EventEngine
{
    public PreparedChocoboDismountMusic? PrepareChocoboDismountMusic(WorldState before, WorldState after, NativeGlobalSoundProcess? sound, NativeWorldMusicLoad? prepared, Func<int, NativeWorldMusicResource> readFile)
    {
        long? startFrame = after.ChocoboDismount?.StartFrame ?? after.NativeFootRuntime?.ChocoboDismountStartFrame;
        if (startFrame is not long start || Scene.WorldMusicSelection is not { Music: > 0 } selection)
        {
            return null;
        }

        NativeWorldMusicLoad? current = prepared ?? Scene.WorldMusicLoad;
        bool sameTransfer = current is { ChocoboDismount: true } && current.SourceFrame == start;
        if (sameTransfer && current!.Playback is { PlayRequested: true })
        {
            return null;
        }
        NativeMusicPlaybackState? playback = sound?.MusicPlayback;
        bool admitted = before.ChocoboDismount is null && after is { NativeChocoboRuntime: not null, ChocoboDismount: not null };
        // Legacy dismount snapshots can retain the old mounted track without a request.
        if (!sameTransfer && !admitted && playback?.CurrentMusicId0 != NativeWorldMusicResource.FileMusic(2))
        {
            return null;
        }

        // 54A7F0 selects with entity 128 at admission. Semantic saves restore the
        // playing track even when the newly allocated event channel is still FF.
        if (!sameTransfer)
        {
            if (NativeRuntimeProfile is not { IsVerified: true } || Scene.MusicProcess?.Bank201 is not byte bank || services is not GameSession session || session.State.WorldArrival is not { } packet || playback is null)
            {
                throw new NotSupportedException("Chocobo dismount music requires its original world audio owners.");
            }
            byte prior = Scene.MusicChannel108?.MusicId is byte retained and not 255
                ? retained
                : session.State.GameplaySceneFacts?.Music?.PrimaryChannel?.MusicId ?? checked((byte)(playback.CurrentMusicId0 ?? 255));
            int index = PendingWorldMusicSelection(128, selection.Music, prior);
            if (index < 0)
            {
                return null;
            }
            NativeWorldMusicResource resource = readFile(index);
            resource.Validate();
            if (resource.File?.Index != index)
            {
                throw new InvalidDataException("Chocobo dismount music resource differs from the native selector.");
            }
            current = new NativeWorldMusicLoad(1, FieldId, start, packet, selection.Music, prior, bank, (byte)(bank ^ 1), NativeWorldMusicOperation.Play, resource.MusicId, resource, 0, 127, 60) { ChocoboDismount = true };
        }

        // The resource provider reads the file synchronously. 543340 still
        // stages the read before stopping the old track, and waits for mode 7 to clear.
        NativeWorldMusicLoad load = current!;
        if (playback is null || sound is null)
        {
            throw new NotSupportedException("Chocobo dismount music requires its retained primary playback process.");
        }
        if (after.NativeChocoboRuntime is null && after.EventUi?.ScriptMode <= 4 && before.NativeChocoboRuntime is null)
        {
            playback = playback.Stop46C060(0).PlayEncoded46B530(0, unchecked((byte)(load.Music!.Value + 1)), 0, ReadMusicCurrentField).FadeGain46BBC0(0, 60, 127);
            NativeMusicPlaybackDecision output = new(0, playback.CurrentMusicId0, true, true, false, 0, 127);
            return new(load with { Playback = output }, sound with { MusicPlayback = playback });
        }
        if (load.Playback is null)
        {
            playback = playback.FadeGain46BBC0(0, 30, 0);
            NativeMusicPlaybackDecision output = new(0, playback.CurrentMusicId0, false, false, false, null, 0);
            return new(load with { Playback = output }, sound with { MusicPlayback = playback });
        }
        if (!load.Playback.StopRequested && after.Frame - start >= 2)
        {
            playback = playback.Stop46B800(0);
            NativeMusicPlaybackDecision output = new(0, playback.CurrentMusicId0, false, true, false, null, null);
            return new(load with { Playback = output }, sound with { MusicPlayback = playback });
        }
        return null;
    }

    public void PublishChocoboDismountMusic(PreparedChocoboDismountMusic change)
    {
        NativeWorldMusicLoad load = change.Load;
        NativeMusicPlaybackDecision output = load.Playback!;
        if (output.StopRequested)
        {
            byte music = load.Music!.Value;
            if (Scene.WorldMusicLoad is not { ChocoboDismount: true, Playback.StopRequested: true } prior || prior.SourceFrame != load.SourceFrame)
            {
                byte encoded = unchecked((byte)(music + 1));
                Scene.MusicProcess = Scene.MusicProcess!.LoadBuffer(load.Bank, new(encoded, NativeMusicBufferProducer.WorldFile543340)) with { Bank201 = load.Bank, SelectionAuthority = NativeMusicSelectionAuthority.World };
            }
            Scene.MusicChannel108 = new NativeMusicChannelState(output.PlayRequested ? 0 : Scene.MusicChannel108!.ChannelId, music);
            Scene.LoadedMusic = music;
            Scene.MusicPlaying = output.PlayRequested;
        }
        Scene.WorldMusicLoad = load;
        AudioCommandExecuted?.Invoke(load);
    }
}
