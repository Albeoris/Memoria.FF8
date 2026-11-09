namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public Ff7FieldProcess CreateNextFieldProcess()
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
            SoundChannelVolumes = (int[])Process.SoundChannelVolumes.Clone(),
            CameraShakeX = Ff7NativeCameraShake.ResetForField(Process.CameraShakeX ?? new()),
            CameraShakeY = Ff7NativeCameraShake.ResetForField(Process.CameraShakeY ?? new()),
            PreloadedMap = Process.PreloadedMap,
            MusicLock = Process.MusicLock
        };
    }
}
