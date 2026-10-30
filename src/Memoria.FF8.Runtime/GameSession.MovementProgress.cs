namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void SetNativeMovementOwners(NativeMovementProgressResult progress, NativeWorldControlState controls, NativeSeeDState? seeD = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null, NativeEventRandomState? eventRandom = null)
    {
        progress.Limits?.Validate();
        if (progress.Limits is not null && progress.Vitals is not null && progress.Limits.InitOutSha256 != progress.Vitals.InitOutSha256)
        {
            throw new InvalidDataException("Native movement learning and characters have different initialization provenance.");
        }
        GameState candidate = PrepareNativeEventOwners(controls, seeD: seeD, characterVitals: progress.Vitals, economy: economy, globalSoundProcess: globalSoundProcess, salaryPresentation: salaryPresentation, eventRandom: eventRandom, eventMemory: progress.Memory);
        candidate = candidate with { LimitProgress = progress.Limits ?? State.LimitProgress };
        State = StageOriginalAchievementNativeMutations(candidate);
    }
}
