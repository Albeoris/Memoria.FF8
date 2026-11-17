namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal NativeBattleMusicProcessState ReadEntryMusic(NativeBattleMusicProcessState? selected)
    {
        if (!HasBattleEntry || this.resources.InitialMusic != selected ||
            NativeMusicProcess is not { Commands: { Pending: false, Loading: false } } initialized)
        {
            throw new InvalidOperationException("Battle entry requires its selected music owner before publishing initialized commands.");
        }
        return initialized;
    }

    private void ImportSharedState(BattlePresentationSharedState state)
    {
        if ((nativeCrtRandom is null) != (state.CrtRandom is null) ||
            (nativeGlobalRandom is null) != (state.GlobalRandom is null) ||
            (nativeBattleUiRandom is null) != (state.UiRandom is null) || state.UiRandom?.State >= 0x8000)
        {
            throw new InvalidDataException("Battle and presentation require matching global, CRT and UI random owners.");
        }
        nativeCrtRandom = state.CrtRandom;
        nativeBattleUiRandom = state.UiRandom;
        nativeGlobalRandom = state.GlobalRandom;
        nativeGlobalRandomCalls = state.GlobalRandomCalls;
        NativeMusicProcess = state.Music;
        NativeGlobalSoundProcess = state.Sound;
        NativeVibrationProcess = state.Vibration;
    }

    private BattlePresentationSharedState ObserveSharedState()
    {
        return new(nativeCrtRandom, nativeBattleUiRandom, nativeGlobalRandom, nativeGlobalRandomCalls, NativeMusicProcess, NativeGlobalSoundProcess, NativeVibrationProcess);
    }
}
