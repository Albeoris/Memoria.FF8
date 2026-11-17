using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class BattleAudioTestOutput
{
    public static BattleAudioPresentation Create(Action<NativeBattleModuleSound>? playModule = null, Action? stopSounds = null, Action<NativeBattleMusicCommandReceipt>? playMusicCommand = null, Action<int>? playUiSound = null, Action<byte>? stopSoundVoice = null)
    {
        return new(playModule ?? (_ => { }), stopSounds ?? (() => { }), playMusicCommand ?? (_ => { }), playUiSound ?? (_ => { }), stopSoundVoice ?? (_ => { }));
    }
}
