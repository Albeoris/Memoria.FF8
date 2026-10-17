namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public bool CanStoreNativeBattleFieldReturn => true;

    public void SetNativeBattleFieldReturn(NativeBattleFieldReturnOwners owners)
    {
        ArgumentNullException.ThrowIfNull(owners);
        owners.Music.MusicProcess.Validate();
        owners.Music.SoundProcess.Validate();
        owners.Music.Receipt.Validate();
        owners.Controls.Validate();
        owners.SeeD.Validate();
        owners.Timer.Validate();
        owners.Salary.Validate();
        owners.Vibration.Validate();
        owners.Fade.Validate();
        owners.Memory.Validate();
        NativeBattleRetainedMemory memory = State.NativeBattleMemory ?? throw new NotSupportedException("Native field return requires its actual retained battle process memory.");
        memory = memory with { MusicProcess = owners.Music.MusicProcess, VibrationProcess = owners.Vibration };
        memory.Validate();
        GameState candidate = State with { Battle = null, NativeBattleMemory = memory, WorldControls = owners.Controls, SeeD = owners.SeeD, ExamTimer = owners.Timer, GlobalSoundProcess = owners.Music.SoundProcess, SalaryPresentation = owners.Salary, VibrationProcess = owners.Vibration, FieldFade = owners.Fade, EventMemory = owners.Memory };
        candidate = AdvanceNativePlaytime(candidate);
        ValidateLiveEventMemoryOwners(candidate, owners.Memory);
        ValidateBattleFactsCheckpoint(candidate);
        State = candidate;
    }
}
