namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public static Ff7FieldProcess CreateInitialFieldProcess(ushort map)
    {
        return new() { PreviousMap = map, BlinkStateInitialized = true, NewFadeOrigin = new(0, 0, 0), FadeState = CreateEntryFade() };
    }

    private static Ff7FadeProcess CreateEntryFade() => new(1, 256, 16, 0, 0, 0, false, EntryCurve: true);

    public Ff7FieldProcess CreateNextFieldProcess(ushort? previousMap = null)
    {
        int current = NormalizeNativeMusicTrack(Process.NativeMusicTrack ?? (Process.Music is null || Process.Music.Track < 0 ? 0 : ResolveNativeMusicTrack(Process.Music.Track)));
        int target = Process.NextFieldMusicTrack < 0 ? current : NormalizeNativeMusicTrack(ResolveNativeMusicTrack(Process.NextFieldMusicTrack));
        int? saved = Process.SavedNativeMusicTrack ?? (Process.SavedMusicTrack < 0 ? null : NormalizeNativeMusicTrack(ResolveNativeMusicTrack(Process.SavedMusicTrack)));
        return new Ff7FieldProcess
        {
            BlinkRandomIndex = Process.BlinkRandomIndex,
            BlinkStateInitialized = true,
            NativeMusicTrack = target,
            MusicTicks = target == current ? Process.MusicTicks : 0,
            SavedNativeMusicTrack = saved,
            MusicVolume = Process.MusicVolume,
            MusicVolumeFade = Process.MusicVolumeFade,
            SoundChannelLoopIds = (int[])Process.SoundChannelLoopIds.Clone(),
            SoundChannelPans = (int[])Process.SoundChannelPans.Clone(),
            SoundChannelPanVersions = (long[])Process.SoundChannelPanVersions.Clone(),
            SoundChannelPanFades = (Ff7SoundVolumeFade?[])Process.SoundChannelPanFades.Clone(),
            SoundPanStateInitialized = Process.SoundPanStateInitialized,
            SoundBufferPanStateInitialized = Process.SoundBufferPanStateInitialized,
            SoundChannelBufferPans = (Ff7SoundBufferPanState?[])Process.SoundChannelBufferPans.Clone(),
            SoundChannelFrequencyPublications = (int[])Process.SoundChannelFrequencyPublications.Clone(),
            SoundChannelFrequencyVersions = (long[])Process.SoundChannelFrequencyVersions.Clone(),
            SoundChannelFrequencyFades = (Ff7SoundVolumeFade?[])Process.SoundChannelFrequencyFades.Clone(),
            SoundChannelPitches = (int[])Process.SoundChannelPitches.Clone(),
            SoundChannelBaseFrequencies = (int[])Process.SoundChannelBaseFrequencies.Clone(),
            SoundGainStateInitialized = Process.SoundGainStateInitialized,
            SoundChannelGainPublications = (int[])Process.SoundChannelGainPublications.Clone(),
            SoundChannelGainVersions = (long[])Process.SoundChannelGainVersions.Clone(),
            SoundChannelVolumes = (int[])Process.SoundChannelVolumes.Clone(),
            SoundChannelVolumeFades = (Ff7SoundVolumeFade?[])Process.SoundChannelVolumeFades.Clone(),
            SoundChannelStoppedByFade = (bool[])Process.SoundChannelStoppedByFade.Clone(),
            SoundVolumeClockSubmillisecondTicks = Process.SoundVolumeClockSubmillisecondTicks,
            CameraShakeX = Ff7NativeCameraShake.ResetForField(Process.CameraShakeX ?? new()),
            CameraShakeY = Ff7NativeCameraShake.ResetForField(Process.CameraShakeY ?? new()),
            PreloadedMap = Process.PreloadedMap,
            PreviousMap = previousMap,
            NewFadeOrigin = Process.NewFadeOrigin,
            FadeState = CreateEntryFade(),
            MusicLock = Process.MusicLock
        };
    }
}
